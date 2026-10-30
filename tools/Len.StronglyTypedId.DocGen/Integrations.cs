using Len.StronglyTypedId.Generators;

namespace Len.StronglyTypedId.DocGen;

/// <summary>
/// 功能索引表里的集成行，由生成器登记表派生。
/// </summary>
/// <remarks>
/// 这是整张表最关键的一处设计：集成行<b>不手工维护</b>，而是遍历
/// <see cref="StronglyTypedIdGenerator.DocumentedGenerators"/> 生成。
/// 新增一个集成时，只要在生成器登记表里补一行（那本来就是必须做的），索引表就会自动多出一行，
/// 不会出现「集成加了、索引没加」的遗漏——protobuf-net 当初漏掉示例正是这类问题。
/// <para>
/// 示例文件与文档锚点按程序集名映射；映射缺失时直接抛异常，而不是静默留空。
/// </para>
/// </remarks>
internal static class Integrations
{
    /// <summary>
    /// 集成程序集名 → 示例文件路径。键与生成器登记表里的程序集名一致。
    /// </summary>
    private static readonly Dictionary<string, string> _sampleFileByAssembly = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System.Text.Json.dll"] = "samples/Len.StronglyTypedId.SampleConsole/Demos/SystemTextJsonDemo.cs",
        ["Newtonsoft.Json.dll"] = "samples/Len.StronglyTypedId.SampleConsole/Demos/NewtonsoftJsonDemo.cs",
        ["Microsoft.EntityFrameworkCore.dll"] = "samples/Len.StronglyTypedId.SampleIntegrations/EfCoreDemo.cs",
        ["Dapper.dll"] = "samples/Len.StronglyTypedId.SampleIntegrations/DapperDemo.cs",
        ["Swashbuckle.AspNetCore.SwaggerGen.dll"] = "samples/Len.StronglyTypedId.Sample3/Program.cs",
        ["Microsoft.AspNetCore.OpenApi.dll"] = "samples/Len.StronglyTypedId.Sample2/Program.cs",
        ["Microsoft.AspNetCore.Mvc.dll"] = "samples/Len.StronglyTypedId.Sample1/Program.cs",
        ["MessagePack.dll"] = "samples/Len.StronglyTypedId.SampleIntegrations/BinarySerializationDemo.cs",
        ["MongoDB.Bson.dll"] = "samples/Len.StronglyTypedId.SampleIntegrations/BinarySerializationDemo.cs",
        ["HotChocolate.Types.dll"] = "samples/Len.StronglyTypedId.SampleGraphQL/Program.cs",
        ["protobuf-net.dll"] = "samples/Len.StronglyTypedId.SampleIntegrations/ProtobufDemo.cs",
    };

    /// <summary>
    /// 集成程序集名 → 文档锚点。两个 JSON 序列化器指向 <c>serialization.md</c>，其余指向
    /// <c>integrations.md</c>。
    /// </summary>
    private static readonly Dictionary<string, (string DocumentPath, string Anchor)> _documentByAssembly =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Text.Json.dll"] = ("docs/serialization.md", "serialization"),
            ["Newtonsoft.Json.dll"] = ("docs/serialization.md", "serialization"),
            ["Microsoft.EntityFrameworkCore.dll"] = ("docs/integrations.md", "entity-framework-core"),
            ["Dapper.dll"] = ("docs/integrations.md", "dapper"),
            ["Swashbuckle.AspNetCore.SwaggerGen.dll"] = ("docs/integrations.md", "swashbuckle"),
            ["Microsoft.AspNetCore.OpenApi.dll"] = ("docs/integrations.md", "aspnetcore-openapi"),
            ["Microsoft.AspNetCore.Mvc.dll"] = ("docs/integrations.md", "aspnet-core-mvc"),
            ["MessagePack.dll"] = ("docs/integrations.md", "messagepack"),
            ["MongoDB.Bson.dll"] = ("docs/integrations.md", "mongodb"),
            ["HotChocolate.Types.dll"] = ("docs/integrations.md", "hotchocolate"),
            ["protobuf-net.dll"] = ("docs/integrations.md", "protobuf-net"),
        };

    /// <summary>
    /// 集成行。核心生成器（<c>Len.StronglyTypedId.dll</c>）不是集成，跳过。
    /// </summary>
    /// <remarks>
    /// 判据是「程序集名不是 <c>Len.StronglyTypedId.dll</c>」，而不是 <c>IsCore</c>：
    /// <c>IsCore</c> 表示的是「实现了 <see cref="ICoreCodeGenerator"/>」——即产物挂在 Id 自己身上，
    /// 而不是放进 <c>StronglyTypedIds</c> 类。System.Text.Json、Newtonsoft.Json 与 protobuf-net
    /// 恰好都属于这一类，用 <c>IsCore</c> 过滤会把它们当成「核心」而漏掉。
    /// </remarks>
    public static IEnumerable<FeatureEntry> Entries
    {
        get
        {
            foreach (var registration in StronglyTypedIdGenerator.DocumentedGenerators)
            {
                var assemblyName = registration.AssemblyName;

                // 真正的主生成器：产物是 Id 本身，不是某个集成。
                if (string.Equals(assemblyName, "Len.StronglyTypedId.dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!_sampleFileByAssembly.TryGetValue(assemblyName, out var samplePath))
                {
                    throw new InvalidOperationException(
                        $"集成「{assemblyName}」已登记进生成器表，但功能索引表没有它的示例文件映射。"
                        + $"请在 {nameof(Integrations)}.{nameof(_sampleFileByAssembly)} 里补一行。");
                }

                if (!_documentByAssembly.TryGetValue(assemblyName, out var document))
                {
                    throw new InvalidOperationException(
                        $"集成「{assemblyName}」已登记进生成器表，但功能索引表没有它的文档映射。"
                        + $"请在 {nameof(Integrations)}.{nameof(_documentByAssembly)} 里补一行。");
                }

                // 功能名用程序集短名（去掉 .dll），读者认得出是哪个包。
                var name = assemblyName.Replace(".dll", string.Empty, StringComparison.OrdinalIgnoreCase);

                yield return new FeatureEntry(
                    new DocText(name, name),
                    new DocText("Integration", "集成"),
                    samplePath,
                    document.DocumentPath,
                    document.Anchor,
                    new DocText(
                        Path.GetFileNameWithoutExtension(document.DocumentPath),
                        Path.GetFileNameWithoutExtension(document.DocumentPath)));
            }
        }
    }
}
