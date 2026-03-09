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
    //   public int       PartnerId { get; set; }
    //   public ResPartner? Partner  { get; set; }      // nullable
    //   public string  Name  { get => Partner?.Name;  set { if (Partner != null) Partner.Name  = value; } }
    //   public string? Email { get => Partner?.Email; set { if (Partner != null) Partner.Email = value; } }
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

### `[Many2one(comodel)]`
Generates a FK integer property + nullable navigation property.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `comodel` *(ctor)* | `string` | — | Technical name of the target model |
| `ForeignKey` | `string?` | `{Property}Id` | FK property name override |
| `Required` | `bool` | `false` | Nullable FK if false |
| `OnDelete` | `OnDeleteAction` | `SetNull` | `SetNull`, `Cascade`, `Restrict` |

### `[One2many(comodel, inverseField)]`
Navigation-only collection backed by the child's Many2one FK.

| Property | Type | Description |
|----------|------|-------------|
| `comodel` *(ctor)* | `string` | Technical name of the child model |
| `inverseField` *(ctor)* | `string` | Name of the Many2one property on the child |

### `[Many2many(comodel)]`
Collection with a join table.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `comodel` *(ctor)* | `string` | — | Technical name of the other model |
| `RelationTable` | `string?` | `{a}_{b}_rel` (sorted) | Join table name |
| `Column1` | `string?` | `{model}_id` | FK column for this side |
| `Column2` | `string?` | `{comodel}_id` | FK column for the other side |

### `[Compute(method)]` + `[Depends(...)]`
Declares a computed property. The property **must** be `partial`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `method` *(ctor)* | `string` | — | Name of the `void` compute method |
| `Store` | `bool` | `false` | If `true`, persisted in DB |
| `Depends` | `string?` | `null` | Comma-separated dependency names (alternative to `[Depends]`) |

```csharp
[Compute(nameof(_compute_subtotal))]
[Depends("UnitPrice", "Quantity")]
public partial decimal Subtotal { get; private set; }   // partial is required

private void _compute_subtotal() => Subtotal = UnitPrice * Quantity;
```

### `[Constrains(fields...)]`
Marks a validation method triggered before save.

```csharp
[Constrains("Price")]
private void _check_price()
{
    if (Price < 0) throw new ModelValidationException("Price cannot be negative.");
}
```

Call `model.ValidateConstraints()` (or `ValidateConstraints(changedFields)`) before saving.

### `[Onchange(fields...)]`
Marks a method triggered when specific fields change.

```csharp
[Onchange("Partner")]
private void _onchange_partner()
{
    if (Partner != null && string.IsNullOrEmpty(Ref))
        Ref = $"SO/{Partner.Name}/001";
}
```

### `[SqlConstraint(name, sql, message)]`
Declares a database-level constraint (class-level attribute).

```csharp
[SqlConstraint("unique_email", "UNIQUE(Email)", "Email must be unique.")]
[SqlConstraint("check_price", "Price >= 0", "Price must be non-negative.")]
public partial class MyModel { ... }
```

- SQL starting with `UNIQUE(...)` → EF Core `HasIndex().IsUnique()`
- Other SQL → `CHECK` constraint

### `[Selection(values...)]`
Restricts a `string` or `string?` property to a predefined set of values, equivalent to Odoo's `fields.Selection`. The generator emits a static `HashSet` and validates the value inside `ValidateConstraints()`.

```csharp
[Selection("draft", "confirmed", "done")]
public string Status { get; set; } = "draft";
```

The property must be `string` or `string?`; applying `[Selection]` to any other type emits **MI0012**.

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
    public partial decimal Subtotal { get; private set; }   // partial is required

    // Stored: persisted in DB, recomputed when dependencies change
    [Compute(nameof(_compute_total), Store = true)]
    [Depends("Subtotal", "Discount")]
    public partial decimal Total { get; private set; }

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
| `MI0007` | Error | `[Constrains]` method not found or wrong signature |
| `MI0008` | Error | `[Onchange]` method not found |
| `MI0009` | Error | `[One2many]` inverse field not found as `[Many2one]` on the comodel |
| `MI0010` | Error | Relation comodel not found in this compilation |
| `MI0011` | Error | `[Compute]` property is not declared `partial` |
| `MI0012` | Error | `[Selection]` applied to a non-`string` property |
| `MI0101` | Warning | Field name conflict between two classical parents |
| `MI0102` | Warning | Model declared in global namespace |

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

