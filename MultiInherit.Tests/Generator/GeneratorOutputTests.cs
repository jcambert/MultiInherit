using MultiInherit.Tests.Helpers;

namespace MultiInherit.Tests.Generator;

/// <summary>
/// Verifies the structural content of the generated .g.cs files.
/// Tests use substring / pattern checks on the emitted source text.
/// </summary>
public class GeneratorOutputTests
{
    // ── Infrastructure ────────────────────────────────────────────────────

    [Fact]
    public void SimpleModel_GeneratesIModelAndPropertyChanged()
    {
        const string source = """
            using MultiInherit;
            namespace Test;
            [Model("simple.model")]
            public partial class SimpleModel
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var sources = GeneratorTestHelper.GetSources(source);

        Assert.Single(sources);
        var gen = sources[0];
        Assert.Contains("IModel", gen);
        Assert.Contains("INotifyPropertyChanged", gen);
        Assert.Contains("PropertyChanged", gen);
    }

    [Fact]
    public void SimpleModel_GeneratesModelName()
    {
        const string source = """
            using MultiInherit;
            namespace Test;
            [Model("acme.product")]
            public partial class AcmeProduct { }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        Assert.Contains("public static string ModelName => \"acme.product\"", gen);
    }

    [Fact]
    public void SimpleModel_GeneratesIdProperty()
    {
        const string source = """
            using MultiInherit;
            namespace Test;
            [Model("test.model")]
            public partial class TestModel { }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        Assert.Contains("public int Id { get; set; }", gen);
    }

    [Fact]
    public void SimpleModel_GeneratesModuleInitializer()
    {
        const string source = """
            using MultiInherit;
            namespace Test;
            [Model("my.model")]
            public partial class MyModel { }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        Assert.Contains("[ModuleInitializer]", gen);
        Assert.Contains("ModelRegistry.Register", gen);
        Assert.Contains("\"my.model\"", gen);
    }

    [Fact]
    public void SimpleModel_GeneratesFieldCatalog()
    {
        const string source = """
            using MultiInherit;
            namespace Test;
            [Model("catalog.model")]
            public partial class CatalogModel
            {
                public string Title { get; set; } = string.Empty;
                public int Count { get; set; }
            }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        Assert.Contains("public static class Fields", gen);
        Assert.Contains("Title", gen);
        Assert.Contains("Count", gen);
    }

    // ── Many2one ──────────────────────────────────────────────────────────

    [Fact]
    public void Many2one_GeneratesForeignKeyProperty()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("res.partner")]
            public partial class ResPartner { public string Name { get; set; } = string.Empty; }

            [Model("sale.order")]
            public partial class SaleOrder
            {
                [Many2one("res.partner", Required = true)]
                public partial ResPartner? Partner { get; set; }
            }
            """;

        var sources = GeneratorTestHelper.GetSources(source);
        var orderGen = sources.First(s => s.Contains("\"sale.order\""));

        // Required FK should be int (not nullable)
        Assert.Contains("public int PartnerId", orderGen);
        Assert.Contains("public partial ResPartner? Partner", orderGen);
    }

