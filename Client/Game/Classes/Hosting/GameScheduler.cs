//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.                                                               
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using OrganismBase;
using Terrarium.Configuration;
using Terrarium.Game;
using Terrarium.Renderer;
using Terrarium.Tools;
using FILETIME=System.Runtime.InteropServices.ComTypes.FILETIME;

namespace Terrarium.Hosting
{
    /// <summary>
    /// This class does all of the work to give the creatures timeslices and ensure 
    /// that they get killed if they take too much time.
    /// </summary>
    internal class GameScheduler : MarshalByRefObject, IGameScheduler
    {
        // If an animal actually gets this much kernel time in a single timeslice, we permanently blacklist them 
        // from the game and restart the game.  This is set to a large time because permanently blacklisting
        // them is a big deal and because some of the kernel time their thread truly gets will be time used by
        // the CLR doing GCs, Jitting, etc. and they shouldn't be penalized for it.
        private const int AnimalDeadlock100NSec = 50000000; // 5 seconds

        // How often the deadlock detection should check to see if an animal has returned the thread
        private const int AnimalDeadlockCheckMSec = 7500; // check for deadlock after 7.5 seconds

        // How many times we retry our check in deadlock detection to see if the thread returned. If we check
        // 3 times (with 7.5 second intervals) and the animal still hasn't gotten 5 seconds
        // of kernel time, we still restart the game, we just don't permanently blacklist them.
        private const int AnimalDeadlockRetries = 3;

        // How long an organism's turn may take before it is removed from the game.
        // Overrunning the quantum a little is handled by skipping turns; this is for
        // organisms that don't return at all.
        private const int TurnTimeLimitMSec = 1000;

        private const int ReportInterval = 150;
        private readonly IntPtr _processHandle;
        private GameEngine _currentEngine;
        private Int64 _lastReport;
        private GameObjectCollection _organisms;
        private int _organismsActivated;
        private IEnumerator _orgEnum;
        private PrivateAssemblyCache _pac;
        private Boolean _penalizeForTime = true;
        private int _tickCount;
        private int _ticksToSuspendBlacklisting;
        private Int64 _totalActivations;

        /// <summary>
        ///  The thread that runs organisms' turns right now.
        /// </summary>
        private ActivationWorker _worker;

        /// <summary>
        ///  A thread that runs organisms' turns, one at a time, and the events
        ///  used to hand it the next organism.  Modern .NET can't abort a thread,
        ///  so when an organism doesn't return in time its worker is abandoned:
        ///  it is marked so it never touches the game again, and a new worker
        ///  takes over.
        /// </summary>
        private sealed class ActivationWorker
        {
            public readonly ManualResetEvent Done = new ManualResetEvent(false);
            public readonly ManualResetEvent HandleRetrieved = new ManualResetEvent(false);
            public readonly ManualResetEvent Ready = new ManualResetEvent(false);
            public volatile bool Abandoned;
            public OrganismWrapper Bug;
            public volatile bool Exit;
            public Thread Thread;
            public IntPtr ThreadHandle;
            public bool ThreadHandleValid;
        }

        public GameScheduler()
        {
            MaxAllowance = EngineSettings.OrganismSchedulingBlacklistOvertime;
            TicksPerSec = 5;
            Quantum = 5000;
            MaxOverage = EngineSettings.OrganismSchedulingMaximumOvertime;
            _organisms = new GameObjectCollection();
            _orgEnum = _organisms.GetEnumerator();

            Debug.Assert(MaxOverage > Quantum);

            AppMgr.CurrentScheduler = this;

            // This handle is a psuedo handle that doesn't need to be closed
            _processHandle = GetCurrentProcess();

            // Create a thread other than the UI thread to run the animals on
            StartWorker();
        }

