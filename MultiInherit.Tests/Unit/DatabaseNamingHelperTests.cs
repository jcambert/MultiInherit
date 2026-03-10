using MultiInherit.EFCore;

namespace MultiInherit.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="DatabaseNamingHelper"/> and <see cref="DatabaseNamingOptions"/>.
/// These tests exercise naming convention logic without requiring a database.
/// </summary>
public class DatabaseNamingHelperTests : IDisposable
{
    // Capture the convention at construction so we can restore it after each test.
    private readonly NamingConvention? _original = DatabaseNamingHelper.Options.Value.NamingConvention;

    public void Dispose() =>
        DatabaseNamingHelper.Configure(opt => opt.NamingConvention = _original);

    // ── ToNameWithNamingConvention — no convention ─────────────────────────

    [Fact]
    public void ToName_NoConvention_ReturnsNameAsIs()
    {
        DatabaseNamingHelper.Configure(opt => opt.NamingConvention = null);

        Assert.Equal("MyTable",   DatabaseNamingHelper.ToNameWithNamingConvention("MyTable"));
        Assert.Equal("res_partner", DatabaseNamingHelper.ToNameWithNamingConvention("res_partner"));
        Assert.Equal("CamelCase", DatabaseNamingHelper.ToNameWithNamingConvention("CamelCase"));
    }

    // ── ToNameWithNamingConvention — SnakeCase ─────────────────────────────

    [Theory]
    [InlineData("MyTable",      "my_table")]
    [InlineData("ResPartner",   "res_partner")]
    [InlineData("SaleOrderLine","sale_order_line")]
    [InlineData("already_snake","already_snake")]
    public void ToName_SnakeCase_ConvertsCorrectly(string input, string expected)
    {
        DatabaseNamingHelper.Configure(opt => opt.NamingConvention = NamingConvention.SnakeCase);

        Assert.Equal(expected, DatabaseNamingHelper.ToNameWithNamingConvention(input));
    }

    // ── ToNameWithNamingConvention — other conventions ─────────────────────

    [Theory]
    [InlineData(NamingConvention.LowerCase,  "MyTable",    "mytable")]
    [InlineData(NamingConvention.UpperCase,  "MyTable",    "MYTABLE")]
    [InlineData(NamingConvention.KebabCase,  "MyTable",    "my-table")]
    [InlineData(NamingConvention.CamelCase,  "MyTable",    "myTable")]
    [InlineData(NamingConvention.UpperSnake, "MyTable",    "MY_TABLE")]
    public void ToName_Conventions_ApplyCorrectly(NamingConvention convention, string input, string expected)
    {
        DatabaseNamingHelper.Configure(opt => opt.NamingConvention = convention);

        Assert.Equal(expected, DatabaseNamingHelper.ToNameWithNamingConvention(input));
    }

    // ── Configure ─────────────────────────────────────────────────────────

    [Fact]
    public void Configure_UpdatesOptionsValue()
    {
        DatabaseNamingHelper.Configure(opt => opt.NamingConvention = NamingConvention.UpperSnake);

        Assert.Equal(NamingConvention.UpperSnake, DatabaseNamingHelper.Options.Value.NamingConvention);
    }

    [Fact]
    public void Configure_Reset_ToNull_DisablesConversion()
    {
        DatabaseNamingHelper.Configure(opt => opt.NamingConvention = NamingConvention.SnakeCase);
        DatabaseNamingHelper.Configure(opt => opt.NamingConvention = null);

        Assert.Equal("MyTable", DatabaseNamingHelper.ToNameWithNamingConvention("MyTable"));
    }

    // ── ModelTableAttribute ────────────────────────────────────────────────

    [Fact]
    public void ModelTableAttribute_TableName_IsSet()
    {
        var attr = new ModelTableAttribute("partners");

        Assert.Equal("partners", attr.TableName);
        Assert.Null(attr.Schema);
    }

    [Fact]
    public void ModelTableAttribute_Schema_CanBeProvided()
    {
        var attr = new ModelTableAttribute("partners") { Schema = "crm" };

        Assert.Equal("partners", attr.TableName);
        Assert.Equal("crm", attr.Schema);
    }
}
