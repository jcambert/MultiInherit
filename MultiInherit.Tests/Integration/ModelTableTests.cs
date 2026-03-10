using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MultiInherit.Tests.Integration.Models;

namespace MultiInherit.Tests.Integration;

/// <summary>
/// Integration tests verifying that [ModelTable] correctly overrides the table name
/// and schema used by ModelDbContext when mapping models to PostgreSQL.
/// </summary>
[Collection("Integration")]
public class ModelTableTests(PostgreSqlFixture fixture)
{
    private TestDbContext Ctx() => fixture.CreateContext();

    // ── EF Core model metadata ─────────────────────────────────────────────

    [Fact]
    public void ModelTable_CustomTableName_IsReflectedInEfCoreModel()
    {
        using var ctx = Ctx();
        var entityType = ctx.Model.FindEntityType(typeof(TestWidget));

        Assert.NotNull(entityType);
        Assert.Equal("custom_widget", entityType.GetTableName());
    }

    [Fact]
    public void ModelTable_WithSchema_SchemaIsReflectedInEfCoreModel()
    {
        using var ctx = Ctx();
        var entityType = ctx.Model.FindEntityType(typeof(TestSchemaWidget));

        Assert.NotNull(entityType);
        Assert.Equal("schema_widget", entityType.GetTableName());
        Assert.Equal("public", entityType.GetSchema());
    }

    [Fact]
    public void ModelWithoutModelTable_UsesDefaultNamingConvention()
    {
        using var ctx = Ctx();
        var entityType = ctx.Model.FindEntityType(typeof(TestCategory));

        Assert.NotNull(entityType);
        // Default: "test.category" → "test_category"
        Assert.Equal("test_category", entityType.GetTableName());
    }

    // ── Physical table existence (pg_tables) ──────────────────────────────

    [Fact]
    public async Task ModelTable_CustomTableName_PhysicalTableExists()
    {
        await using var ctx = Ctx();
        var exists = await ctx.Database
            .SqlQueryRaw<int>(
                "SELECT 1 FROM information_schema.tables WHERE table_name = 'custom_widget'")
            .AnyAsync();

        Assert.True(exists, "Table 'custom_widget' was not found in the database.");
    }

    [Fact]
    public async Task ModelTable_WithSchema_PhysicalTableExistsInSchema()
    {
        await using var ctx = Ctx();
        var exists = await ctx.Database
            .SqlQueryRaw<int>(
                "SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'schema_widget'")
            .AnyAsync();

        Assert.True(exists, "Table 'public.schema_widget' was not found in the database.");
    }

    // ── CRUD through custom-named table ───────────────────────────────────

    [Fact]
    public async Task ModelTable_Insert_And_Retrieve()
    {
        await using var ctx = Ctx();
        var widget = new TestWidget { Label = "Custom-Widget-1" };
        ctx.Set<TestWidget>().Add(widget);
        await ctx.SaveChangesAsync();

        await using var verify = Ctx();
        var loaded = await verify.Set<TestWidget>().FindAsync(widget.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Custom-Widget-1", loaded.Label);
    }

    [Fact]
    public async Task ModelTable_Update_Persists()
    {
        await using var ctx = Ctx();
        var widget = new TestWidget { Label = "OldLabel" };
        ctx.Set<TestWidget>().Add(widget);
        await ctx.SaveChangesAsync();

        await using (var ctx2 = Ctx())
        {
            var entity = await ctx2.Set<TestWidget>().FindAsync(widget.Id);
            entity!.Label = "NewLabel";
            await ctx2.SaveChangesAsync();
        }

        await using var verify = Ctx();
        var updated = await verify.Set<TestWidget>().FindAsync(widget.Id);
        Assert.Equal("NewLabel", updated!.Label);
    }

    [Fact]
    public async Task ModelTable_Delete_Removes()
    {
        await using var ctx = Ctx();
        var widget = new TestWidget { Label = "ToDelete" };
        ctx.Set<TestWidget>().Add(widget);
        await ctx.SaveChangesAsync();

        await using (var ctx2 = Ctx())
        {
            var entity = await ctx2.Set<TestWidget>().FindAsync(widget.Id);
            ctx2.Set<TestWidget>().Remove(entity!);
            await ctx2.SaveChangesAsync();
        }

        await using var verify = Ctx();
        Assert.Null(await verify.Set<TestWidget>().FindAsync(widget.Id));
    }

    [Fact]
    public async Task ModelTable_SchemaModel_Insert_And_Retrieve()
    {
        await using var ctx = Ctx();
        var item = new TestSchemaWidget { Label = "Schema-Item-1" };
        ctx.Set<TestSchemaWidget>().Add(item);
        await ctx.SaveChangesAsync();

        await using var verify = Ctx();
        var loaded = await verify.Set<TestSchemaWidget>().FindAsync(item.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Schema-Item-1", loaded.Label);
    }

}
// Note: DefaultSchema integration tests live in DefaultSchemaTests.cs (own fixture/collection)