        private void StartWorker()
        {
            var worker = new ActivationWorker();

            // Background, so an abandoned worker still stuck in organism code
            // doesn't keep the process alive
            worker.Thread = new Thread(ActivateBug) {Priority = ThreadPriority.AboveNormal, IsBackground = true};
            worker.Thread.Start(worker);

            // Wait for the ActivateBug routine to get far enough to
            // retreive its threadhandle which is needed to get accurate
            // timings on how much time the animal actually used on its thread.
            var success = worker.HandleRetrieved.WaitOne();
            Debug.Assert(success);
            _worker = worker;
        }

        public Boolean DetectDeadlock
        {
            get
            {
                // Don't kill the bug if a debugger is attached
                if (Debugger.IsAttached)
                {
                    return false;
                }
                if (_ticksToSuspendBlacklisting > 0)
                {
                    return false;
                }
                return !SuspendBlacklisting;
            }
        }

        public PrivateAssemblyCache PrivateAssemblyCache
        {
            set
            {
                if (_pac != null)
                {
                    _pac.Close();
                }

                // Hook this pac up to our assemblyresolve event
                _pac = value;
                _pac.HookAssemblyResolve();
            }
        }

        #region IGameScheduler Members

        public void Close()
        {
            var worker = _worker;
            if (worker.ThreadHandleValid)
            {
                CloseHandle(worker.ThreadHandle);
                worker.ThreadHandleValid = false;
            }

            // Tell the ActivateBug routine to go ahead and exit the thread.
            worker.Exit = true;
            worker.Ready.Set();
        }

        // Called by the game engine when it is time to give a set of animals slices of time
        // to execute their logic.  This is what sets everything in this class in motion.
        public void Tick()
        {
            var activated = 0;

            // See comment on ticksToSuspendBlacklisting for why this is useful.
            if (_ticksToSuspendBlacklisting > 0)
            {
                Debug.WriteLine("Suspending blacklisting for this tick.");
            }

            if (_organismsActivated < _organisms.Count)
            {
                // Loop through a set of animals
                while (activated < OrganismsPerTick)
                {
                    if (_orgEnum.MoveNext())
                    {
                        activated++;

                        // Give the animal a timeslice, but monitor for cases where it tries to
                        // hang the machine and deal with them gracefully
                        RunAnimalWithDeadlockDetection((OrganismWrapper) _orgEnum.Current);
                    }
                    else
                    {
                        break;
                    }
                } // end animal loop
            }

            _organismsActivated += activated;
            _tickCount++;

            if (_organismsActivated == _organisms.Count && _tickCount == TicksPerSec)
            {
                // Start over at the first animal once we've gone through them all 
                // and we've gone through all the ticks required for a turn.
                _orgEnum.Reset();
                _organismsActivated = 0;
                _tickCount = 0;
                if (_ticksToSuspendBlacklisting > 0)
                {
                    _ticksToSuspendBlacklisting--;
                }
            }
        }

        // This is our last line of defense to deal with animals that try to hang the game (deadlock). 
        // (the first line of defense is abandoning the thread after TurnTimeLimitMSec, see description in ActivateBug()).
        // Thus, it must have robust code that can always fail in some graceful way, and should blacklist
        // any animal that hangs. Because blacklisting an animal is very drastic, we go through great pains
        // to do it fairly, which means that we want to ensure that the animal is getting actual time to run
        // in the OS kernel, and the elapsed time isn't simply because the system is starving its thread
        // or something.  If the animal got plenty of kernel time and didn't come back, we restart the game
        // and blacklist them permanently.  If they aren't getting kernel time but it still taking way too long
        // we simply restart the game.

        // Add an organism to get timesliced.
        public void Add(Organism org, string id)
        {
            // We should only be adding organisms at the beginning
            Debug.Assert(_organismsActivated == 0);

            // Reconstruct its world boundary
            OrganismWorldBoundary.SetWorldBoundary(org, id);

            if (_organisms.ContainsKey(id))
            {
                throw new OrganismAlreadyExistsException();
            }

            _organisms.Add(new OrganismWrapper(org));
        }

