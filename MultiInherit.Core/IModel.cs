namespace MultiInherit;

/// <summary>
/// Marker interface implemented by all generated models.
/// Equivalent to Odoo's BaseModel.
/// </summary>
public interface IModel
{
    /// <summary>Technical name of the model, e.g. "res.partner"</summary>
    static abstract string ModelName { get; }

    /// <summary>Primary key. Generated models inherit this from the generated partial class.</summary>
    int Id { get; set; }
}

/// <summary>
/// Runtime metadata attached to every model instance.
/// </summary>
public sealed record ModelMeta(
    string Name,
    Type ClrType,
    IReadOnlyList<string> Inherits,
    IReadOnlyList<string>? DelegationInherits = null,
    /// <summary>
    /// Properties on this model that are delegated to a parent model via [Inherits].
    /// These properties forward reads/writes to the parent record and must NOT be
    /// mapped as columns in this model's own table.
    /// </summary>
    IReadOnlyList<string>? DelegatedPropertyNames = null);
