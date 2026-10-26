namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Newtonsoft.Json 的转换器与全局注册入口。
/// </summary>
/// <remarks>
/// <para>
/// 本生成器承担两件事，与 MessagePack 生成器的分工一致：
/// </para>
/// <list type="number">
/// <item>为「声明程序集没有引用 Newtonsoft.Json」的外部 Id 生成**顶层**转换器；</item>
/// <item>生成 <c>AddStronglyTypedId</c> 注册入口，把每个 Id 该用的转换器注册进去。</item>
/// </list>
/// <para>
/// 为什么需要顶层转换器（后果详见 <see cref="StronglyTypedIdInfo.RequiresExternalNewtonsoftJsonConverter"/>）：
/// Newtonsoft 的转换器嵌在 Id 内部，只能生成在声明 Id 的那个程序集里 —— 消费方补一份会撞 CS0426
/// （类型已存在）。于是声明程序集没引用 Newtonsoft.Json 时，那个 Id 在任何程序集里都没有 Newtonsoft 转换器。
/// </para>
/// <para>
/// 声明程序集里的内嵌转换器与 <c>[JsonConverter]</c> 特性一律保留：它们给「声明程序集自己就引用了
/// Newtonsoft」的情形提供零配置；且 Newtonsoft 的特性优先级高于
/// <c>JsonSerializerSettings.Converters</c>，两边同时存在也不会打架。
/// </para>
/// </remarks>
internal class NewtonsoftJsonRegistrationCodeGenerator : IIntegrationCodeGenerator
{
    internal static readonly IIntegrationCodeGenerator Instance = new NewtonsoftJsonRegistrationCodeGenerator();

    public int Order { get; } = 9;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var externalIds = discoveredIds.ExternalFor(IntegrationFlags.NewtonsoftJson)
            .Where(static idInfo => idInfo.RequiresExternalNewtonsoftJsonConverter)
            .ToList();

        GenerateExternalConverters(externalIds, context, version);

        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.NewtonsoftJson)
            .Where(static idInfo => idInfo.HasNewtonsoftJsonConverter || idInfo.RequiresExternalNewtonsoftJsonConverter)
            .ToList();

        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        // 顶层转换器只在外部 Id 之间需要消歧：内嵌版嵌在各自 Id 内部，与顶层版不同层，不会撞名。
        var ambiguousNames = GetAmbiguousNames(externalIds);

        var registrations = stronglyTypedIds
            .Select(idInfo =>
            {
                // 有内嵌版的注册内嵌版（声明程序集自己就带转换器）；没有的注册消费方补出来的顶层版，
                // 否则那个 Id 在 Newtonsoft 下会被写成对象形态，而 System.Text.Json 写的是字符串。
                var converterType = idInfo.RequiresExternalNewtonsoftJsonConverter
                    ? $"global::Len.StronglyTypedId.StronglyTypedIds.{GetConverterName(idInfo, ambiguousNames.Contains(idInfo.Name))}"
                    : $"{idInfo.FullyQualifiedName}.NewtonsoftJsonConverter";

                return $$"""
                            if (registered.Add(typeof({{converterType}})))
                            {
                                settings.Converters.Add(new {{converterType}}());
                            }
                    """;
            });

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// Employ strongly typed IDs in Newtonsoft.Json.
                /// </summary>
                /// <remarks>Idempotent: a converter that is already registered is left alone, so calling this
                /// from several modules — or twice from the same one — does not grow the converter list.</remarks>
                /// <param name="settings"><see cref="global::Newtonsoft.Json.JsonSerializerSettings"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonRegistrationCodeGenerator), version)}}
                public static void AddStronglyTypedId(this global::Newtonsoft.Json.JsonSerializerSettings settings)
                {
                    // 先收集已注册的转换器类型再逐个比对：重复调用时列表不增长。
                    var registered = new global::System.Collections.Generic.HashSet<global::System.Type>();

                    foreach (var converter in settings.Converters)
                    {
                        registered.Add(converter.GetType());
                    }

            {{string.Join("\n\n", registrations)}}
                }
            }
            """;

        context.AddSource("StronglyTypedIds.NewtonsoftJson.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }

    /// <summary>
    /// 为「声明程序集没有引用 Newtonsoft.Json」的外部 Id 生成顶层转换器。
    /// </summary>
    private static void GenerateExternalConverters(List<StronglyTypedIdInfo> externalIds, SourceProductionContext context, string version)
    {
        if (externalIds.Count == 0)
        {
            return;
        }

        var ambiguousNames = GetAmbiguousNames(externalIds);

        foreach (var idInfo in externalIds)
        {
            var converterName = GetConverterName(idInfo, ambiguousNames.Contains(idInfo.Name));

            var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// Serializes <see cref="{{idInfo.FullName}}"/> with Newtonsoft.Json.
                    /// </summary>
                    /// <remarks>
                    /// Generated in the consuming assembly because the id's own assembly did not reference
                    /// Newtonsoft.Json, so it carries no converter of its own. Without this one the id would
                    /// be written as an object (<c>{"Value":"…"}</c>) here while System.Text.Json writes it
                    /// as a plain string.
                    /// </remarks>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonRegistrationCodeGenerator), version)}}
                    internal sealed class {{converterName}} : global::Newtonsoft.Json.JsonConverter<{{idInfo.FullyQualifiedName}}?>
                    {
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonRegistrationCodeGenerator), version)}}
                        public override {{idInfo.FullyQualifiedName}}? ReadJson(
                            global::Newtonsoft.Json.JsonReader reader,
                            global::System.Type objectType,
                            {{idInfo.FullyQualifiedName}}? existingValue,
                            bool hasExistingValue,
                            global::Newtonsoft.Json.JsonSerializer serializer){{NewtonsoftJsonCodeGenerator.GetReadTail(idInfo, idInfo.FullyQualifiedName)}}

                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonRegistrationCodeGenerator), version)}}
                        public override void WriteJson(
                            global::Newtonsoft.Json.JsonWriter writer,
                            {{idInfo.FullyQualifiedName}}? value,
                            global::Newtonsoft.Json.JsonSerializer serializer){{NewtonsoftJsonCodeGenerator.GetWriteTail(idInfo)}}
                    }
                }
                """;

            context.AddSource($"{idInfo.HintName}.NewtonsoftJsonExternal.g.cs", GeneratedCode.NormalizeLineEndings(code));
        }
    }

    /// <summary>
    /// 取在多个 Id 之间重名的短名集合。
    /// </summary>
    /// <remarks>
    /// 分组键用 Name 而非 EscapedName：这里比的是「是否重名」，名字不落进生成代码；
    /// 且两者是 1:1 映射（仅保留关键字差一个 @ 前缀），分组结果本来完全相同。
    /// </remarks>
    private static ImmutableHashSet<string> GetAmbiguousNames(List<StronglyTypedIdInfo> stronglyTypedIds)
        => stronglyTypedIds
            .GroupBy(idInfo => idInfo.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 取顶层转换器的类名，消歧规则与 EF Core 转换器、Dapper 处理器一致。
    /// </summary>
    private static string GetConverterName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}NewtonsoftJsonConverter"
            : $"{idInfo.EscapedName}NewtonsoftJsonConverter";
}
