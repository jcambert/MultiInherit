using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace MultiInherit.EFCore;

/// <summary>
/// Base DbContext that automatically maps every registered model
/// and configures relations declared via [Many2one], [One2many], [Many2many].
/// </summary>
public abstract class ModelDbContext : DbContext
{
    protected ModelDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        AutoMapModels(builder);
        ConfigureDelegationInheritance(builder);
        ConfigureRelations(builder);
        ConfigureSqlConstraints(builder);
    }

    // ── Auto-map all IModel types ─────────────────────────────────────────

    private static void AutoMapModels(ModelBuilder builder)
    {
        foreach (var meta in MultiInherit.ModelRegistry.All())
        {
            var entity = builder.Entity(meta.ClrType);
            entity.ToTable(meta.Name.Replace('.', '_'));
            EnsurePrimaryKey(entity, meta.ClrType);
            IgnoreNonStoredComputed(entity, meta.ClrType);
        }
    }

    // ── Delegation inheritance FKs ────────────────────────────────────────

    private static void ConfigureDelegationInheritance(ModelBuilder builder)
    {
        foreach (var meta in MultiInherit.ModelRegistry.All())
        {
            foreach (var parentName in meta.Inherits)
            {
                var parentMeta = MultiInherit.ModelRegistry.Get(parentName);
                if (parentMeta == null) continue;
                var navProp = FindNavigationProperty(meta.ClrType, parentMeta.ClrType);
                if (navProp == null) continue;
                var fkProp = FindPropertyByName(meta.ClrType, navProp.Name + "Id");
                if (fkProp == null) continue;
                try
                {
                    builder.Entity(meta.ClrType)
                        .HasOne(parentMeta.ClrType, navProp.Name)
                        .WithMany()
                        .HasForeignKey(fkProp.Name)
                        .OnDelete(DeleteBehavior.Restrict);
                }
                catch { /* complex topologies need manual config */ }
            }
        }
    }

    // ── Relational fields ─────────────────────────────────────────────────

    private static void ConfigureRelations(ModelBuilder builder)
    {
        foreach (var meta in MultiInherit.ModelRegistry.All())
        {
            var clrType = meta.ClrType;

            foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Many2one
                var m2o = prop.GetCustomAttribute<MultiInherit.Many2oneAttribute>();
                if (m2o != null)
                {
                    var comodelMeta = MultiInherit.ModelRegistry.Get(m2o.ComodelName);
                    if (comodelMeta == null) continue;

                    var fkName  = m2o.ForeignKey ?? prop.Name + "Id";
                    var onDelete = m2o.OnDelete switch
                    {
                        MultiInherit.OnDeleteAction.Cascade  => DeleteBehavior.Cascade,
                        MultiInherit.OnDeleteAction.Restrict => DeleteBehavior.Restrict,
                        _                                    => DeleteBehavior.SetNull
                    };

                    try
                    {
                        builder.Entity(clrType)
                            .HasOne(comodelMeta.ClrType, prop.Name)
                            .WithMany()
                            .HasForeignKey(fkName)
                            .IsRequired(m2o.Required)
                            .OnDelete(onDelete);
                    }
                    catch { }
                    continue;
                }

                // One2many — no column on this side; configure from child's Many2one
                var o2m = prop.GetCustomAttribute<MultiInherit.One2manyAttribute>();
                if (o2m != null)
                {
                    // Navigation is configured by the child's Many2one — ignore here
                    builder.Entity(clrType).Ignore(prop.Name);
                    continue;
                }

                // Many2many — explicit join table
                var m2m = prop.GetCustomAttribute<MultiInherit.Many2manyAttribute>();
                if (m2m != null)
                {
                    var comodelMeta = MultiInherit.ModelRegistry.Get(m2m.ComodelName);
                    if (comodelMeta == null) continue;

                    var parts = new[] { meta.Name, m2m.ComodelName }
                        .Select(n => n.Replace('.', '_'))
                        .OrderBy(n => n).ToArray();
                    var table   = m2m.RelationTable ?? $"{parts[0]}_{parts[1]}_rel";
                    var col1    = m2m.Column1 ?? meta.Name.Replace('.', '_') + "_id";
                    var col2    = m2m.Column2 ?? m2m.ComodelName.Replace('.', '_') + "_id";

                    // Find inverse nav on comodel (if declared)
                    var inverseProp = comodelMeta.ClrType
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p =>
                            p.GetCustomAttribute<MultiInherit.Many2manyAttribute>()?.ComodelName == meta.Name);

                    try
                    {
                        var left  = builder.Entity(clrType);
                        if (inverseProp != null)
                        {
                            left.HasMany(comodelMeta.ClrType, prop.Name)
                                .WithMany(inverseProp.Name)
                                .UsingEntity(table,
                                    l => l.HasOne(comodelMeta.ClrType).WithMany().HasForeignKey(col2),
                                    r => r.HasOne(clrType).WithMany().HasForeignKey(col1));
                        }
                        else
                        {
                            left.HasMany(comodelMeta.ClrType, prop.Name)
                                .WithMany()
                                .UsingEntity(table,
                                    l => l.HasOne(comodelMeta.ClrType).WithMany().HasForeignKey(col2),
                                    r => r.HasOne(clrType).WithMany().HasForeignKey(col1));
                        }
                    }
                    catch { }
                }
            }
        }
    }

    // ── SQL constraints ───────────────────────────────────────────────────

    private static void ConfigureSqlConstraints(ModelBuilder builder)
    {
        foreach (var meta in MultiInherit.ModelRegistry.All())
        {
            var sqlConstrField = meta.ClrType.GetField(
                "SqlConstraints",
                BindingFlags.Public | BindingFlags.Static);
            if (sqlConstrField?.GetValue(null) is not (string, string, string)[] constraints)
                continue;

            var entity = builder.Entity(meta.ClrType);
            foreach (var (name, sql, _) in constraints)
            {
                var trimmed = sql.Trim();
                if (trimmed.StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase))
                {
                    // UNIQUE(col1, col2) → index avec IsUnique
                    var start = trimmed.IndexOf('(');
                    var end   = trimmed.LastIndexOf(')');
                    if (start >= 0 && end > start)
                    {
                        var cols = trimmed.Substring(start + 1, end - start - 1)
                            .Split(',')
                            .Select(c => c.Trim())
                            .ToArray();
                        entity.HasIndex(cols).IsUnique().HasDatabaseName(name);
                    }
                }
                else
                {
                    // Prédicat booléen → CHECK constraint
                    entity.ToTable(t => t.HasCheckConstraint(name, trimmed));
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void EnsurePrimaryKey(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder b, Type clrType)
    {
        var hasPk = clrType.GetProperties()
            .Any(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)
                   || p.Name.Equals(clrType.Name + "Id", StringComparison.OrdinalIgnoreCase));
        if (!hasPk)
        {
            b.Property<int>("Id").ValueGeneratedOnAdd();
            b.HasKey("Id");
        }
    }

    private static void IgnoreNonStoredComputed(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder b, Type clrType)
    {
        foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var ca = prop.GetCustomAttribute<MultiInherit.ComputeAttribute>();
            if (ca != null && !ca.Store) b.Ignore(prop.Name);
        }
    }

    private static PropertyInfo? FindNavigationProperty(Type childType, Type parentType)
        => childType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.PropertyType == parentType);

    private static PropertyInfo? FindPropertyByName(Type type, string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
}