`ModelMeta` exposes:
- `Name` — technical model name
- `ClrType` — the CLR type
- `Inherits` — all parent model names (classical + delegation)
- `DelegationInherits` — delegation parent names only (`[Inherits]`)

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
- Configures FK relationships for `[Inherits]` delegation parents only (not classical)
- Configures `[Many2one]` / `[Many2many]` FK relationships
- Ignores non-stored computed fields (`Store = false`)
- Routes `UNIQUE(...)` sql constraints to `HasIndex().IsUnique()`, others to `CHECK`

**Odoo-like query API:**
```csharp
// env['res.partner'].search([('name', 'like', 'Acme')])
var partners = ctx.Search<ResPartner>("res.partner", p => p.Name.Contains("Acme"));

// env['res.partner'].browse(42)
var partner = await ctx.Browse<ResPartner>("res.partner", 42);

// env['res.partner']  — returns IQueryable<ResPartner>
var query = ctx.Model<ResPartner>("res.partner");
```

---

## Project structure

```
MultiInherit/
├── MultiInherit.Core/                 # Attributes + IModel + ModelRegistry (net10.0)
│   ├── Attributes.cs                  # [Model], [Inherit], [Inherits], [ModelField]
│   ├── RelationAttributes.cs          # [Many2one], [One2many], [Many2many]
│   ├── ComputeAttribute.cs            # [Compute], [Depends]
│   ├── ConstraintAttributes.cs        # [Constrains], [Onchange], [SqlConstraint]
│   ├── IModel.cs                      # IModel interface + ModelMeta record
│   ├── ModelFieldInfo.cs              # ModelFieldInfo (static field catalog)
│   └── ModelRegistry.cs              # Thread-safe runtime registry
│
├── MultiInherit.Generator/            # Roslyn IIncrementalGenerator (netstandard2.0)
│   ├── ModelGenerator.cs              # Entry point
│   ├── ModelParser.cs                 # Semantic extraction from INamedTypeSymbol
│   ├── ModelDeclaration.cs            # Raw data model (pre-resolution)
│   ├── ModelResolver.cs               # Cross-model graph resolution + diagnostics
│   ├── ResolvedModel.cs               # Resolved data model (post-resolution)
│   ├── CodeEmitter.cs                 # C# source emitter
│   ├── Diagnostics.cs                 # Descriptors MI0001–MI0012, MI0101–MI0102
│   ├── AnalyzerReleases.Shipped.md    # NuGet analyzer release tracking
│   └── AnalyzerReleases.Unshipped.md # Pending rules for next release
│
├── MultiInherit.EFCore/               # EF Core integration (net10.0)
│   ├── ModelDbContext.cs              # Auto-maps all models + configures relations
│   └── ModelDbContextExtensions.cs   # .Model<T>(), .Search<T>(), .Browse<T>()
│
├── MultiInherit.Sample/               # Usage examples (net10.0)
│   ├── Models.cs                      # All three inheritance modes + relations
│   └── Program.cs                     # Runtime demo
│
└── MultiInherit.Tests/                # xUnit v3 test project (net10.0)
    ├── Generator/                     # Source generator tests
    ├── Unit/                          # Unit tests (ModelRegistry, etc.)
    ├── Integration/                   # EF Core integration tests (PostgreSQL)
    └── Helpers/                       # GeneratorTestHelper
```

---

## Requirements

- .NET 10 SDK
- C# 14 (`LangVersion` in consuming projects) — required for `partial` properties
- Generator itself targets `netstandard2.0` (Roslyn constraint)

---

## Roadmap

- [x] NuGet packaging — `MultiInherit.Core`, `MultiInherit.Generator` (analyzer), `MultiInherit.EFCore`
- [x] `[Selection]` field — restricts a `string` property to a predefined set of values, validated in `ValidateConstraints()`
- [ ] `[Default(nameof(GetDefault))]` — computed default value via method
- [ ] Migrations EF Core aware de la délégation (`[Inherits]`)
- [ ] Génération OpenAPI/JSON Schema depuis `ModelFieldInfo`
- [ ] Support multi-assembly (comodels dans des assemblies séparées)
- [ ] Égalité structurelle sur `ResolvedModel` pour optimiser le cache incrémental Roslyn
