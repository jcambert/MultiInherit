namespace MultiInherit.EFCore;

/// <summary>
/// Configuration options for database identifier naming.
/// </summary>
public sealed class DatabaseNamingOptions
{
    /// <summary>
    /// Convention applied to table and schema names when using
    /// <see cref="DatabaseNamingHelper.ToNameWithNamingConvention"/> or
    /// <see cref="TableBuilderExtensions"/>.
    /// <c>null</c> means no transformation (names are used as-is).
    /// </summary>
    public NamingConvention? NamingConvention { get; set; }

    /// <summary>
    /// Default schema applied to every model that does not declare an explicit schema
    /// via <c>[ModelTable(Schema = "...")]</c>.
    /// Passed through the active <see cref="NamingConvention"/> before being applied.
    /// <c>null</c> means no default schema (database-provider default is used).
    /// </summary>
    public string? DefaultSchema { get; set; }
}

/// <summary>
/// Static helper that holds the database naming convention used by
/// <see cref="ModelDbContext"/> and <see cref="TableBuilderExtensions"/>.
/// <para>
/// Configure once at startup — typically before <c>EnsureCreated</c> or migrations run:
/// <code>
/// DatabaseNamingHelper.Configure(opt => opt.NamingConvention = NamingConvention.SnakeCase);
/// </code>
/// </para>
/// </summary>
public static class DatabaseNamingHelper
{
    private static readonly DatabaseNamingOptions _options = new();

    /// <summary>
    /// Current options. Exposed as <c>Options.Value</c> so that
    /// <see cref="TableBuilderExtensions"/> can access the convention
    /// without taking a DI dependency.
    /// </summary>
    public static OptionsProxy Options { get; } = new(_options);

    /// <summary>
    /// Applies the configured naming convention to <paramref name="name"/>.
    /// If no convention is set, returns <paramref name="name"/> unchanged.
    /// </summary>
    public static string ToNameWithNamingConvention(string name)
        => name.ToNamingConvention(_options.NamingConvention);

    /// <summary>
    /// Configures the naming convention used globally by <see cref="ModelDbContext"/>
    /// and <see cref="TableBuilderExtensions"/>.
    /// Call this before any <c>OnModelCreating</c> runs.
    /// </summary>
    public static void Configure(Action<DatabaseNamingOptions> setup) => setup(_options);

    /// <summary>
    /// Thin wrapper that exposes <see cref="DatabaseNamingOptions"/> via a <c>.Value</c>
    /// property, mirroring the <c>IOptions&lt;T&gt;</c> pattern without requiring DI.
    /// </summary>
    public sealed class OptionsProxy(DatabaseNamingOptions value)
    {
        /// <summary>The current <see cref="DatabaseNamingOptions"/>.</summary>
        public DatabaseNamingOptions Value { get; } = value;
    }
}
