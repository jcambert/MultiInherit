namespace MultiInherit;

/// <summary>
/// Runtime descriptor for a single field on a model.
/// Accessible via the generated <c>ModelName.Fields.PropertyName</c> catalog.
/// </summary>
/// <param name="Name">Technical field name (matches property name)</param>
/// <param name="ClrType">CLR type of the field</param>
/// <param name="IsComputed">True if the field is decorated with [Compute]</param>
/// <param name="IsStored">False for non-stored computed fields (recomputed on every read)</param>
public sealed record ModelFieldInfo(
    string Name,
    Type ClrType,
    bool IsComputed,
    bool IsStored
);
