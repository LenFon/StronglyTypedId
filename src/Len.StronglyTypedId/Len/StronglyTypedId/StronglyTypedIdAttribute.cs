namespace Len.StronglyTypedId;

/// <summary>
/// Marks a partial record (or record struct) as a strongly typed id.
/// </summary>
/// <remarks>
/// <para>
/// The annotated type must declare exactly one primary constructor parameter, and that parameter
/// must be named <c>Value</c>. The source generator then emits the strongly typed id implementation,
/// the <c>IParsable&lt;TSelf&gt;</c> support and the JSON / Entity Framework Core / Swagger integrations.
/// </para>
/// <para>
/// The generator constrains the <em>shape</em> of a strongly typed id, not its <em>values</em>. Set
/// <see cref="Validator"/> to reject invalid values at the construction entry points.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public class StronglyTypedIdAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of a static validator method declared on the strongly typed id type itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The method must be a static member of the annotated type with the signature
    /// <c>static bool Validate(TPrimitiveId value)</c>, returning <see langword="true"/> when
    /// <paramref name="value"/> is acceptable:
    /// </para>
    /// <code>
    /// [StronglyTypedId(Validator = nameof(Validate))]
    /// public partial record struct OrderId(Guid Value)
    /// {
    ///     private static bool Validate(Guid value) =&gt; value != Guid.Empty;
    /// }
    /// </code>
    /// <para>
    /// The generated <c>Create</c> and <c>TryParse</c> members call the method named here, so the JSON and
    /// Entity Framework Core integrations — which all construct through <c>Create</c> — reject invalid values
    /// too. Constructing the record through its primary constructor directly is <em>not</em> intercepted:
    /// the primary constructor is declared by you, and a source generator cannot inject code into it.
    /// </para>
    /// <para>
    /// When this property is left unset, no validator code is emitted at all and the generated members behave
    /// exactly as if the property did not exist.
    /// </para>
    /// </remarks>
    public string? Validator { get; set; }
}
