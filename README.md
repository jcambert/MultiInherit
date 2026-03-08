# MultiInherit — Odoo-style multiple inheritance for C#

A Roslyn **Source Generator** that brings Odoo's three inheritance mechanisms to C#,
with zero reflection overhead at runtime.

---

## The three inheritance modes

| Odoo | C# attribute | Effect |
|------|-------------|--------|
| `_inherit` (without `_name`) | `[Inherit("model")]` on the **same** class name | **Extension** — adds fields/methods to an existing model in-place |
| `_inherit` + `_name` | `[Model("new.model")] [Inherit("parent")]` | **Classical** — new model that copies parent fields |
| `_inherits` | `[Inherits("parent", ForeignKey = "FkId")]` | **Delegation** — FK + transparent field forwarding |

---

## Quick start

### 1. Declare a base model

```csharp
[Model("res.partner")]
public partial class ResPartner
{
    public string Name  { get; set; } = string.Empty;
    public string? Email { get; set; }
}
```

### 2. Extend it in-place (extension inheritance)

```csharp
// Different file, same model name — like a Python mixin module
[Inherit("res.partner")]
public partial class ResPartner
{
    public string? Phone { get; set; }          // new field
    public override string ToString() => Name;  // override method
}
```
The generator merges both partial class shards. No base class needed.

### 3. Classical inheritance (new model, copied fields)

```csharp
[Model("res.employee")]
[Inherit("res.partner")]          // copies Name, Email, Phone
public partial class ResEmployee
{
    public string? JobTitle { get; set; }
}
```
The generator emits `Name`, `Email`, `Phone` as owned properties on `ResEmployee`.

### 4. Delegation inheritance (FK + transparent access)

```csharp
[Model("hr.employee")]
[Inherits("res.partner", ForeignKey = "PartnerId")]
public partial class HrEmployee
{
    public string? Department { get; set; }
    // Generated:
    //   public int PartnerId { get; set; }
    //   public ResPartner Partner { get; set; }
    //   public string Name  { get => Partner.Name;  set => Partner.Name  = value; }
    //   public string? Email { get => Partner.Email; set => Partner.Email = value; }
}
```

### 5. Multiple parents

```csharp
[Model("sale.order.line")]
[Inherit("res.partner")]
[Inherit("res.employee")]   // fields from both are merged
public partial class SaleOrderLine
{
    public decimal PriceUnit { get; set; }
}
```

---

## Attributes reference

### `[Model(name)]`
Declares a new model.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `name` *(ctor)* | `string` | — | Technical name, e.g. `"res.partner"` |
| `Description` | `string?` | `null` | Human-readable label |

### `[Inherit(parentModelName)]`
Repeatable. Marks classical **or** extension inheritance depending on context.

| Property | Type | Description |
|----------|------|-------------|
| `parentModelName` *(ctor)* | `string` | Technical name of the parent model |

Rules:
- If the class has **no** `[Model]` → extension in-place.
- If `[Model]` name **equals** parent name → extension in-place.
- If `[Model]` name **differs** from parent name → classical copy.

### `[Inherits(parentModelName)]`
Repeatable. Delegation inheritance.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `parentModelName` *(ctor)* | `string` | — | Technical name of the parent model |
| `ForeignKey` | `string?` | `{ParentClass}Id` | Name of the generated FK property |

### `[ModelField]`
Optional metadata on a property (equivalent to Odoo field kwargs).

| Property | Type | Default |
|----------|------|---------|
| `String` | `string?` | `null` — field label |
| `Required` | `bool` | `false` |
| `Readonly` | `bool` | `false` |
| `Help` | `string?` | `null` |
| `Default` | `string?` | `null` |

---

## Runtime registry

Every model is automatically registered at startup via `[ModuleInitializer]`:

```csharp
// Enumerate all models
foreach (var meta in ModelRegistry.All())
    Console.WriteLine($"{meta.Name} → {meta.ClrType.Name}");

// Look up by technical name
ModelMeta? meta = ModelRegistry.Get("res.partner");

// Look up by CLR type
ModelMeta? meta = ModelRegistry.Get<ResPartner>();

// Dynamic instantiation
ResPartner p = ModelRegistry.CreateInstance<ResPartner>("res.partner");
```

---

## Project structure

