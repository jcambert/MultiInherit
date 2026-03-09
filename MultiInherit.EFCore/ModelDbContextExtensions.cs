using Microsoft.EntityFrameworkCore;

namespace MultiInherit.EFCore;

/// <summary>
/// Extension methods to work with models in a <see cref="ModelDbContext"/>
/// using their technical names, mirroring the Odoo env['model.name'] syntax.
/// </summary>
public static class ModelDbContextExtensions
{
    /// <summary>
    /// Returns an <see cref="IQueryable{T}"/> for a model by its technical name.
    /// Equivalent to <c>env['res.partner']</c> in Odoo.
    /// <example>
    /// var partners = ctx.Model&lt;ResPartner&gt;("res.partner");
    /// </example>
    /// </summary>
    public static IQueryable<T> Model<T>(this DbContext ctx, string modelName)
        where T : class
    {
        var meta = MultiInherit.ModelRegistry.Get(modelName)
            ?? throw new InvalidOperationException($"Model '{modelName}' is not registered.");

        if (meta.ClrType != typeof(T))
            throw new InvalidOperationException(
                $"Model '{modelName}' maps to '{meta.ClrType.Name}', not '{typeof(T).Name}'.");

        return ctx.Set<T>();
    }

    /// <summary>
    /// Returns an untyped <see cref="IQueryable"/> by technical model name.
    /// Useful for dynamic/meta-programming scenarios.
    /// </summary>
    public static IQueryable ModelDynamic(this DbContext ctx, string modelName)
    {
        var meta = MultiInherit.ModelRegistry.Get(modelName)
            ?? throw new InvalidOperationException($"Model '{modelName}' is not registered.");

        // Use EF Core's non-generic Set<T> equivalent via reflection
        var method = typeof(DbContext)
            .GetMethod(nameof(DbContext.Set), 1, Type.EmptyTypes)!
            .MakeGenericMethod(meta.ClrType);

        return (IQueryable)method.Invoke(ctx, null)!;
    }

    /// <summary>
    /// Searches records of a model by technical name using a predicate.
    /// Equivalent to <c>env['res.partner'].search([...])</c> in Odoo.
    /// </summary>
    public static IQueryable<T> Search<T>(
        this DbContext ctx,
        string modelName,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate)
        where T : class
        => ctx.Model<T>(modelName).Where(predicate);

    /// <summary>
    /// Browses a single record by id.
    /// Equivalent to <c>env['res.partner'].browse(id)</c> in Odoo.
    /// </summary>
    public static ValueTask<T?> Browse<T>(
        this DbContext ctx,
        string modelName,
        int id,
        CancellationToken ct = default)
        where T : class
        => ctx.Set<T>().FindAsync(keyValues: new object[] { id }, cancellationToken: ct);
}
