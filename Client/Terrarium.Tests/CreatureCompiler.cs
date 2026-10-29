using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OrganismBase;

namespace Terrarium.Tests
{
    /// <summary>
    ///  Compiles creature source code into assemblies for the tests.
    /// </summary>
    internal static class CreatureCompiler
    {
        /// <summary>
        ///  A minimal animal; the body is inserted into its class.
        /// </summary>
        public const string AnimalTemplate = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OrganismBase;

[assembly: OrganismClass(""Fixture.TestAnimal"")]
[assembly: AuthorInformation(""Terrarium tests"", ""tests@example.com"")]

namespace Fixture
{
    [Carnivore(false)]
    [MatureSize(26)]
    [AnimalSkin(AnimalSkinFamily.Beetle)]
    [MarkingColor(KnownColor.Red)]
    [MaximumEnergyPoints(10)]
    [EatingSpeedPoints(10)]
    [AttackDamagePoints(10)]
    [DefendDamagePoints(10)]
    [MaximumSpeedPoints(10)]
    [CamouflagePoints(10)]
    [EyesightPoints(40)]
    public class TestAnimal : Animal
    {
        protected override void Initialize()
        {
            Idle += OnIdle;
        }

        private void OnIdle(object sender, IdleEventArgs e)
        {
            Act();
        }

        public override void SerializeAnimal(MemoryStream m) { }
        public override void DeserializeAnimal(MemoryStream m) { }

%BODY%
    }
}
";

        /// <summary>
        ///  Compiles an animal whose class contains the given members.  The
        ///  class must define a method "void Act()".
        /// </summary>
        public static string CompileAnimal(string directory, string assemblyName, string members,
                                           bool allowUnsafe = false)
        {
            return Compile(directory, assemblyName, new[] {AnimalTemplate.Replace("%BODY%", members)}, allowUnsafe);
        }

        /// <summary>
        ///  Compiles source files into directory/assemblyName.dll and returns its path.
        /// </summary>
        public static string Compile(string directory, string assemblyName, IEnumerable<string> sources,
                                     bool allowUnsafe = false)
        {
            var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source)).ToList();
            var compilation = CSharpCompilation.Create(
                assemblyName,
                trees,
                References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                             allowUnsafe: allowUnsafe,
                                             optimizationLevel: OptimizationLevel.Release));

            var path = Path.Combine(directory, assemblyName + ".dll");
            var result = compilation.Emit(path);
            if (!result.Success)
            {
                throw new InvalidOperationException("Creature didn't compile:\n" +
                                                    string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            }
            return path;
        }

        private static IEnumerable<MetadataReference> References()
        {
            // The .NET base library the tests run on, plus OrganismBase
            var platform = ((string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal) ||
                               Path.GetFileName(path) == "netstandard.dll" ||
                               Path.GetFileName(path) == "mscorlib.dll");
            return platform.Append(typeof (Animal).Assembly.Location)
                           .Select(path => MetadataReference.CreateFromFile(path));
        }
    }
}
