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

    /// <summary>
    /// Gets or sets a value indicating whether a <see cref="System.ComponentModel.TypeConverter"/> is generated
    /// for the strongly typed id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is off by default, and it is <em>not</em> turned on automatically when an integration is referenced.
    /// <see cref="System.ComponentModel.TypeConverterAttribute"/> changes the global semantics of the type:
    /// <see cref="System.ComponentModel.TypeDescriptor"/> is the landing point of <c>XmlSerializer</c>,
    /// <c>IConfiguration</c> binding, XAML and the WinForms/WPF design-time surfaces, and a type may already
    /// carry its own <c>[TypeConverter]</c> — generating one on top of that would collide.
    /// </para>
    /// <para>
    /// Turning it on also closes the read half of Newtonsoft.Json dictionary keys: restoring a key from its text
    /// goes through <c>TypeDescriptor</c> rather than <c>JsonConverter.ReadJson</c>, so a custom
    /// <c>JsonConverter</c> cannot take over that path.
    /// </para>
    /// <code>
    /// [StronglyTypedId(TypeConverter = true)]
    /// public partial record struct OrderId(Guid Value);
    /// </code>
    /// </remarks>
    public bool TypeConverter { get; set; }

    /// <summary>
    /// Gets or sets the shape the strongly typed id takes in JSON. The default is
    /// <see cref="Len.StronglyTypedId.JsonShape.Primitive"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Len.StronglyTypedId.JsonShape.Primitive"/> keeps the id interchangeable with its primitive
    /// value in the payload (<c>"3f2c…"</c>), so introducing an id does not change the wire contract.
    /// <see cref="Len.StronglyTypedId.JsonShape.Object"/> writes <c>{ "value": … }</c> instead, which is what
    /// an existing contract that already promised the object form needs.
    /// </para>
    /// <para>
    /// The setting applies to both the <c>System.Text.Json</c> and <c>Newtonsoft.Json</c> converters. It does
    /// <em>not</em> affect dictionary keys: a JSON property name is inherently a string, so keys always carry
    /// the primitive text regardless of this setting.
    /// </para>
    /// <code>
    /// [StronglyTypedId(JsonShape = JsonShape.Object)]
    /// public partial record struct OrderId(Guid Value);
    /// </code>
    /// </remarks>
    public JsonShape JsonShape { get; set; }

    /// <summary>
    /// Gets or sets a literal prefix carried by the canonical text form of the strongly typed id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The prefix is prepended by <c>ToString()</c> and stripped by <c>Parse</c> / <c>TryParse</c>, so the
    /// canonical text form becomes <c>"ord_3f2c…"</c> rather than <c>"3f2c…"</c>. Every text-based integration —
    /// <c>System.Text.Json</c>, <c>Newtonsoft.Json</c>, MVC model binding, route constraints and the generated
    /// <c>TypeConverter</c> — routes through those two members, so all of them carry the prefix as well.
    /// </para>
    /// <para>
    /// It does <em>not</em> affect the Entity Framework Core value converter or the Dapper type handler: those
    /// exchange the <em>primitive value</em> with the database, not its text, so a prefixed id keeps storing the
    /// bare primitive and existing columns are untouched.
    /// </para>
    /// <para>
    /// How the prefix is read back is governed by <see cref="PrefixReadMode"/>: by default reading is lenient
    /// (<c>TryParse</c> accepts both the prefixed and the bare form), so existing payloads and stored text keep
    /// round-tripping while new text is always written with the prefix. Set
    /// <see cref="Len.StronglyTypedId.PrefixReadMode.Strict"/> to require the prefix instead.
    /// </para>
    /// <code>
    /// [StronglyTypedId(Prefix = "ord_")]
    /// public partial record struct OrderId(Guid Value);
    /// </code>
    /// </remarks>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets how the canonical text form is read back when <see cref="Prefix"/> is configured. The default is
    /// <see cref="Len.StronglyTypedId.PrefixReadMode.Lenient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Len.StronglyTypedId.PrefixReadMode.Lenient"/> accepts both the prefixed and the bare form, so existing
    /// payloads and stored text keep round-tripping while new text is always written with the prefix.
    /// <see cref="Len.StronglyTypedId.PrefixReadMode.Strict"/> requires the prefix and fails on text that lacks it, which
    /// makes <c>ToString</c> and <c>Parse</c> a bijection.
    /// </para>
    /// <para>
    /// The setting has no effect when <see cref="Prefix"/> is unset.
    /// </para>
    /// <code>
    /// [StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Strict)]
    /// public partial record struct OrderId(Guid Value);
    /// </code>
    /// </remarks>
    public PrefixReadMode PrefixReadMode { get; set; }
}
