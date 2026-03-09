namespace MultiInherit;

/// <summary>
/// Restricts a <c>string</c> property to a predefined set of values,
/// equivalent to Odoo <c>fields.Selection</c>.
///
/// The generator emits a static validation set and checks it inside
/// <c>ValidateConstraints()</c>. The property must be <c>string</c> or <c>string?</c>.
///
/// <example>
/// [Selection("draft", "in_progress", "done")]
/// public string Status { get; set; } = "draft";
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class SelectionAttribute(params string[] values) : Attribute
{
    /// <summary>The allowed string values for this field.</summary>
    public string[] Values { get; } = values;
}
