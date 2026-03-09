# CLAUDE.md — MultiInherit project context

> Ce fichier est lu automatiquement par Claude Code à chaque session.
> Il décrit l'architecture, les conventions et les règles du projet.

---

## Vue d'ensemble

**MultiInherit** est une bibliothèque C# qui implémente les trois mécanismes
d'héritage d'Odoo via un **Roslyn Source Generator** (`IIncrementalGenerator`).

| Mécanisme Odoo | Attribut C# | Effet |
|---|---|---|
| `_inherit` (sans `_name`) | `[Inherit("model")]` | Extension en place — ajoute champs/méthodes |
| `_inherit` + `_name` | `[Model("new")] [Inherit("parent")]` | Héritage classique — copie les champs |
| `_inherits` | `[Inherits("parent", ForeignKey="FkId")]` | Délégation — FK + accès transparent |

---

## Structure de la solution

```
MultiInherit/
├── MultiInherit.slnx                  # Solution format slnx (.NET 9+)
├── Directory.Build.props              # Propriétés MSBuild partagées
├── Directory.Packages.props           # Central Package Manager (CPM)
│
├── MultiInherit.Core/                 # Attributs + interfaces runtime (net10.0)
│   ├── Attributes.cs                  # [Model], [Inherit], [Inherits], [ModelField]
│   ├── RelationAttributes.cs          # [Many2one], [One2many], [Many2many], OnDeleteAction
│   ├── ComputeAttribute.cs            # [Compute], [Depends]
│   ├── ConstraintAttributes.cs        # [Constrains], [SqlConstraint], [Onchange], ModelValidationException
│   ├── IModel.cs                      # interface IModel + record ModelMeta
│   ├── ModelFieldInfo.cs              # record ModelFieldInfo (catalogue statique)
│   └── ModelRegistry.cs              # Registre thread-safe, peuplé via [ModuleInitializer]
│
├── MultiInherit.Generator/            # Roslyn IIncrementalGenerator (netstandard2.0 obligatoire)
│   ├── ModelGenerator.cs              # Point d'entrée — filtre syntaxique + sémantique
│   ├── ModelParser.cs                 # Extraction des données depuis INamedTypeSymbol
│   ├── ModelDeclaration.cs            # Modèle de données brut (pré-résolution)
│   ├── ModelResolver.cs               # Résolution cross-modèles + validation du graphe
│   ├── ResolvedModel.cs               # Modèle résolu (post-résolution)
│   ├── CodeEmitter.cs                 # Émetteur de code C# (classes partielles)
│   └── Diagnostics.cs                 # Descripteurs MI0001–MI0102
│
├── MultiInherit.EFCore/               # Intégration Entity Framework Core (net10.0)
│   ├── ModelDbContext.cs              # DbContext de base — auto-mappe tous les modèles
│   └── ModelDbContextExtensions.cs   # .Model<T>(), .Search<T>(), .Browse<T>()
│
└── MultiInherit.Sample/               # Exemple d'utilisation (net10.0, LangVersion 14)
    ├── Models.cs                      # Démos des 3 héritages + relations + contraintes
    └── Program.cs                     # Programme de démonstration runtime
```

---

## Prérequis

- **.NET 10 SDK** (`dotnet --version` ≥ 10.0.0)
- **C# 14** dans les projets consommateurs (`<LangVersion>14</LangVersion>`)
- Le générateur cible **netstandard2.0** (contrainte Roslyn — ne pas changer)

---

## Commandes fréquentes

```bash
# Build complet
dotnet build MultiInherit.slnx

# Exécuter le sample
dotnet run --project MultiInherit.Sample

# Voir le code généré (fichiers .g.cs)
dotnet build MultiInherit.Sample --verbosity normal
# Les fichiers sont dans : MultiInherit.Sample/obj/Debug/net10.0/generated/

# Lancer les tests (quand le projet test sera créé)
dotnet test MultiInherit.slnx

# Ajouter un package (CPM — toujours dans Directory.Packages.props)
# 1. Ajouter <PackageVersion Include="Pkg" Version="x.y.z" /> dans Directory.Packages.props
# 2. Ajouter <PackageReference Include="Pkg" /> dans le .csproj (sans Version)
```

