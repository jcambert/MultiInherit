namespace MultiInherit;

/// <summary>
/// Specifies the naming conventions that can be used to format identifiers in code.
/// </summary>
/// <remarks>Use this enumeration to select a consistent naming style for variables, properties, methods, or other
/// identifiers. Adhering to a naming convention helps improve code readability and maintainability, and may be required
/// to comply with specific coding standards or style guides.</remarks>
public enum NamingConvention
{
    /// <summary>
    /// Gets or sets the value in snake_case format.
    /// </summary>
    SnakeCase,
    /// <summary>
    /// Gets or sets the value in CamelCase format.
    /// </summary>
    CamelCase,
    /// <summary>
    /// Gets the upper snake case representation of a string, where words are separated by underscores and all letters
    /// are uppercase.
    /// </summary>
    /// <remarks>This property transforms a given string into a format suitable for upper snake case usage,
    /// which is often used in configuration files and certain programming conventions.</remarks>
    UpperSnake,
    /// <summary>
    /// Gets the kebab case representation of the string, with words separated by hyphens and all characters in
    /// lowercase.
    /// </summary>
    /// <remarks>This property is useful for converting strings into a format suitable for use in URLs, CSS
    /// class names, or other contexts where a standardized, lowercase, hyphen-separated format is required. The output
    /// is consistently formatted regardless of the input's original casing or word separators.</remarks>
    KebabCase,
    /// <summary>
    /// Gets or sets the text in lowercase format.
    /// </summary>
    LowerCase,
    /// <summary>
    /// Gets or sets the string value converted to uppercase.
    /// </summary>
    /// <remarks>This property ensures that the string is always represented in uppercase format, which can be
    /// useful for case-insensitive comparisons or standardization of input data.</remarks>
    UpperCase,
}
