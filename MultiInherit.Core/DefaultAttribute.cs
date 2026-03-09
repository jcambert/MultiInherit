namespace MultiInherit;

/// <summary>
/// Specifies that a <c>partial</c> property's initial value is provided by an instance method
/// on the same class, equivalent to Odoo's <c>default=</c> field parameter.
///
/// The generator implements the property with a lazy backing field initialized on first access
/// by calling the named method. The method must be non-static and return a value assignable
/// to the property type.
///
/// <example>
/// [Default(nameof(GetDefaultStatus))]
/// public partial string Status { get; set; }
///
/// private string GetDefaultStatus() => "draft";
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DefaultAttribute(string methodName) : Attribute
{
    /// <summary>Name of the instance method that provides the initial value.</summary>
    public string MethodName { get; } = methodName;
}
