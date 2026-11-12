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
        =>
        [
            // ── 核心能力 ──────────────────────────────────────────────
            Core(
                "Create / TryCreate",
                "Create / TryCreate",
                Kinds.GeneratedMember,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
                "docs/generated-code.md"),

            Core(
                "Parse / TryParse (string and span)",
                "Parse / TryParse（string 与 span）",
                Kinds.GeneratedMember,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
                "docs/generated-code.md"),

            Core(
                "Formatting (IFormattable / ISpanFormattable)",
                "格式化（IFormattable / ISpanFormattable）",
                Kinds.GeneratedMember,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
                "docs/generated-code.md"),

            Core(
                "UTF-8 span (IUtf8SpanParsable)",
                "UTF-8 span（IUtf8SpanParsable）",
                Kinds.GeneratedMember,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
                "docs/declaration.md",
                "trycreate-and-utf8"),

            Core(
                "Comparison and ordering",
                "比较与排序",
                Kinds.GeneratedMember,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
                "docs/generated-code.md"),

            Core(
                "Ordinal comparison for string primitives",
                "string 基元的序数比较",
                Kinds.GeneratedMember,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/StringPrimitiveDemo.cs",
                "docs/generated-code.md"),

            Core(
                "Deconstruction",
                "解构",
                Kinds.GeneratedMember,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs",
                "docs/generated-code.md"),

            Core(
                "Debugger view (DebuggerDisplay / DebuggerTypeProxy)",
                "调试器视图（DebuggerDisplay / DebuggerTypeProxy）",
                Kinds.GeneratedMember,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/DebuggerViewDemo.cs",
                "docs/generated-code.md"),

            Core(
                "Reflection helpers",
                "反射助手",
                Kinds.RuntimeApi,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/ReflectionDemo.cs",
                "docs/runtime.md",
                "reflection-helpers"),

            Core(
                "Generic constraint (IStronglyTypedId<TSelf>)",
                "泛型约束（IStronglyTypedId<TSelf>）",
                Kinds.RuntimeApi,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/StructureDemo.cs",
                "docs/runtime.md",
                "using-the-interface"),

            Core(
                "Primitive matrix (23 types + enum)",
                "基元矩阵（23 种 + enum）",
                Kinds.Primitive,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/PrimitiveMatrixDemo.cs",
                "docs/generated-code.md"),

            Core(
                "Enum primitives (including [Flags])",
                "枚举基元（含 [Flags]）",
                Kinds.Primitive,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/FlagsDemo.cs",
                "docs/generated-code.md",
                "enum-backed-ids"),

            Core(
                "Custom primitives",
                "自定义基元",
                Kinds.Primitive,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/CustomPrimitiveDemo.cs",
                "docs/limitations.md",
                "primitive-types"),

            Core(
                "Reference-type ids (record class)",
                "引用类型 Id（record class）",
                Kinds.Primitive,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/ReferenceTypeDemo.cs",
                "docs/generated-code.md"),

            Core(
                "Nested types and generic containers",
                "嵌套类型与泛型容器",
                Kinds.Declaration,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/StructureDemo.cs",
                "docs/declaration.md",
                "nested-types"),

            // ── 校验与归一化 ──────────────────────────────────────────
            Core(
                "Value validation (Validator)",
                "验证器（Validator）",
                Kinds.Validation,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/ValidationDemo.cs",
                "docs/declaration.md",
                "value-validation"),

            Core(
                "Value normalization (Normalizer)",
                "归一化（Normalizer）",
                Kinds.Validation,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/ValidationDemo.cs",
                "docs/declaration.md",
                "value-normalization"),

            Core(
                "Custom failure type (ExceptionType)",
                "自定义失败异常（ExceptionType）",
                Kinds.Validation,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/ExceptionDemo.cs",
                "docs/declaration.md",
                "value-validation"),

            Core(
                "Redacted messages (RedactValidationMessage)",
                "消息脱敏（RedactValidationMessage）",
                Kinds.Validation,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/ExceptionDemo.cs",
                "docs/declaration.md",
                "value-validation"),

            // ── 配置 ──────────────────────────────────────────────────
            Core(
                "Named instances",
                "命名实例",
                Kinds.Configuration,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/InstanceDemo.cs",
                "docs/declaration.md",
                "named-instances"),

            Core(
                "Text prefix (Prefix / PrefixReadMode)",
                "文本前缀（Prefix / PrefixReadMode）",
                Kinds.Configuration,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/TextDemo.cs",
                "docs/serialization.md",
                "serialization"),

            Core(
                "JSON shape (JsonShape)",
                "JSON 形态（JsonShape）",
                Kinds.Configuration,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/TextDemo.cs",
                "docs/serialization.md",
                "serialization"),

            Core(
                "TypeConverter",
                "TypeConverter",
                Kinds.Configuration,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/ConfigurationBindingDemo.cs",
                "docs/declaration.md",
                "type-converter"),

            Core(
                "Configuration binding (IConfiguration)",
                "配置绑定（IConfiguration）",
                Kinds.Configuration,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/ConfigurationBindingDemo.cs",
                "docs/declaration.md",
                "type-converter"),

            Core(
                "Integration filter (Integrations)",
                "集成过滤与白名单（Integrations）",
                Kinds.Configuration,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/IntegrationsDemo.cs",
                "docs/declaration.md",
                "integrations-filter"),

            Core(
                "Assembly-level defaults (StronglyTypedIdDefaults)",
                "程序集级默认值（StronglyTypedIdDefaults）",
                Kinds.Configuration,
                "samples/Len.StronglyTypedId.SampleDefaults/Program.cs",
                "docs/declaration.md",
                "assembly-defaults"),

            Core(
                "Culture invariance",
                "文化不变性",
                Kinds.Configuration,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/CultureDemo.cs",
                "docs/generated-code.md"),

            // ── 序列化 ────────────────────────────────────────────────
            Core(
                "System.Text.Json dictionary keys and edge cases",
                "System.Text.Json 字典键与边界",
                Kinds.Serialization,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/JsonEdgeCasesDemo.cs",
                "docs/serialization.md",
                "serialization"),

            Core(
                "Source-generated JsonSerializerContext",
                "源生成 JsonSerializerContext",
                Kinds.Serialization,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/SourceGeneratedJsonDemo.cs",
                "docs/aot.md"),

            Core(
                "Newtonsoft.Json dictionary keys",
                "Newtonsoft.Json 字典键",
                Kinds.Serialization,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/NewtonsoftJsonDemo.cs",
                "docs/serialization.md",
                "serialization"),

            // ── 测试 ──────────────────────────────────────────────────
            Core(
                "Test helpers (Any / AnyIdOptions / Conformance)",
                "测试辅助（Any / AnyIdOptions / Conformance）",
                Kinds.Testing,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/TestingDemo.cs",
                "docs/testing.md",
                "testing"),

            Core(
                "AutoFixture and Bogus adapters",
                "AutoFixture 与 Bogus 适配",
                Kinds.Testing,
                "samples/Len.StronglyTypedId.SampleConsole/Demos/TestingDemo.cs",
                "docs/testing.md",
                "testing"),

            // ── 集成：由生成器登记表驱动，新增集成自动成行 ──────────────
            .. Integrations.Entries,
        ];

    /// <summary>
    /// 功能类型列的文案。取值集合固定，故集中成常量，避免 32 条条目各写一遍两种语言。
    /// </summary>
    private static class Kinds
    {
        public static readonly DocText GeneratedMember = new("Generated member", "生成成员");

        public static readonly DocText RuntimeApi = new("Runtime API", "运行时 API");

        public static readonly DocText Primitive = new("Primitive", "基元");

        public static readonly DocText Declaration = new("Declaration", "声明");

        public static readonly DocText Validation = new("Validation", "校验");

        public static readonly DocText Configuration = new("Configuration", "配置");

        public static readonly DocText Serialization = new("Serialization", "序列化");

        public static readonly DocText Testing = new("Testing", "测试");
    }

    /// <summary>
    /// 构造一条核心功能条目。
    /// </summary>
    /// <param name="nameEn">英文功能名。</param>
    /// <param name="nameZh">中文功能名。</param>
    /// <param name="kind">功能类型（两种语言的文案由 <see cref="Kinds"/> 提供）。</param>
    /// <param name="samplePath">示例文件路径（相对仓库根）。</param>
    /// <param name="documentPath">文档路径（相对仓库根）。</param>
    /// <param name="documentAnchor">文档内的锚点；<see langword="null"/> 表示整页。</param>
    /// <remarks>
    /// 两个名字都<b>必须显式给出</b>，不能只写一个再复制到另一侧：那样英文页会静默渲染出中文，
    /// 而 <c>--verify</c> 只比对「文件与渲染结果一致」，两边同时是中文时它照样通过。
    /// </remarks>
    private static FeatureEntry Core(
        string nameEn,
        string nameZh,
        DocText kind,
        string samplePath,
        string documentPath,
        string? documentAnchor = null)
        => new(
            new DocText(nameEn, nameZh),
            kind,
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