---

## Pipeline du générateur (ordre d'exécution)

```
Compilation Roslyn
    │
    ▼
ModelGenerator.IsCandidate()       — filtre syntaxique rapide (partial + nos attributs)
    │
    ▼
ModelGenerator.GetModelDeclaration()
    └── ModelParser.Parse()         — extraction sémantique depuis INamedTypeSymbol
                                     → ModelDeclaration (données brutes)
    │
    ▼  (tous les ModelDeclaration collectés en batch)
ModelResolver.Resolve()
    ├── DetectCircularInheritance() — DFS, émet MI0002
    ├── BuildFieldMap()             — fusionne les shards d'extension
    ├── BuildRelationMap()          — valide comodels, inverse fields
    └── → ResolvedModel[]
    │
    ▼
CodeEmitter.Emit(ResolvedModel)    — génère la classe partielle .g.cs
    ├── Infrastructure (INotifyPropertyChanged, ModelName)
    ├── Classical inherited fields
    ├── Delegation blocks (FK + navigation + forwarding)
    ├── Relations (Many2one FK+nav, One2many collection, Many2many collection)
    ├── Computed fields (dirty flag, lazy eval)
    ├── Field catalog (static class Fields { ... })
    ├── ValidateConstraints() dispatcher
    ├── TriggerOnchange() dispatcher
    └── [ModuleInitializer] → ModelRegistry.Register()
```

---

## Code généré — exemple

Pour ce modèle :
```csharp
[Model("sale.order.line")]
public partial class SaleOrderLine
{
    [Many2one("sale.order", Required = true, OnDelete = OnDeleteAction.Cascade)]
    public SaleOrder? Order { get; set; }

    public decimal UnitPrice { get; set; }

    [Compute(nameof(_compute_subtotal))]
    [Depends("UnitPrice", "Quantity")]
    public decimal Subtotal { get; private set; }

    private void _compute_subtotal() => Subtotal = UnitPrice * Quantity;

    [Constrains("UnitPrice")]
    private void _check_price()
    {
        if (UnitPrice < 0) throw new ModelValidationException("Price cannot be negative.");
    }
}
```

Le générateur produit `SaleOrderLine.g.cs` avec :
```csharp
partial class SaleOrderLine : IModel, INotifyPropertyChanged
{
    public static string ModelName => "sale.order.line";

    // Many2one FK
    public int OrderId { get; set; }
    public SaleOrder? Order { get => _order; set { _order = value; OrderId = value?.Id ?? 0; OnPropertyChanged(...); __TriggerOnchange(...); } }

    // Computed with dirty flag
    private decimal _subtotal;
    private bool _subtotal_dirty = true;
    public decimal Subtotal { get { if (_subtotal_dirty) { _compute_subtotal(); _subtotal_dirty = false; } return _subtotal; } private set { _subtotal = value; } }
    private static readonly string[] __Subtotal_depends = { "UnitPrice", "Quantity" };

    // Dispatchers
    partial void __InvalidateDependents(string changedField) { ... }
    public void ValidateConstraints(...) { _check_price(); }
    public void TriggerOnchange(...) { ... }

    [ModuleInitializer]
    internal static void __RegisterModel_sale_order_line() => ModelRegistry.Register(...);
}
```

---

## Codes de diagnostic

| Code | Sévérité | Déclencheur |
|------|----------|-------------|
| `MI0001` | Erreur | Parent introuvable dans la compilation |
| `MI0002` | Erreur | Héritage circulaire détecté |
| `MI0003` | Erreur | Classe non déclarée `partial` |
| `MI0004` | Erreur | Méthode `[Compute]` inexistante |
| `MI0005` | Erreur | Propriété computed avec `public` setter |
| `MI0006` | Erreur | Collision de nom de FK générée |
| `MI0007` | Erreur | Méthode `[Constrains]` introuvable |
| `MI0008` | Erreur | Méthode `[Onchange]` introuvable |
| `MI0009` | Erreur | Champ inverse `[One2many]` introuvable sur le comodel |
| `MI0010` | Erreur | Comodel de relation introuvable |
| `MI0101` | Warning | Conflit de nom de champ entre deux parents classiques |
| `MI0102` | Warning | Modèle déclaré dans le namespace global |

