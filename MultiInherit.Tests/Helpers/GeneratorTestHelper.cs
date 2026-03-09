using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MultiInherit.Generator;

namespace MultiInherit.Tests.Helpers;

/// <summary>
/// Utility for running the ModelGenerator against in-memory source code
/// and inspecting diagnostics / generated output.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// Compiles the given source strings, runs the ModelGenerator,
    /// and returns generator-emitted diagnostics plus all generated source texts.
    /// </summary>
    public static (ImmutableArray<Diagnostic> Diagnostics, string[] Sources) Run(
        params string[] sources)
    {
        var trees = sources
            .Select(s => CSharpSyntaxTree.ParseText(s))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: trees,
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ModelGenerator();
        var driver = CSharpGeneratorDriver
            .Create(generator)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var runResult = driver.GetRunResult();

        // Generator-emitted diagnostics (via spc.ReportDiagnostic)
        var diagnostics = runResult.Diagnostics;

        var generatedSources = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString())
            .ToArray();

        return (diagnostics, generatedSources);
    }

    /// <summary>Returns only the generator-emitted diagnostics.</summary>
    public static ImmutableArray<Diagnostic> GetDiagnostics(params string[] sources)
        => Run(sources).Diagnostics;

    /// <summary>Returns only the generated source files.</summary>
    public static string[] GetSources(params string[] sources)
        => Run(sources).Sources;

    // ── References ────────────────────────────────────────────────────────

    /// <summary>
    /// Construit la liste des références metadata en commençant par les assemblies
    /// explicitement requises (garantie de présence), puis en complétant avec les
    /// autres assemblies chargées dans le domaine d'application.
    /// L'approche AppDomain seule est non-déterministe : une assembly non encore
    /// chargée au moment de l'appel serait absente de la liste.
    /// </summary>
    private static IEnumerable<MetadataReference> GetReferences()
    {
        // Assemblies toujours requises — chargées explicitement
        var pinned = new[]
        {
            typeof(object).Assembly,                                                    // System.Private.CoreLib
            typeof(System.Linq.Enumerable).Assembly,                                   // System.Linq
            typeof(System.Collections.Generic.List<>).Assembly,                        // System.Collections
            typeof(System.ComponentModel.INotifyPropertyChanged).Assembly,             // System.ObjectModel
            typeof(System.Runtime.CompilerServices.ModuleInitializerAttribute).Assembly, // System.Runtime
            typeof(MultiInherit.ModelAttribute).Assembly,                              // MultiInherit.Core
        };

        var locations = new HashSet<string>(
            pinned.Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                  .Select(a => a.Location),
            StringComparer.OrdinalIgnoreCase);

        // Complément : autres assemblies déjà chargées dans le processus de test
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if (!asm.IsDynamic && !string.IsNullOrWhiteSpace(asm.Location))
                locations.Add(asm.Location);

        return locations.Select(loc => (MetadataReference)MetadataReference.CreateFromFile(loc));
    }
}
