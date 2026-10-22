namespace Len.StronglyTypedId;

/// <summary>
/// Declares a named static instance of a strongly typed id.
/// </summary>
/// <remarks>
/// The instance is generated as <c>public static readonly TSelf Name = Create(value)</c>, so it passes the same normalizer and validator as <c>Create</c>.
/// The value is the primitive value in invariant text form, without any <see cref="StronglyTypedIdAttribute.Prefix"/>; it is validated at compile time and emitted as a literal, so no parsing happens at run time.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class StronglyTypedIdInstanceAttribute(string name, string value) : Attribute
{
    /// <summary>
    /// Gets the name of the generated static field.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the primitive value, in its invariant text form.
    /// </summary>
    public string Value { get; } = value;
}