    [Fact]
    public void Many2one_Optional_GeneratesNullableForeignKey()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("res.partner")]
            public partial class ResPartner { public string Name { get; set; } = string.Empty; }

            [Model("sale.order")]
            public partial class SaleOrder
            {
                [Many2one("res.partner", Required = false, OnDelete = OnDeleteAction.SetNull)]
                public partial ResPartner? Partner { get; set; }
            }
            """;

        var sources = GeneratorTestHelper.GetSources(source);
        var orderGen = sources.First(s => s.Contains("\"sale.order\""));

        Assert.Contains("public int? PartnerId", orderGen);
    }

    [Fact]
    public void Many2one_NavigationProperty_SyncsFK()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("res.partner")]
            public partial class ResPartner { public string Name { get; set; } = string.Empty; }

            [Model("sale.order")]
            public partial class SaleOrder
            {
                [Many2one("res.partner", Required = true)]
                public partial ResPartner? Partner { get; set; }
            }
            """;

        var sources = GeneratorTestHelper.GetSources(source);
        var orderGen = sources.First(s => s.Contains("\"sale.order\""));

        // Setter syncs FK
        Assert.Contains("PartnerId = value?.Id ?? 0", orderGen);
        Assert.Contains("OnPropertyChanged", orderGen);
    }

    // ── One2many ──────────────────────────────────────────────────────────

    [Fact]
    public void One2many_GeneratesLazyCollection()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("order.header")]
            public partial class OrderHeader
            {
                [One2many("order.line", "OrderId")]
                public partial System.Collections.Generic.ICollection<OrderLine> Lines { get; set; }
            }

            [Model("order.line")]
            public partial class OrderLine
            {
                [Many2one("order.header", Required = true, OnDelete = OnDeleteAction.Cascade)]
                public partial OrderHeader? Order { get; set; }
            }
            """;

        var sources = GeneratorTestHelper.GetSources(source);
        var headerGen = sources.First(s => s.Contains("\"order.header\""));

        Assert.Contains("ICollection<OrderLine>", headerGen);
        Assert.Contains("new List<OrderLine>()", headerGen);
    }

    // ── Many2many ─────────────────────────────────────────────────────────

    [Fact]
    public void Many2many_GeneratesJoinTableComment()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("res.tag")]
            public partial class ResTag { public string Name { get; set; } = string.Empty; }

            [Model("res.partner")]
            public partial class ResPartner
            {
                [Many2many("res.tag")]
                public partial System.Collections.Generic.ICollection<ResTag> Tags { get; set; }
            }
            """;

        var sources = GeneratorTestHelper.GetSources(source);
        var partnerGen = sources.First(s => s.Contains("\"res.partner\""));

        // Join table name is alphabetically sorted: res_partner_res_tag_rel
        Assert.Contains("res_partner_res_tag_rel", partnerGen);
        Assert.Contains("ICollection<ResTag>", partnerGen);
    }

    // ── Computed fields ───────────────────────────────────────────────────

    [Fact]
    public void ComputedField_NonStored_GeneratesDirtyFlag()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("line.model")]
            public partial class LineModel
            {
                public decimal UnitPrice { get; set; }
                public int Quantity { get; set; }

                [Compute(nameof(_computeSubtotal))]
                [Depends("UnitPrice", "Quantity")]
                public partial decimal Subtotal { get; private set; }

                private void _computeSubtotal() => Subtotal = UnitPrice * Quantity;
            }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        // Dirty flag and lazy evaluation
        Assert.Contains("_subtotal_dirty", gen);
        Assert.Contains("if (_subtotal_dirty)", gen);
        Assert.Contains("_computeSubtotal();", gen);
        Assert.Contains("__Subtotal_depends", gen);
    }

    [Fact]
    public void ComputedField_Stored_GeneratesSimpleBackingField()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("order.model")]
            public partial class OrderModel
            {
                [Compute(nameof(_computeTotal), Store = true)]
                [Depends("Lines")]
                public partial decimal Total { get; private set; }

                private void _computeTotal() => Total = 42m;
            }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        // Stored: no dirty flag, but does raise OnPropertyChanged
        Assert.DoesNotContain("_total_dirty", gen);
        Assert.Contains("OnPropertyChanged(nameof(Total))", gen);
    }

    [Fact]
    public void ComputedField_DependentFieldChange_InvalidatesDirty()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("item.model")]
            public partial class ItemModel
            {
                public decimal Price { get; set; }

                [Compute(nameof(_computeDisplay))]
                [Depends("Price")]
                public partial string Display { get; private set; }

                private void _computeDisplay() => Display = Price.ToString("C");
            }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        // __InvalidateDependents must check the depends list
        Assert.Contains("__InvalidateDependents", gen);
        Assert.Contains("__Display_depends", gen);
    }

    // ── Extension inheritance ─────────────────────────────────────────────

    [Fact]
    public void ExtensionInheritance_MergesFieldsFromShards()
    {
        const string shard1 = """
            using MultiInherit;
            namespace Test;
            [Model("res.partner")]
            public partial class ResPartner
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        const string shard2 = """
            using MultiInherit;
            namespace Test;
            [Inherit("res.partner")]
            public partial class ResPartner
            {
                public string? Phone { get; set; }
            }
            """;

        // Extension should produce exactly ONE generated file (merged shards)
        var sources = GeneratorTestHelper.GetSources(shard1, shard2);
        Assert.Single(sources);
    }

    // ── Classical inheritance ─────────────────────────────────────────────

    [Fact]
    public void ClassicalInheritance_CopiesParentFieldsToChild()
    {
        const string parent = """
            using MultiInherit;
            namespace Test;
            [Model("base.person")]
            public partial class BasePerson
            {
                public string FirstName { get; set; } = string.Empty;
                public string LastName { get; set; } = string.Empty;
            }
            """;

        const string child = """
            using MultiInherit;
            namespace Test;
            [Model("hr.employee"), Inherit("base.person")]
            public partial class HrEmployee
            {
                public string Department { get; set; } = string.Empty;
            }
            """;

        var sources = GeneratorTestHelper.GetSources(parent, child);

        // Child generated file should contain the inherited fields
        var empGen = sources.First(s => s.Contains("\"hr.employee\""));
        Assert.Contains("FirstName", empGen);
        Assert.Contains("LastName", empGen);
        // Child's own field
        Assert.Contains("Department", empGen);
    }

    // ── Delegation inheritance ────────────────────────────────────────────

    [Fact]
    public void DelegationInheritance_GeneratesFKAndNavigation()
    {
        const string parent = """
            using MultiInherit;
            namespace Test;
            [Model("res.partner")]
            public partial class ResPartner
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        const string child = """
            using MultiInherit;
            namespace Test;
            [Model("hr.employee")]
            [Inherits("res.partner", ForeignKey = "PartnerId")]
            public partial class HrEmployee { }
            """;

        var sources = GeneratorTestHelper.GetSources(parent, child);
        var empGen = sources.First(s => s.Contains("\"hr.employee\""));

        // FK integer property
        Assert.Contains("public int PartnerId", empGen);
        // Navigation property
        Assert.Contains("ResPartner", empGen);
        // Delegated field forwarding
        Assert.Contains("Name", empGen);
    }

    // ── Constraints ───────────────────────────────────────────────────────

    [Fact]
    public void Constraints_GeneratesValidateConstraintsDispatcher()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("product.model")]
            public partial class ProductModel
            {
                public decimal Price { get; set; }

                [Constrains("Price")]
                private void _checkPrice()
                {
                    if (Price < 0) throw new ModelValidationException("Price cannot be negative.", nameof(Price));
                }
            }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        Assert.Contains("public void ValidateConstraints", gen);
        Assert.Contains("_checkPrice()", gen);
    }

    // ── Onchange ──────────────────────────────────────────────────────────

    [Fact]
    public void Onchange_GeneratesTriggerOnchangeDispatcher()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("res.partner")]
            public partial class ResPartner { public string Name { get; set; } = string.Empty; }

            [Model("sale.order")]
            public partial class SaleOrder
            {
                [Many2one("res.partner")]
                public partial ResPartner? Partner { get; set; }

                public string Ref { get; set; } = string.Empty;

                [Onchange("Partner")]
                private void _onchangePartner()
                {
                    if (Partner != null && string.IsNullOrEmpty(Ref))
                        Ref = $"SO/{Partner.Name}/001";
                }
            }
            """;

        var sources = GeneratorTestHelper.GetSources(source);
        var orderGen = sources.First(s => s.Contains("\"sale.order\""));

        Assert.Contains("public void TriggerOnchange", orderGen);
        Assert.Contains("_onchangePartner()", orderGen);
        Assert.Contains("__TriggerOnchange", orderGen);
    }

    // ── SQL constraints ───────────────────────────────────────────────────

    [Fact]
    public void SqlConstraint_GeneratesStaticArray()
    {
        const string source = """
            using MultiInherit;
            namespace Test;

            [Model("unique.model")]
            [SqlConstraint("unique_name", "UNIQUE(name)", "Name must be unique.")]
            public partial class UniqueModel
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        Assert.Contains("SqlConstraints", gen);
        Assert.Contains("unique_name", gen);
        Assert.Contains("UNIQUE(name)", gen);
    }

    // ── Namespace ─────────────────────────────────────────────────────────

    [Fact]
    public void GeneratedFile_IncludesCorrectNamespace()
    {
        const string source = """
            using MultiInherit;
            namespace My.Company.Models;
            [Model("acme.product")]
            public partial class AcmeProduct { }
            """;

        var gen = GeneratorTestHelper.GetSources(source)[0];

        Assert.Contains("namespace My.Company.Models", gen);
    }
}
