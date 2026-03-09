using Microsoft.CodeAnalysis;

namespace MultiInherit.Generator;

internal static class ModelResolver
{
    public static (IReadOnlyList<ResolvedModel> Models, IReadOnlyList<Diagnostic> Diagnostics) Resolve(
        IEnumerable<ModelDeclaration> declarations,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diagnostics = new List<Diagnostic>();

        var byModel = new Dictionary<string, List<ModelDeclaration>>(StringComparer.Ordinal);
        foreach (var decl in declarations)
        {
            if (!byModel.TryGetValue(decl.ModelName, out var list))
                byModel[decl.ModelName] = list = [];
            list.Add(decl);
        }

        DetectCircularInheritance(byModel, diagnostics);

        var allFieldsByModel    = BuildFieldMap(byModel, ct);
        var allComputedByModel  = BuildComputedMap(byModel, ct);
        var allRelationsByModel = BuildRelationMap(byModel, ct);

        var results = new List<ResolvedModel>();

        foreach (var byModelEntry in byModel)
        {
            var modelName = byModelEntry.Key;
            var shards = byModelEntry.Value;
            ct.ThrowIfCancellationRequested();
            var primary = shards.FirstOrDefault(s => s.IsNewModel) ?? shards[0];

            // ── Classical inheritance ──────────────────────────────────────
            var classicalFields = new List<FieldDeclaration>();
            var allParents      = new List<string>();

            foreach (var parentName in primary.ClassicalParents)
            {
                allParents.Add(parentName);
                if (!byModel.ContainsKey(parentName))
                {
                    diagnostics.Add(Diagnostics.Make(Diagnostics.ParentModelNotFound, primary.Location, modelName, parentName));
                    continue;
                }
                if (!allFieldsByModel.TryGetValue(parentName, out var pFields)) continue;
                foreach (var f in pFields)
                {
                    var existing = classicalFields.FirstOrDefault(x => x.PropertyName == f.PropertyName);
                    if (existing != null)
                    {
                        diagnostics.Add(Diagnostics.Make(Diagnostics.FieldNameConflict, primary.Location, modelName, f.PropertyName, parentName));
                        classicalFields.Remove(existing);
                    }
                    classicalFields.Add(f);
                }
            }

            // ── Delegation ─────────────────────────────────────────────────
            var delegations = new List<ResolvedDelegation>();
            foreach (var del in primary.DelegationParents)
            {
                allParents.Add(del.ParentModelName);
                if (!byModel.ContainsKey(del.ParentModelName))
                {
                    diagnostics.Add(Diagnostics.Make(Diagnostics.ParentModelNotFound, primary.Location, modelName, del.ParentModelName));
                    continue;
                }
                var parentClassName = RealClassName(del.ParentModelName, byModel);
                var navProp = del.ForeignKeyName.EndsWith("Id")
                    ? del.ForeignKeyName.Substring(0, del.ForeignKeyName.Length - 2)
                    : del.ForeignKeyName + "Nav";
                var delegatedFields = allFieldsByModel.TryGetValue(del.ParentModelName, out var df)
                    ? (IReadOnlyList<FieldDeclaration>)df : [];
                delegations.Add(new ResolvedDelegation(del.ParentModelName, parentClassName, del.ForeignKeyName, navProp, delegatedFields));
            }

            // ── Relations ──────────────────────────────────────────────────
            var resolvedRelations = ResolveRelations(
                modelName, primary, allRelationsByModel, byModel, diagnostics);

            // ── Constraints + Onchange (merged across shards, dédupliqués par nom) ──
            // INamedTypeSymbol.GetMembers() retourne tous les membres de toutes les
            // déclarations partielles, donc chaque shard voit les mêmes méthodes.
            var constraintMethods = shards.SelectMany(s => s.ConstraintMethods)
                .GroupBy(c => c.MethodName).Select(g => g.First()).ToList();
            var onchangeMethods = shards.SelectMany(s => s.OnchangeMethods)
                .GroupBy(c => c.MethodName).Select(g => g.First()).ToList();
            var sqlConstraints = shards.SelectMany(s => s.SqlConstraints)
                .GroupBy(c => c.Name).Select(g => g.First()).ToList();

            var ownFields = allFieldsByModel.TryGetValue(modelName, out var of)
                ? of : (IReadOnlyList<FieldDeclaration>)[];
            var computedFields = allComputedByModel.TryGetValue(modelName, out var cf)
                ? cf : (IReadOnlyList<ComputedFieldDeclaration>)[];

            results.Add(new ResolvedModel(
                ModelName:                  modelName,
                Namespace:                  primary.Namespace,
                ClassName:                  primary.ClassName,
                OwnFields:                  ownFields,
                ClassicallyInheritedFields: classicalFields,
                DelegationParents:          delegations,
                ComputedFields:             computedFields,
                Relations:                  resolvedRelations,
                ConstraintMethods:          constraintMethods,
                OnchangeMethods:            onchangeMethods,
                SqlConstraints:             sqlConstraints,
                AllParentNames:             allParents
            ));
        }

        return (results, diagnostics);
    }

