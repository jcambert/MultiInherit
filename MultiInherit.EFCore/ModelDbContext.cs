using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Reflection;

namespace MultiInherit.EFCore;

/// <summary>
/// Base DbContext that automatically maps every registered model
/// and configures relations declared via [Many2one], [One2many], [Many2many].
///
/// Delegation inheritance ([Inherits]) is also handled:
/// <list type="bullet">
/// <item>Delegated properties (forwarded from parent) are ignored in EF Core mapping.</item>
/// <item>The delegation FK is configured as required with CASCADE on delete.</item>
/// <item>Deleting a delegating entity automatically deletes its delegated parent records.</item>
/// </list>
/// </summary>
public abstract class ModelDbContext(DbContextOptions options) : DbContext(options)
{
    ///<inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        AutoMapModels(builder);
        ConfigureDelegationInheritance(builder);
        ConfigureRelations(builder);
        ConfigureSqlConstraints(builder);
    }

    // ── Cascade-delete delegated parents when a delegating entity is removed ─

    /// <summary>
    /// Saves all changes made in this context to the underlying database.
    /// </summary>
    /// <remarks>This override ensures that any parent entities delegated to a removed entity are also deleted
    /// as part of the save operation. Call this method to persist changes, including cascading deletions, to the
    /// database.</remarks>
    /// <param name="acceptAllChangesOnSuccess">true to accept all changes in the context after the changes have been successfully saved to the database; false
    /// to retain the changes so that SaveChanges can be called again in the event of a failure.</param>
    /// <returns>The number of state entries written to the database. This can be zero if no entities were added, modified, or
    /// deleted.</returns>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        CascadeDeleteDelegatedParents();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// Asynchronously saves all changes made in this context to the underlying database, applying any necessary
    /// cascading deletes before persisting data.
    /// </summary>
    /// <remarks>This method performs cascading deletes for delegated parent entities prior to saving changes.
    /// It overrides the base implementation to ensure referential integrity is maintained when deleting related
    /// entities.</remarks>
    /// <param name="acceptAllChangesOnSuccess">true to automatically accept all changes in the context after a successful save operation; otherwise, false.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to request cancellation of the asynchronous save operation.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries
    /// written to the database.</returns>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await CascadeDeleteDelegatedParentsAsync();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void CascadeDeleteDelegatedParents()
        => MarkDelegatedParentsForDeletion();

    private Task CascadeDeleteDelegatedParentsAsync()
    {
        MarkDelegatedParentsForDeletion();
        return Task.CompletedTask;
    }

    /// <summary>
    /// For every entity marked as Deleted that has delegation parents ([Inherits]),
    /// also marks the delegated parent record for deletion.
    ///
    /// Strategy: if the navigation property is already loaded, use it directly.
    /// Otherwise, create a stub entity from the FK value and mark it for deletion
    /// (EF Core supports deleting by PK-only stub without loading the entity).
    /// </summary>
    private void MarkDelegatedParentsForDeletion()
    {
        var deleted = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in deleted)
        {
            var clrType = entry.Entity.GetType();
            var meta = MultiInherit.ModelRegistry.All().FirstOrDefault(m => m.ClrType == clrType);
            if (meta?.DelegationInherits == null || meta.DelegationInherits.Count == 0) continue;

            foreach (var parentName in meta.DelegationInherits)
            {
                var parentMeta = MultiInherit.ModelRegistry.Get(parentName);
                if (parentMeta == null) continue;

                var navProp = FindNavigationProperty(clrType, parentMeta.ClrType);
                if (navProp == null) continue;

                // Use already-loaded navigation if available
                var parentObj = navProp.GetValue(entry.Entity);
                if (parentObj != null)
                {
                    Entry(parentObj).State = EntityState.Deleted;
                    continue;
                }

                // Navigation not loaded: resolve via FK and create a stub for deletion.
                // EF Core can delete a stub entity if its PK is set (no SELECT needed).
                var fkProp = FindPropertyByName(clrType, navProp.Name + "Id");
                if (fkProp == null) continue;
                var fkValue = fkProp.GetValue(entry.Entity);
                if (fkValue is not int fkId || fkId == 0) continue;

                // Check the change tracker first to avoid double-marking
                var tracked = ChangeTracker.Entries()
                    .FirstOrDefault(e => e.Entity.GetType() == parentMeta.ClrType
                                      && e.Entity is IModel m && m.Id == fkId);
                if (tracked != null)
                {
                    tracked.State = EntityState.Deleted;
                }
                else
                {
                    // Attach a minimal stub and mark it for deletion
                    var stub = Activator.CreateInstance(parentMeta.ClrType);
                    if (stub is IModel model) model.Id = fkId;
                    Entry(stub!).State = EntityState.Deleted;
                }
            }
        }
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
            // Delegated properties live in the parent's table — do NOT create columns here.
            foreach (var propName in meta.DelegatedPropertyNames ?? [])
                entity.Ignore(propName);
        }
    }

    // ── Delegation inheritance FKs ────────────────────────────────────────

    private static void ConfigureDelegationInheritance(ModelBuilder builder)
    {
        foreach (var meta in MultiInherit.ModelRegistry.All())
        {
            // Use DelegationInherits (not Inherits) to target only [Inherits] parents,
            // not classical [Inherit] parents which have no FK on this model.
            var delegationParents = meta.DelegationInherits ?? [];
            foreach (var parentName in delegationParents)
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
                        .IsRequired(true)
                        // CASCADE: if the parent record is deleted directly,
                        // cascade to the dependent (child) record.
                        // Application-level reverse cascade (child→parent) is
                        // handled in SaveChanges/SaveChangesAsync.
                        .OnDelete(DeleteBehavior.Cascade);
                }
                catch (InvalidOperationException) { /* complex topology — configure manually */ }
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

                    var fkName = m2o.ForeignKey ?? prop.Name + "Id";
                    var onDelete = m2o.OnDelete switch
                    {
                        MultiInherit.OnDeleteAction.Cascade => DeleteBehavior.Cascade,
                        MultiInherit.OnDeleteAction.Restrict => DeleteBehavior.Restrict,
                        _ => DeleteBehavior.SetNull
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
                    catch (InvalidOperationException) { /* déjà configuré par l'autre côté */ }
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
                    var table = m2m.RelationTable ?? $"{parts[0]}_{parts[1]}_rel";
                    var col1 = m2m.Column1 ?? meta.Name.Replace('.', '_') + "_id";
                    var col2 = m2m.Column2 ?? m2m.ComodelName.Replace('.', '_') + "_id";

                    // Find inverse nav on comodel (if declared)
                    var inverseProp = comodelMeta.ClrType
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p =>
                            p.GetCustomAttribute<MultiInherit.Many2manyAttribute>()?.ComodelName == meta.Name);

                    try
                    {
                        var left = builder.Entity(clrType);
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
                    catch (InvalidOperationException) { /* déjà configuré par l'autre côté du M2M */ }
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
                    var end = trimmed.LastIndexOf(')');
                    if (start >= 0 && end > start)
                    {
                        // Résolution insensible à la casse : mappe les noms issus du SQL
                        // vers les noms de propriétés CLR exacts (HasIndex est case-sensitive).
                        var clrProps = meta.ClrType
                            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

                        var cols = trimmed.Substring(start + 1, end - start - 1)
                            .Split(',')
                            .Select(c => c.Trim())
                            .Select(c => clrProps.TryGetValue(c, out var p) ? p.Name : c)
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
