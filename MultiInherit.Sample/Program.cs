using MultiInherit;
using MultiInherit.Sample.Models;

Console.WriteLine("═══ MultiInherit — Full demo ═══");
Console.WriteLine();

// ── 1. Registry ───────────────────────────────────────────────────────────
Console.WriteLine("Registered models:");
foreach (var meta in ModelRegistry.All().OrderBy(m => m.Name))
{
    var parents = meta.Inherits.Count > 0 ? $" ← {string.Join(", ", meta.Inherits)}" : "";
    Console.WriteLine($"  • {meta.Name,-25} [{meta.ClrType.Name}]{parents}");
}
Console.WriteLine();

// ── 2. Many2one + Onchange ────────────────────────────────────────────────
var partner = new ResPartner { Name = "Acme Corp", Email = "contact@acme.com" };

var product = new ProductTemplate
{
    Name      = "Widget Pro",
    ListPrice = 49.99m,
    Vendor    = partner   // Many2one set: VendorId auto-synced
};
Console.WriteLine($"[Many2one]   Product '{product.Name}', VendorId = {product.VendorId}");
Console.WriteLine($"             Vendor.Name = {product.Vendor?.Name}");
Console.WriteLine();

// ── 3. One2many + auto-Ref onchange ──────────────────────────────────────
var order = new SaleOrder { Ref = "" };
order.Partner = partner;  // triggers [Onchange("Partner")] → sets Ref
Console.WriteLine($"[Onchange]   After setting Partner, Ref = '{order.Ref}'");
Console.WriteLine();

// ── 4. Computed subtotal + total ──────────────────────────────────────────
var line1 = new SaleOrderLine
{
    Order     = order,
    Product   = product,
    Quantity  = 3,
    UnitPrice = 49.99m,
    Discount  = 10m
};
// TriggerOnchange shows product auto-filled price:
line1.TriggerOnchange(["Product"]);
Console.WriteLine($"[Computed]   Line1: qty={line1.Quantity} × {line1.UnitPrice:C} - {line1.Discount}% = {line1.Subtotal:C}");

var line2 = new SaleOrderLine { Order = order, Quantity = 1, UnitPrice = 199m };
order.Lines.Add(line1);
order.Lines.Add(line2);
order.TriggerOnchange(["Lines"]);   // recalculate total
Console.WriteLine($"             Order total (stored computed) = {order.Total:C}");
Console.WriteLine();

// ── 5. Many2many tags ─────────────────────────────────────────────────────
var vipTag    = new ResTag { Name = "VIP",    Color = "gold" };
var techTag   = new ResTag { Name = "Tech",   Color = "blue" };
partner.Tags.Add(vipTag);
partner.Tags.Add(techTag);
Console.WriteLine($"[Many2many]  Partner tags: {string.Join(", ", partner.Tags.Select(t => t.Name))}");
Console.WriteLine();

// ── 6. Constraint validation ──────────────────────────────────────────────
Console.WriteLine("[Constrains] Testing validation:");
try
{
    partner.Email = "not-an-email";
    partner.ValidateConstraints(["Email"]);
}
catch (ModelValidationException ex)
{
    Console.WriteLine($"  ✓ Caught: {ex.Message} (field: {ex.FieldName})");
}
partner.Email = "contact@acme.com";   // restore valid value

try
{
    var badLine = new SaleOrderLine { Quantity = -1, UnitPrice = 10m };
    badLine.ValidateConstraints();
}
catch (ModelValidationException ex)
{
    Console.WriteLine($"  ✓ Caught: {ex.Message} (field: {ex.FieldName})");
}
Console.WriteLine();

// ── 7. SQL constraints catalog ────────────────────────────────────────────
Console.WriteLine("[SQL constraints] ResPartner:");
foreach (var (name, sql, msg) in ResPartner.SqlConstraints)
    Console.WriteLine($"  • {name}: {sql}  → \"{msg}\"");
Console.WriteLine();

// ── 8. Field catalog ──────────────────────────────────────────────────────
Console.WriteLine("[Field catalog] SaleOrderLine.Fields:");
Console.WriteLine($"  UnitPrice : computed={SaleOrderLine.Fields.UnitPrice.IsComputed}");
Console.WriteLine($"  Subtotal  : computed={SaleOrderLine.Fields.Subtotal.IsComputed}, stored={SaleOrderLine.Fields.Subtotal.IsStored}");
Console.WriteLine($"  Order     : computed={SaleOrderLine.Fields.Order.IsComputed}");
Console.WriteLine();

// ── 9. Dynamic instantiation ──────────────────────────────────────────────
var dyn = ModelRegistry.CreateInstance<ResPartner>("res.partner");
dyn.Name = "Dynamic Corp";
Console.WriteLine($"[Registry]   Dynamic instance: {dyn.DisplayName}");
