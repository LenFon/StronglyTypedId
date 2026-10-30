namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 System.Text.Json 的全局注册入口。
/// </summary>
internal class SystemTextJsonRegistrationCodeGenerator : IIntegrationCodeGenerator
{
    internal static readonly IIntegrationCodeGenerator Instance = new SystemTextJsonRegistrationCodeGenerator();

    public int Order { get; } = 8;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.SystemTextJson)
            .Where(static idInfo => idInfo.HasSystemTextJsonConverter)
            .ToList();

        // 一个 Id 都没有时不产出空壳类：既有行为是「无集成则不生成 StronglyTypedIds」，
        // 空注册入口只会平白多出一个成员。
        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        var registrations = stronglyTypedIds
            .Select(idInfo => $$"""
                        if (registered.Add(typeof({{idInfo.FullyQualifiedName}}.SystemTextJsonConverter)))
                        {
                            options.Converters.Add(new {{idInfo.FullyQualifiedName}}.SystemTextJsonConverter());
                        }
                """);

        // 源生成的 JsonSerializerContext 对「本次编译声明」的类型走 fast-path，不取 [JsonConverter]，
        // 于是 Id 被按属性写成 {"Value":…}。resolver 让调用方用一行把 Id 的 JsonTypeInfo 插到 context 之前。
        var resolverEntries = stronglyTypedIds
            .Select(idInfo => $$"""
                        if (type == typeof({{idInfo.FullyQualifiedName}}))
                        {
                            return global::System.Text.Json.Serialization.Metadata.JsonMetadataServices.CreateValueInfo<{{idInfo.FullyQualifiedName}}>(
                                options,
                                new {{idInfo.FullyQualifiedName}}.SystemTextJsonConverter());
                        }
                """);

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// 在 System.Text.Json 中启用强类型 Id。
                /// </summary>
                /// <remarks>幂等：已注册的转换器保持原样，因此从多个模块调用，或从同一模块调用两次，都不会让转换器列表增长。</remarks>
                /// <param name="options"><see cref="global::System.Text.Json.JsonSerializerOptions"/>.</param>
                /// <returns>同一个 <paramref name="options"/>，便于链式调用。</returns>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonRegistrationCodeGenerator), version)}}
                public static global::System.Text.Json.JsonSerializerOptions AddStronglyTypedIds(this global::System.Text.Json.JsonSerializerOptions options)
                {
                    // 幂等：已注册时完全不触碰集合，因为 JsonSerializerOptions 一旦被使用就会冻结，再 Add 会抛异常。
                    var registered = new global::System.Collections.Generic.HashSet<global::System.Type>();

                    foreach (var converter in options.Converters)
                    {
                        registered.Add(converter.GetType());
                    }

            {{string.Join("\n\n", registrations)}}

                    return options;
                }

                /// <summary>
                /// 让源生成的 <c>JsonSerializerContext</c> 把强类型 Id 写成与反射式序列化相同的形态。
                /// </summary>
                /// <remarks>
                /// 源生成器对<b>本次编译声明</b>的类型走 fast-path，不读取生成器补在 partial 声明上的
                /// <c>[JsonConverter]</c>，于是 Id 被按属性写成 <c>{"Value":…}</c>，而反射式序列化写的是字符串。
                /// 把本方法的结果赋给 <c>TypeInfoResolver</c> 即可对齐：Id 的 <c>JsonTypeInfo</c> 排在
                /// <paramref name="resolver"/> 之前，其余类型仍由原 resolver 处理。
                /// <para>
                /// 顺序不能反过来：<c>JsonTypeInfoResolver.Combine</c> 取第一个返回非 null 的 resolver，
                /// 而 context 对 Id 也会返回一份（对象形态的）<c>JsonTypeInfo</c>。
                /// </para>
                /// </remarks>
                /// <param name="resolver">原本要用的 resolver，通常是源生成的 <c>JsonSerializerContext</c>。</param>
                /// <returns>组合后的 resolver。</returns>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonRegistrationCodeGenerator), version)}}
                public static global::System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver WithStronglyTypedIds(
                    this global::System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver resolver)
                    => global::System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(
                        StronglyTypedIdJsonTypeInfoResolver.Instance,
                        resolver);
            }

            /// <summary>
            /// 为强类型 Id 提供 <c>JsonTypeInfo</c> 的 resolver。
            /// </summary>
            /// <remarks>
            /// 只认强类型 Id，其余类型一律返回 <see langword="null"/>，由组合中的下一个 resolver 处理。
            /// 转换器实例由 <c>JsonMetadataServices</c> 直接包装，不经过反射，AOT 与裁剪下同样可用。
            /// </remarks>
            {{GeneratedCode.CompilerGeneratedAttribute}}
            {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonRegistrationCodeGenerator), version)}}
            internal sealed class StronglyTypedIdJsonTypeInfoResolver : global::System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver
            {
                /// <summary>
                /// 无状态，共享一个实例即可。
                /// </summary>
                public static StronglyTypedIdJsonTypeInfoResolver Instance { get; } = new();

                private StronglyTypedIdJsonTypeInfoResolver()
                {
                }

                /// <inheritdoc/>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonRegistrationCodeGenerator), version)}}
                public global::System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(
                    global::System.Type type,
                    global::System.Text.Json.JsonSerializerOptions options)
                {
            {{string.Join("\n\n", resolverEntries)}}

                    return null;
                }
            }
            """;

        GeneratedCode.AddGeneratedSource(context, "StronglyTypedIds.SystemTextJson.g.cs", code);
    }
}
