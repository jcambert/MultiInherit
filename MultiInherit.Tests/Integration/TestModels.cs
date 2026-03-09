namespace MultiInherit.Tests.Integration.Models;

// ════════════════════════════════════════════════════════════════════════════
// Delegation inheritance test models
// test.contact  — delegated parent (owns the stored fields)
// test.employee — delegating child ([Inherits("test.contact")])
// ════════════════════════════════════════════════════════════════════════════

[Model("test.contact", Description = "Test Contact")]
public partial class TestContact
{
    [ModelField(String = "Name", Required = true)]
    public string Name { get; set; } = string.Empty;

    [ModelField(String = "Email")]
    public string? Email { get; set; }
}

/// <summary>
/// Employee delegates identity fields (Name, Email) to test.contact via [Inherits].
/// The FK ContactId is auto-generated; Name and Email are forwarded transparently.
/// </summary>
[Model("test.employee", Description = "Test Employee")]
[Inherits("test.contact")]
public partial class TestEmployee
{
    [ModelField(String = "Department")]
    public string Department { get; set; } = string.Empty;
}

// ════════════════════════════════════════════════════════════════════════════
// test.category  — simple lookup (Many2one target)
// ════════════════════════════════════════════════════════════════════════════

[Model("test.category", Description = "Test Category")]
public partial class TestCategory
{
    [ModelField(String = "Name", Required = true)]
    public string Name { get; set; } = string.Empty;

    [ModelField(String = "Description")]
    public string? Description { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// test.tag  — lookup for Many2many
// ════════════════════════════════════════════════════════════════════════════

[Model("test.tag", Description = "Test Tag")]
public partial class TestTag
{
    [ModelField(String = "Label", Required = true)]
    public string Label { get; set; } = string.Empty;
}

// ════════════════════════════════════════════════════════════════════════════
// test.item  — product with Many2one → category + computed field
// ════════════════════════════════════════════════════════════════════════════

[Model("test.item", Description = "Test Item")]
public partial class TestItem
{
    [ModelField(String = "Title", Required = true)]
    public string Title { get; set; } = string.Empty;

    [ModelField(String = "Price")]
    public decimal Price { get; set; }

    [ModelField(String = "Quantity")]
    public int Quantity { get; set; } = 1;

    // Many2one → test.category (optional)
    [Many2one("test.category", String = "Category", Required = false, OnDelete = OnDeleteAction.SetNull)]
    public partial TestCategory? Category { get; set; }

    // Computed non-stored field
    [Compute(nameof(_computeTotal))]
    [Depends("Price", "Quantity")]
    public partial decimal Total { get; private set; }

    private void _computeTotal() => Total = Price * Quantity;

    // Constraint
    [Constrains("Price")]
    private void _checkPrice()
    {
        if (Price < 0)
            throw new ModelValidationException("Price cannot be negative.", nameof(Price));
    }
}

// ════════════════════════════════════════════════════════════════════════════
// test.order  — order with One2many lines
// ════════════════════════════════════════════════════════════════════════════

[Model("test.order", Description = "Test Order")]
public partial class TestOrder
{
    [ModelField(String = "Reference", Required = true)]
    public string Reference { get; set; } = string.Empty;

    // One2many → test.order.line
    [One2many("test.order.line", "OrderId", String = "Lines")]
    public partial ICollection<TestOrderLine> Lines { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// test.order.line  — order line with Many2one back to order + item
// ════════════════════════════════════════════════════════════════════════════

[Model("test.order.line", Description = "Test Order Line")]
public partial class TestOrderLine
{
    // Many2one → test.order (parent)
    [Many2one("test.order", String = "Order", Required = true, OnDelete = OnDeleteAction.Cascade)]
    public partial TestOrder? Order { get; set; }

    // Many2one → test.item
    [Many2one("test.item", String = "Item", Required = true, OnDelete = OnDeleteAction.Restrict)]
    public partial TestItem? Item { get; set; }

    [ModelField(String = "Quantity")]
    public int Quantity { get; set; } = 1;
}
