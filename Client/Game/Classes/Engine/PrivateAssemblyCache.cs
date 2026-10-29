//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.                                                              
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using OrganismBase;
using Terrarium.Configuration;
using Terrarium.Hosting;
using Terrarium.Tools;

namespace Terrarium.Game
{
    /// <summary>
    ///  A special cache that protects storage of organism assemblies
    ///  and provides assembly resolving features.  We store all organism assemblies
    ///  in a special obfuscated directory so that malicious code can't send assemblies
    ///  to Terrarium and then take advantage of exploits in them later.
    /// </summary>
    [Serializable]
    public class PrivateAssemblyCache
    {
        private static readonly string _versionDirectoryPreamble;
        private string _dataFile;
        private string _dataPath;
        private bool _hookAssemblyResolve;
        private Hashtable _hsh;
        private long _pacSize;
        private bool _trackLastRun = true;

        /// <summary>
        ///  Loads this cache's creature assemblies.
        /// </summary>
        [NonSerialized] private CreatureLoadContext _loadContext;

        /// <summary>
        ///  The creature assemblies loaded so far, by short name.
        /// </summary>
        [NonSerialized] private Dictionary<string, Assembly> _loaded;

        /// <summary>
        ///  Initializes a versioned directory preamble used to create
        ///  versioned assembly directories.
        /// </summary>
        static PrivateAssemblyCache()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            _versionDirectoryPreamble = version.Major + "." + version.Minor + "." + version.Build;
        }

        /// <summary>
        ///  Creates a new private assembly cache initialized with a data path and
        ///  data file.
        /// </summary>
        /// <param name="dataPath">Path where the data will be stored.</param>
        /// <param name="dataFile">Path to a tracking data file.</param>
        public PrivateAssemblyCache(string dataPath, string dataFile)
        {
            Initialize(dataPath, dataFile, true, true);
        }

        /// <summary>
        ///  Creates a new private assembly cache initialized with a data path,
        ///  data file, and controls whether assemblies are resolved, and organism
        ///  tracking is in effect.
        /// </summary>
        /// <param name="dataPath">Path to where the cache will be stored.</param>
        /// <param name="dataFile">Path to the tracking data file.</param>
        /// <param name="hookAssemblyResolve">Determines if the assembly resolving events are hooked.</param>
        /// <param name="trackLastRun">Determines if organism tracking is senabled.</param>
        public PrivateAssemblyCache(string dataPath, string dataFile, bool hookAssemblyResolve, bool trackLastRun)
        {
            Initialize(dataPath, dataFile, hookAssemblyResolve, trackLastRun);
        }

