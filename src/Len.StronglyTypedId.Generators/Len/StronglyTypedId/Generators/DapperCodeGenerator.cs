using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Dapper 的 <c>SqlMapper.TypeHandler&lt;TId&gt;</c> 与统一注册入口。
/// </summary>
/// <remarks>
/// Dapper 默认把未知类型整体序列化为 JSON（按 <c>object</c> 处理），强类型 Id 会退化成一段 JSON 字符串
/// 存进数据库，既读不通也破坏强类型语义。实现 <c>TypeHandler&lt;TId&gt;</c> 后，DbParameter 写入基元值、
/// 读取时还原为强类型 Id，与 EF Core / JSON 的行为保持一致。
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

            // Dapper 2.x 的 SqlMapper.TypeHandler<T> 基类签名为：
            //   abstract void SetValue(System.Data.IDbDataParameter, T)   ← 形参非可空 T
            //   abstract T Parse(System.Object)                            ← 返回非可空 T
            // 可空标注（T?）只对引用类型合法，值类型 T? 即 Nullable<T>，与基类 T 不匹配（CS0115/CS0508）。
            // 故形参/返回一律用非可空 T，方法体里再做 null → DBNull 映射，使数据库 NULL 与「无 Id」语义对应。
            // 值类型 Id 取 .Value 还原基元；引用类型 Id 直接取 value（运行时为 null 即对应数据库 NULL）。
        var setValueBody = idInfo.TypeKindSuffix is null
            ? "parameter.Value = value is null ? (object)global::System.DBNull.Value : value;"
            : "parameter.Value = value.Value;";

        // Parse 体（基类签名 Parse(System.Object)）：
        //   DBNull → default（值类型 0 / 引用类型 null，对应数据库 NULL 与「无 Id」语义）；
        //   非 DBNull → 强转基元再经 Create 还原强类型 Id。
        // 用 if 语句体重写，避免三元表达式里 `!`（非空断言）与 `global::`（全局命名空间限定符）
        // 在解析层面的歧义：Roslyn 在特定上下文下会把 `(global::X)(value!)` 里的 `!` 误识为
        // 全局命名空间前缀（CS0400），而 `if` 语句体的 `value as global::X` 或 `value is global::X`
        // 不存在该歧义。
        // 注意：PrimitiveIdTypeName 由 SymbolDisplayFormat.FullyQualifiedFormat 产出，已含
        // global:: 前缀（如 global::System.Guid / global::System.Int32），模板里不再套 global::，
        // 避免重复前缀导致 CS0400。
        var parseBody = $$"""
        {
            if (value is global::System.DBNull)
            {
                return default;
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
                    public override void SetValue(global::System.Data.IDbDataParameter parameter, {{idInfo.FullyQualifiedName}} value) =>
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
