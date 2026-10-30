// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <c>[assembly: StronglyTypedIdDefaults(...)]</c> 上 <c>JsonShape</c> / <c>TypeConverter</c> /
/// <c>PrefixReadMode</c> 三项默认值的单元测试。
/// </summary>
/// <remarks>
/// 三项的优先级规则不同，用例分别钉住：
/// <list type="bullet">
/// <item><c>JsonShape</c> / <c>PrefixReadMode</c> 是枚举，未写与写了默认值可区分 → 逐类型可双向覆盖。</item>
/// <item><c>TypeConverter</c> 是 <see langword="bool"/>，未写与写了 <see langword="false"/> 不可区分 → 只能单向打开。</item>
/// </list>
/// </remarks>
public class AssemblyDefaultsTests
{
    #region JsonShape

    /// <summary>
    /// 程序集默认对象形态时，未写 <c>JsonShape</c> 的 Id 采用对象形态。
    /// </summary>
    [Fact]
    public void JsonShape_Should_ComeFromAssemblyDefault_WhenTypeOmitsIt()
    {
        var generated = RunGenerator(AssemblyObjectJsonShapeSource);

        generated["Len.StronglyTypedId.OrderId.SystemTextJson.g.cs"].Should().Contain("writer.WriteStartObject()");
    }

    /// <summary>
    /// 逐类型显式写回 <c>Primitive</c> 能覆盖程序集默认的 <c>Object</c>。
    /// </summary>
    /// <remarks>
    /// 这是枚举型默认值相对 <see langword="bool"/> 型的关键差别：<c>GetEnumValue</c> 未写时返回
    /// <see langword="null"/>，于是「显式写了 0」与「没写」可区分，逐类型才能双向覆盖。
    /// </remarks>
    [Fact]
    public void JsonShape_Should_PreferTypeLevel_WhenTypeExplicitlyWritesPrimitive()
    {
        var generated = RunGenerator(AssemblyObjectJsonShapeTypePrimitiveSource);

        generated["Len.StronglyTypedId.OrderId.SystemTextJson.g.cs"].Should().NotContain("writer.WriteStartObject()");
    }

    /// <summary>
    /// 未声明程序集默认时行为不变：仍是基元形态。
    /// </summary>
    [Fact]
    public void JsonShape_Should_StayPrimitive_WhenAssemblyDefaultIsAbsent()
    {
        var generated = RunGenerator(PlainSource);

        generated["Len.StronglyTypedId.OrderId.SystemTextJson.g.cs"].Should().NotContain("writer.WriteStartObject()");
    }

    #endregion

    #region TypeConverter

    /// <summary>
    /// 程序集默认打开 <c>TypeConverter</c> 后，未写该参数的 Id 也生成转换器。
    /// </summary>
    [Fact]
    public void TypeConverter_Should_ComeFromAssemblyDefault_WhenTypeOmitsIt()
    {
        var generated = RunGenerator(AssemblyTypeConverterSource);

        generated["Len.StronglyTypedId.OrderId.g.cs"].Should().Contain("OrderIdTypeConverter");
    }

    /// <summary>
    /// 未声明程序集默认时仍不生成：默认关闭是既有行为，不能被新参数改变。
    /// </summary>
    [Fact]
    public void TypeConverter_Should_StayOff_WhenAssemblyDefaultIsAbsent()
    {
        var generated = RunGenerator(PlainSource);

        generated["Len.StronglyTypedId.OrderId.g.cs"].Should().NotContain("OrderIdTypeConverter");
    }

    /// <summary>
    /// 逐类型 <c>TypeConverter = true</c> 与程序集默认并存时不会重复生成转换器。
    /// </summary>
    [Fact]
    public void TypeConverter_Should_NotDuplicate_WhenBothLevelsEnableIt()
    {
        var generated = RunGenerator(AssemblyTypeConverterTypeAlsoSource);

        var core = generated["Len.StronglyTypedId.OrderId.g.cs"];

        core.Split("public sealed class OrderIdTypeConverter").Should().HaveCount(2);
    }