        public int OrganismsPerTick
        {
            get { return (_organisms.Count/TicksPerSec) + 1; }
        }

        public int TicksPerSec { get; set; }

        public int Quantum { get; set; }

        public long MaxOverage { get; set; }

        /// <summary>
        /// in microseconds, time before component is removed
        /// default is 10,000,000 (10 secs)
        /// </summary>
        public long MaxAllowance { get; set; }

        // Going into power saving mode can cause threads to suck a bunch of time
        // This method gets called so animals don't get unnecessarily blacklisted in this case
        // This method needs to be threadsafe because it will be called from the systemevents thread
        public void TemporarilySuspendBlacklisting()
        {
            _ticksToSuspendBlacklisting = 2;
        }

        public bool SuspendBlacklisting { get; set; }

        // This returns true if we should shutoff the simple Timeouts that don't hang the machine
        public Boolean PenalizeForTime
        {
            get
            {
                // If we're not detecting deadlock, don't bother detecting simple timeouts since they are
                // the least of our worries
                return DetectDeadlock && _penalizeForTime;
            }

            set { _penalizeForTime = value; }
        }

        public Organism GetOrganism(string id)
        {
            return (Organism) _organisms[id];
        }

        public ICollection Organisms
        {
            get
            {
                var list = new List<Organism>(_organisms.Count);
                foreach (OrganismWrapper wrapper in _organisms)
                {
                    list.Add(wrapper.Organism);   
                }
                return list;
            }
        }

        public object SaveOrganisms()
        {
            return _organisms;
        }

        public void RestoreOrganisms(object organisms)
        {
            _organisms = (GameObjectCollection) organisms ?? new GameObjectCollection();
            _orgEnum = _organisms.GetEnumerator();
        }

        public void CompleteOrganismDeserialization()
        {
            try
            {
                _organisms.CompleteOrganismDeserialization();
            }
            catch
            {
                _organisms = new GameObjectCollection();
                _orgEnum = _organisms.GetEnumerator();

                throw;
            }
        }

        public void Remove(string organismID)
        {
            _organisms.Remove(organismID);
        }

        public void Create(Type species, string id)
        {
            var newOrganism = (Organism) Activator.CreateInstance(species);
            if (null == newOrganism)
            {
                throw new Exception("Failed to create instance of: " + species);
            }

            Add(newOrganism, id);
        }

        public TickActions GatherTickActions()
        {
            var act = new TickActions();
            act.GatherActionsFromOrganisms(this);
            return act;
        }

        public WorldState CurrentState { get; set; }

        public AppDomain OrganismAppDomain
        {
            get { return AppDomain.CurrentDomain; }
        }

        public GameEngine CurrentGameEngine
        {
            get { return _currentEngine; }

            set
            {
                _currentEngine = value;
                PrivateAssemblyCache = _currentEngine.Pac;
            }
        }

        public string GetOrganismTimingReport(string organismID)
        {
            var w = _organisms.GetWrapperForOrganism(organismID);
            if (w == null)
            {
                return "[organism doesn't exist]";
            }

            // Only report timings if we are penalizing for slow times because this means
            // we've shut off all elements that slow down the animals like tracing
            if (!PenalizeForTime)
            {
                return "Inaccurate time due to debugging :" + w.LastTime + " microseconds";
            }

            return w.LastTime > Quantum
                       ?
                           string.Format(
                               "Warning: Time to execute last turn: {0} microseconds is over maxiumum allowed time of {1} microseconds.  Animal may be penalized by skipping a turn.",
                               w.LastTime, Quantum)
                       :
                           string.Format(
                               "Time to execute last turn: {0} microseconds.  [less than maximum allowed time of {1} microseconds.]",
                               w.LastTime, Quantum);
        }

        #endregion