    // ── Relation resolution ───────────────────────────────────────────────

    private static IReadOnlyList<ResolvedRelation> ResolveRelations(
        string modelName,
        ModelDeclaration primary,
        Dictionary<string, IReadOnlyList<RelationDeclaration>> allRelations,
        Dictionary<string, List<ModelDeclaration>> byModel,
        List<Diagnostic> diagnostics)
    {
        var result = new List<ResolvedRelation>();
        if (!allRelations.TryGetValue(modelName, out var rels)) return result;

        foreach (var rel in rels)
        {
            // Validate comodel exists
            if (!byModel.ContainsKey(rel.ComodelName))
            {
                diagnostics.Add(Diagnostics.Make(Diagnostics.RelationComodelNotFound,
                    rel.Location, rel.PropertyName, modelName, rel.ComodelName));
                continue;
            }

            var comodelClassName = RealClassName(rel.ComodelName, byModel);

            // One2many: validate inverse field exists on comodel as Many2one
            if (rel.Kind == RelationKind.One2many && rel.InverseField != null)
            {
                if (allRelations.TryGetValue(rel.ComodelName, out var comodelRels))
                {
                    var inverse = comodelRels.FirstOrDefault(r =>
                        r.Kind == RelationKind.Many2one &&
                        (r.PropertyName == rel.InverseField || r.ForeignKeyName == rel.InverseField));
                    if (inverse == null)
                        diagnostics.Add(Diagnostics.Make(Diagnostics.One2manyInverseNotFound,
                            rel.Location, rel.PropertyName, modelName, rel.InverseField, rel.ComodelName));
                }
            }

            // Derive join table name for Many2many
            string? relationTable = rel.RelationTable;
            if (rel.Kind == RelationKind.Many2many && relationTable == null)
            {
                var parts = new[] { modelName, rel.ComodelName }
                    .Select(n => n.Replace('.', '_'))
                    .OrderBy(n => n)
                    .ToArray();
                relationTable = $"{parts[0]}_{parts[1]}_rel";
            }

            result.Add(new ResolvedRelation(
                Kind:             rel.Kind,
                PropertyName:     rel.PropertyName,
                ComodelName:      rel.ComodelName,
                ComodelClassName: comodelClassName,
                Label:            rel.Label,
                Help:             rel.Help,
                Required:         rel.Required,
                ForeignKeyName:   rel.ForeignKeyName,
                OnDelete:         rel.OnDelete,
                InverseField:     rel.InverseField,
                RelationTable:    relationTable,
                Column1:          rel.Column1 ?? modelName.Replace('.', '_') + "_id",
                Column2:          rel.Column2 ?? rel.ComodelName.Replace('.', '_') + "_id"
            ));
        }

        return result;
    }

    // ── Field maps ────────────────────────────────────────────────────────

