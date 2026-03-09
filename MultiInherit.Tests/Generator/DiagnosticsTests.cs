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

    // ── MI0003 — Class must be partial ───────────────────────────────────

    [Fact]
    public void MI0003_NonPartialClass_EmitsDiagnosticAndNoSource()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;
            [Model("bad.model")]
            public class NonPartialModel { }
            """;

        var (diags, sources) = GeneratorTestHelper.Run(source);

        Assert.Contains(diags, d => d.Id == "MI0003");
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

    // ── MI0004 — Compute method not found ─────────────────────────────────

    [Fact]
    public void MI0004_ComputeMethod_NotFound()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                [Compute("_compute_missing")]
                [Depends("Name")]
                public partial string Display { get; private set; }

                public string Name { get; set; } = string.Empty;
                // _compute_missing does not exist
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0004");
    }

    [Fact]
    public void MI0004_NotEmitted_WhenComputeMethodExists()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                public string Name { get; set; } = string.Empty;

                [Compute(nameof(_computeDisplay))]
                [Depends("Name")]
                public partial string Display { get; private set; }

                private void _computeDisplay() => Display = Name.ToUpper();
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0004");
    }

    // ── MI0005 — Computed property must be read-only ───────────────────────

    [Fact]
    public void MI0005_ComputedProperty_HasPublicSetter()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                public string Name { get; set; } = string.Empty;

                [Compute(nameof(_computeDisplay))]
                [Depends("Name")]
                public string Display { get; set; } = string.Empty;

                private void _computeDisplay() => Display = Name.ToUpper();
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0005");
    }

    // ── MI0006 — Foreign key collision ────────────────────────────────────

    [Fact]
    public void MI0006_ForeignKey_Collision_WithExistingProperty()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("res.partner")]
            public partial class ResPartner { public string Name { get; set; } = string.Empty; }

            [Model("sale.order")]
            [Inherits("res.partner")]
            public partial class SaleOrder
            {
                // PartnerId is the default FK derived from "res.partner",
                // but that property already exists explicitly.
                public int PartnerId { get; set; }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0006");
    }

    [Fact]
    public void MI0006_NotEmitted_WhenForeignKeyExplicit_NoConflict()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("res.partner")]
            public partial class ResPartner { public string Name { get; set; } = string.Empty; }

            [Model("sale.order")]
            [Inherits("res.partner", ForeignKey = "CustomPartnerId")]
            public partial class SaleOrder { }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0006");
    }

    // ── MI0007 — Constrains method wrong signature ─────────────────────────

    [Fact]
    public void MI0007_ConstrainsMethod_IsStatic()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                public string Name { get; set; } = string.Empty;

                [Constrains("Name")]
                private static void _checkName() { }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0007");
    }

    [Fact]
    public void MI0007_ConstrainsMethod_ReturnsNonVoid()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                public string Name { get; set; } = string.Empty;

                [Constrains("Name")]
                private bool _checkName() => true;
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0007");
    }

    [Fact]
    public void MI0007_NotEmitted_WhenConstrainsMethodValid()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                public string Name { get; set; } = string.Empty;

                [Constrains("Name")]
                private void _checkName()
                {
                    if (string.IsNullOrEmpty(Name))
                        throw new ModelValidationException("Required.", nameof(Name));
                }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0007");
    }

    // ── MI0008 — Onchange method wrong signature ───────────────────────────

    [Fact]
    public void MI0008_OnchangeMethod_IsStatic()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                public string Name { get; set; } = string.Empty;

                [Onchange("Name")]
                private static void _onchangeName() { }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0008");
    }

    [Fact]
    public void MI0008_NotEmitted_WhenOnchangeMethodValid()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                public string Name { get; set; } = string.Empty;
                public string DisplayName { get; set; } = string.Empty;

                [Onchange("Name")]
                private void _onchangeName()
                {
                    DisplayName = Name.Trim();
                }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0008");
    }

    // ── MI0011 — Computed property must be partial ─────────────────────────

    [Fact]
    public void MI0011_ComputedProperty_NotPartial()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                public string Name { get; set; } = string.Empty;

                [Compute(nameof(_computeDisplay))]
                [Depends("Name")]
                public string Display { get; private set; } = string.Empty;

                private void _computeDisplay() => Display = Name.ToUpper();
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0011");
    }

    [Fact]
    public void MI0011_NotEmitted_WhenComputedPropertyIsPartial()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                public string Name { get; set; } = string.Empty;

                [Compute(nameof(_computeDisplay))]
                [Depends("Name")]
                public partial string Display { get; private set; }

                private void _computeDisplay() => Display = Name.ToUpper();
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0011");
    }

    // ── MI0012 — Selection on non-string property ──────────────────────────

    [Fact]
    public void MI0012_Selection_OnIntProperty()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                [Selection("1", "2", "3")]
                public int Status { get; set; }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0012");
    }

    [Fact]
    public void MI0012_NotEmitted_WhenSelectionOnString()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                [Selection("draft", "confirmed", "done")]
                public string Status { get; set; } = "draft";
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0012");
    }

    [Fact]
    public void MI0012_NotEmitted_WhenSelectionOnNullableString()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                [Selection("draft", "confirmed", "done")]
                public string? Status { get; set; }
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0012");
    }

    // ── MI0102 — Model in global namespace ────────────────────────────────

    [Fact]
    public void MI0102_ModelInGlobalNamespace_EmitsWarning()
    {
        const string source = """
            using MultiInherit;
            [Model("global.model")]
            public partial class GlobalModel { public string Name { get; set; } = string.Empty; }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0102");
        Assert.Equal(DiagnosticSeverity.Warning, diags.First(d => d.Id == "MI0102").Severity);
    }

    [Fact]
    public void MI0102_NotEmitted_WhenModelInNamespace()
    {
        const string source = """
            using MultiInherit;
            namespace MyApp.Models;
            [Model("my.model")]
            public partial class MyModel { public string Name { get; set; } = string.Empty; }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0102");
    }

    // ── MI0013 — Default method not found ─────────────────────────────────

    [Fact]
    public void MI0013_DefaultMethod_NotFound()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                [Default("GetDefaultStatus")]
                public partial string Status { get; set; }
                // GetDefaultStatus does not exist
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0013");
    }

    [Fact]
    public void MI0013_DefaultMethod_WrongReturnType()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                [Default(nameof(GetDefaultStatus))]
                public partial string Status { get; set; }

                private int GetDefaultStatus() => 42;  // returns int, not string
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.Contains(diags, d => d.Id == "MI0013");
    }

    [Fact]
    public void MI0013_NotEmitted_WhenDefaultMethodValid()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                [Default(nameof(GetDefaultStatus))]
                public partial string Status { get; set; }

                private string GetDefaultStatus() => "draft";
            }
            """;

        var diags = GeneratorTestHelper.GetDiagnostics(source);

        Assert.DoesNotContain(diags, d => d.Id == "MI0013");
    }

    [Fact]
    public void MI0013_DefaultMethod_GeneratesPropertyImplementation()
    {
        const string source = """
            using MultiInherit;
            namespace TestModels;

            [Model("my.model")]
            public partial class MyModel
            {
                [Default(nameof(GetDefaultStatus))]
                public partial string Status { get; set; }

                private string GetDefaultStatus() => "draft";
            }
            """;

        var (diags, sources) = GeneratorTestHelper.Run(source);

        Assert.DoesNotContain(diags, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Single(sources);
        var src = sources[0];
        Assert.Contains("GetDefaultStatus()", src);
        Assert.Contains("_status_backing", src);
        Assert.Contains("_status_defaulted", src);
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
