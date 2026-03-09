using System.Collections.Generic;
using System.Linq;

namespace MultiInherit.Generator;

/// <summary>
/// Deep structural equality for <see cref="ResolvedModel"/>.
///
/// <see cref="ResolvedModel"/> is a <c>record</c> whose properties are <see cref="IReadOnlyList{T}"/>,
/// which defaults to reference equality. Without this comparer, the Roslyn incremental pipeline
/// cannot cache per-model outputs and re-emits every .g.cs file on each keystroke.
///
/// Pass this to the pipeline via <c>.WithComparer(ResolvedModelComparer.Instance)</c>.
/// </summary>
internal sealed class ResolvedModelComparer : IEqualityComparer<ResolvedModel>
{
    public static readonly ResolvedModelComparer Instance = new ResolvedModelComparer();
    private ResolvedModelComparer() { }

    public bool Equals(ResolvedModel? x, ResolvedModel? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return x.ModelName == y.ModelName
            && x.Namespace == y.Namespace
            && x.ClassName == y.ClassName
            && SeqEqual(x.OwnFields, y.OwnFields, FieldEq)
            && SeqEqual(x.ClassicallyInheritedFields, y.ClassicallyInheritedFields, FieldEq)
            && SeqEqual(x.DelegationParents, y.DelegationParents, DelegationEq)
            && SeqEqual(x.ComputedFields, y.ComputedFields, ComputedFieldEq)
            && SeqEqual(x.Relations, y.Relations, RelationEq)
            && SeqEqual(x.ConstraintMethods, y.ConstraintMethods, ConstraintMethodEq)
            && SeqEqual(x.OnchangeMethods, y.OnchangeMethods, OnchangeMethodEq)
            && SeqEqual(x.SqlConstraints, y.SqlConstraints, SqlConstraintEq)
            && x.AllParentNames.SequenceEqual(y.AllParentNames);
    }

    public int GetHashCode(ResolvedModel obj)
    {
        unchecked
        {
            int h = obj.ModelName?.GetHashCode() ?? 0;
            h = h * 397 ^ (obj.Namespace?.GetHashCode() ?? 0);
            h = h * 397 ^ (obj.ClassName?.GetHashCode() ?? 0);
            h = h * 397 ^ obj.OwnFields.Count;
            h = h * 397 ^ obj.ClassicallyInheritedFields.Count;
            h = h * 397 ^ obj.DelegationParents.Count;
            h = h * 397 ^ obj.ComputedFields.Count;
            h = h * 397 ^ obj.Relations.Count;
            h = h * 397 ^ obj.ConstraintMethods.Count;
            return h;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static bool SeqEqual<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, System.Func<T, T, bool> eq)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!eq(a[i], b[i])) return false;
        return true;
    }

    private static bool ArrEqual(string[]? a, string[]? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private static bool FieldEq(FieldDeclaration a, FieldDeclaration b)
        => a.PropertyName == b.PropertyName
        && a.TypeName == b.TypeName
        && a.IsNullable == b.IsNullable
        && a.HasSetter == b.HasSetter
        && a.Label == b.Label
        && a.Required == b.Required
        && a.Readonly == b.Readonly
        && a.Help == b.Help
        && a.Default == b.Default
        && a.DefaultMethod == b.DefaultMethod
        && a.IsPartialProperty == b.IsPartialProperty
        && ArrEqual(a.SelectionValues, b.SelectionValues);

    private static bool DelegationEq(ResolvedDelegation a, ResolvedDelegation b)
        => a.ParentModelName == b.ParentModelName
        && a.ParentClassName == b.ParentClassName
        && a.ForeignKeyName == b.ForeignKeyName
        && a.NavigationPropertyName == b.NavigationPropertyName
        && SeqEqual(a.DelegatedFields, b.DelegatedFields, FieldEq);

    private static bool ComputedFieldEq(ComputedFieldDeclaration a, ComputedFieldDeclaration b)
        => a.PropertyName == b.PropertyName
        && a.TypeName == b.TypeName
        && a.IsNullable == b.IsNullable
        && a.ComputeMethod == b.ComputeMethod
        && a.Store == b.Store
        && a.Label == b.Label
        && a.Help == b.Help
        && ArrEqual(a.DependsOn, b.DependsOn);

    private static bool RelationEq(ResolvedRelation a, ResolvedRelation b)
        => a.Kind == b.Kind
        && a.PropertyName == b.PropertyName
        && a.ComodelName == b.ComodelName
        && a.ComodelClassName == b.ComodelClassName
        && a.Label == b.Label
        && a.Help == b.Help
        && a.Required == b.Required
        && a.ForeignKeyName == b.ForeignKeyName
        && a.OnDelete == b.OnDelete
        && a.InverseField == b.InverseField
        && a.RelationTable == b.RelationTable
        && a.Column1 == b.Column1
        && a.Column2 == b.Column2;

    private static bool ConstraintMethodEq(ConstraintMethodDeclaration a, ConstraintMethodDeclaration b)
        => a.MethodName == b.MethodName && ArrEqual(a.Fields, b.Fields);

    private static bool OnchangeMethodEq(OnchangeMethodDeclaration a, OnchangeMethodDeclaration b)
        => a.MethodName == b.MethodName && ArrEqual(a.Fields, b.Fields);

    private static bool SqlConstraintEq(SqlConstraintDeclaration a, SqlConstraintDeclaration b)
        => a.Name == b.Name && a.Sql == b.Sql && a.Message == b.Message;
}
