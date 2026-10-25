namespace Len.StronglyTypedId;

/// <summary>
/// Specifies how a strongly typed id is read back from prefixed text.
/// </summary>
public enum PrefixReadMode
{
    /// <summary>
    /// The prefix is optional: it is stripped when present, and the text is parsed as-is when absent.
    /// This is the default.
    /// </summary>
    Lenient = 0,

    /// <summary>
    /// The prefix is required: parsing fails when it is missing.
    /// </summary>
    Strict = 1,
}
