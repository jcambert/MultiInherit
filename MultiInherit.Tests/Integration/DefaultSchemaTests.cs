using Microsoft.EntityFrameworkCore;
using MultiInherit.EFCore;
using MultiInherit.Tests.Integration.Models;
using Testcontainers.PostgreSql;

namespace MultiInherit.Tests.Integration;

// ── Fixture ───────────────────────────────────────────────────────────────

/// <summary>
/// Isolated PostgreSQL fixture that configures a default schema
/// before EnsureCreated runs — avoiding model cache collisions with
/// the shared "Integration" fixture.
/// </summary>
public sealed class DefaultSchemaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Configure default schema BEFORE EnsureCreated so the model is built with it.
        DatabaseNamingHelper.Configure(opt => opt.DefaultSchema = "myschema");

        // Create the "myschema" schema in PostgreSQL first (not auto-created by EnsureCreated).
        await using var setup = CreateContext();
        await setup.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS myschema");
        await setup.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Reset default schema so other tests are not affected.
        DatabaseNamingHelper.Configure(opt => opt.DefaultSchema = null);
        await _container.DisposeAsync();
    }

    public TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(ConnectionString)
            .Options;
        return new TestDbContext(options);
    }
}

[CollectionDefinition("DefaultSchema")]
public class DefaultSchemaCollection : ICollectionFixture<DefaultSchemaFixture> { }

// ── Tests ─────────────────────────────────────────────────────────────────

/// <summary>
/// Integration tests verifying that <see cref="DatabaseNamingOptions.DefaultSchema"/>
/// applies to every model that does not declare an explicit schema via <c>[ModelTable]</c>.
/// Uses its own isolated PostgreSQL container so <c>EnsureCreated</c> runs with the
/// desired <c>DefaultSchema</c> value already configured.
/// </summary>
[Collection("DefaultSchema")]
public class DefaultSchemaTests(DefaultSchemaFixture fixture)
{
    private TestDbContext Ctx() => fixture.CreateContext();

    // ── EF Core model metadata ─────────────────────────────────────────────

    [Fact]
    public void DefaultSchema_IsApplied_ToModelWithoutExplicitSchema()
    {
        using var ctx = Ctx();

        // TestCategory has no [ModelTable] → should land in default schema.
        Assert.Equal("myschema", ctx.Model.GetDefaultSchema());
    }

    [Fact]
    public void DefaultSchema_DoesNotOverride_ExplicitModelTableSchema()
    {
        using var ctx = Ctx();

        // TestSchemaWidget has [ModelTable(Schema = "public")] — explicit schema wins.
        var entityType = ctx.Model.FindEntityType(typeof(TestSchemaWidget));
        Assert.NotNull(entityType);
        Assert.Equal("public", entityType.GetSchema());
    }

    // ── Physical table existence ───────────────────────────────────────────

    [Fact]
    public async Task DefaultSchema_PhysicalTable_ExistsInCorrectSchema()
    {
        await using var ctx = Ctx();
        var exists = await ctx.Database
            .SqlQueryRaw<int>(
                "SELECT 1 FROM information_schema.tables " +
                "WHERE table_schema = 'myschema' AND table_name = 'test_category'")
            .AnyAsync();

        Assert.True(exists, "Table 'myschema.test_category' was not found.");
    }

    [Fact]
    public async Task DefaultSchema_CustomTableModel_ExistsInDefaultSchema()
    {
        await using var ctx = Ctx();
        // TestWidget has [ModelTable("custom_widget")] but no explicit Schema
        // → should be created in "myschema"
        var exists = await ctx.Database
            .SqlQueryRaw<int>(
                "SELECT 1 FROM information_schema.tables " +
                "WHERE table_schema = 'myschema' AND table_name = 'custom_widget'")
            .AnyAsync();

        Assert.True(exists, "Table 'myschema.custom_widget' was not found.");
    }

    // ── CRUD ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DefaultSchema_CRUD_WorksInDefaultSchema()
    {
        await using var ctx = Ctx();
        var cat = new TestCategory { Name = $"Schema-Cat-{Guid.NewGuid():N}"[..20] };
        ctx.Set<TestCategory>().Add(cat);
        await ctx.SaveChangesAsync();

        await using var verify = Ctx();
        var loaded = await verify.Set<TestCategory>().FindAsync(cat.Id);
        Assert.NotNull(loaded);
        Assert.Equal(cat.Name, loaded.Name);
    }
}
