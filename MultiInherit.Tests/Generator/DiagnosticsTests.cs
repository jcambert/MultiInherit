using Microsoft.CodeAnalysis;
using MultiInherit.Tests.Helpers;

namespace MultiInherit.Tests.Generator;

/// <summary>
/// Tests that the generator emits the expected diagnostics for each MI code.
///
/// NOTE — diagnostics architecture:
///   • ModelResolver → MI0001, MI0002, MI0009, MI0010, MI0101  (correctly emitted)
///   • ModelParser   → MI0003–MI0008, MI0102  (collected but currently DROPPED
///     because parseDiagnostics list in RegisterSourceOutput is never populated).
///     These tests document the current observable behavior.
/// </summary>
public class DiagnosticsTests
{
    // ── MI0001 — Parent model not found ───────────────────────────────────

    [Fact]
    public void MI0001_ClassicalInheritance_ParentNotDeclared()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;
            [Model("child.model"), Inherit("nonexistent.parent")]
            public partial class ChildModel { }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0001");
    }

    [Fact]
    public void MI0001_DelegationInheritance_ParentNotDeclared()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;
            [Model("child.model")]
            [Inherits("ghost.parent", ForeignKey = "GhostId")]
            public partial class ChildModel { }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0001");
    }

    [Fact]
    public void MI0001_NotEmitted_WhenParentExists()
    {
        const string parent = """
            using MultiInherit;
            namespace TestModels;
            [Model("parent.model")]
            public partial class ParentModel { public string Name { get; set; } = string.Empty; }
            """;

        const string child = """
            using MultiInherit;
            namespace TestModels;
            [Model("child.model"), Inherit("parent.model")]
            public partial class ChildModel { }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(parent, child);

        Assert.DoesNotContain(diags, d => d.Id == "MI0001");
    }

    // ── MI0002 — Circular inheritance ─────────────────────────────────────

    [Fact]
    public void MI0002_DirectCycle_AB()
    {
        const string a = """
            using MultiInherit;
            namespace TestModels;
            [Model("model.a"), Inherit("model.b")]
            public partial class ModelA { }
            """;

        const string b = """
            using MultiInherit;
            namespace TestModels;
            [Model("model.b"), Inherit("model.a")]
            public partial class ModelB { }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(a, b);

        Assert.Contains(diags, d => d.Id == "MI0002");
    }

    [Fact]
    public void MI0002_IndirectCycle_ABC()
    {
        const string a = """
            using MultiInherit;
            namespace TestModels;
            [Model("m.a"), Inherit("m.c")]
            public partial class MA { }
            """;

        const string b = """
            using MultiInherit;
            namespace TestModels;
            [Model("m.b"), Inherit("m.a")]
            public partial class MB { }
            """;

        const string c = """
            using MultiInherit;
            namespace TestModels;
            [Model("m.c"), Inherit("m.b")]
            public partial class MC { }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(a, b, c);

        Assert.Contains(diags, d => d.Id == "MI0002");
    }

    [Fact]
    public void MI0002_NotEmitted_WhenNoCircle()
    {
        const string parent = """
            using MultiInherit;
            namespace TestModels;
            [Model("base.model")]
            public partial class BaseModel { public string Name { get; set; } = string.Empty; }
            """;

        const string child = """
            using MultiInherit;
            namespace TestModels;
            [Model("child.model"), Inherit("base.model")]
            public partial class ChildModel { }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(parent, child);

        Assert.DoesNotContain(diags, d => d.Id == "MI0002");
    }

    // ── MI0003 — Class must be partial (currently dropped from parser) ─────

    /// <remarks>
    /// ModelParser emits MI0003 but the diagnostic is lost before reaching
    /// RegisterSourceOutput. Non-partial classes return null from Parse()
    /// and simply produce no generated source — no error is surfaced.
    /// This test documents the current observable behavior.
    /// </remarks>
    [Fact]
    public void MI0003_NonPartialClass_ProducesNoSource_NoDiagnostic()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;
            [Model("bad.model")]
            public class NonPartialModel { }
            """;

        var (diags, sources) = GeneratorTestHelper.Run(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0003");
        Assert.Empty(sources);
    }

    // ── MI0009 — One2many inverse field not found ─────────────────────────

    [Fact]
    public void MI0009_One2many_InverseNotFound()
    {
        const string source = """
            using MultiInherit;
            using System.Collections.Generic;
            namespace TestModels;

            [Model("order.header")]
            public partial class OrderHeader
            {
                [One2many("order.line", "NonExistentField")]
                public partial System.Collections.Generic.ICollection<OrderLine> Lines { get; set; }
            }

            [Model("order.line")]
            public partial class OrderLine
            {
                public string Name { get; set; } = string.Empty;
                // No Many2one named NonExistentField
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0009");
    }

    [Fact]
    public void MI0009_NotEmitted_WhenInverseExists()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

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

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0009");
    }

    // ── MI0010 — Relation comodel not found ───────────────────────────────

    [Fact]
    public void MI0010_Many2one_ComodelNotFound()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("sale.order")]
            public partial class SaleOrder
            {
                [Many2one("ghost.partner")]
                public partial object? Partner { get; set; }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0010");
    }

    [Fact]
    public void MI0010_Many2many_ComodelNotFound()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("product.tmpl")]
            public partial class ProductTmpl
            {
                [Many2many("ghost.tag")]
                public partial System.Collections.Generic.ICollection<object> Tags { get; set; }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0010");
    }

    [Fact]
    public void MI0010_NotEmitted_WhenComodelExists()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("res.tag")]
            public partial class ResTag { public string Name { get; set; } = string.Empty; }

            [Model("res.partner")]
            public partial class ResPartner
            {
                [Many2many("res.tag")]
                public partial System.Collections.Generic.ICollection<ResTag> Tags { get; set; }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0010");
    }

    // ── MI0101 — Field name conflict between classical parents ─────────────

    [Fact]
    public void MI0101_FieldConflict_BetweenTwoClassicalParents()
    {
        const string parent1 = """
            using MultiInherit;
            namespace TestModels;
            [Model("base.a")]
            public partial class BaseA { public string SharedName { get; set; } = string.Empty; }
            """;

        const string parent2 = """
            using MultiInherit;
            namespace TestModels;
            [Model("base.b")]
            public partial class BaseB { public string SharedName { get; set; } = string.Empty; }
            """;

        const string child = """
            using MultiInherit;
            namespace TestModels;
            [Model("child.model"), Inherit("base.a"), Inherit("base.b")]
            public partial class ChildModel { }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(parent1, parent2, child);

        Assert.Contains(diags, d => d.Id == "MI0101");
        Assert.Equal(DiagnosticSeverity.Warning, diags.First(d => d.Id == "MI0101").Severity);
    }

    [Fact]
    public void MI0101_NotEmitted_WhenNoConflict()
    {
        const string parent1 = """
            using MultiInherit;
            namespace TestModels;
            [Model("base.a")]
            public partial class BaseA { public string NameA { get; set; } = string.Empty; }
            """;

        const string parent2 = """
            using MultiInherit;
            namespace TestModels;
            [Model("base.b")]
            public partial class BaseB { public string NameB { get; set; } = string.Empty; }
            """;

        const string child = """
            using MultiInherit;
            namespace TestModels;
            [Model("child.model"), Inherit("base.a"), Inherit("base.b")]
            public partial class ChildModel { }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(parent1, parent2, child);

        Assert.DoesNotContain(diags, d => d.Id == "MI0101");
    }

    // ── No diagnostics on valid model ─────────────────────────────────────

    [Fact]
    public void ValidModel_ProducesNoDiagnostics()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("valid.model")]
            public partial class ValidModel
            {
                public string Name { get; set; } = string.Empty;
                public int Count { get; set; }

                [Compute(nameof(_computeDisplay))]
                [Depends("Name", "Count")]
                public partial string Display { get; private set; }

                private void _computeDisplay() => Display = $"{Name} ({Count})";

                [Constrains("Name")]
                private void _checkName()
                {
                    if (string.IsNullOrWhiteSpace(Name))
                        throw new ModelValidationException("Name required.", nameof(Name));
                }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Empty(diags.Where(d => d.Severity == DiagnosticSeverity.Error));
    }
}
