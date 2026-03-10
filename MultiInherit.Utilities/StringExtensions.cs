namespace MultiInherit;

/// <summary>
/// 
/// </summary>
public static class StringExtensions
{
    extension(string name)
    {
        /// <summary>
        /// Converts the current name to the specified naming convention.
        /// </summary>
        /// <remarks>Use this method to format names according to different coding standards, which can be
        /// useful for code generation, serialization, or adhering to project-specific naming requirements.</remarks>
        /// <param name="convention">The naming convention to apply to the name. Supported values include SnakeCase, CamelCase, UpperSnake,
        /// KebabCase, LowerCase, and UpperCase. If null, the original name is returned.</param>
        /// <returns>A string representing the name converted to the specified naming convention. If the convention is null or
        /// not recognized, the original name is returned.</returns>
        public string ToNamingConvention(NamingConvention? convention)
        => convention switch
        {
            NamingConvention.SnakeCase => name.ToSnakeCase(),
            NamingConvention.CamelCase => name.ToCamelCase(),
            NamingConvention.UpperSnake => name.ToUpperSnake(),
            NamingConvention.KebabCase => name.ToKebabCase(),
            NamingConvention.LowerCase => name.ToLowerInvariant(),
            NamingConvention.UpperCase => name.ToUpperInvariant(),
            _ => name,
        };

        /// <summary>
        ///     Convert a Pascal cased string to kebab case.
        /// </summary>
        public string ToKebabCase() =>
            string.IsNullOrEmpty(name)
                ? name
                : StringPatterns.KebabCaseRegex().Replace(name, "-$1")
                    .Trim()
                    .ToLowerInvariant();

        /// <summary>
        /// Converts the current string to its equivalent snake_case representation, with words separated by underscores
        /// and all characters in lowercase.
        /// </summary>
        /// <remarks>Leading underscores are removed from the result. This method is useful for formatting
        /// identifiers or text to match snake_case naming conventions commonly used in certain programming languages
        /// and data formats.</remarks>
        /// <returns>A string containing the snake_case version of the current string. If the current string is null or empty,
        /// returns the original string.</returns>
        public string ToSnakeCase()
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            string s = string
                .Join("_", StringPatterns.SnakeCaseRegex()
                    .Matches(name)
                    .Select(m => m.Value))
                .ToLowerInvariant();

            return s.StartsWith('_')
                ? s[1..]
                : s;
        }


        /// <summary>
        /// Converts the first character of the string to lowercase, returning the result in camel case format.
        /// </summary>
        /// <remarks>This method is useful for formatting identifiers according to camel case conventions,
        /// which are commonly used in programming. Only the first character is modified; the remainder of the string is
        /// unchanged.</remarks>
        /// <returns>A string in camel case format. If the original string is null or empty, the same value is returned.</returns>
        public string ToCamelCase() =>
            string.IsNullOrEmpty(name)
                ? name
                : char.ToLowerInvariant(name[0]) + name[1..];

        /// <summary>
        /// Converts the current string to upper case and formats it in snake case.
        /// </summary>
        /// <remarks>This method first transforms the string to snake case before converting it to upper
        /// case. It is useful for formatting identifiers in a consistent style.</remarks>
        /// <returns>A string representing the current string in upper snake case format.</returns>
        public string ToUpperSnake() => name.ToSnakeCase().ToUpperInvariant();

        /// <summary>
        /// Removes the specified trailing substring from the current string instance, if it is present.
        /// </summary>
        /// <remarks>The comparison to determine if the substring is at the end of the string is
        /// case-sensitive and uses ordinal comparison.</remarks>
        /// <param name="toRemove">The substring to remove from the end of the current string. This parameter cannot be null or empty.</param>
        /// <returns>A new string with the specified substring removed from the end if it was present; otherwise, the original
        /// string.</returns>
        public string RemoveTrailing(string toRemove) =>
            name.EndsWith(toRemove, StringComparison.Ordinal)
                ? name[..^toRemove.Length]
                : name;
    }
}