    private static Dictionary<string, IReadOnlyList<FieldDeclaration>> BuildFieldMap(
        Dictionary<string, List<ModelDeclaration>> byModel, CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<FieldDeclaration>>(StringComparer.Ordinal);
        foreach (var byModelEntry in byModel)
        {
            var modelName = byModelEntry.Key;
            var shards = byModelEntry.Value;
            ct.ThrowIfCancellationRequested();
            var merged = new Dictionary<string, FieldDeclaration>(StringComparer.Ordinal);
            foreach (var shard in shards)
                foreach (var f in shard.OwnFields)
                    merged[f.PropertyName] = f;
            result[modelName] = merged.Values.ToList();
        }
        return result;
    }

    private static Dictionary<string, IReadOnlyList<ComputedFieldDeclaration>> BuildComputedMap(
        Dictionary<string, List<ModelDeclaration>> byModel, CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<ComputedFieldDeclaration>>(StringComparer.Ordinal);
        foreach (var byModelEntry in byModel)
        {
            var modelName = byModelEntry.Key;
            var shards = byModelEntry.Value;
            ct.ThrowIfCancellationRequested();
            var merged = new Dictionary<string, ComputedFieldDeclaration>(StringComparer.Ordinal);
            foreach (var shard in shards)
                foreach (var cf in shard.ComputedFields)
                    merged[cf.PropertyName] = cf;
            result[modelName] = merged.Values.ToList();
        }
        return result;
    }

    private static Dictionary<string, IReadOnlyList<RelationDeclaration>> BuildRelationMap(
        Dictionary<string, List<ModelDeclaration>> byModel, CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<RelationDeclaration>>(StringComparer.Ordinal);
        foreach (var byModelEntry in byModel)
        {
            var modelName = byModelEntry.Key;
            var shards = byModelEntry.Value;
            ct.ThrowIfCancellationRequested();
            var merged = new Dictionary<string, RelationDeclaration>(StringComparer.Ordinal);
            foreach (var shard in shards)
                foreach (var r in shard.Relations)
                    merged[r.PropertyName] = r;
            result[modelName] = merged.Values.ToList();
        }
        return result;
    }

    // ── Circular inheritance (DFS) ────────────────────────────────────────

    private static void DetectCircularInheritance(
        Dictionary<string, List<ModelDeclaration>> byModel,
        List<Diagnostic> diagnostics)
    {
        var visited  = new HashSet<string>(StringComparer.Ordinal);
        var inStack  = new HashSet<string>(StringComparer.Ordinal);
        // reported évite les doublons : si deux cycles partagent un nœud,
        // le second cycle peut ne pas être signalé individuellement — comportement
        // conservateur intentionnel pour éviter les diagnostics redondants.
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var modelName in byModel.Keys)
            Dfs(modelName, []);

        void Dfs(string current, List<string> stack)
        {
            if (reported.Contains(current)) return;
            if (inStack.Contains(current))
            {
                var cycleStart = stack.IndexOf(current);
                var cycle      = stack.Skip(cycleStart).Append(current);
                var p = byModel[current].FirstOrDefault(s => s.IsNewModel) ?? byModel[current][0];
                diagnostics.Add(Diagnostics.Make(Diagnostics.CircularInheritance, p.Location,
                    current, string.Join(" → ", cycle)));
                reported.Add(current);
                return;
            }
            if (visited.Contains(current)) return;

            visited.Add(current);
            inStack.Add(current);
            stack.Add(current);

            if (byModel.TryGetValue(current, out var shards))
            {
                var primary = shards.FirstOrDefault(s => s.IsNewModel) ?? shards[0];
                // ExtensionParents are same-model shards (not real inheritance ancestors)
                foreach (var parent in primary.ClassicalParents
                    .Concat(primary.DelegationParents.Select(d => d.ParentModelName)))
                    Dfs(parent, stack);
            }

            stack.RemoveAt(stack.Count - 1);
            inStack.Remove(current);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string RealClassName(string modelName, Dictionary<string, List<ModelDeclaration>> byModel)
    {
        if (byModel.TryGetValue(modelName, out var shards))
            return (shards.FirstOrDefault(s => s.IsNewModel) ?? shards[0]).ClassName;
        return string.Concat(modelName.Split('.').Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
    }
}
