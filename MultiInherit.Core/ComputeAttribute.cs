namespace MultiInherit;

/// <summary>
/// Marks a property as computed: its value is derived by calling a method
/// rather than being stored directly.
///
/// Equivalent to Odoo's compute= kwarg on a field.
///
/// <example>
/// [Model("sale.order")]
/// public partial class SaleOrder
/// {
///     public decimal UnitPrice { get; set; }
///     public int     Quantity  { get; set; }
///
///     // Computed field — no setter, value set by _compute_total
///     [ModelField(String = "Total", Readonly = true)]
///     [Compute(nameof(_compute_total), Store = false)]
///     public decimal Total { get; private set; }
///
///     private void _compute_total()
///     {
///         Total = UnitPrice * Quantity;
///     }
/// }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ComputeAttribute(string methodName) : Attribute
{
    /// <summary>Name of the void instance method that computes this field.</summary>
    public string MethodName { get; } = methodName;

    /// <summary>
    /// Whether to persist the computed value in the database.
    /// When false (default) the value is recomputed on every read.
    /// </summary>
    public bool Store { get; init; } = false;

    /// <summary>
    /// Comma-separated list of field names that trigger recomputation
    /// when their values change. Equivalent to Odoo depends=.
    /// </summary>
    public string? Depends { get; init; }
}

/// <summary>
/// Marks a property as depending on other fields for cache invalidation.
/// Used together with [Compute] to declare which fields trigger recomputation.
/// Can also be stacked directly on the property instead of using Compute.Depends.
/// <example>
/// [Compute("_compute_display_name")]
/// [Depends("Name", "Email")]
/// public string DisplayName { get; private set; } = string.Empty;
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class DependsAttribute(params string[] fields) : Attribute
{
    public IReadOnlyList<string> Fields { get; } = fields;
}
