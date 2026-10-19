namespace Len.StronglyTypedId;

/// <summary>
/// Marks a partial record (or record struct) as a strongly typed id.
/// </summary>
/// <remarks>
/// The annotated type must declare exactly one primary constructor parameter, named <c>Value</c>. The generator
/// then emits the implementation, <c>IParsable&lt;TSelf&gt;</c> support and the JSON / EF Core / Swagger integrations.
/// It constrains the <em>shape</em> of an id, not its <em>values</em> — set <see cref="Validator"/> for that.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public class StronglyTypedIdAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of a static validator method declared on the strongly typed id type itself.
    /// </summary>
    /// <remarks>
    /// The method must be a static member of the annotated type with the signature
    /// <c>static bool Validate(TPrimitiveId value)</c>, returning <see langword="true"/> when the value is acceptable:
    /// <code>
    /// [StronglyTypedId(Validator = nameof(Validate))]
    /// public partial record struct OrderId(Guid Value)
    /// {
    ///     private static bool Validate(Guid value) =&gt; value != Guid.Empty;
    /// }
    /// </code>
    /// The generated <c>Create</c> and <c>TryParse</c> members call it, so the JSON and EF Core integrations reject
    /// invalid values too. The generator cannot inject code into a constructor you declared, so direct construction
    /// remains possible — the analyzer reports it as <c>STIA0011</c> instead. When unset, no validator code is
    /// emitted at all.
    /// </remarks>
    public string? Validator { get; set; }

    /// <summary>
    /// Gets or sets the name of a static normalizer method declared on the strongly typed id type itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The method must be a static member of the annotated type with the signature
    /// <c>static TPrimitiveId Normalize(TPrimitiveId value)</c>, returning the canonical form of the value:
    /// </para>
    /// <code>
    /// [StronglyTypedId(Normalizer = nameof(Normalize), Validator = nameof(Validate))]
    /// public partial record struct EmailAddress(string Value)
    /// {
    ///     private static string Normalize(string value) =&gt; value.Trim().ToLowerInvariant();
    ///
    ///     private static bool Validate(string value) =&gt; value.Contains('@');
    /// }
    /// </code>
    /// <para>
    /// The generated <c>Create</c>, <c>TryCreate</c> and every <c>TryParse</c> overload normalize the incoming
    /// value <em>before</em> validating it, so the validator always inspects — and the id always stores — the
    /// canonical value. Parsing <c>"  A@B.com  "</c> therefore yields the same id as parsing <c>"a@b.com"</c>,
    /// and <c>ToString()</c> round-trips the canonical form rather than whatever the caller happened to pass.
    /// </para>
    /// <para>
    /// Normalization is a <em>repair</em>, not a rejection: use it to trim, fold case or drop separators, and
    /// keep <see cref="Validator"/> for values that must be refused outright. Like the validator, it cannot
    /// intercept your primary constructor — the analyzer reports direct construction as <c>STIA0011</c>.
    /// When unset, no normalization code is emitted at all.
    /// </para>
    /// </remarks>
    public string? Normalizer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a <see cref="System.ComponentModel.TypeConverter"/> is generated
    /// for the strongly typed id.
    /// </summary>
    /// <remarks>
    /// Off by default and never turned on implicitly: <see cref="System.ComponentModel.TypeConverterAttribute"/>
    /// changes the global semantics of the type (<c>TypeDescriptor</c> backs <c>XmlSerializer</c>,
    /// <c>IConfiguration</c> binding, XAML and WinForms/WPF design-time), and the type may already carry its own
    /// <c>[TypeConverter]</c>. Enabling it also closes the read half of Newtonsoft.Json dictionary keys, which go
    /// through <c>TypeDescriptor</c> rather than <c>JsonConverter.ReadJson</c>.
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
    /// Applies to both the <c>System.Text.Json</c> and <c>Newtonsoft.Json</c> converters, but not to dictionary keys:
    /// a JSON property name is always a string, so keys carry the primitive text regardless.
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
    /// Has no effect when <see cref="Prefix"/> is unset.
    /// <code>
    /// [StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Strict)]
    /// public partial record struct OrderId(Guid Value);
    /// </code>
    /// </remarks>
    public PrefixReadMode PrefixReadMode { get; set; }
}