        private void RunAnimalWithDeadlockDetection(OrganismWrapper currentAnimal)
        {
            Int64 kernelStart;
            Int64 userStart;
            var tries = 0;
            var blacklist = false;
            var shutdownWithoutBlacklist = false;

            // Hand the activation thread an animal and kick off processing
            var worker = _worker;
            worker.Bug = currentAnimal;
            var validTime = getAnimalThreadTime(worker, out kernelStart, out userStart);
            worker.Done.Reset();
            worker.Ready.Set();

            if (worker.Done.WaitOne(TurnTimeLimitMSec, false))
            {
                return;
            }

            // The organism hasn't returned in time.  When timings count (they
            // don't while the trace window is open or a debugger is attached),
            // leave it running on its thread, remove it from the game and carry
            // on with a new thread.
            if (PenalizeForTime && !Debugger.IsAttached)
            {
                AbandonWorker(worker, currentAnimal);
                return;
            }

            // Otherwise wait for the animal, periodically checking whether it has
            // hung the thread for good.
            while (true)
            {
                // If the animal returns, Done will get set on the activation thread and we'll continue.
                // if not, this will timeout.
                var executionDone = worker.Done.WaitOne(AnimalDeadlockCheckMSec, false);

                if (!executionDone)
                {
                    Trace.WriteLine(string.Format("Animal thread not stopped after {0} mSec, checking kernel time.",
                                                  AnimalDeadlockCheckMSec));

                    if (DetectDeadlock)
                    {
                        // If we were unable to retreive the time from getAnimalThreadTime() above, just wait
                        // for the full number of tries
                        if (validTime)
                        {
                            // Only permanently blacklist the animal if we are penalizing for time.  PenalizeForTime
                            // is only false when the tracewindow is up since it affects animal timings
                            if (PenalizeForTime)
                            {
                                Int64 kernelStop;
                                Int64 userStop;
                                validTime = getAnimalThreadTime(worker, out kernelStop, out userStop);
                                if (validTime)
                                {
                                    var totalTime = (kernelStop - kernelStart) + (userStop - userStart);

                                    // Give the animal a bunch of time since lots of things can happen on their thread
                                    // that is actually reflected as time their thread actually got in the kernel like
                                    // GCs, Jitting, etc.
                                    if (totalTime > AnimalDeadlock100NSec)
                                    {
                                        Trace.WriteLine(string.Format(
                                                            "Thread overtime: {0} seconds, Blacklist and exit",
                                                            ((totalTime)/(double) 10000000)));
                                        blacklist = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine("Invalid Time From GetThreadTimes()");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("Not penalizing for time -- don't permanently blacklist.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Invalid Time From GetThreadTimes()");
                        }

                        tries++;

                        if (tries >= AnimalDeadlockRetries)
                        {
                            // If we've tried this many times and got to this point, either the animal never got
                            // a lot of actual kernel time, or we're not penalizing for time (the user has the debugger attached,
                            // or trace window open, for example). Don't blacklist them, but go ahead and restart the game
                            Trace.WriteLine(
                                string.Format(
                                    "Tried accessing animal thread {0} times, but not blacklisted yet -- restart.",
                                    tries));
                            shutdownWithoutBlacklist = true;
                            break;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Deadlock detection off");
                    }
                }
                else
                {
                    break;
                }
            }

            handleRestartsAndBlacklist(currentAnimal, blacklist);
            handleRestartWithoutBlacklist(blacklist, shutdownWithoutBlacklist);
        }

        // Gives up on a worker whose organism didn't return in time.  The organism
        // is removed along with the rest of its species, which runs the same code
        // and would hang a new thread on its next turn, and the species is
        // blacklisted so it isn't loaded again.  The abandoned thread stays stuck
        // in the organism's code until it returns or the game exits.
        private void AbandonWorker(ActivationWorker worker, OrganismWrapper hungAnimal)
        {
            worker.Abandoned = true;
            try
            {
                worker.Thread.Priority = ThreadPriority.Lowest;
            }
            catch (ThreadStateException)
            {
                // It finished after all
            }

            if (worker.ThreadHandleValid)
            {
                CloseHandle(worker.ThreadHandle);
                worker.ThreadHandleValid = false;
            }

            var hungAssembly = hungAnimal.Organism.GetType().Assembly;
            var species = (Species) CurrentState.GetOrganismState(hungAnimal.Organism.ID).Species;
            Trace.WriteLine("Organism " + hungAnimal.Organism.ID + " (" + species.Name +
                            ") didn't finish its turn in " + TurnTimeLimitMSec + " mSec; removing its species.");

            foreach (OrganismWrapper wrapper in _organisms)
            {
                if (wrapper.Active && wrapper.Organism.GetType().Assembly == hungAssembly)
                {
                    wrapper.Active = false;
                    _currentEngine.RemoveOrganismQueued(new KilledOrganism(wrapper.Organism.ID,
                                                                           PopulationChangeReason.Timeout));
                }
            }

            _currentEngine.Pac.BlacklistAssemblies(new[] {species.AssemblyInfo.FullName});
            _currentEngine.OnEngineStateChanged(new EngineStateChangedEventArgs(
                                                    EngineStateChangeType.Other,
                                                    "'" + species.Name + "' was removed for taking too long.",
                                                    "'" + species.Name + "' was removed from the Terrarium because one of them didn't finish its turn in time."));

            StartWorker();
        }

        private static void handleRestartWithoutBlacklist(bool blacklist, bool shutdownWithoutBlacklist)
        {
            if (shutdownWithoutBlacklist || blacklist)
            {
                throw new MaliciousOrganismException();
            }
        }

        private void handleRestartsAndBlacklist(OrganismWrapper currentAnimal, bool blacklist)
        {
            if (DetectDeadlock && blacklist)
            {
                Debug.WriteLine(string.Format("Permanently blacklisting: {0}",
                                              ((Species) currentAnimal.Organism.State.Species).AssemblyInfo.FullName));

                // Mark the animal in a magic file on disk so when we restart we will blacklist them.  We can't do it now
                // because the assembly is locked.
                _currentEngine.Pac.LastRun = ((Species) currentAnimal.Organism.State.Species).AssemblyInfo.FullName;

                throw new MaliciousOrganismException();
            }
        }

        // This routine is run on an activation thread.  It just waits until a new animal is ready to
        // be run and then runs it and waits again.  There are two ways we attempt to mitigate the
        // fact that an animal may not want to return execution and hang the machine:
        //
        // 1. RunAnimalWithDeadlockDetection() waits TurnTimeLimitMSec for the turn.  If the
        //      animal hasn't returned, its species is removed and this thread abandoned: it
        //      is left to finish (or not) on its own and never touches the game again.
        // 2. When timings don't count (trace window open), RunAnimalWithDeadlockDetection()
        //      keeps waiting with a much longer duration, and if the thread never comes back
        //      it restarts the game and permanently blacklists the animal.
        internal void ActivateBug(object state)
        {
            var worker = (ActivationWorker) state;
            var monitor = new TimeMonitor();

            // First retreive the thread handle for this thread so we can get accurate reads on how
            // much actual time an animal spent executing.
            // This is a pseudo handle that can't be used on other threads until it is duplicated
            var pseudoThreadHandle = GetCurrentThread();
            worker.ThreadHandleValid = DuplicateHandle(
                _processHandle, // handle to source process
                pseudoThreadHandle, // handle to duplicate
                _processHandle, // handle to target process
                ref worker.ThreadHandle, // duplicate handle
                0, // requested access
                false, // handle inheritance option
                2 // optional actions
                );

            // Notify StartWorker that we have retreived the handle.
            var setSuccess = worker.HandleRetrieved.Set();
            Debug.Assert(setSuccess);

            // If we get an exception that somehow happens outside the try blocks below, it will end this thread,
            // be caught by our global exception handler and our deadlock logic will restart the game automatically because
            // it will think the thread never came back and is deadlocked.
            while (true)
            {
                // Wait for the first animal
                worker.Ready.WaitOne();
                if (worker.Exit)
                {
                    return;
                }

                var bug = worker.Bug;
                var success = false;
                Int64 duration = 0;
                var deathReason = PopulationChangeReason.NotDead;
                var exceptionInfo = "";
                var skippedTurn = false;

                // short circut inactive bugs
                if (bug.Active)
                {
                    var state2 = CurrentState.GetOrganismState(bug.Organism.ID);
                    if (!state2.IsAlive)
                    {
                        bug.Active = false;
                    }
                    else
                    {
                        try
                        {
                            // See if the animal has consistently taken too much time and has finally
                            // gone over the maxAllowance.
                            if (bug.Overage > MaxAllowance)
                            {
                                Trace.WriteLine("Organism blacklisted: bug.Overage > _maxAllowance");
                                deathReason = PopulationChangeReason.Timeout;
                                bug.Active = false;
                            }
                            else if (bug.Overage > MaxOverage)
                            {
                                // if the bug is overtime, don't schedule it
                                // but deduce one quantum from its overage
                                // until it's back under the allowable limit
                                bug.Overage -= Quantum;
                                if (bug.Overage < 0)
                                {
                                    bug.Overage = 0;
                                }

                                // We still need to call InternalMain with "true" so that it can clear
                                // out state for event handlers.  This won't run organism code
                                bug.Organism.InternalMain(true);

                                bug.Organism.WriteTrace(
                                    "Animal's turn was skipped because they took longer than " +
                                    Quantum + " microseconds for their turn too many times.");

                                // We didn't fail, we're just skipping them.  Don't remove them from the world.
                                success = true;
                                skippedTurn = true;
                            }
                            else
                            {
                                // Time the animal
                                monitor.Start();

                                // Hook up the tracing event handler if the animal is selected
                                if (GameConfig.LoggingMode != "None")
                                {
                                    if (GameConfig.LoggingMode != "Full")
                                    {
                                        var renderInfo = bug.Organism.State.RenderInfo;
                                        if (renderInfo != null && ((TerrariumSprite) renderInfo).Selected)
                                        {
                                            bug.Organism.Trace += traceEventHandler;
                                        }
                                    }
                                    else
                                    {
                                        bug.Organism.Trace += traceEventHandler;
                                    }
                                }

                                // Actually allow the animal to run their code now.
                                bug.Organism.InternalMain(false);

                                // Determine how much time the animal spent
                                duration = monitor.EndGetMicroseconds();

                                // This means we didn't throw an exception
                                success = true;
                            }
                        }
                        catch (SecurityException e)
                        {
                            // Organism needs to be removed from the game because it had an exception
                            deathReason = PopulationChangeReason.SecurityViolation;
                            exceptionInfo = e.ToString();
                        }
                        catch (OrganismBlacklistedException)
                        {
                            // This exception gets thrown when we try to give a time slice to an animal
                            // that has been replaced by the special TerrariumOrganism, whose whole purpose
                            // is to just replace a blacklisted animal until it is safe to remove them
                            // from the game, which is now.
                            deathReason = PopulationChangeReason.OrganismBlacklisted;
                        }
                        catch (Exception e)
                        {
                            // Organism needs to be removed from the game because it had an exception
                            deathReason = PopulationChangeReason.Error;
                            exceptionInfo = e.ToString();
                        }
                        finally
                        {
                            bug.Organism.Trace -= traceEventHandler;
                        }

                        // An abandoned worker's organism has already been removed, and the
                        // game has moved on to other organisms on a new worker
                        if (worker.Abandoned)
                        {
                            return;
                        }

                        if (!success)
                        {
                            // Organism needs to be removed from the game because it had an exception
                            if (deathReason == PopulationChangeReason.Timeout)
                            {
                                _currentEngine.RemoveOrganismQueued(new KilledOrganism(bug.Organism.ID, deathReason));
                            }
                            else
                            {
                                // The organism threw an exception
                                _currentEngine.RemoveOrganismQueued(new KilledOrganism(bug.Organism.ID, deathReason,
                                                                                       exceptionInfo));
                                Trace.WriteLine("Exception in Animal: \r\n" + exceptionInfo);
                            }
                        }
                        else
                        {
                            _totalActivations++;
                            if (PenalizeForTime)
                            {
                                bug.TotalTime += duration;
                                bug.LastTime = duration;
                                if (duration > Quantum)
                                {
                                    bug.Overage += (duration - Quantum);
                                }
                                else if (bug.Overage > 0 && !skippedTurn)
                                {
                                    // If the animal ran under time, subtract this off of
                                    // their overage
                                    bug.Overage -= (Quantum - duration);
                                    if (bug.Overage < 0)
                                    {
                                        bug.Overage = 0;
                                    }
                                }
                            }
                            else
                            {
                                bug.LastTime = duration;
                                bug.Overage = 0;
                            }

                            bug.TotalActivations++;
                        }

                        if (_totalActivations > _lastReport + ReportInterval)
                        {
                            _lastReport = _totalActivations;
                        }
                    }
                }

                // Tell the UI thread we are done with this animal
                worker.Ready.Reset();
                worker.Done.Set();
            }
        }

        /// <summary>
        /// Determines how much actual kernel and user time this thread has actually gotten
        /// </summary>
        /// <param name="worker">The worker whose thread is measured.</param>
        /// <param name="kernel"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        private static bool getAnimalThreadTime(ActivationWorker worker, out Int64 kernel, out Int64 user)
        {
            if (worker.ThreadHandleValid)
            {
                var c = new FILETIME();
                var e = new FILETIME();
                var kernelFileTime = new FILETIME();
                var userFileTime = new FILETIME();

                var success = (GetThreadTimes(worker.ThreadHandle, ref c, ref e, ref kernelFileTime, ref userFileTime) > 0);
                if (success)
                {
                    kernel = (((Int64) kernelFileTime.dwHighDateTime) << 32) + kernelFileTime.dwLowDateTime;
                    user = (((Int64) userFileTime.dwHighDateTime) << 32) + userFileTime.dwLowDateTime;
                    return true;
                }
                kernel = 0;
                user = 0;
                return false;
            }
            kernel = 0;
            user = 0;
            return false;
        }

        private static void traceEventHandler(object sender, params object[] items)
        {
            if (items.Length == 1)
            {
                var item = items[0].ToString();
                if (item.StartsWith("#"))
                {
                    return;
                }
            }

            for (var i = 0; i < items.Length; i++)
            {
                Trace.WriteLine(items[i]);
            }
        }

        /// <summary>
        ///  Used to get thread times for the current thread.  This enables
        ///  the Hosting code to time out creatures.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern int GetThreadTimes(
            IntPtr hThread,
            ref FILETIME lpCreationTime,
            ref FILETIME lpExitTime,
            ref FILETIME lpKernelTime,
            ref FILETIME lpUserTime
            );

        /// <summary>
        ///  Used to get the current thread handle for use with GetThreadTimes
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern IntPtr GetCurrentThread();

        /// <summary>
        ///  Used to get the current process handle for use with DuplicateHandle
        /// </summary>
        [DllImport("kernel32")]
        private static extern IntPtr GetCurrentProcess();

        /// <summary>
        ///  Used to duplicate the thread handle returned from GetCurrentThread
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32")]
        private static extern bool DuplicateHandle(
            IntPtr hSourceProcessHandle, // handle to source process
            IntPtr hSourceHandle, // handle to duplicate
            IntPtr hTargetProcessHandle, // handle to target process
            ref IntPtr lpTargetHandle, // duplicate handle
            int dwDesiredAccess, // requested access
            bool bInheritHandle, // handle inheritance option
            int dwOptions // optional actions
            );

        /// <summary>
        ///  Used to close all handles allocated by GetCurrentThread, GetCurrentProcess,
        ///  and DuplicateHandle
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32")]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}