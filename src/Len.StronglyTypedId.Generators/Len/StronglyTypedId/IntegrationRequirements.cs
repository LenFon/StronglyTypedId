namespace Len.StronglyTypedId;

/// <summary>
/// 集成 → 所需程序集与最低主版本的对照表。
/// </summary>
/// <remarks>
/// 生成器的模块闸门与分析器的 STIA0019 / STIA0030 都从这里取：门控承诺「≥ 这个版本可用」，
/// 分析器就必须在低于该版本时说出来。两处各写一份的话，改了下限却漏改另一处，门控就退化成猜测。
/// </remarks>
internal static class IntegrationRequirements
{
    internal static readonly IntegrationRequirement EntityFrameworkCore = new(
        IntegrationFlags.EntityFrameworkCore, "EntityFrameworkCore", 7, "Microsoft.EntityFrameworkCore");

    internal static readonly IntegrationRequirement Dapper = new(
        IntegrationFlags.Dapper, "Dapper", 2, "Dapper");

    /// <summary>
    /// <c>System.Text.Json</c> 没有版本下限：任何版本都带得动生成的转换器。
    /// </summary>
    internal static readonly IntegrationRequirement SystemTextJson = new(
        IntegrationFlags.SystemTextJson, "SystemTextJson", 0, "System.Text.Json");

    internal static readonly IntegrationRequirement NewtonsoftJson = new(
        IntegrationFlags.NewtonsoftJson, "NewtonsoftJson", 13, "Newtonsoft.Json");

    internal static readonly IntegrationRequirement Swagger = new(
        IntegrationFlags.Swagger, "Swagger", 6, "Swashbuckle.AspNetCore.SwaggerGen");

    internal static readonly IntegrationRequirement AspNetCoreOpenApi = new(
        IntegrationFlags.AspNetCoreOpenApi, "AspNetCoreOpenApi", 9, "Microsoft.AspNetCore.OpenApi");

    /// <summary>
    /// MVC 的「门面 / 实现」拆分随 .NET 版本变化（.NET 8 落在 Abstractions、.NET 10 落在 Core），
    /// 三者任一出现即视为 MVC 可用。
    /// </summary>
    internal static readonly IntegrationRequirement AspNetCoreMvc = new(
        IntegrationFlags.AspNetCoreMvc,
        "AspNetCoreMvc",
        2,
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Mvc.Core",
        "Microsoft.AspNetCore.Mvc.Abstractions");

    internal static readonly IntegrationRequirement MessagePack = new(
        IntegrationFlags.MessagePack, "MessagePack", 2, "MessagePack");

    internal static readonly IntegrationRequirement MongoDb = new(
        IntegrationFlags.MongoDb, "MongoDb", 3, "MongoDB.Bson");

    /// <summary>
    /// 门控与生成器一致：HotChocolate 的 scalar 基类在 <c>HotChocolate.Types</c>，装配入口在
    /// <c>HotChocolate.Execution</c>；引用前者必然带上后者，故只查前者。
    /// </summary>
    internal static readonly IntegrationRequirement HotChocolate = new(
        IntegrationFlags.HotChocolate, "HotChocolate", 13, "HotChocolate.Types");

    /// <summary>
    /// 同上：特性在 <c>protobuf-net.Core</c>、Serializer 在 <c>protobuf-net</c>，引用后者必然带上前者。
    /// </summary>
    internal static readonly IntegrationRequirement Protobuf = new(
        IntegrationFlags.Protobuf, "Protobuf", 3, "protobuf-net");

    /// <summary>
    /// 全部集成，顺序即诊断的上报顺序（同一编译里保持稳定）。
    /// </summary>
    internal static readonly ImmutableArray<IntegrationRequirement> All =
    [
        EntityFrameworkCore,
        Dapper,
        SystemTextJson,
        NewtonsoftJson,
        Swagger,
        AspNetCoreOpenApi,
        AspNetCoreMvc,
        MessagePack,
        MongoDb,
        HotChocolate,
        Protobuf,
    ];

    /// <summary>
    /// 判断模块集合里是否存在满足最低主版本的对应程序集（生成器侧闸门）。
    /// </summary>
    /// <remarks>
    /// 程序集名在这里统一补 <c>.dll</c>：<see cref="ModuleInfo.Name"/> 取自元数据读取器，带扩展名；
    /// 而 <see cref="Compilation.ReferencedAssemblyNames"/> 用的是简单名。表里只存简单名，边界处各自适配。
    /// </remarks>
    internal static bool IsSatisfied(ImmutableArray<ModuleInfo> modules, IntegrationRequirement requirement)
        => requirement.AssemblyNames.Any(name => modules.HasModule(name + ".dll", requirement.MinMajor));

    /// <summary>
    /// 取编译实际引用的对应程序集版本；未引用任何候选程序集时返回 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 多个门面同时存在时取最高版本：只要有一个够新，集成就能用，不该因为另一个旧门面而误报。
    /// </remarks>
    internal static Version? GetReferencedVersion(Compilation compilation, IntegrationRequirement requirement)
    {
        Version? best = null;

        foreach (var identity in compilation.ReferencedAssemblyNames)
        {
            if (!requirement.AssemblyNames.Any(name => string.Equals(name, identity.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // netstandard2.0 的 Version 没有比较运算符，只能走 CompareTo。
            if (best is null || identity.Version.CompareTo(best) > 0)
            {
                best = identity.Version;
            }
        }

        return best;
    }
}

/// <summary>
/// 单个集成的程序集要求：标志位、显示名、最低主版本与候选程序集名。
/// </summary>
internal sealed class IntegrationRequirement(int flag, string displayName, int minMajor, params string[] assemblyNames)
{
    /// <summary>
    /// 对应的 <see cref="IntegrationFlags"/> 位。
    /// </summary>
    public int Flag { get; } = flag;

    /// <summary>
    /// 诊断文案里的集成名（与枚举成员同名）。
    /// </summary>
    public string DisplayName { get; } = displayName;

    /// <summary>
    /// 可用的最低主版本；<c>0</c> 表示不限版本。
    /// </summary>
    public int MinMajor { get; } = minMajor;

    /// <summary>
    /// 候选程序集简单名（不含 <c>.dll</c>）；MVC 有三个门面，其余只有一个。
    /// </summary>
    public ImmutableArray<string> AssemblyNames { get; } = [.. assemblyNames];

    /// <summary>
    /// 主程序集简单名：README 表格与 STIA0019 文案里引用的那个。
    /// </summary>
    public string AssemblyName => AssemblyNames[0];
}
