namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Newtonsoft.Json 的转换器与全局注册入口。
/// </summary>
/// <remarks>
/// 承担两件事：为「声明程序集没有引用 Newtonsoft.Json」的外部 Id 生成顶层转换器，以及生成
/// <c>AddStronglyTypedIds</c> 注册入口。
/// <para>
/// 需要顶层转换器的原因见 <see cref="StronglyTypedIdInfo.RequiresExternalNewtonsoftJsonConverter"/>：
/// 内嵌转换器只能生成在声明 Id 的程序集里，消费方补一份会撞 CS0426。
/// 声明程序集里的内嵌转换器与 <c>[JsonConverter]</c> 特性一律保留，供自身已引用 Newtonsoft 的情形零配置使用。
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
        var ambiguousNames = IntegrationNaming.GetAmbiguousNames(externalIds);

        var registrations = stronglyTypedIds
            .Select(idInfo =>
            {
                // 有内嵌版的注册内嵌版（声明程序集自己就带转换器）；没有的注册消费方补出来的顶层版，
                // 否则那个 Id 在 Newtonsoft 下会被写成对象形态，而 System.Text.Json 写的是字符串。
                var converterType = idInfo.RequiresExternalNewtonsoftJsonConverter
                    ? $"global::Len.StronglyTypedId.StronglyTypedIds.{IntegrationNaming.GetTypeName(idInfo, ambiguousNames, "NewtonsoftJsonConverter")}"
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
                /// 在 Newtonsoft.Json 中启用强类型 Id。
                /// </summary>
                /// <remarks>幂等：已注册的转换器保持原样，因此从多个模块调用，或从同一模块调用两次，都不会让转换器列表增长。</remarks>
                /// <param name="settings"><see cref="global::Newtonsoft.Json.JsonSerializerSettings"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonRegistrationCodeGenerator), version)}}
                public static void AddStronglyTypedIds(this global::Newtonsoft.Json.JsonSerializerSettings settings)
                {
                    // 幂等：先收集已注册的转换器类型，重复调用时列表不增长。
                    var registered = new global::System.Collections.Generic.HashSet<global::System.Type>();

                    foreach (var converter in settings.Converters)
                    {
                        registered.Add(converter.GetType());
                    }

            {{string.Join("\n\n", registrations)}}
                }
            }
            """;

        GeneratedCode.AddGeneratedSource(context, "StronglyTypedIds.NewtonsoftJson.g.cs", code);
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

        var ambiguousNames = IntegrationNaming.GetAmbiguousNames(externalIds);

        foreach (var idInfo in externalIds)
        {
            var converterName = IntegrationNaming.GetTypeName(idInfo, ambiguousNames, "NewtonsoftJsonConverter");

            var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// 用 Newtonsoft.Json 序列化 <see cref="{{idInfo.FullName}}"/>。
                    /// </summary>
                    /// <remarks>
                    /// 生成在消费方程序集中，因为该 Id 自身所在程序集未引用 Newtonsoft.Json，自身不带转换器。
                    /// 没有这一个，该 Id 在此会被写成对象（<c>{"Value":"…"}</c>），而 System.Text.Json 却把它写成普通字符串。
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

            GeneratedCode.AddGeneratedSource(context, $"{idInfo.HintName}.NewtonsoftJsonExternal.g.cs", code);
        }
    }
}
