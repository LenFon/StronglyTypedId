using System.Collections.Immutable;
using System.Globalization;
using Len.StronglyTypedId.Generators;

namespace Len.StronglyTypedId.DocGen;

/// <summary>
/// 功能索引表的行数据：每个功能点一条，指向它的示例与文档。
/// </summary>
/// <remarks>
/// 这里的「功能」是<b>面向读者</b>的划分，不是生成器内部的模块划分：一个生成器可能对应多行
/// （例如 System.Text.Json 同时有转换器与注册入口），而若干行也可能来自同一份文档。
/// <para>
/// 集成类条目由 <see cref="StronglyTypedIdGenerator.DocumentedGenerators"/> 驱动，
/// 因此新增集成时只要在生成器登记表里补一行，索引表会自动多出一行——这正是这张表要防的遗漏。
/// </para>
/// </remarks>
internal static class Features
{
    /// <summary>
    /// 全部功能条目，顺序即表格行序：先核心能力，再配置，最后集成。
    /// </summary>
    public static readonly ImmutableArray<FeatureEntry> Entries = Build();

    private static ImmutableArray<FeatureEntry> Build()
    {
        var entries = ImmutableArray.CreateBuilder<FeatureEntry>();

        // ── 核心能力 ──────────────────────────────────────────────
        entries.Add(Core(
            "Create / TryCreate",
            "生成成员",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
            "docs/generated-code.md"));

        entries.Add(Core(
            "Parse / TryParse（string 与 span）",
            "生成成员",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
            "docs/generated-code.md"));

        entries.Add(Core(
            "格式化（IFormattable / ISpanFormattable）",
            "生成成员",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
            "docs/generated-code.md"));

        entries.Add(Core(
            "UTF-8 span（IUtf8SpanParsable）",
            "生成成员",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
            "docs/declaration.md",
            "trycreate-and-utf8"));

        entries.Add(Core(
            "比较与排序",
            "生成成员",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
            "docs/generated-code.md"));

        entries.Add(Core(
            "string 基元的序数比较",
            "生成成员",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/StringPrimitiveDemo.cs",
            "docs/generated-code.md"));

        entries.Add(Core(
            "解构",
            "生成成员",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
            "docs/generated-code.md"));

        entries.Add(Core(
            "调试器视图（DebuggerDisplay / DebuggerTypeProxy）",
            "生成成员",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/DebuggerViewDemo.cs",
            "docs/generated-code.md"));

        entries.Add(Core(
            "反射助手",
            "运行时 API",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/ReflectionDemo.cs",
            "docs/runtime.md",
            "reflection-helpers"));

        entries.Add(Core(
            "泛型约束（IStronglyTypedId<TSelf>）",
            "运行时 API",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/StructureDemo.cs",
            "docs/runtime.md",
            "using-the-interface"));

        entries.Add(Core(
            "基元矩阵（23 种 + enum）",
            "基元",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/PrimitiveMatrixDemo.cs",
            "docs/generated-code.md"));

        entries.Add(Core(
            "枚举基元（含 [Flags]）",
            "基元",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/FlagsDemo.cs",
            "docs/generated-code.md",
            "enum-backed-ids"));

        entries.Add(Core(
            "自定义基元",
            "基元",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/CustomPrimitiveDemo.cs",
            "docs/limitations.md",
            "primitive-types"));

        entries.Add(Core(
            "引用类型 Id（record class）",
            "基元",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/ReferenceTypeDemo.cs",
            "docs/generated-code.md"));

        entries.Add(Core(
            "嵌套类型与泛型容器",
            "声明",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/StructureDemo.cs",
            "docs/declaration.md",
            "nested-types"));

        // ── 校验与归一化 ──────────────────────────────────────────
        entries.Add(Core(
            "验证器（Validator）",
            "校验",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/ValidationDemo.cs",
            "docs/declaration.md",
            "value-validation"));

        entries.Add(Core(
            "归一化（Normalizer）",
            "校验",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/ValidationDemo.cs",
            "docs/declaration.md",
            "value-normalization"));

        entries.Add(Core(
            "自定义失败异常（ExceptionType）",
            "校验",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/ExceptionDemo.cs",
            "docs/declaration.md",
            "value-validation"));

        entries.Add(Core(
            "消息脱敏（RedactValidationMessage）",
            "校验",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/ExceptionDemo.cs",
            "docs/declaration.md",
            "value-validation"));

        // ── 配置 ──────────────────────────────────────────────────
        entries.Add(Core(
            "命名实例",
            "配置",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/InstanceDemo.cs",
            "docs/declaration.md",
            "named-instances"));

        entries.Add(Core(
            "文本前缀（Prefix / PrefixReadMode）",
            "配置",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/TextDemo.cs",
            "docs/serialization.md",
            "serialization"));

        entries.Add(Core(
            "JSON 形态（JsonShape）",
            "配置",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/TextDemo.cs",
            "docs/serialization.md",
            "serialization"));

        entries.Add(Core(
            "TypeConverter",
            "配置",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/ConfigurationBindingDemo.cs",
            "docs/declaration.md",
            "type-converter"));

        entries.Add(Core(
            "配置绑定（IConfiguration）",
            "配置",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/ConfigurationBindingDemo.cs",
            "docs/declaration.md",
            "type-converter"));

        entries.Add(Core(
            "集成过滤与白名单（Integrations）",
            "配置",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/IntegrationsDemo.cs",
            "docs/declaration.md",
            "integrations-filter"));

        entries.Add(Core(
            "程序集级默认值（StronglyTypedIdDefaults）",
            "配置",
            "samples/Len.StronglyTypedId.SampleDefaults/Program.cs",
            "docs/declaration.md",
            "assembly-defaults"));

        entries.Add(Core(
            "文化不变性",
            "配置",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/CultureDemo.cs",
            "docs/generated-code.md"));

        // ── 序列化 ────────────────────────────────────────────────
        entries.Add(Core(
            "System.Text.Json 字典键与边界",
            "序列化",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/JsonEdgeCasesDemo.cs",
            "docs/serialization.md",
            "serialization"));

        entries.Add(Core(
            "源生成 JsonSerializerContext",
            "序列化",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/SourceGeneratedJsonDemo.cs",
            "docs/aot.md"));

        entries.Add(Core(
            "Newtonsoft.Json 字典键",
            "序列化",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/NewtonsoftJsonDemo.cs",
            "docs/serialization.md",
            "serialization"));

        // ── 测试 ──────────────────────────────────────────────────
        entries.Add(Core(
            "测试辅助（Any / AnyIdOptions / Conformance）",
            "测试",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/TestingDemo.cs",
            "docs/testing.md",
            "testing"));

        entries.Add(Core(
            "AutoFixture 与 Bogus 适配",
            "测试",
            "samples/Len.StronglyTypedId.SampleConsole/Demos/TestingDemo.cs",
            "docs/testing.md",
            "testing"));

        // ── 集成：由生成器登记表驱动，新增集成自动成行 ──────────────
        foreach (var integration in Integrations.Entries)
        {
            entries.Add(integration);
        }

        return entries.ToImmutable();
    }