```
MultiInherit/
├── MultiInherit.Core/          # Attributes + IModel + ModelRegistry
│   ├── Attributes.cs           # [Model], [Inherit], [Inherits], [ModelField]
│   ├── IModel.cs               # IModel interface + ModelMeta record
│   └── ModelRegistry.cs        # Thread-safe runtime registry
│
├── MultiInherit.Generator/     # Roslyn IIncrementalGenerator
│   ├── ModelGenerator.cs       # Entry point (IIncrementalGenerator)
│   ├── ModelParser.cs          # Semantic extraction from INamedTypeSymbol
│   ├── ModelDeclaration.cs     # Raw data model (pre-resolution)
│   ├── ModelResolver.cs        # Cross-model graph resolution
│   ├── ResolvedModel.cs        # Resolved data model (post-resolution)
│   └── CodeEmitter.cs          # C# source emitter
│
└── MultiInherit.Sample/        # Usage examples
    ├── Models.cs               # All three inheritance modes
    └── Program.cs              # Runtime demo
```

---

## Requirements

- .NET 10 SDK
- C# 14 (`LangVersion` in consuming projects)
- Generator itself targets `netstandard2.0` (Roslyn constraint)

---

## Limitations & roadmap

- [ ] Computed fields (`@property` equivalent)
- [ ] Many2one / One2many / Many2many relational fields
- [ ] `onchange` / `constrains` hooks
- [ ] ORM integration (EF Core provider)
- [ ] Circular inheritance detection with clear diagnostics

---

## Computed fields

Equivalent to Odoo's `compute=` and `@api.depends`.

```csharp
[Model("sale.order")]
public partial class SaleOrder
{
    public decimal UnitPrice { get; set; }
    public int     Quantity  { get; set; }
    public decimal Discount  { get; set; }

    // Non-stored: recomputed lazily when a dependency changes
    [Compute(nameof(_compute_subtotal), Depends = "UnitPrice,Quantity")]
    public decimal Subtotal { get; private set; }

    // Stored: persisted in DB, recomputed when dependencies change
    [Compute(nameof(_compute_total), Store = true)]
    [Depends("Subtotal", "Discount")]
    public decimal Total { get; private set; }

    private void _compute_subtotal() => Subtotal = UnitPrice * Quantity;
    private void _compute_total()    => Total = Subtotal * (1 - Discount / 100m);
}
```

**What the generator produces:**
- Backing field + dirty flag for non-stored fields
- Lazy evaluation: recomputes only when dirty
- `__InvalidateDependents(changedField)` dispatcher called on every setter
- `INotifyPropertyChanged` events fired on change
- `SaleOrder.Fields.Subtotal` catalog entry with `IsComputed = true, IsStored = false`

**Build-time diagnostics:**
- `MI0004` — error if the compute method does not exist on the class
- `MI0005` — error if a computed property has a `public` setter

---

## Build-time diagnostics

| Code | Severity | Description |
|------|----------|-------------|
| `MI0001` | Error | Parent model referenced in `[Inherit]`/`[Inherits]` not found |
| `MI0002` | Error | Circular inheritance detected (DFS cycle detection) |
| `MI0003` | Error | Model class is not `partial` |
| `MI0004` | Error | `[Compute("method")]` references a non-existent method |
| `MI0005` | Error | Computed property has a `public` setter |
| `MI0006` | Error | Generated FK name collides with an existing property |
| `MI0101` | Warning | Field name conflict between two classical parents |
| `MI0102` | Warning | Model declared in global namespace |

---

## EF Core integration (`MultiInherit.EFCore`)

```csharp
// AppDbContext.cs
public class AppDbContext : ModelDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    // DbSets are registered automatically from ModelRegistry
}

// Registration
services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=app.db"));
```

**What `ModelDbContext` does automatically:**
- Maps every `[Model]` class to a table named `model_name` (dots → underscores)
- Adds a shadow `Id` primary key if none is declared
- Configures FK relationships for `[Inherits]` delegation parents
- Ignores non-stored computed fields (`Store = false`)

**Odoo-like query API:**
```csharp
// env['res.partner'].search([('name', 'like', 'Acme')])
var partners = ctx.Search<ResPartner>("res.partner", p => p.Name.Contains("Acme"));

// env['res.partner'].browse(42)
var partner = await ctx.Browse<ResPartner>("res.partner", 42);

// env['res.partner']  — returns IQueryable<ResPartner>
var query = ctx.Model<ResPartner>("res.partner");
```
