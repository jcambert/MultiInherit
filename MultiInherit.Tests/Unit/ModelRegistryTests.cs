using MultiInherit.Tests.Integration.Models;

namespace MultiInherit.Tests.Unit;

/// <summary>
/// Tests for <see cref="MultiInherit.ModelRegistry"/>.
/// These tests rely on the test models (TestCategory, TestItem…) defined
/// in Integration/TestModels.cs, which are auto-registered at startup
/// via the generated [ModuleInitializer] code.
/// </summary>
public class ModelRegistryTests
{
    // ── Get by name ───────────────────────────────────────────────────────

    [Fact]
    public void Get_ByName_ReturnsRegisteredModel()
    {
        var meta = ModelRegistry.Get("test.category");

        Assert.NotNull(meta);
        Assert.Equal("test.category", meta.Name);
    }

    [Fact]
    public void Get_ByName_UnknownModel_ReturnsNull()
    {
        var meta = ModelRegistry.Get("does.not.exist");

        Assert.Null(meta);
    }

    // ── Get by type ───────────────────────────────────────────────────────

    [Fact]
    public void Get_ByType_ReturnsRegisteredModel()
    {
        var meta = ModelRegistry.Get(typeof(TestCategory));

        Assert.NotNull(meta);
        Assert.Equal("test.category", meta.Name);
        Assert.Equal(typeof(TestCategory), meta.ClrType);
    }

    [Fact]
    public void Get_Generic_ReturnsRegisteredModel()
    {
        var meta = ModelRegistry.Get<TestCategory>();

        Assert.NotNull(meta);
        Assert.Equal("test.category", meta.Name);
    }

    // ── All ───────────────────────────────────────────────────────────────

    [Fact]
    public void All_ContainsAllTestModels()
    {
        var all = ModelRegistry.All().Select(m => m.Name).ToHashSet();

        Assert.Contains("test.category", all);
        Assert.Contains("test.item", all);
        Assert.Contains("test.tag", all);
    }

    // ── ModelMeta content ─────────────────────────────────────────────────

    [Fact]
    public void ModelMeta_ClrType_IsCorrect()
    {
        var meta = ModelRegistry.Get("test.item")!;

        Assert.Equal(typeof(TestItem), meta.ClrType);
    }

    [Fact]
    public void ModelMeta_Inherits_IsEmptyForBaseModel()
    {
        var meta = ModelRegistry.Get("test.category")!;

        Assert.Empty(meta.Inherits);
    }

    // ── CreateInstance ────────────────────────────────────────────────────

    [Fact]
    public void CreateInstance_ByName_CreatesCorrectType()
    {
        var instance = ModelRegistry.CreateInstance("test.category");

        Assert.NotNull(instance);
        Assert.IsType<TestCategory>(instance);
    }

    [Fact]
    public void CreateInstance_Generic_ReturnsStronglyTyped()
    {
        var instance = ModelRegistry.CreateInstance<TestCategory>("test.category");

        Assert.NotNull(instance);
        Assert.IsType<TestCategory>(instance);
    }

    [Fact]
    public void CreateInstance_UnknownModel_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => ModelRegistry.CreateInstance("unknown.model"));
    }

    // ── ModelName static property ─────────────────────────────────────────

    [Fact]
    public void ModelName_StaticProperty_ReturnsCorrectName()
    {
        Assert.Equal("test.category", TestCategory.ModelName);
        Assert.Equal("test.item", TestItem.ModelName);
    }

    // ── IModel.Id ────────────────────────────────────────────────────────

    [Fact]
    public void Model_ImplementsIModel_WithIdProperty()
    {
        IModel model = new TestCategory();

        model.Id = 42;
        Assert.Equal(42, model.Id);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────

    [Fact]
    public void Model_RaisesPropertyChanged_WhenMany2oneSet()
    {
        // Own fields (defined directly in user code) don't get a generated
        // wrapper — they stay as plain auto-properties.
        // Many2one navigation properties DO raise PropertyChanged because
        // the generator emits the full setter logic.
        var item = new TestItem { Title = "PC" };
        var changedProps = new List<string?>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        item.Category = new TestCategory { Name = "Electronics" };

        // Generator emits OnPropertyChanged(nameof(Category)) + OnPropertyChanged(nameof(CategoryId))
        Assert.Contains("Category", changedProps);
        Assert.Contains("CategoryId", changedProps);
    }
}
