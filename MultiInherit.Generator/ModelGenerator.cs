using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

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

        // Étape batch : résolution cross-modèles (parents, cycles, relations).
        // Les dépendances inter-modèles imposent de traiter tous les modèles ensemble.
        var resolvedBatch = allResults.Select(static (results, ct) =>
        {
            var diagsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();
            foreach (var r in results)
                diagsBuilder.AddRange(r.Diagnostics);

            var declarations = results
                .Where(static r => r.Declaration is not null)
                .Select(static r => r.Declaration!);

            var (models, resolveDiags) = ModelResolver.Resolve(declarations, ct);
            diagsBuilder.AddRange(resolveDiags);

            return (Models: ImmutableArray.CreateRange(models), Diagnostics: diagsBuilder.ToImmutable());
        });

        // Diagnostics enregistrés séparément de l'émission des sources
        context.RegisterSourceOutput(resolvedBatch, static (spc, batch) =>
        {
            foreach (var d in batch.Diagnostics)
                spc.ReportDiagnostic(d);
        });

        // Émission par modèle : ResolvedModelComparer permet à Roslyn d'éviter de
        // régénérer les fichiers .g.cs inchangés lors des rebuilds incrémentiels.
        var perModel = resolvedBatch
            .SelectMany(static (batch, _) => batch.Models)
            .WithComparer(ResolvedModelComparer.Instance);
        context.RegisterSourceOutput(perModel, static (spc, model) =>
        {
            spc.CancellationToken.ThrowIfCancellationRequested();

            var source = CodeEmitter.Emit(model);
            var hintName = $"{model.Namespace}.{model.ClassName}.g.cs"
                .Replace('<', '_').Replace('>', '_')
                .TrimStart('.');

            spc.AddSource(hintName, source);
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
                if (name is "Model" or "ModelAttribute"
                         or "Inherit" or "InheritAttribute"
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
