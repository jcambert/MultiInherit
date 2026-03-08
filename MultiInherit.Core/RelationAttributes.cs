namespace MultiInherit;

// ═══════════════════════════════════════════════════════════════════════════
// Relational field attributes — equivalent to Odoo field types
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Many-to-one relation: this model holds a foreign key to <paramref name="comodelName"/>.
/// Equivalent to Odoo <c>fields.Many2one('res.partner')</c>.
///
/// <example>
/// [Model("sale.order")]
/// public partial class SaleOrder
/// {
///     [Many2one("res.partner", string: "Customer", Required = true, OnDelete = OnDeleteAction.Restrict)]
///     public ResPartner? Partner { get; set; }
///     // Generator adds: public int PartnerId { get; set; }
/// }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class Many2oneAttribute(string comodelName) : Attribute
{
    /// <summary>Technical name of the target model, e.g. "res.partner".</summary>
    public string ComodelName { get; } = comodelName;

    /// <summary>Field label (String kwarg in Odoo).</summary>
    public string? String { get; init; }

    /// <summary>Whether the relation is required (NOT NULL on the FK).</summary>
    public bool Required { get; init; }

    /// <summary>Behaviour when the comodel record is deleted.</summary>
    public OnDeleteAction OnDelete { get; init; } = OnDeleteAction.SetNull;

    /// <summary>Help / tooltip text.</summary>
    public string? Help { get; init; }

    /// <summary>
    /// Name of the FK integer property to generate.
    /// Defaults to <c>{PropertyName}Id</c>.
    /// </summary>
    public string? ForeignKey { get; init; }
}

/// <summary>
/// One-to-many relation: a virtual collection of child records.
/// No column is stored on this model — the FK lives on the child.
/// Equivalent to Odoo <c>fields.One2many('sale.order.line', 'order_id')</c>.
///
/// <example>
/// [Model("sale.order")]
/// public partial class SaleOrder
/// {
///     [One2many("sale.order.line", "OrderId", String = "Order Lines")]
///     public ICollection&lt;SaleOrderLine&gt; Lines { get; set; }
/// }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class One2manyAttribute(string comodelName, string inverseField) : Attribute
{
    /// <summary>Technical name of the child model.</summary>
    public string ComodelName { get; } = comodelName;

    /// <summary>Name of the Many2one property on the child that points back to this model.</summary>
    public string InverseField { get; } = inverseField;

    /// <summary>Field label.</summary>
    public string? String { get; init; }

    /// <summary>Help / tooltip text.</summary>
    public string? Help { get; init; }
}

/// <summary>
/// Many-to-many relation: a join table is generated automatically.
/// Equivalent to Odoo <c>fields.Many2many('res.tag')</c>.
///
/// <example>
/// [Model("res.partner")]
/// public partial class ResPartner
/// {
///     [Many2many("res.tag", String = "Tags")]
///     public ICollection&lt;ResTag&gt; Tags { get; set; }
/// }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class Many2manyAttribute(string comodelName) : Attribute
{
    /// <summary>Technical name of the related model.</summary>
    public string ComodelName { get; } = comodelName;

    /// <summary>Field label.</summary>
    public string? String { get; init; }

    /// <summary>
    /// Explicit join-table name.
    /// Defaults to <c>{model1}_{model2}_rel</c> (dots replaced by underscores, alphabetically sorted).
    /// </summary>
    public string? RelationTable { get; init; }

    /// <summary>Column name for this model's FK in the join table. Defaults to <c>{model}_id</c>.</summary>
    public string? Column1 { get; init; }

    /// <summary>Column name for the comodel's FK in the join table. Defaults to <c>{comodel}_id</c>.</summary>
    public string? Column2 { get; init; }

    /// <summary>Help / tooltip text.</summary>
    public string? Help { get; init; }
}

/// <summary>Cascade behaviour when the referenced record is deleted.</summary>
public enum OnDeleteAction
{
    /// <summary>Set the FK to NULL (default).</summary>
    SetNull,

    /// <summary>Delete this record too (CASCADE).</summary>
    Cascade,

    /// <summary>Raise an error if any child exists (RESTRICT).</summary>
    Restrict,
}