    private static FeatureEntry Core(
        string name,
        string kind,
        string samplePath,
        string documentPath,
        string? documentAnchor = null)
        => new(
            new DocText(name, name),
            new DocText(kind, kind),
            samplePath,
            documentPath,
            documentAnchor,
            new DocText(DocumentLabel(documentPath), DocumentLabel(documentPath)));

    /// <summary>
    /// 文档列的链接文本：用文件名（去掉扩展名），读者一眼知道跳到哪一页。
    /// </summary>
    private static string DocumentLabel(string documentPath)
        => Path.GetFileNameWithoutExtension(documentPath);
}

/// <summary>
/// 功能索引表的一行。
/// </summary>
/// <param name="Name">功能名。</param>
/// <param name="Kind">类型：生成成员 / 基元 / 配置 / 集成……</param>
/// <param name="SamplePath">示例文件路径（相对仓库根）；<see langword="null"/> 表示暂无示例。</param>
/// <param name="DocumentPath">文档路径（相对仓库根）。</param>
/// <param name="DocumentAnchor">文档内的锚点；<see langword="null"/> 表示整页。</param>
/// <param name="DocumentLabel">文档列的链接文本。</param>
internal sealed record FeatureEntry(
    DocText Name,
    DocText Kind,
    string? SamplePath,
    string DocumentPath,
    string? DocumentAnchor,
    DocText DocumentLabel);
