using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Dapper 的 <c>SqlMapper.TypeHandler&lt;TId&gt;</c> 与统一注册入口。
/// </summary>
/// <remarks>
/// Dapper 默认把未知类型按 <c>object</c> 序列化为 JSON，强类型 Id 会退化成一段 JSON 字符串存进数据库。
/// 实现 <c>TypeHandler&lt;TId&gt;</c> 后写入基元值、读取时还原为 Id。
/// </remarks>
internal class DapperCodeGenerator : IIntegrationCodeGenerator
{
    internal static readonly IIntegrationCodeGenerator Instance = new DapperCodeGenerator();

    public int Order { get; } = 5;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.External.ToList();

        var ambiguousNames = stronglyTypedIds
            .GroupBy(idInfo => idInfo.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);

        var registrations = new StringBuilder();

        foreach (var idInfo in stronglyTypedIds)
        {
            var handlerName = GetHandlerName(idInfo, ambiguousNames.Contains(idInfo.Name));

            // 基类签名是 SetValue(IDbDataParameter, T?) / Parse(object) → T。值类型 T 的可空标注会被擦除，
            // 引用类型 T 必须照抄 T?，否则 CS8765。Parse 返回非可空 T，引用类型 Id 遇 NULL 时用 default! 表态。
        var setValueParameter = idInfo.TypeKindSuffix is null
            ? $"{idInfo.FullyQualifiedName}? value"
            : $"{idInfo.FullyQualifiedName} value";

        var setValueBody = idInfo.TypeKindSuffix is null
            ? "parameter.Value = value is null ? (object)global::System.DBNull.Value : value;"
            : "parameter.Value = value.Value;";

        var parseDefaultValue = idInfo.TypeKindSuffix is null ? "default!" : "default";

        // DBNull → default，否则强转基元再经 Create 还原。用 if 语句体而非三元表达式：Roslyn 在
        // `(global::X)(value!)` 里会把 `!` 误识为全局命名空间前缀（CS0400）。PrimitiveIdTypeName 已含
        // global:: 前缀，模板里不再套。
        var parseBody = $$"""
        {
            if (value is global::System.DBNull)
            {
                return {{parseDefaultValue}};
            }

            return {{idInfo.FullyQualifiedName}}.Create(({{idInfo.PrimitiveIdTypeName}})value!);
        }
        """;

        var handlerCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <inheritdoc/>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(DapperCodeGenerator), version)}}
                class {{handlerName}} : global::Dapper.SqlMapper.TypeHandler<{{idInfo.FullyQualifiedName}}>
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(DapperCodeGenerator), version)}}
                    public override void SetValue(global::System.Data.IDbDataParameter parameter, {{setValueParameter}}) =>
                        {{setValueBody}}

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(DapperCodeGenerator), version)}}
                    public override {{idInfo.FullyQualifiedName}} Parse(object? value)
                        {{parseBody}}
                }
            }
            """;

            context.AddSource($"{idInfo.HintName}.Dapper.g.cs", GeneratedCode.NormalizeLineEndings(handlerCode));

            registrations.Append($"\n\t\tglobal::Dapper.SqlMapper.AddTypeHandler<{idInfo.FullyQualifiedName}>(new {handlerName}());");
        }

            var registrationCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// Employ strongly typed IDs in Dapper.
                    /// </summary>
                    /// <param name="connection">The Dapper connection to register the type handlers against.</param>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(DapperCodeGenerator), version)}}
                    public static void ApplyTo(global::System.Data.IDbConnection connection)
                    {{{registrations}}
                    }
                }
                """;

        context.AddSource("StronglyTypedIds.Dapper.g.cs", GeneratedCode.NormalizeLineEndings(registrationCode));
    }

    /// <summary>
    /// 取类型处理器的类名，消歧规则与 EF Core 转换器一致。
    /// </summary>
    private static string GetHandlerName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}TypeHandler"
            : $"{idInfo.Name}TypeHandler";
}
