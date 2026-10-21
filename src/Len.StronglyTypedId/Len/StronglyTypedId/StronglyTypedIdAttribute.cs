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
    /// Only the validator rejection in <c>Create</c> uses this type. <c>Parse</c> throws
    /// <see cref="FormatException"/> instead, following the <see cref="IParsable{TSelf}"/> convention:
    /// its failure covers text that is not a valid id at all, which is a malformed-input error rather
    /// than a domain-rule violation.
    /// </remarks>
    public Type? ExceptionType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a validator rejection leaves the rejected value out of the
    /// exception message. The default is <see langword="false"/>, which keeps the value in the message.
    /// </summary>
    /// <remarks>
    /// The rejected text travels with the exception message into logs, APM traces and error reports. For an
    /// id wrapping <see cref="string"/> that text is user input — an e-mail address, a phone number, a token
    /// fragment — often rejected precisely because it is malformed, but still personal data. Turning this on
    /// yields <c>The value is not a valid OrderId.</c> instead of
    /// <c>The value '...' is not a valid OrderId.</c>.
    /// </remarks>
    public bool RedactValidationMessage { get; set; }

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
