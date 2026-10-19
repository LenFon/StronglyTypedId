namespace Len.StronglyTypedId;

/// <summary>
/// Marks a partial record (or record struct) as a strongly typed id.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public class StronglyTypedIdAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of a static validator method declared on the strongly typed id type itself.
    /// </summary>
    public string? Validator { get; set; }

    /// <summary>
    /// Gets or sets the name of a static normalizer method declared on the strongly typed id type itself.
    /// </summary>
    public string? Normalizer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a <see cref="System.ComponentModel.TypeConverter"/> is
    /// generated for the strongly typed id.
    /// </summary>
    public bool TypeConverter { get; set; }

    /// <summary>
    /// Gets or sets the shape the strongly typed id takes in JSON. The default is
    /// <see cref="Len.StronglyTypedId.JsonShape.Primitive"/>.
    /// </summary>
    public JsonShape JsonShape { get; set; }

    /// <summary>
    /// Gets or sets a literal prefix carried by the canonical text form of the strongly typed id.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets how the canonical text form is read back when <see cref="Prefix"/> is configured. The
    /// default is <see cref="Len.StronglyTypedId.PrefixReadMode.Lenient"/>.
    /// </summary>
    public PrefixReadMode PrefixReadMode { get; set; }
}
