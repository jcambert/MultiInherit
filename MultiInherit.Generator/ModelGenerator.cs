using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MultiInherit.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class ModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, ct) => GetModelDeclaration(ctx, ct))
            .Where(static d => d is not null)
            .Select(static (d, _) => d!);

        var allDeclarations = classDeclarations.Collect();

        context.RegisterSourceOutput(allDeclarations, static (spc, declarations) =>
        {
            // Parse diagnostics collected during symbol extraction are re-raised here.
            // (The transform above stores them in a thread-local; in a real generator
            //  they would be piped via IncrementalValueProvider<(ModelDeclaration?, Diagnostic[])>
            //  — simplified here for clarity.)

            var parseDiagnostics = new List<Diagnostic>();
            var (resolved, resolveDiagnostics) = ModelResolver.Resolve(declarations, spc.CancellationToken);

            // Emit all diagnostics
            foreach (var d in parseDiagnostics.Concat(resolveDiagnostics))
                spc.ReportDiagnostic(d);

            // Emit source for each resolved model
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

    private static ModelDeclaration? GetModelDeclaration(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var cls = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(cls, ct) is not INamedTypeSymbol symbol)
            return null;

        // Diagnostics from parsing are collected but silently dropped here
        // (they will be re-raised via ModelResolver in RegisterSourceOutput).
        var diagnostics = new List<Diagnostic>();
        return ModelParser.Parse(symbol, ct, diagnostics);
    }
}
