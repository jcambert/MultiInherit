using System.Collections.Concurrent;

namespace MultiInherit;

/// <summary>
/// Central registry of all models discovered at startup.
/// Generated code calls Register() via module initializers.
/// </summary>
public static class ModelRegistry
{
    private static readonly ConcurrentDictionary<string, ModelMeta> _byName = new();
    private static readonly ConcurrentDictionary<Type, ModelMeta> _byType = new();

    /// <summary>Called by generated module initializers — do not call manually.</summary>
    public static void Register(ModelMeta meta)
    {
        _byName[meta.Name] = meta;
        _byType[meta.ClrType] = meta;
    }

    /// <summary>Retrieve metadata by technical model name.</summary>
    public static ModelMeta? Get(string modelName)
        => _byName.TryGetValue(modelName, out var m) ? m : null;

    /// <summary>Retrieve metadata by CLR type.</summary>
    public static ModelMeta? Get(Type clrType)
        => _byType.TryGetValue(clrType, out var m) ? m : null;

    /// <summary>Retrieve metadata by CLR type (generic helper).</summary>
    public static ModelMeta? Get<T>() where T : IModel => Get(typeof(T));

    /// <summary>All registered models.</summary>
    public static IEnumerable<ModelMeta> All() => _byName.Values;

    /// <summary>Create a new instance of a model by technical name.</summary>
    public static object CreateInstance(string modelName)
    {
        var meta = Get(modelName)
            ?? throw new InvalidOperationException($"Model '{modelName}' is not registered.");
        try
        {
            // Activator.CreateInstance lève MissingMethodException (pas null)
            // si le constructeur sans paramètre est absent.
            return Activator.CreateInstance(meta.ClrType)
                ?? throw new InvalidOperationException($"Cannot instantiate '{meta.ClrType.FullName}'.");
        }
        catch (MissingMethodException)
        {
            throw new InvalidOperationException(
                $"Model '{modelName}' (type '{meta.ClrType.FullName}') does not expose " +
                "a public parameterless constructor.");
        }
    }

    /// <summary>Create a new strongly-typed instance.</summary>
    public static T CreateInstance<T>(string modelName) where T : class
        => (T)CreateInstance(modelName);
}
