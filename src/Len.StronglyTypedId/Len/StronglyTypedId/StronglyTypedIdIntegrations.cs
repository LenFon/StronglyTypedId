namespace Len.StronglyTypedId;

/// <summary>
/// Specifies which integration code is generated for strongly typed ids.
/// </summary>
/// <remarks>
/// This is a <b>filter</b>, not a switch: code is only generated when the current compilation actually
/// references the corresponding assembly, and this enum can only narrow that down further. When the
/// parameter is not specified, <see cref="All"/> behaviour applies.
/// </remarks>
[Flags]
public enum StronglyTypedIdIntegrations
{
    /// <summary>
    /// Not specified. Means "follow the assembly-level default; if there is none, generate whatever the
    /// compilation references". This is the default value.
    /// </summary>
    /// <remarks>
    /// It only serves to tell "the parameter was not written" apart from "written, but nothing selected",
    /// which is why <see cref="None"/> has to occupy a bit of its own.
    /// </remarks>
    Default = 0,

    /// <summary>
    /// Explicitly disables all integrations. The core implementation (factory, parsing, comparison,
    /// formatting) is unaffected, and neither is an explicitly enabled
    /// <see cref="StronglyTypedIdAttribute.TypeConverter"/>.
    /// </summary>
    /// <remarks>
    /// Only a marker for "explicitly specified"; it adds nothing when combined with other flags:
    /// <c>None | Dapper</c> is equivalent to <c>Dapper</c>.
    /// </remarks>
    None = 1 << 0,

    /// <summary>
    /// The Entity Framework Core value converters plus <c>ApplyTo(ModelConfigurationBuilder)</c> /
    /// <c>ApplyTo(ModelBuilder)</c>. Requires a reference to <c>Microsoft.EntityFrameworkCore</c> 7.0.0 or
    /// later.
    /// </summary>
    EntityFrameworkCore = 1 << 1,

    /// <summary>
    /// Dapper's <c>SqlMapper.TypeHandler&lt;TId&gt;</c> plus <c>ApplyTo()</c>. Registration is process-wide
    /// and global, so calling it once is enough. Requires a reference to <c>Dapper</c> 2.0.0 or later.
    /// </summary>
    Dapper = 1 << 2,

    /// <summary>
    /// The System.Text.Json converter, the <c>[JsonConverter]</c> attribute and
    /// <c>ApplyTo(JsonSerializerOptions)</c>. Requires a reference to <c>System.Text.Json</c>.
    /// </summary>
    SystemTextJson = 1 << 3,

    /// <summary>
    /// The Newtonsoft.Json converter, the <c>[JsonConverter]</c> attribute and
    /// <c>AddStronglyTypedId(JsonSerializerSettings)</c>. Requires a reference to <c>Newtonsoft.Json</c>
    /// 13.0.0 or later.
    /// </summary>
    NewtonsoftJson = 1 << 4,

    /// <summary>
    /// Swashbuckle's <c>ApplyTo(SwaggerGenOptions)</c>. Requires a reference to
    /// <c>Swashbuckle.AspNetCore.SwaggerGen</c> 6.0.0 or later.
    /// </summary>
    Swagger = 1 << 5,

    /// <summary>
    /// The ASP.NET Core OpenAPI <c>ApplyTo(OpenApiOptions)</c>. Requires a reference to
    /// <c>Microsoft.AspNetCore.OpenApi</c> 9.0.0 or later.
    /// </summary>
    /// <remarks>
    /// 8.0.x only has <c>WithOpenApi()</c>; the schema transformer API arrived in 9.0.0, so nothing can be
    /// generated on <c>net8.0</c> — use <see cref="Swagger"/> instead.
    /// </remarks>
    AspNetCoreOpenApi = 1 << 6,

    /// <summary>
    /// ASP.NET Core MVC model binding and route constraints (<c>ApplyTo(MvcOptions)</c> /
    /// <c>ApplyTo(RouteOptions)</c>, which also back Minimal API's <c>{id:OrderId}</c> constraints).
    /// Requires a reference to <c>Microsoft.AspNetCore.Mvc</c> 2.0.0 or later (or its <c>.Core</c> /
    /// <c>.Abstractions</c> facade).
    /// </summary>
    AspNetCoreMvc = 1 << 7,

    /// <summary>
    /// The MessagePack <c>IMessagePackFormatter&lt;TId&gt;</c> plus
    /// <c>ApplyTo(MessagePackSerializerOptions)</c>. Requires a reference to <c>MessagePack</c> 2.0.0 or
    /// later.
    /// </summary>
    /// <remarks>
    /// The formatter lives in the generated <c>StronglyTypedIds</c> class rather than being attached to the
    /// id, so it is wired up through the entry point. The binary payload holds the primitive value itself,
    /// so the text prefix only decorates the text forms (<c>ToString()</c> / JSON) — except for
    /// <c>BigInteger</c>, <c>Int128</c> and <c>UInt128</c>, which are written as text, prefix included.
    /// MessagePack can encode those three natively; the text form is kept only so the payload stays
    /// compatible with what earlier versions wrote.
    /// </remarks>
    MessagePack = 1 << 8,

    /// <summary>
    /// The MongoDB BSON <c>IBsonSerializer&lt;TId&gt;</c> plus <c>RegisterBsonSerializers()</c>. Requires a
    /// reference to <c>MongoDB.Bson</c> 3.0.0 or later.
    /// </summary>
    /// <remarks>
    /// The serializer lives in the generated <c>StronglyTypedIds</c> class rather than being attached to the
    /// id, so it is registered through the entry point. As with <see cref="MessagePack"/>, the payload holds
    /// the primitive value itself, so the text prefix does not enter the binary form — except for
    /// <c>BigInteger</c>, <c>Int128</c> and <c>UInt128</c>, which are written as text because BSON has no
    /// serializer for them.
    /// </remarks>
    MongoDb = 1 << 9,

    /// <summary>
    /// Every integration. Still subject to the gate that checks whether the corresponding assembly is
    /// referenced.
    /// </summary>
    All = EntityFrameworkCore | Dapper | SystemTextJson | NewtonsoftJson | Swagger | AspNetCoreOpenApi | AspNetCoreMvc | MessagePack | MongoDb,
}
