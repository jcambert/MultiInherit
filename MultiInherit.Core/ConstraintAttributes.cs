namespace MultiInherit;

// ═══════════════════════════════════════════════════════════════════════════
// Constraint attributes
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Marks a method as a Python-style constraint that is called before save.
/// The method must be a non-static void method and should throw
/// <see cref="ModelValidationException"/> if the constraint is violated.
/// Equivalent to Odoo <c>@api.constrains('field1', 'field2')</c>.
///
/// <example>
/// [Constrains("Email", "Name")]
/// private void _check_email()
/// {
///     if (Email != null &amp;&amp; !Email.Contains('@'))
///         throw new ModelValidationException("Email must contain @");
/// }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ConstrainsAttribute(params string[] fields) : Attribute
{
    /// <summary>Field names that trigger this constraint when modified.</summary>
    public IReadOnlyList<string> Fields { get; } = fields;
}

/// <summary>
/// Declares a SQL-level constraint to be added to the table definition.
/// Equivalent to Odoo <c>_sql_constraints</c>.
///
/// <example>
/// [SqlConstraint("unique_partner_email", "UNIQUE(email)", "Email must be unique per partner.")]
/// public partial class ResPartner { ... }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SqlConstraintAttribute(string name, string sql, string message) : Attribute
{
    public string Name    { get; } = name;
    public string Sql     { get; } = sql;
    public string Message { get; } = message;
}

/// <summary>
/// Exception thrown by constraint methods when a validation rule is violated.
/// </summary>
public sealed class ModelValidationException(string message, string? fieldName = null)
    : Exception(message)
{
    /// <summary>Optional field name that caused the violation.</summary>
    public string? FieldName { get; } = fieldName;
}

// ═══════════════════════════════════════════════════════════════════════════
// Onchange attribute
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Marks a method to be called when any of the specified fields change,
/// allowing side-effects like resetting dependent fields or showing warnings.
/// Unlike [Compute], the method does not return a value but can mutate other fields.
/// Equivalent to Odoo <c>@api.onchange('field1', 'field2')</c>.
///
/// <example>
/// [Onchange("PartnerId")]
/// private void _onchange_partner()
/// {
///     // Auto-fill address from partner when partner changes
///     if (Partner != null)
///         Street = Partner.Street;
/// }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class OnchangeAttribute(params string[] fields) : Attribute
{
    /// <summary>Fields whose change triggers this method.</summary>
    public IReadOnlyList<string> Fields { get; } = fields;
}
