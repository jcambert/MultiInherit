namespace MultiInherit.Generator;

/// <summary>A model after cross-model resolution.</summary>
internal sealed record ResolvedModel(
    string ModelName,
    string Namespace,
    string ClassName,
    IReadOnlyList<FieldDeclaration> OwnFields,
    IReadOnlyList<FieldDeclaration> ClassicallyInheritedFields,
    IReadOnlyList<ResolvedDelegation> DelegationParents,
    IReadOnlyList<ComputedFieldDeclaration> ComputedFields,
    IReadOnlyList<ResolvedRelation> Relations,
    IReadOnlyList<ConstraintMethodDeclaration> ConstraintMethods,
    IReadOnlyList<OnchangeMethodDeclaration> OnchangeMethods,
    IReadOnlyList<SqlConstraintDeclaration> SqlConstraints,
    IReadOnlyList<string> AllParentNames
);

internal sealed record ResolvedDelegation(
    string ParentModelName,
    string ParentClassName,
    string ForeignKeyName,
    string NavigationPropertyName,
    IReadOnlyList<FieldDeclaration> DelegatedFields
);

internal sealed record ResolvedRelation(
    RelationKind Kind,
    string PropertyName,
    string ComodelName,
    string ComodelClassName,
    string? Label,
    string? Help,
    bool Required,
    // Many2one
    string ForeignKeyName,      // always populated (derived or explicit)
    string OnDelete,
    // One2many
    string? InverseField,
    // Many2many
    string? RelationTable,
    string? Column1,
    string? Column2
);
