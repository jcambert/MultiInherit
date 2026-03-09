namespace MultiInherit.Sample.Models;

// ════════════════════════════════════════════════════════════════════════════
// res.tag  — simple lookup model (Many2many target)
// ════════════════════════════════════════════════════════════════════════════

[Model("res.tag", Description = "Tag")]
[SqlConstraint("unique_tag_name", "UNIQUE(name)", "Tag name must be unique.")]
public partial class ResTag
{
    [ModelField(String = "Name", Required = true)]
    public string Name { get; set; } = string.Empty;

    public string? Color { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// res.partner  — base model with SQL constraint + Tags M2M
// ════════════════════════════════════════════════════════════════════════════

[Model("res.partner", Description = "Contact / Partner")]
[SqlConstraint("unique_partner_email", "UNIQUE(email)", "Email must be unique.")]
public partial class ResPartner
{
    [ModelField(String = "Name", Required = true)]
    public string Name { get; set; } = string.Empty;

    [ModelField(String = "Email")]
    public string? Email { get; set; }

    [ModelField(String = "Street")]
    public string? Street { get; set; }

    // Many2many → res.tag (join table: res_partner_res_tag_rel)
    [Many2many("res.tag", String = "Tags")]
    public partial ICollection<ResTag> Tags { get; set; }

    // One2many → sale.order (inverse: Partner)
    [One2many("sale.order", "PartnerId", String = "Sales Orders")]
    public partial ICollection<SaleOrder> Orders { get; set; }

    // Computed display name
    [Compute(nameof(_compute_display_name))]
    [Depends("Name", "Email")]
    public partial string DisplayName { get; private set; }

    private void _compute_display_name()
        => DisplayName = Email != null ? $"{Name} <{Email}>" : Name;

    // Constraint: email format
    [Constrains("Email")]
    private void _check_email()
    {
        if (Email != null && !Email.Contains('@'))
            throw new ModelValidationException("Email must contain '@'.", nameof(Email));
    }
}

// Extension: add Phone in a separate shard
[Inherit("res.partner")]
public partial class ResPartner
{
    [ModelField(String = "Phone")]
    public string? Phone { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// product.template  — product with Many2one to res.partner (vendor)
// ════════════════════════════════════════════════════════════════════════════

[Model("product.template", Description = "Product")]
public partial class ProductTemplate
{
    [ModelField(String = "Name", Required = true)]
    public string Name { get; set; } = string.Empty;

    [ModelField(String = "List Price")]
    public decimal ListPrice { get; set; }

    // Many2one → res.partner  (vendor)
    [Many2one("res.partner", String = "Vendor", OnDelete = OnDeleteAction.SetNull)]
    public partial ResPartner? Vendor { get; set; }
    // Generator adds: public int? VendorId { get; set; }

    // Many2many → res.tag
    [Many2many("res.tag", String = "Tags")]
    public partial ICollection<ResTag> Tags { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// sale.order  — order header with Many2one to partner + One2many lines
// ════════════════════════════════════════════════════════════════════════════

[Model("sale.order", Description = "Sales Order")]
public partial class SaleOrder
{
    [ModelField(String = "Reference", Required = true)]
    public string Ref { get; set; } = string.Empty;

    // Many2one → res.partner (customer)
    [Many2one("res.partner", String = "Customer", Required = true, OnDelete = OnDeleteAction.Restrict)]
    public partial ResPartner? Partner { get; set; }
    // Generator adds: public int PartnerId { get; set; }

    // One2many → sale.order.line
    [One2many("sale.order.line", "OrderId", String = "Order Lines")]
    public partial ICollection<SaleOrderLine> Lines { get; set; }

    // Computed totals
    [Compute(nameof(_compute_total), Store = true)]
    [Depends("Lines")]
    public partial decimal Total { get; private set; }

    private void _compute_total()
        => Total = Lines.Sum(l => l.Subtotal);

    // Onchange: when partner changes, populate reference
    [Onchange("Partner")]
    private void _onchange_partner()
    {
        if (Partner != null && string.IsNullOrEmpty(Ref))
            Ref = $"SO/{Partner.Name.Replace(" ", "").ToUpper()}/001";
    }

    [Constrains("Ref")]
    private void _check_ref()
    {
        if (string.IsNullOrWhiteSpace(Ref))
            throw new ModelValidationException("Reference cannot be empty.", nameof(Ref));
    }
}

// ════════════════════════════════════════════════════════════════════════════
// sale.order.line  — order lines with Many2one back to order + product
// ════════════════════════════════════════════════════════════════════════════

[Model("sale.order.line", Description = "Sales Order Line")]
public partial class SaleOrderLine
{
    // Many2one → sale.order (parent)
    [Many2one("sale.order", String = "Order", Required = true, OnDelete = OnDeleteAction.Cascade)]
    public partial SaleOrder? Order { get; set; }
    // Generator adds: public int OrderId { get; set; }

    // Many2one → product.template
    [Many2one("product.template", String = "Product", Required = true, OnDelete = OnDeleteAction.Restrict)]
    public partial ProductTemplate? Product { get; set; }
    // Generator adds: public int ProductId { get; set; }

    [ModelField(String = "Quantity")]
    public int Quantity { get; set; } = 1;

    [ModelField(String = "Unit Price")]
    public decimal UnitPrice { get; set; }

    [ModelField(String = "Discount (%)")]
    public decimal Discount { get; set; }

    // Computed subtotal (non-stored)
    [Compute(nameof(_compute_subtotal))]
    [Depends("UnitPrice", "Quantity", "Discount")]
    public partial decimal Subtotal { get; private set; }

    private void _compute_subtotal()
        => Subtotal = UnitPrice * Quantity * (1 - Discount / 100m);

    // Onchange: auto-fill price from product
    [Onchange("Product")]
    private void _onchange_product()
    {
        if (Product != null)
            UnitPrice = Product.ListPrice;
    }

    [Constrains("Quantity", "UnitPrice")]
    private void _check_values()
    {
        if (Quantity <= 0)
            throw new ModelValidationException("Quantity must be positive.", nameof(Quantity));
        if (UnitPrice < 0)
            throw new ModelValidationException("Unit price cannot be negative.", nameof(UnitPrice));
    }
}