    #endregion

    #region PrefixReadMode

    /// <summary>
    /// 程序集默认严格读取时，配了前缀的 Id 缺前缀即失败。
    /// </summary>
    [Fact]
    public void PrefixReadMode_Should_ComeFromAssemblyDefault_WhenTypeOmitsIt()
    {
        var generated = RunGenerator(AssemblyStrictPrefixSource);

        generated["Len.StronglyTypedId.OrderId.g.cs"].Should().Contain("!value.StartsWith(\"ord_\", global::System.StringComparison.Ordinal)");
    }

    /// <summary>
    /// 逐类型显式写回 <c>Lenient</c> 能覆盖程序集默认的 <c>Strict</c>。
    /// </summary>
    [Fact]
    public void PrefixReadMode_Should_PreferTypeLevel_WhenTypeExplicitlyWritesLenient()
    {
        var generated = RunGenerator(AssemblyStrictPrefixTypeLenientSource);

        generated["Len.StronglyTypedId.OrderId.g.cs"]
            .Should().NotContain("!value.StartsWith(\"ord_\", global::System.StringComparison.Ordinal)");
    }

    /// <summary>
    /// 程序集默认严格但 Id 没配前缀时，严格策略无从生效，产物与未声明默认时一致。
    /// </summary>
    [Fact]
    public void PrefixReadMode_Should_HaveNoEffect_WhenIdHasNoPrefix()
    {
        var generated = RunGenerator(AssemblyStrictPrefixNoPrefixSource);

        generated["Len.StronglyTypedId.OrderId.g.cs"].Should().NotContain("StartsWith(\"ord_\"");
    }

    #endregion

    #region 辅助

    private const string PlainSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    private const string AssemblyObjectJsonShapeSource = """
        using System;

        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(JsonShape = Len.StronglyTypedId.JsonShape.Object)]

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    private const string AssemblyObjectJsonShapeTypePrimitiveSource = """
        using System;

        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(JsonShape = Len.StronglyTypedId.JsonShape.Object)]

        namespace Len.StronglyTypedId;

        [StronglyTypedId(JsonShape = JsonShape.Primitive)]
        public partial record struct OrderId(Guid Value);
        """;

    private const string AssemblyTypeConverterSource = """
        using System;

        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(TypeConverter = true)]

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    private const string AssemblyTypeConverterTypeAlsoSource = """
        using System;

        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(TypeConverter = true)]

        namespace Len.StronglyTypedId;

        [StronglyTypedId(TypeConverter = true)]
        public partial record struct OrderId(Guid Value);
        """;

    private const string AssemblyStrictPrefixSource = """
        using System;

        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(PrefixReadMode = Len.StronglyTypedId.PrefixReadMode.Strict)]

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Prefix = "ord_")]
        public partial record struct OrderId(Guid Value);
        """;

    private const string AssemblyStrictPrefixTypeLenientSource = """
        using System;

        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(PrefixReadMode = Len.StronglyTypedId.PrefixReadMode.Strict)]

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Lenient)]
        public partial record struct OrderId(Guid Value);
        """;

    private const string AssemblyStrictPrefixNoPrefixSource = """
        using System;

        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(PrefixReadMode = Len.StronglyTypedId.PrefixReadMode.Strict)]

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    private static Dictionary<string, string> RunGenerator(string sourceCode)
    {
        var compilation = CreateCompilation(sourceCode);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return result.Results[0].GeneratedSources
            .ToDictionary(source => source.HintName, source => source.SourceText.ToString());
    }

    private static CSharpCompilation CreateCompilation(string sourceCode)
    {
        // System.Text.Json 是延迟加载的：必须显式引用一次，它才会出现在编译引用里，
        // SystemTextJsonCodeGenerator 也才会被选入。
        var seed = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
        };

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(seed)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(AssemblyReferenceCache.FromAssembly)
            .ToArray();

        return CSharpCompilation.Create(
            "Len.StronglyTypedId.AssemblyDefaults.Test",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    #endregion
}
