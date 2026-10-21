namespace Len.StronglyTypedId;

/// <summary>
/// Declares a named static instance of a strongly typed id.
/// </summary>
/// <remarks>
/// <para>
/// The instance is generated as <c>public static readonly TSelf Name = Create(value)</c>, so it goes through the
/// same normalizer and validator as <c>Create</c>. An instance is therefore never a privileged value: whatever
/// <c>ToString()</c> produces for it can always be read back by <c>Parse</c>.
/// </para>
/// <para>
/// The value is the <em>primitive</em> value in its invariant text form — the same text <c>Parse</c> accepts, but
/// <em>without</em> any <see cref="StronglyTypedIdAttribute.Prefix"/>: a prefix decorates the canonical text form,
/// while an instance carries a primitive value.
/// </para>
/// <para>
/// The value is validated at compile time and translated into a literal (or a constructor call) in the generated
/// code, so no parsing happens at run time. A value the validator rejects is not detected at compile time; the
/// type initializer throws instead.
/// </para>
/// <code>
/// [StronglyTypedId]
/// [StronglyTypedIdInstance("Unspecified", "-1")]
/// [StronglyTypedIdInstance("Anonymous", "0")]
/// public partial record struct UserId(int Value);
/// </code>
/// </remarks>
/// <param name="name">The name of the generated static field.</param>
/// <param name="value">The primitive value, in its invariant text form.</param>
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
