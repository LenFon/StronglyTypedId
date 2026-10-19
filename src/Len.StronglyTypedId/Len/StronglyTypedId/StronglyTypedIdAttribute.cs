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
    /// Gets or sets the exception type thrown by <c>Create</c> when the validator rejects a value. The type
    /// must derive from <see cref="Exception"/>, must not be abstract, and must expose a constructor that
    /// takes a single <see cref="string"/> message. Defaults to <see cref="ArgumentException"/>.
    /// </summary>
    /// <remarks>
    /// Only the validator rejection in <c>Create</c> uses this type. <c>Parse</c> keeps throwing
    /// <see cref="ArgumentException"/>, because its failure also covers text that is not a valid id at all —
    /// a malformed-input error rather than a domain-rule violation.
    /// </remarks>
    public Type? ExceptionType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a <see cref="System.ComponentModel.TypeConverter"/> is
    /// generated for the strongly typed id.
    /// </summary>
    public bool TypeConverter { get; set; }

    /// <summary>
    /// Gets or sets which integration code is generated for the strongly typed id. The default is
    /// <see cref="StronglyTypedIdIntegrations.Default"/>, which means "generate whatever the compilation
    /// references".
    /// </summary>
    /// <remarks>
    /// This is a filter, not a switch: an integration is only generated when the compilation references its
    /// assembly <em>and</em> the corresponding flag is set. <see cref="StronglyTypedIdIntegrations.None"/>
    /// suppresses every integration but leaves the core implementation untouched, and does not affect an
    /// explicitly enabled <see cref="TypeConverter"/>.
    /// </remarks>
    public StronglyTypedIdIntegrations Integrations { get; set; }

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
