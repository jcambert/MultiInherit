namespace MultiInherit;

/// <summary>
/// Declares a new model with a technical name.
/// Equivalent to setting _name in Odoo.
/// <example>
/// [Model("res.partner")]
/// public partial class ResPartner { ... }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ModelAttribute(string name) : Attribute
{
    /// <summary>Technical name of the model, e.g. "res.partner"</summary>
    public string Name { get; } = name;

    /// <summary>Human-readable description (optional)</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Extends an existing model in-place (extension inheritance),
/// or copies its fields into a new model (classical inheritance)
/// depending on whether [Model] is also present.
///
/// Maps to Odoo _inherit.
/// Multiple parents are supported.
/// <example>
/// // Extension in-place (no [Model] = same model):
/// [Inherit("res.partner")]
/// public partial class ResPartner { public string Phone { get; set; } }
///
/// // Classical: new model copying parent fields:
/// [Model("res.employee"), Inherit("res.partner")]
/// public partial class ResEmployee { ... }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class InheritAttribute(string parentModelName) : Attribute
{
    public string ParentModelName { get; } = parentModelName;
}

/// <summary>
/// Delegation inheritance: the model stores a FK to the parent model
/// and transparently exposes its fields as if they were its own.
/// Maps to Odoo _inherits = {"res.partner": "partner_id"}.
/// <example>
/// [Model("hr.employee")]
/// [Inherits("res.partner", ForeignKey = "PartnerId")]
/// public partial class HrEmployee
/// {
///     // PartnerId is auto-generated
///     // Name, Email etc. are delegated to the linked ResPartner
/// }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class InheritsAttribute(string parentModelName) : Attribute
{
    public string ParentModelName { get; } = parentModelName;

    /// <summary>
    /// Name of the foreign key property to generate.
    /// Defaults to {ParentClassName}Id, e.g. "PartnerId".
    /// </summary>
    public string? ForeignKey { get; init; }
}

/// <summary>
/// Overrides the database table name and optional schema for a model.
/// By default, <c>ModelDbContext</c> derives the table name from the model's technical name
/// (e.g. <c>"res.partner"</c> → <c>res_partner</c>).
/// Use this attribute when the target table has a different name or lives in a specific schema.
/// <example>
/// [ModelTable("partners", Schema = "crm")]
/// [Model("res.partner")]
/// public partial class ResPartner { ... }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ModelTableAttribute(string tableName) : Attribute
{
    /// <summary>Explicit table name. Passed through the configured <c>DatabaseNamingHelper</c> naming convention before use.</summary>
    public string TableName { get; } = tableName;

    /// <summary>Optional schema (e.g. <c>"public"</c>, <c>"crm"</c>). Null means default schema.</summary>
    public string? Schema { get; init; }
}

/// <summary>
/// Optional metadata on a model field (property).
/// Equivalent to Odoo field definition kwargs.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ModelFieldAttribute : Attribute
{
    /// <summary>Column / field label</summary>
    public string? String { get; init; }

    /// <summary>If true the field is required (not null/empty)</summary>
    public bool Required { get; init; }

    /// <summary>If true the field is read-only</summary>
    public bool Readonly { get; init; }

    /// <summary>Help tooltip text</summary>
    public string? Help { get; init; }

    /// <summary>Default value expression (string for simplicity)</summary>
    public string? Default { get; init; }
}
