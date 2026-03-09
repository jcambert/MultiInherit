using Microsoft.CodeAnalysis;

namespace MultiInherit.Generator;

/// <summary>
/// All diagnostic descriptors emitted by the MultiInherit source generator.
/// Prefix MI = MultiInherit.
/// </summary>
internal static class Diagnostics
{
    private const string Category = "MultiInherit";

    // ── Errors (MI00xx) ───────────────────────────────────────────────────

    /// <summary>A model referenced in [Inherit] or [Inherits] was not found in the compilation.</summary>
    public static readonly DiagnosticDescriptor ParentModelNotFound = new(
        id: "MI0001",
        title: "Parent model not found",
        messageFormat: "Model '{0}' references parent '{1}' which is not declared in this compilation. " +
                            "Ensure the parent model is defined with [Model(\"{1}\")] and is part of the same project.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0001"
    );

    /// <summary>Circular inheritance detected.</summary>
    public static readonly DiagnosticDescriptor CircularInheritance = new(
        id: "MI0002",
        title: "Circular inheritance detected",
        messageFormat: "Model '{0}' is part of a circular inheritance chain: {1}. " +
                            "Circular inheritance is not supported.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0002"
    );

    /// <summary>Class is not declared as partial.</summary>
    public static readonly DiagnosticDescriptor ClassMustBePartial = new(
        id: "MI0003",
        title: "Model class must be partial",
        messageFormat: "Class '{0}' uses MultiInherit attributes but is not declared as 'partial'. " +
                            "Add the 'partial' modifier to allow source generation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0003"
    );

    /// <summary>[Compute] method not found on the class.</summary>
    public static readonly DiagnosticDescriptor ComputeMethodNotFound = new(
        id: "MI0004",
        title: "Compute method not found",
        messageFormat: "Property '{0}' on model '{1}' declares [Compute(\"{2}\")] but no method named '{2}' " +
                            "was found on the class. The method must be a non-static void method.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0004"
    );

    /// <summary>[Compute] property must have no setter (it is computed).</summary>
    public static readonly DiagnosticDescriptor ComputedPropertyMustBeReadOnly = new(
        id: "MI0005",
        title: "Computed property must be read-only",
        messageFormat: "Property '{0}' on model '{1}' is decorated with [Compute] and must not have a public setter. " +
                            "Remove the setter or use 'private set'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0005"
    );

    /// <summary>Delegation FK collision: the FK property name already exists on the model.</summary>
    public static readonly DiagnosticDescriptor ForeignKeyCollision = new(
        id: "MI0006",
        title: "Foreign key property name collision",
        messageFormat: "Model '{0}': the FK name '{1}' generated for [Inherits(\"{2}\")] conflicts with " +
                            "an existing property. Use the ForeignKey named argument to choose a different name.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0006"
    );

    /// <summary>[Constrains] method not found or has wrong signature.</summary>
    public static readonly DiagnosticDescriptor ConstrainsMethodNotFound = new(
        id: "MI0007",
        title: "Constrains method not found",
        messageFormat: "[Constrains] on method '{0}' in model '{1}': no matching non-static void method found",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>[Onchange] method not found.</summary>
    public static readonly DiagnosticDescriptor OnchangeMethodNotFound = new(
        id: "MI0008",
        title: "Onchange method not found",
        messageFormat: "[Onchange] on method '{0}' in model '{1}': no matching non-static void method found",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>One2many inverse field not found on the comodel.</summary>
    public static readonly DiagnosticDescriptor One2manyInverseNotFound = new(
        id: "MI0009",
        title: "One2many inverse field not found",
        messageFormat: "Property '{0}' on model '{1}': inverse field '{2}' not found as a [Many2one] on '{3}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>Relation comodel not found in this compilation.</summary>
    public static readonly DiagnosticDescriptor RelationComodelNotFound = new(
        id: "MI0010",
        title: "Relation comodel not found",
        messageFormat: "Property '{0}' on model '{1}': comodel '{2}' is not declared in this compilation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>[Default] method not found or has wrong return type.</summary>
    public static readonly DiagnosticDescriptor DefaultMethodNotFound = new(
        id: "MI0013",
        title: "Default method not found",
        messageFormat: "Property '{0}' on model '{1}' declares [Default(\"{2}\")] but no matching non-static method named '{2}' with a compatible return type was found on the class",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0013"
    );

    /// <summary>[Selection] can only be applied to string or string? properties.</summary>
    public static readonly DiagnosticDescriptor SelectionOnNonStringProperty = new(
        id: "MI0012",
        title: "Selection field must be a string property",
        messageFormat: "Property '{0}' on model '{1}' has [Selection] but its type is '{2}'. " +
                            "[Selection] only supports string and string? properties.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0012"
    );

    /// <summary>[Compute] property must be declared as partial.</summary>
    public static readonly DiagnosticDescriptor ComputedPropertyMustBePartial = new(
        id: "MI0011",
        title: "Computed property must be partial",
        messageFormat: "Property '{0}' on model '{1}' is decorated with [Compute] but is not declared as 'partial'. " +
                            "Add the 'partial' modifier to allow the source generator to implement the property.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0011"
    );

    // ── Warnings (MI01xx) ─────────────────────────────────────────────────

    /// <summary>Field name conflict between two classical parents — later parent wins.</summary>
    public static readonly DiagnosticDescriptor FieldNameConflict = new(
        id: "MI0101",
        title: "Inherited field name conflict",
        messageFormat: "Model '{0}': field '{1}' is defined in multiple parent models. " +
                            "The definition from '{2}' will be used. Use [Inherit] order to control priority.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0101"
    );

    /// <summary>A [Model] class is not in any namespace (global namespace).</summary>
    public static readonly DiagnosticDescriptor GlobalNamespaceModel = new(
        id: "MI0102",
        title: "Model in global namespace",
        messageFormat: "Model '{0}' (class '{1}') is declared in the global namespace. " +
                            "Consider placing it in a named namespace.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/yourorg/MultiInherit/docs/MI0102"
    );

    // ── Helpers ───────────────────────────────────────────────────────────

    public static Diagnostic Make(
        DiagnosticDescriptor descriptor,
        Microsoft.CodeAnalysis.Location? location,
        params object[] args)
        => Diagnostic.Create(descriptor, location ?? Microsoft.CodeAnalysis.Location.None, args);
}
