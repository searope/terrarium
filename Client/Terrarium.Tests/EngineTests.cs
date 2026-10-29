using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OrganismBase;
using Terrarium.Game;

[assembly: DoNotParallelize]

namespace Terrarium.Tests
{
    /// <summary>
    ///  Runs the game engine without the UI: introducing creatures, checking
    ///  them, saving and loading, and timing them out.  The engine is a
    ///  process-wide singleton, so these tests run one at a time.
    /// </summary>
    [TestClass]
    public class EngineTests
    {
        private string _directory;

        [TestInitialize]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "terrarium-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void StopEngine()
        {
            if (GameEngine.Current != null)
            {
                GameEngine.Current.StopGame(false);
            }

            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
                // A creature that hung may still hold its file; the temp folder is cleaned up eventually
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private string SavePath
        {
            get { return Path.Combine(_directory, "test.ter"); }
        }

        private static void RunTicks(int ticks)
        {
            var target = GameEngine.Current.CurrentVector.State.TickNumber + ticks;
            while (GameEngine.Current.CurrentVector.State.TickNumber < target)
            {
                GameEngine.Current.ProcessTurn();
            }
        }

        private static List<OrganismState> Organisms()
        {
            return GameEngine.Current.CurrentVector.State.Organisms.Cast<OrganismState>().ToList();
        }

        private string CompileSampleHerbivore()
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "Creatures", "Herbivore");
            var sources = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText);
            return CreatureCompiler.Compile(_directory, "Herbivore", sources);
        }

        [TestMethod]
        public void SampleHerbivoreIsIntroducedAndRuns()
        {
            GameEngine.NewTerrariumGame(_directory, SavePath);
            var species = GameEngine.Current.AddNewOrganism(CompileSampleHerbivore(), Point.Empty, false);

            RunTicks(20);

            Assert.AreEqual("Herbivore", species.Name, true);
            Assert.AreEqual(10, Organisms().Count(o => ((Species) o.Species).Name == species.Name));
        }

        [TestMethod]
        public void OrdinaryCreatureCodeIsAllowed()
        {
            // Lambdas, LINQ, collections, string interpolation and array
            // initializers all compile to things the checker must accept
            var creature = CreatureCompiler.CompileAnimal(_directory, "Ordinary", @"
        private readonly List<int> _seen = new List<int>();
        private readonly Dictionary<string, Point> _places = new Dictionary<string, Point>();
        private readonly Random _random = new Random();
        private const int Limit = 5;

        private void Act()
        {
            int[] steps = { 1, 2, 3 };
            _seen.AddRange(steps.Where(s => s % 2 == 1).Select(s => s * Limit));
            _places[$""{Position.X},{Position.Y}""] = Position;
            var text = new System.Text.StringBuilder().Append(Math.Max(_seen.Count, _random.Next(10))).ToString();
            if (!IsMoving && text.Length > 0)
            {
                BeginMoving(new MovementVector(new Point(Position.X + 10, Position.Y + 10), 2));
            }
        }");

            GameEngine.NewTerrariumGame(_directory, SavePath);
            GameEngine.Current.AddNewOrganism(creature, Point.Empty, false);
            RunTicks(5);

            Assert.AreEqual(10, Organisms().Count);
        }

        [TestMethod]
        [DataRow("files", @"private void Act() { File.WriteAllText(""x.txt"", ""x""); }", false)]
        [DataRow("threads", @"private void Act() { new System.Threading.Thread(() => { }).Start(); }", false)]
        [DataRow("reflection", @"private void Act() { var methods = GetType().GetMethods(); }", false)]
        [DataRow("process exit", @"private void Act() { Environment.Exit(0); }", false)]
        [DataRow("processes", @"private void Act() { System.Diagnostics.Process.Start(""calc""); }", false)]
        [DataRow("network", @"private void Act() { new System.Net.Http.HttpClient(); }", false)]
        [DataRow("locks", @"private void Act() { lock (this) { } }", false)]
        [DataRow("static fields", @"private static int _count; private void Act() { _count++; }", false)]
        [DataRow("native code", @"[System.Runtime.InteropServices.DllImport(""user32.dll"")] private static extern int MessageBeep(int type);
        private void Act() { MessageBeep(0); }", false)]
        [DataRow("pointers", @"private unsafe void Act() { int x = 0; int* p = &x; *p = 1; }", true)]
        public void ForbiddenCreatureCodeIsRejected(string what, string members, bool allowUnsafe)
        {
            var creature = CreatureCompiler.CompileAnimal(_directory, "Forbidden", members, allowUnsafe);
            GameEngine.NewTerrariumGame(_directory, SavePath);

            var error = Assert.ThrowsExactly<OrganismAssemblyFailedValidationException>(
                () => GameEngine.Current.AddNewOrganism(creature, Point.Empty, false), what);

            StringAssert.Contains(error.Message, "Please fix the following");
            Assert.AreEqual(0, Organisms().Count, "no organisms should have been added");
        }

        [TestMethod]
        public void SavedTerrariumLoadsWithTheSameOrganisms()
        {
            GameEngine.NewTerrariumGame(_directory, SavePath);
            GameEngine.Current.AddNewOrganism(CompileSampleHerbivore(), Point.Empty, false);
            RunTicks(40);

            var tick = GameEngine.Current.CurrentVector.State.TickNumber;
            var before = Organisms().ToDictionary(o => o.ID, Describe);
            GameEngine.Current.StopGame(true);

            StringAssert.StartsWith(File.ReadAllText(SavePath), "{\"format\":\"Terrarium.Save\"");

            GameEngine.LoadTerrariumGame(_directory, SavePath);
            Assert.IsNull(GameEngine.Current.LoadWarning, GameEngine.Current.LoadWarning);
            Assert.AreEqual(tick, GameEngine.Current.CurrentVector.State.TickNumber);
            CollectionAssert.AreEquivalent(before, Organisms().ToDictionary(o => o.ID, Describe));

            // The loaded creatures keep running
            RunTicks(20);
            Assert.AreEqual(10, Organisms().Count);
        }

        [TestMethod]
        public void OldSaveFileIsKeptAndANewTerrariumStarts()
        {
            // BinaryFormatter streams start with a zero byte
            File.WriteAllBytes(SavePath, new byte[] {0, 1, 0, 0, 0, 255, 255, 255, 255, 1, 0, 0, 0});

            GameEngine.LoadTerrariumGame(_directory, SavePath);

            StringAssert.Contains(GameEngine.Current.LoadWarning, "older version of Terrarium");
            Assert.IsTrue(File.Exists(SavePath + ".bak"));
            Assert.AreEqual(0, Organisms().Count);
        }

        [TestMethod]
        public void CreatureThatNeverReturnsIsRemoved()
        {
            var creature = CreatureCompiler.CompileAnimal(_directory, "Hangs", @"
        private void Act()
        {
            while (true)
            {
            }
        }");

            GameEngine.NewTerrariumGame(_directory, SavePath);
            GameEngine.Current.AddNewOrganism(creature, Point.Empty, false);
            var started = DateTime.UtcNow;
            RunTicks(10);

            // The game kept going, the whole species is gone and it can't come back
            Assert.IsTrue(DateTime.UtcNow - started < TimeSpan.FromSeconds(30), "the game should not wait on the creature");
            Assert.AreEqual(0, Organisms().Count);
            CollectionAssert.Contains(GameEngine.Current.Pac.GetBlacklistedAssemblies(), "hangs");
        }

        private static string Describe(OrganismState state)
        {
            return string.Format("{0},{1} energy {2:F3} radius {3} age {4}", state.Position.X, state.Position.Y,
                                 state.StoredEnergy, state.Radius, state.TickAge);
        }
    }
}
