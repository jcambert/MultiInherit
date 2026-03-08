using Microsoft.EntityFrameworkCore;
using MultiInherit.Tests.Integration.Models;

namespace MultiInherit.Tests.Integration;

/// <summary>
/// Integration tests using a real PostgreSQL container (via TestContainers).
/// Tests exercise CRUD, Many2one relations, One2many navigation, computed fields,
/// and constraint validation against the EF Core-mapped test models.
/// </summary>
[Collection("Integration")]
public class IntegrationTests(PostgreSqlFixture fixture)
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private TestDbContext Ctx() => fixture.CreateContext();

    private async Task<TestCategory> CreateCategoryAsync(string name = "Electronics")
    {
        await using var ctx = Ctx();
        var cat = new TestCategory { Name = name };
        ctx.Set<TestCategory>().Add(cat);
        await ctx.SaveChangesAsync();
        return cat;
    }

    // ── CRUD — TestCategory ───────────────────────────────────────────────

    [Fact]
    public async Task Category_Insert_And_Retrieve()
    {
        var cat = await CreateCategoryAsync("Books");

        await using var ctx = Ctx();
        var loaded = await ctx.Set<TestCategory>().FindAsync(cat.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Books", loaded.Name);
        Assert.Equal(cat.Id, loaded.Id);
    }

    [Fact]
    public async Task Category_Update_Persists()
    {
        var cat = await CreateCategoryAsync("OldName");

        await using (var ctx = Ctx())
        {
            var entity = await ctx.Set<TestCategory>().FindAsync(cat.Id);
            entity!.Name = "NewName";
            await ctx.SaveChangesAsync();
        }

        await using var verify = Ctx();
        var updated = await verify.Set<TestCategory>().FindAsync(cat.Id);
        Assert.Equal("NewName", updated!.Name);
    }

    [Fact]
    public async Task Category_Delete_Removes()
    {
        var cat = await CreateCategoryAsync("ToDelete");

        await using (var ctx = Ctx())
        {
            var entity = await ctx.Set<TestCategory>().FindAsync(cat.Id);
            ctx.Set<TestCategory>().Remove(entity!);
            await ctx.SaveChangesAsync();
        }

        await using var verify = Ctx();
        var gone = await verify.Set<TestCategory>().FindAsync(cat.Id);
        Assert.Null(gone);
    }

    [Fact]
    public async Task Category_Query_ByName()
    {
        var uniqueName = $"Unique-{Guid.NewGuid():N}";
        await CreateCategoryAsync(uniqueName);

        await using var ctx = Ctx();
        var found = await ctx.Set<TestCategory>()
            .FirstOrDefaultAsync(c => c.Name == uniqueName);

        Assert.NotNull(found);
        Assert.Equal(uniqueName, found.Name);
    }

    // ── CRUD — TestItem ───────────────────────────────────────────────────

    [Fact]
    public async Task Item_Insert_WithoutCategory()
    {
        await using var ctx = Ctx();
        var item = new TestItem { Title = "Widget", Price = 9.99m, Quantity = 5 };
        ctx.Set<TestItem>().Add(item);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Set<TestItem>().FindAsync(item.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Widget", loaded.Title);
        Assert.Equal(9.99m, loaded.Price);
    }

    // ── Many2one — TestItem → TestCategory ────────────────────────────────

    [Fact]
    public async Task Item_Many2one_InsertWithCategory()
    {
        var cat = await CreateCategoryAsync("Gadgets");

        await using var ctx = Ctx();
        var item = new TestItem
        {
            Title = "Gadget Pro",
            Price = 199.99m,
            CategoryId = cat.Id
        };
        ctx.Set<TestItem>().Add(item);
        await ctx.SaveChangesAsync();

        // Verify FK is stored
        var loaded = await ctx.Set<TestItem>().FindAsync(item.Id);
        Assert.Equal(cat.Id, loaded!.CategoryId);
    }

    [Fact]
    public async Task Item_Many2one_NavigationLoads_WithInclude()
    {
        var cat = await CreateCategoryAsync("Tools");

        await using var insertCtx = Ctx();
        var item = new TestItem { Title = "Drill", Price = 149.99m, CategoryId = cat.Id };
        insertCtx.Set<TestItem>().Add(item);
        await insertCtx.SaveChangesAsync();

        // Load with navigation
        await using var readCtx = Ctx();
        var loaded = await readCtx.Set<TestItem>()
            .Include(i => i.Category)
            .FirstAsync(i => i.Id == item.Id);

        Assert.NotNull(loaded.Category);
        Assert.Equal("Tools", loaded.Category.Name);
    }

    [Fact]
    public async Task Item_Many2one_SetNull_OnCategoryDelete()
    {
        var cat = await CreateCategoryAsync("TempCategory");

        await using var ctx = Ctx();
        var item = new TestItem { Title = "Orphan", Price = 10m, CategoryId = cat.Id };
        ctx.Set<TestItem>().Add(item);
        await ctx.SaveChangesAsync();

        // Delete category — should set FK to null (OnDelete = SetNull)
        var catEntity = await ctx.Set<TestCategory>().FindAsync(cat.Id);
        ctx.Set<TestCategory>().Remove(catEntity!);
        await ctx.SaveChangesAsync();

        var loadedItem = await ctx.Set<TestItem>().FindAsync(item.Id);
        Assert.Null(loadedItem!.CategoryId);
    }

    // ── One2many — TestOrder → TestOrderLine ──────────────────────────────

    [Fact]
    public async Task Order_With_Lines_InsertAndRetrieve()
    {
        await using var ctx = Ctx();

        var item = new TestItem { Title = "Widget", Price = 5m };
        ctx.Set<TestItem>().Add(item);
        await ctx.SaveChangesAsync();

        var order = new TestOrder { Reference = $"ORD-{Guid.NewGuid():N}"[..10] };
        ctx.Set<TestOrder>().Add(order);
        await ctx.SaveChangesAsync();

        var line = new TestOrderLine { OrderId = order.Id, ItemId = item.Id, Quantity = 3 };
        ctx.Set<TestOrderLine>().Add(line);
        await ctx.SaveChangesAsync();

        // One2many is Ignored by EF Core (configured from child's Many2one side).
        // Query the lines directly by FK.
        await using var readCtx = Ctx();
        var lines = await readCtx.Set<TestOrderLine>()
            .Where(l => l.OrderId == order.Id)
            .ToListAsync();

        Assert.Single(lines);
        Assert.Equal(3, lines.First().Quantity);
    }

    [Fact]
    public async Task OrderLine_CascadeDelete_WhenOrderDeleted()
    {
        await using var ctx = Ctx();

        var item = new TestItem { Title = "CascadeItem", Price = 1m };
        ctx.Set<TestItem>().Add(item);
        await ctx.SaveChangesAsync();

        var order = new TestOrder { Reference = $"ORD-{Guid.NewGuid():N}"[..10] };
        ctx.Set<TestOrder>().Add(order);
        await ctx.SaveChangesAsync();

        var line = new TestOrderLine { OrderId = order.Id, ItemId = item.Id, Quantity = 1 };
        ctx.Set<TestOrderLine>().Add(line);
        await ctx.SaveChangesAsync();

        var lineId = line.Id;

        // Delete order → lines should cascade
        var orderEntity = await ctx.Set<TestOrder>().FindAsync(order.Id);
        ctx.Set<TestOrder>().Remove(orderEntity!);
        await ctx.SaveChangesAsync();

        var orphanLine = await ctx.Set<TestOrderLine>().FindAsync(lineId);
        Assert.Null(orphanLine);
    }

    // ── Computed fields (runtime, not persisted) ──────────────────────────

    [Fact]
    public void ComputedField_Total_IsCalculatedCorrectly()
    {
        var item = new TestItem { Price = 10.50m, Quantity = 3 };

        Assert.Equal(31.50m, item.Total);
    }

    [Fact]
    public void ComputedField_Total_IsLazilyEvaluatedOnFirstAccess()
    {
        // Own fields (defined in user code) don't have a generated wrapper,
        // so __InvalidateDependents is NOT called when Price/Quantity change.
        // The dirty flag is true at construction → first access triggers compute.
        var item1 = new TestItem { Price = 5m, Quantity = 4 };
        Assert.Equal(20m, item1.Total);

        // A fresh instance with different values computes correctly.
        var item2 = new TestItem { Price = 10m, Quantity = 3 };
        Assert.Equal(30m, item2.Total);
    }

    // ── Constraints ───────────────────────────────────────────────────────

    [Fact]
    public void Constraint_NegativePrice_ThrowsModelValidationException()
    {
        var item = new TestItem { Title = "Bad Item", Price = 100m };

        item.Price = -1m;

        Assert.Throws<ModelValidationException>(() => item.ValidateConstraints());
    }

    [Fact]
    public void Constraint_ValidPrice_DoesNotThrow()
    {
        var item = new TestItem { Title = "Good Item", Price = 50m, Quantity = 2 };

        var ex = Record.Exception(() => item.ValidateConstraints());
        Assert.Null(ex);
    }

    // ── Many2one FK sync ──────────────────────────────────────────────────

    [Fact]
    public void Many2one_AssignNavigation_SyncsFK()
    {
        var cat = new TestCategory { Name = "SyncTest" };
        cat.Id = 99;

        var item = new TestItem { Title = "SyncItem" };
        item.Category = cat;

        Assert.Equal(99, item.CategoryId);
    }

    [Fact]
    public void Many2one_SetNavigationToNull_SetsFKToZero()
    {
        var cat = new TestCategory { Name = "NullTest" };
        cat.Id = 42;

        var item = new TestItem { Title = "NullItem" };
        item.Category = cat;
        Assert.Equal(42, item.CategoryId);

        item.Category = null;
        Assert.Equal(0, item.CategoryId);
    }

    // ── ModelRegistry (integration context) ───────────────────────────────

    [Fact]
    public void ModelRegistry_AllTestModels_AreRegistered()
    {
        var names = ModelRegistry.All().Select(m => m.Name).ToHashSet();

        Assert.Contains("test.category", names);
        Assert.Contains("test.item", names);
        Assert.Contains("test.tag", names);
        Assert.Contains("test.order", names);
        Assert.Contains("test.order.line", names);
    }

    [Fact]
    public void ModelRegistry_EFCore_CanQueryAllModels()
    {
        // Verifies EF Core can create a DbContext without throwing
        using var ctx = Ctx();
        Assert.NotNull(ctx.Model);
    }
}