        /// <summary>
        ///  We track when organisms are running and Terrarium shuts down unexpectedly so that
        ///  we can blacklist them.
        /// </summary>
        public string LastRun
        {
            get
            {
                using (var lastRunFile = File.Open(AssemblyDirectory + "\\data.dat", FileMode.OpenOrCreate))
                {
                    using (var lastRunReader = new BinaryReader(lastRunFile))
                    {
                        return lastRunFile.Length == 0 ? "" : lastRunReader.ReadString();
                    }
                }
            }

            set
            {
                using (var lastRunFile = File.Open(AssemblyDirectory + "\\data.dat", FileMode.OpenOrCreate))
                {
                    using (var lastRunWriter = new BinaryWriter(lastRunFile))
                    {
                        // If we're tracking who is running OR we're not but we're clearing it
                        // go ahead and write. This last case is needed since, even if we aren't
                        // tracking who is running, we need to clear it on startup and we want to
                        // make sure we do
                        if (_trackLastRun || (!_trackLastRun && value.Length == 0))
                        {
                            lastRunWriter.Write(value);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///  Returns the path to the assembly cache directory.
        /// </summary>
        /// <threadsafe/>
        public string AssemblyDirectory
        {
            get { return BaseAssemblyDirectory; }
        }

        /// <summary>
        ///  Returns the base assembly directory without obfuscation.
        /// </summary>
        /// <threadsafe/>
        private string BaseAssemblyDirectory
        {
            get { return GetBaseAssemblyDirectory(_dataPath, _dataFile); }
        }

        /// <summary>
        ///  Return the approximate size of the PAC in memory based on loaded
        ///  assemblies.
        /// </summary>
        public long PacSize
        {
            get { return _pacSize; }
        }

        /// <summary>
        ///  Return the approximate amount of organisms loaded from
        ///  the current PAC.
        /// </summary>
        public int PacOrganismCount
        {
            get { return _hsh.Count; }
        }

        /// <summary>
        ///  Provides access to the versioned directory preamble for generating
        ///  cache directories.
        /// </summary>
        /// <threadsafe/>
        public static string VersionedDirectoryPreamble
        {
            get { return _versionDirectoryPreamble; }
        }

        /// <summary>
        ///  Helper function for initializing the PAC constructors.
        /// </summary>
        /// <param name="dataPath">Path to where the cache will be stored.</param>
        /// <param name="dataFile">Path to the tracking data file.</param>
        /// <param name="hookAssemblyResolve">Determines if the assembly resolving events are hooked.</param>
        /// <param name="trackLastRun">Determines if organism tracking is senabled.</param>
        private void Initialize(string dataPath, string dataFile, bool hookAssemblyResolve, bool trackLastRun)
        {
            _trackLastRun = trackLastRun;
            _dataPath = string.IsNullOrEmpty(dataPath) ? Path.GetFullPath(".") : Path.GetFullPath(dataPath);

            _dataFile = Path.GetFileName(dataFile);
            _hsh = new Hashtable();
            _loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            _loadContext = new CreatureLoadContext("Creatures in " + _dataFile, ResolveCreatureReference, false);

            if (!Directory.Exists(BaseAssemblyDirectory))
            {
                Directory.CreateDirectory(BaseAssemblyDirectory);
            }

            // We need to ask the CLR to ask us to find the assemblies because they are in a 
            // non-standard location.
            _hookAssemblyResolve = hookAssemblyResolve;
            if (hookAssemblyResolve)
            {
                HookAssemblyResolve();
            }
        }

        /// <summary>
        ///  Event used to notify clients when assemblies in the
        ///  PAC have changed.
        /// </summary>
        public event PacAssembliesChangedEventHandler PacAssembliesChanged;

        /// <summary>
        ///  Helper function for calling the PacAssembliesChanged
        ///  event.
        /// </summary>
        /// <param name="e">Null</param>
        protected void OnPacAssembliesChanged(EventArgs e)
        {
            if (PacAssembliesChanged != null)
            {
                PacAssembliesChanged(this, e);
            }
        }

        /// <summary>
        ///  Hooks the assembly resolving events for the current app
        ///  domain.
        /// </summary>
        public void HookAssemblyResolve()
        {
            if (_hookAssemblyResolve)
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            }
        }

        /// <summary>
        ///  Handles controlled shut-down of the PAC and unhooking the assembly
        ///  resolving events.
        /// </summary>
        public void Close()
        {
            if (_hookAssemblyResolve)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
            }
        }

        /// <summary>
        ///  Given an assembly full name, generates a library file name
        ///  for an assembly.
        /// </summary>
        /// <param name="fullName">The assembly full name.</param>
        /// <returns>The new file name generated from an assembly name and dll extension.</returns>
        /// <threadsafe/>
        public string GetFileName(string fullName)
        {
            var directory = Path.GetFullPath(AssemblyDirectory);
            var fileName = Path.GetFullPath(Path.Combine(directory, GetAssemblyShortName(fullName) + ".dll"));

            // An assembly name could contain path characters; make sure the file
            // can only ever be inside the cache directory
            if (!string.Equals(Path.GetDirectoryName(fileName), directory.TrimEnd(Path.DirectorySeparatorChar),
                               StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The assembly name '" + fullName + "' isn't a valid organism assembly name.");
            }

            return fileName;
        }

        /// <summary>
        ///  Given an assembly full name, generates an assembly short
        ///  name.
        /// </summary>
        /// <param name="fullName">The assembly full name.</param>
        /// <returns>The short name for the assembly.</returns>
        /// <threadsafe/>
        public static string GetAssemblyShortName(string fullName)
        {
            var chunks = fullName.Split(new[] {','});
            // return tolower so case insensitive stuff works ok
            return chunks[0].ToLower(CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///  Given an assembly full name, generates an assembly
        ///  version string.
        /// </summary>
        /// <param name="fullName">The assembly full name.</param>
        /// <returns>The version of the assembly.</returns>
        /// <threadsafe/>
        public static string GetAssemblyVersion(string fullName)
        {
            var chunks = fullName.Split(new[] {','});
            var versionChunk = chunks[1];
            var versionPieces = versionChunk.Split(new[] {'='});
            return versionPieces[1];
        }

        /// <summary>
        ///  Creates a base assembly directory without obfuscation from a path and file name.
        /// </summary>
        /// <param name="dataPath">The base path of the data file.</param>
        /// <param name="dataFile">The name of the datafile for creating a cache directory.</param>
        /// <returns>The base assembly directory.</returns>
        /// <threadsafe/>
        public static string GetBaseAssemblyDirectory(string dataPath, string dataFile)
        {
            var dataFilePart = dataFile.Substring(0, dataFile.LastIndexOf("."));
            return Path.Combine(dataPath, dataFilePart + "_Assemblies");
        }

        /// <summary>
        ///  Blacklist a series of assemblies by setting their assemblies to zero length
        ///  files.  This is a way of ensuring that we don't load them again, or replace them
        ///  with fresh working copies because the file name gets reserved with an invalid assembly.
        /// </summary>
        /// <param name="assemblies">The assemblies to be blacklisted.</param>
        public void BlacklistAssemblies(string[] assemblies)
        {
            // GetFileName only returns files inside the assembly directory, so
            // the caller can't make this file get created somewhere else
            if (assemblies != null)
            {
                foreach (var assembly in assemblies)
                {
                    var fileName = GetFileName(assembly);
                    try
                    {
                        // Replace this file with a zero length file (or create one), so we won't get it again
                        using (var stream = File.Create(fileName))
                        {
                            stream.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Error blacklisting organism:");
                        ErrorLog.LogHandledException(e);
                    }
                }
            }
        }

        /// <summary>
        ///  Load an organism assembly.  Calculate the size of the PAC
        ///  based on the assemblies loaded.
        /// </summary>
        /// <param name="fullName">The name of the assembly to load.</param>
        /// <returns>The loaded assembly.</returns>
        public Assembly LoadOrganismAssembly(string fullName)
        {
            if (!_hsh.ContainsKey(fullName))
            {
                _hsh[fullName] = fullName;
                _pacSize += new FileInfo(GetFileName(fullName)).Length;
            }

            // GetFileName only returns files inside the assembly directory, so a
            // bogus name can't load an assembly from anywhere else
            var shortName = GetAssemblyShortName(fullName);
            Assembly organismAssembly;
            lock (_loaded)
            {
                if (!_loaded.TryGetValue(shortName, out organismAssembly))
                {
                    // Load from memory so the file isn't locked: blacklisting
                    // replaces it with an empty file
                    var fileName = GetFileName(fullName);
                    var symbols = Path.ChangeExtension(fileName, ".pdb");
                    using (var assembly = new MemoryStream(File.ReadAllBytes(fileName)))
                    using (var pdb = File.Exists(symbols) ? new MemoryStream(File.ReadAllBytes(symbols)) : null)
                    {
                        organismAssembly = _loadContext.LoadFromStream(assembly, pdb);
                    }
                    _loaded[shortName] = organismAssembly;
                }
            }

            return organismAssembly;
        }

        /// <summary>
        ///  Determine if the assembly with the given full name exists
        ///  within the PAC.
        /// </summary>
        /// <param name="fullName">The full name of the assembly to check for.</param>
        /// <returns>True if the assembly exists, false otherwise.</returns>
        public Boolean Exists(string fullName)
        {
            // GetFileName only returns files inside the assembly directory
            return File.Exists(GetFileName(fullName));
        }

        /// <summary>
        ///  Saves the array of bytes given an assembly full name to
        ///  the private assembly cache.
        /// </summary>
        /// <param name="bytes">The bytes of the assembly.</param>
        /// <param name="fullName">The full name of the original assembly.</param>
        public void SaveOrganismBytes(byte[] bytes, string fullName)
        {
            var directory = AssemblyDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var fileName = GetFileName(fullName);

            if (File.Exists(fileName))
            {
                return;
            }

            // GetFileName only returns files inside the assembly directory, so
            // the caller can't make this file get created somewhere else
            var targetStream = File.Create(fileName);
            try
            {
                targetStream.Write(bytes, 0, bytes.Length);
            }
            catch
            {
                targetStream.Close();

                // If something happens, delete the file so we don't have
                // a corrupted file hanging around
                File.Delete(fileName);

                throw;
            }
            finally
            {
                targetStream.Close();
            }

            OnPacAssembliesChanged(new EventArgs());
        }

        /// <summary>
        ///  Save an assembly given a full path to the assembly and the full
        ///  name of the assembly to the cache.
        /// </summary>
        /// <param name="assemblyPath">A local path to the assembly.</param>
        /// <param name="fullName">The full name of the assembly.</param>
        public void SaveOrganismAssembly(string assemblyPath, string fullName)
        {
            SaveOrganismAssembly(assemblyPath, null, fullName);
        }

        /// <summary>
        ///  Save an assembly along with symbols given a full path to the
        ///  assembly and symbols to the cache.
        /// </summary>
        /// <param name="assemblyPath">A local path to the assembly.</param>
        /// <param name="symbolPath">A local path to the symbols.</param>
        /// <param name="fullName">The full name of the assembly.</param>
        public void SaveOrganismAssembly(string assemblyPath, string symbolPath, string fullName)
        {
            var directory = AssemblyDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var fileName = GetFileName(fullName);
            var symName = Path.ChangeExtension(GetFileName(fullName), ".pdb");

            if (File.Exists(fileName))
            {
                return;
            }

            VerifyAssembly(File.ReadAllBytes(assemblyPath));

            var sourceStream = File.OpenRead(assemblyPath);
            FileStream symStream = null;
            try
            {
                if (!string.IsNullOrEmpty(symbolPath) && File.Exists(symbolPath))
                {
                    symStream = File.OpenRead(symbolPath);
                }
            }
            catch
            {
                // In case anything goes wrong with the symbols (access denied and others), we are good
            }

            // GetFileName only returns files inside the assembly directory, so
            // the caller can't make this file get created somewhere else
            try
            {
                var targetStream = File.Create(fileName);
                try
                {
                    var bytes = new byte[65536];
                    int bytesRead;
                    while ((bytesRead = sourceStream.Read(bytes, 0, 65536)) > 0)
                    {
                        targetStream.Write(bytes, 0, bytesRead);
                    }
                }
                catch
                {
                    targetStream.Close();

                    // If something happens, delete the file so we don't have
                    // a corrupted file hanging around
                    File.Delete(fileName);

                    throw;
                }
                finally
                {
                    targetStream.Close();
                }

                if (symStream != null)
                {
                    try
                    {
                        targetStream = File.Create(symName);
                        try
                        {
                            var bytes = new byte[65536];
                            int bytesRead;
                            while ((bytesRead = symStream.Read(bytes, 0, 65536)) > 0)
                            {
                                targetStream.Write(bytes, 0, bytesRead);
                            }
                        }
                        catch
                        {
                            targetStream.Close();

                            // If something happens, delete the file so we don't have
                            // a corrupted file hanging around
                            File.Delete(symName);

                            throw;
                        }
                        finally
                        {
                            targetStream.Close();
                        }
                    }
                    catch
                    {
                        // No reason to crash
                    }
                }
            }
            finally
            {
                sourceStream.Close();
            }

            OnPacAssembliesChanged(new EventArgs());
        }

        /// <summary>
        ///  Gets a full listing of all of the assemblies in the
        ///  cache.  This can be used to populate dropdowns or lists.
        /// </summary>
        /// <returns>A set of OrganismAssemblyInfo objects for each assembly.</returns>
        public OrganismAssemblyInfo[] GetAssemblies()
        {
            var infoList = new ArrayList();

            if (!Directory.Exists(AssemblyDirectory))
            {
                return new OrganismAssemblyInfo[0];
            }

            var fileEntries = Directory.GetFiles(AssemblyDirectory);
            foreach (var fileName in fileEntries)
            {
                if (Path.GetExtension(fileName).ToLower(CultureInfo.InvariantCulture) != ".dll") continue;
                var info = new FileInfo(fileName);
                if (info.Length <= 0) continue;
                // Read the name without loading the assembly
                var fullName = AssemblyName.GetAssemblyName(fileName).FullName;
                infoList.Add(new OrganismAssemblyInfo(fullName, GetAssemblyShortName(fullName)));
            }

            return (OrganismAssemblyInfo[]) infoList.ToArray(typeof (OrganismAssemblyInfo));
        }

        /// <summary>
        ///  Get a list of all assemblies in the cache that have
        ///  0 bytes.  A 0 byte assembly fails to load.
        /// </summary>
        /// <returns>A listing of all 0 byte assemblies in the cache.</returns>
        public string[] GetBlacklistedAssemblies()
        {
            var blacklistedAssemblies = new ArrayList();

            if (Directory.Exists(AssemblyDirectory))
            {
                var fileEntries = Directory.GetFiles(AssemblyDirectory);
                foreach (var fileName in fileEntries)
                {
                    if (Path.GetExtension(fileName).ToLower(CultureInfo.InvariantCulture) == ".dll")
                    {
                        var info = new FileInfo(fileName);
                        if (info.Length == 0)
                        {
                            blacklistedAssemblies.Add(Path.GetFileNameWithoutExtension(fileName));
                        }
                    }
                }
            }

            return (string[]) blacklistedAssemblies.ToArray(typeof (string));
        }

        /// <summary>
        ///  Called whenever assemblies are being resolved for use.
        /// </summary>
        /// <param name="sender">Unknown</param>
        /// <param name="args">Assembly resolve information</param>
        /// <returns>The assembly that matches the resolve information.</returns>
        internal Assembly ResolveAssembly(Object sender, ResolveEventArgs args)
        {
            // Test for OrganismBase
            var matchAssembly = getAssemblyWithTerrariumBindingPolicy(args.Name,
                                                                      Assembly.GetAssembly(typeof (Animal)));
            if (matchAssembly != null)
            {
                return matchAssembly;
            }

            // Test for Terrarium
            matchAssembly = getAssemblyWithTerrariumBindingPolicy(args.Name,
                                                                  Assembly.GetAssembly(typeof (PrivateAssemblyCache)));
            if (matchAssembly != null)
            {
                return matchAssembly;
            }

            // See if it is an organism assembly; Type.GetType on the creature
            // types named in save files and species gets here

            // Since .NET 4 this event is also raised for satellite resource
            // assemblies (Controls.resources and the like) and generated
            // serializers.  Returning null lets the runtime fall back as usual
            // instead of failing on a file the cache never had.
            if (!File.Exists(GetFileName(args.Name)))
            {
                return null;
            }

            return LoadOrganismAssembly(args.Name);
        }

        /// <summary>
        ///  Check to see if an assembly can be found that matches a reduced
        ///  set of fusion version requirements.  We ignore the revision.
        /// </summary>
        /// <param name="testAssemblyName">The name of the test assembly.</param>
        /// <param name="potentialMatchAssembly">A potentially matching assembly.</param>
        /// <returns>The matching assembly if it really matches, else null.</returns>
        private static Assembly getAssemblyWithTerrariumBindingPolicy(string testAssemblyName,
                                                                      Assembly potentialMatchAssembly)
        {
            Debug.Assert(potentialMatchAssembly != null);
            try
            {
                if (GetAssemblyShortName(testAssemblyName) == GetAssemblyShortName(potentialMatchAssembly.FullName))
                {
                    var testAssemblyVersion = new Version(GetAssemblyVersion(testAssemblyName));
                    Debug.Assert(testAssemblyVersion != null);
                    var potentialAssemblyVersion = potentialMatchAssembly.GetName().Version;

                    if (testAssemblyVersion.Major == potentialAssemblyVersion.Major &&
                        testAssemblyVersion.Minor == potentialAssemblyVersion.Minor &&
                        testAssemblyVersion.Build == potentialAssemblyVersion.Build)
                    {
                        return potentialMatchAssembly;
                    }
                }

                return null;
            }
            catch (Exception e)
            {
                ErrorLog.LogHandledException(e);
                return null;
            }
        }

        /// <summary>
        ///  Creates a temp file that can't be guessed for security reasons.
        /// </summary>
        /// <returns>A temp file GUID encoded for security.</returns>
        public static string GetSafeTempFileName()
        {
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        /// <summary>
        ///  Used for assembly validation with XML reporting in AsmCheck
        /// </summary>
        internal static void VerifyAssembly(byte[] assembly)
        {
            var problems = CreatureVerifier.Verify(assembly);
            if (problems.Count != 0)
            {
                throw new OrganismAssemblyFailedValidationException(problems);
            }
        }

        /// <summary>
        ///  Resolves an assembly a creature references: the game's own
        ///  OrganismBase and Game assemblies, or another assembly in this
        ///  cache.  Returns null to let the default context resolve it (the
        ///  .NET base library).
        /// </summary>
        internal Assembly ResolveCreatureReference(AssemblyName name)
        {
            var shared = ResolveSharedAssembly(name);
            if (shared != null)
            {
                return shared;
            }

            return File.Exists(GetFileName(name.FullName)) ? LoadOrganismAssembly(name.FullName) : null;
        }

        /// <summary>
        ///  Returns the game's copy of OrganismBase or Game when a creature
        ///  references a compatible version of it, or null otherwise.
        /// </summary>
        internal static Assembly ResolveSharedAssembly(AssemblyName name)
        {
            return getAssemblyWithTerrariumBindingPolicy(name.FullName, typeof (Animal).Assembly) ??
                   getAssemblyWithTerrariumBindingPolicy(name.FullName, typeof (PrivateAssemblyCache).Assembly);
        }
    }
}