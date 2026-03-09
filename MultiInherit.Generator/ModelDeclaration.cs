using Microsoft.CodeAnalysis;

namespace MultiInherit.Generator;

/// <summary>All information extracted from a single [Model] or [Inherit] class declaration.</summary>
internal sealed record ModelDeclaration(
    string ModelName,
    string Namespace,
    string ClassName,
    bool   IsNewModel,
    IReadOnlyList<string> ClassicalParents,
    IReadOnlyList<string> ExtensionParents,
    IReadOnlyList<DelegationParent> DelegationParents,
    IReadOnlyList<FieldDeclaration> OwnFields,
    IReadOnlyList<ComputedFieldDeclaration> ComputedFields,
    IReadOnlyList<RelationDeclaration> Relations,
    IReadOnlyList<ConstraintMethodDeclaration> ConstraintMethods,
    IReadOnlyList<OnchangeMethodDeclaration> OnchangeMethods,
    IReadOnlyList<SqlConstraintDeclaration> SqlConstraints,
    Location? Location,
    string FilePath
);

// ── Stored field ──────────────────────────────────────────────────────────

internal sealed record FieldDeclaration(
    string    PropertyName,
    string    TypeName,
    bool      IsNullable,
    bool      HasSetter,
    string?   Label,
    bool      Required,
    bool      Readonly,
    string?   Help,
    string?   Default,
    string[]? SelectionValues   = null,   // non-null → champ [Selection]
    string?   DefaultMethod     = null,   // non-null → champ [Default], générateur implémenter la propriété partial
    bool      IsPartialProperty = false   // true quand la propriété est déclarée partial + [Default]
);

// ── Computed field ────────────────────────────────────────────────────────

internal sealed record ComputedFieldDeclaration(
    string    PropertyName,
    string    TypeName,
    bool      IsNullable,
    string    ComputeMethod,
    bool      Store,
    string[]  DependsOn,
    string?   Label,
    string?   Help,
    Location? Location
);

// ── Relations ─────────────────────────────────────────────────────────────

internal enum RelationKind { Many2one, One2many, Many2many }

internal sealed record RelationDeclaration(
    RelationKind Kind,
    string       PropertyName,
    string       ComodelName,        // "res.partner"
    string       ComodelClassName,   // "ResPartner" (resolved later)
    string?      Label,
    string?      Help,
    bool         Required,
    // Many2one specific
    string?      ForeignKeyName,     // explicit override or derived
    string       OnDelete,           // "SetNull" | "Cascade" | "Restrict"
    // One2many specific
    string?      InverseField,       // property name on child model
    // Many2many specific
    string?      RelationTable,
    string?      Column1,
    string?      Column2,
    Location?    Location
);

// ── Constraints ───────────────────────────────────────────────────────────

internal sealed record ConstraintMethodDeclaration(
    string   MethodName,
    string[] Fields,
    Location? Location
);

internal sealed record SqlConstraintDeclaration(
    string Name,
    string Sql,
    string Message
);

// ── Onchange ──────────────────────────────────────────────────────────────

internal sealed record OnchangeMethodDeclaration(
    string   MethodName,
    string[] Fields,
    Location? Location
);

// ── Delegation (from [Inherits]) ──────────────────────────────────────────

internal sealed record DelegationParent(
    string ParentModelName,
    string ForeignKeyName
);