---

## Conventions de code

- **Namespaces** : `MultiInherit` (Core), `MultiInherit.Generator`, `MultiInherit.EFCore`
- **Nommage des tables EF Core** : `res.partner` → `res_partner` (points → underscores)
- **Nommage des FK générées** : `[Many2one]` sur `Partner` → `PartnerId` ; configurable via `ForeignKey=`
- **Join tables M2M** : `{model1}_{model2}_rel` (noms triés alphabétiquement)
- **Fichiers générés** : `{Namespace}.{ClassName}.g.cs` dans `obj/generated/`
- **LangVersion du générateur** : toujours **12** (netstandard2.0)
- **LangVersion des consommateurs** : **14** (net10.0)

---

## Tâches à venir (roadmap)

### Priorité haute
- [x] **Tests unitaires** — projet `MultiInherit.Tests` avec xUnit v3 (83 tests, 0 échec)
  - ✅ Tous les diagnostics MI0001–MI0012 et MI0101–MI0102 testés
  - ✅ Tests de code généré (snapshot, ClassicalInheritance, Delegation, Relations, Computed)
  - ✅ Tests d'intégration EF Core (TestContainers PostgreSQL)

### Priorité moyenne
- [x] **NuGet packaging**
  - `MultiInherit.Core` → package ordinaire avec README
  - `MultiInherit.Generator` → package analyzer (`analyzers/dotnet/cs/`), `PackageType=Analyzer`, `DevelopmentDependency=true`, release tracking (`AnalyzerReleases.Unshipped.md`)
  - `MultiInherit.EFCore` → package optionnel avec README
  - Version partagée dans `Directory.Build.props` (`<Version>0.1.0</Version>`)
  - Dépendance Core→Generator documentée dans `.nuspec` pour la publication
- [x] **Champ `[Selection]`** — `SelectionAttribute`, MI0012, validation générée dans `ValidateConstraints()`
- [x] **`[Default(nameof(GetDefault))]`** — `DefaultAttribute`, MI0013, propriété `partial` avec backing field lazy initialisé via méthode

### Priorité basse
- [x] **Égalité structurelle `ResolvedModel`** — `ResolvedModelComparer` + `.WithComparer()` dans le pipeline
- [ ] Migrations EF Core aware de la délégation (`[Inherits]`)
- [ ] Génération OpenAPI/JSON Schema depuis `ModelFieldInfo`
- [ ] Support multi-assembly (comodels dans des assemblies séparées)

---

## Pièges connus

1. **Le générateur cible netstandard2.0** — ne jamais utiliser d'API .NET 5+ dans `MultiInherit.Generator`.  
   Utiliser à la place les API Roslyn disponibles sur netstandard2.0.

2. **Propagation des diagnostics** — les diagnostics émis dans `ModelParser` (appelé depuis le `transform`)  
   sont actuellement collectés puis re-émis dans `RegisterSourceOutput`. Ce n'est pas idéal ;  
   la bonne approche est d'utiliser `IncrementalValueProvider<(ModelDeclaration?, Diagnostic[])>`.

3. **`[ModuleInitializer]`** — nécessite que l'assembly consommatrice soit compilée en tant qu'assembly  
   (pas en tant que script). Fonctionne pour tous les projets `OutputType=Exe` ou `OutputType=Library`.

4. **Classes partielles sur plusieurs fichiers** — le générateur fusionne correctement les shards  
   mais **toutes les déclarations doivent être dans le même projet**.  
   Les comodels dans des assemblies séparées ne sont pas encore résolus (voir roadmap).
