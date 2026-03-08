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
    /// Collects metadata references from all currently loaded assemblies.
    /// This guarantees that MultiInherit.Core (already loaded by the test
    /// process) is included, together with all BCL assemblies.
    /// </summary>
    private static IEnumerable<MetadataReference> GetReferences()
        => AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location));
}
