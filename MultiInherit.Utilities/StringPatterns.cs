using System.Text.RegularExpressions;

namespace MultiInherit;

public static partial class StringPatterns
{
    [GeneratedRegex("[A-Z]{2,}(?=[A-Z][a-z]+[0-9]*|\b)|[A-Z]?[a-z]+[0-9]*|[A-Z]|[0-9]+")]
    public static partial Regex SnakeCaseRegex();

    [GeneratedRegex("[A-Z]+(?=[A-Z][a-z])|[A-Z][a-z]+")]
    public static partial Regex UpperSnakeRegex();


    [GeneratedRegex("(?<!^)([A-Z][a-z]|(?<=[a-z])[A-Z0-9])")]
    public static partial Regex KebabCaseRegex();
}