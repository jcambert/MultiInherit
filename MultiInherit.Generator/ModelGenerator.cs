using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MultiInherit.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class ModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var allResults = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, ct) => GetModelDeclaration(ctx, ct))
            .Where(static r => r.Declaration is not null || r.Diagnostics.Length > 0)
            .Collect();

        context.RegisterSourceOutput(allResults, static (spc, results) =>
        {
            // Re-emit parser diagnostics collected during the transform phase
            foreach (var result in results)
                foreach (var d in result.Diagnostics)
                    spc.ReportDiagnostic(d);

            // Resolve only non-null declarations
            var declarations = results
                .Where(static r => r.Declaration is not null)
                .Select(static r => r.Declaration!);

            var (resolved, resolveDiagnostics) = ModelResolver.Resolve(declarations, spc.CancellationToken);

            foreach (var d in resolveDiagnostics)
                spc.ReportDiagnostic(d);

            foreach (var model in resolved)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();

                var source   = CodeEmitter.Emit(model);
                var hintName = $"{model.Namespace}.{model.ClassName}.g.cs"
                    .Replace('<', '_').Replace('>', '_')
                    .TrimStart('.');

                spc.AddSource(hintName, source);
            }
        });
    }

    // ── Syntax predicate ──────────────────────────────────────────────────

    private static bool IsCandidate(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax cls) return false;
        if (cls.AttributeLists.Count == 0) return false;

        foreach (var attrList in cls.AttributeLists)
        foreach (var attr in attrList.Attributes)
        {
            var name = attr.Name.ToString();
            if (name is "Model"    or "ModelAttribute"
                     or "Inherit"  or "InheritAttribute"
                     or "Inherits" or "InheritsAttribute")
                return true;
        }
        return false;
    }

    // ── Semantic transform ────────────────────────────────────────────────

    private static (ModelDeclaration? Declaration, Diagnostic[] Diagnostics) GetModelDeclaration(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var cls = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(cls, ct) is not INamedTypeSymbol symbol)
            return (null, Array.Empty<Diagnostic>());

        var diagnostics = new List<Diagnostic>();
        var decl = ModelParser.Parse(symbol, ct, diagnostics);
        return (decl, diagnostics.Count > 0 ? diagnostics.ToArray() : Array.Empty<Diagnostic>());
    }
}
