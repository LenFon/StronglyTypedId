// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// N2（命名实例）的生成器产物契约：值文本如何在编译期被翻译成字面量、非法声明如何被跳过，
/// 以及「未声明实例时产物逐字节不变」这条回归护栏。
/// </summary>
public class InstanceCodeGeneratorTests
{
    #region 辅助：驱动生成器并捕获产物

    private static CSharpCompilation CreateCompilation(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union([typeof(StronglyTypedIdAttribute).Assembly])
            .Distinct()
            .Where(assembly => !assembly.IsDynamic)
            .Where(ShouldReferenceAssembly)
            .Select(AssemblyReferenceCache.TryResolveReference)
            .OfType<MetadataReference>()
            .ToArray();

        return CSharpCompilation.Create(
            "Len.StronglyTypedId.Instance.Test",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static Dictionary<string, string> GetGeneratedCode(string sourceCode)
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

    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase"
            or "Microsoft.EntityFrameworkCore"
            or "Swashbuckle.AspNetCore.SwaggerGen"
            or "Microsoft.OpenApi"
            or "Microsoft.AspNetCore.Mvc"
            or "Microsoft.AspNetCore.Mvc.Core"
            or "Microsoft.AspNetCore.Mvc.Abstractions"
            or "Microsoft.AspNetCore.OpenApi"
            or "Microsoft.AspNetCore.Routing"
            or "Dapper");

    /// <summary>
    /// 取核心产物（<c>OrderId.g.cs</c>）。按后缀匹配而非写死 hint 名：hint 名带命名空间，
    /// 而本文件的源码统一声明在 <c>Len.StronglyTypedId.Tests</c> 下。
    /// </summary>
    private static string GetCore(string sourceCode)
        => GetGeneratedCode(sourceCode)
            .Single(pair => pair.Key.EndsWith(".OrderId.g.cs", StringComparison.Ordinal))
            .Value;

    /// <summary>
    /// 拼一份「单个实例」的源码。
    /// </summary>
    private static string Single(string primitiveType, string value, string name = "Sentinel")
        => $$"""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("{{name}}", "{{value}}")]
            public partial record struct OrderId({{primitiveType}} Value);
            """;

    /// <summary>
    /// 断言生成产物里出现了指定的实例字段声明（含 4 空格缩进）。
    /// </summary>
    private static void ShouldContainField(string generated, string typeName, string name, string expression)
        => generated.Should().Contain($"    public static readonly {typeName} {name} = Create({expression});");

    #endregion

    #region 整型

    [Fact]
    public void Core_Should_EmitInt32Literal()
    {
        var generated = GetCore(Single("int", "-1", "Unspecified"));

        ShouldContainField(generated, "OrderId", "Unspecified", "(int)(-1)");
    }

    /// <summary>
    /// <c>long.MinValue</c> 的字面量必须先加括号再取负：直接写 <c>-9223372036854775808</c> 会被解析成
    /// 「一元负号作用于 9223372036854775808」，而后者超出 <c>long</c> 范围（CS0266）。
    /// </summary>
    [Fact]
    public void Core_Should_EmitInt64MinValue_AsParenthesizedCast()
    {
        var generated = GetCore(Single("long", "-9223372036854775808", "Min"));

        ShouldContainField(generated, "OrderId", "Min", "(long)(-9223372036854775808)");
    }

    /// <summary>
    /// 无符号最大值：字面量本身已能表示，但仍统一走显式转换，避免推断成 <c>ulong</c> 以外的类型。
    /// </summary>
    [Fact]
    public void Core_Should_EmitUInt64MaxValue()
    {
        var generated = GetCore(Single("ulong", "18446744073709551615", "Max"));

        ShouldContainField(generated, "OrderId", "Max", "(ulong)(18446744073709551615)");
    }

    /// <summary>
    /// 窄整型的边界值必须按目标类型校验：<c>256</c> 对 <c>byte</c> 越界，故不生成。
    /// </summary>
    [Fact]
    public void Core_Should_SkipInstance_WhenValueOverflowsPrimitive()
    {
        var generated = GetCore(Single("byte", "256", "TooBig"));

        generated.Should().NotContain("TooBig");
    }

    /// <summary>
    /// 其余窄整型同样走「显式转换 + 目标类型边界校验」：字面量默认推断成 <c>int</c>，
    /// 不加强制转换会让 <c>sbyte</c> / <c>ushort</c> 这类字段声明编译不过。
    /// </summary>
    [Theory]
    [InlineData("sbyte", "-128", "(sbyte)(-128)")]
    [InlineData("short", "-32768", "(short)(-32768)")]
    [InlineData("ushort", "65535", "(ushort)(65535)")]
    [InlineData("uint", "4294967295", "(uint)(4294967295)")]
    public void Core_Should_EmitNarrowIntegralLiteral(string primitiveType, string value, string expression) =>
        ShouldContainField(GetCore(Single(primitiveType, value)), "OrderId", "Sentinel", expression);

    /// <summary>
    /// 每种基元都有自己的「文本是否合法」判据，判据失效时最坏的后果是把非法文本发射成
    /// 编译不过的字面量（例如 <c>new Guid("not-a-guid")</c> 是运行期异常，而 <c>(byte)(999)</c> 是编译错误）。
    /// 本用例逐基元确认非法文本被整条跳过。
    /// </summary>
    [Theory]
    [InlineData("sbyte", "128")]
    [InlineData("short", "40000")]
    [InlineData("ushort", "-1")]
    [InlineData("uint", "-1")]
    [InlineData("float", "abc")]
    [InlineData("double", "abc")]
    [InlineData("decimal", "abc")]
    [InlineData("Guid", "not-a-guid")]
    [InlineData("DateTime", "not-a-date")]
    [InlineData("DateTimeOffset", "not-a-date")]
    [InlineData("TimeSpan", "not-a-span")]
    [InlineData("Half", "abc")]
    public void Core_Should_SkipInstance_WhenTextIsNotParseable(string primitiveType, string value) =>
        GetCore(Single(primitiveType, value, "Bad")).Should().NotContain("Bad");

    #endregion

    #region 浮点与 decimal

    /// <summary>
    /// <c>float</c> 必须用往返格式（"R"）并带 <c>F</c> 后缀，否则 <c>0.1</c> 会退化成 <c>0.1</c> 的
    /// 双精度近似值再截断，往返后不再相等。
    /// </summary>
    [Fact]
    public void Core_Should_EmitSingleWithRoundTripFormat()
    {
        var generated = GetCore(Single("float", "0.1"));

        ShouldContainField(generated, "OrderId", "Sentinel", "0.1F");
    }

    [Fact]
    public void Core_Should_EmitDoubleSpecialValues_AsConstants()
    {
        GetCore(Single("double", "NaN")).Should().Contain("= Create(global::System.Double.NaN);");
        GetCore(Single("double", "Infinity")).Should().Contain("= Create(global::System.Double.PositiveInfinity);");
        GetCore(Single("double", "-Infinity")).Should().Contain("= Create(global::System.Double.NegativeInfinity);");
    }

    /// <summary>
    /// <c>decimal</c> 没有往返格式串（<c>ToString("R")</c> 会抛 <see cref="FormatException"/>），
    /// 只能依赖不变文化的默认格式。
    /// </summary>
    [Fact]
    public void Core_Should_EmitDecimal_WithMSuffix()
    {
        var generated = GetCore(Single("decimal", "1.5"));

        ShouldContainField(generated, "OrderId", "Sentinel", "1.5M");
    }

    /// <summary>
    /// <c>Half</c> 在生成器所在的 netstandard2.0 上不存在，只能先按 <c>float</c> 校验再显式转换。
    /// </summary>
    [Fact]
    public void Core_Should_EmitHalf_AsConvertedSingle()
    {
        var generated = GetCore(Single("Half", "1.5"));

        ShouldContainField(generated, "OrderId", "Sentinel", "(global::System.Half)(1.5F)");
    }

    /// <summary>
    /// D4：<c>NaN</c> / <c>±Infinity</c> 是 <c>Half</c> 的合法取值（<c>Half.Parse</c> 接受），
    /// 编译期不能把它们判成非法，直接发射对应常量。
    /// </summary>
    [Theory]
    [InlineData("NaN", "global::System.Half.NaN")]
    [InlineData("Infinity", "global::System.Half.PositiveInfinity")]
    [InlineData("-Infinity", "global::System.Half.NegativeInfinity")]
    public void Core_Should_EmitHalfSpecialValue_AsConstant(string value, string expression)
    {
        var generated = GetCore(Single("Half", value));

        ShouldContainField(generated, "OrderId", "Sentinel", expression);
    }

    /// <summary>
    /// D4：<c>(65504, 65520)</c> 会舍入回最大有限值（实测 <c>65519.9 → 65504</c>），不是溢出。
    /// 拿 65504 当上限会把这类合法文本误判成非法。
    /// </summary>
    [Fact]
    public void Core_Should_AcceptHalf_WhenValueRoundsToMaxFinite()
    {
        var generated = GetCore(Single("Half", "65510"));

        ShouldContainField(generated, "OrderId", "Sentinel", "(global::System.Half)(65510F)");
    }

    /// <summary>
    /// 超出 <c>Half</c> 范围的有限值会被静默转成无穷，必须挡掉。
    /// </summary>
    [Fact]
    public void Core_Should_SkipHalf_WhenValueExceedsRange()
    {
        var generated = GetCore(Single("Half", "70000", "TooBig"));

        generated.Should().NotContain("TooBig");
    }

    #endregion

    #region 文本、字符、布尔

    [Fact]
    public void Core_Should_EmitStringLiteral_WithEscaping()
    {
        var generated = GetCore(Single("string", "a\\\"b"));

        ShouldContainField(generated, "OrderId", "Sentinel", "\"a\\\"b\"");
    }

    /// <summary>
    /// 空串是 <c>string</c> 基元的合法取值：<c>Parse</c> 的「非空」条件只约束读入路径。
    /// </summary>
    [Fact]
    public void Core_Should_EmitEmptyStringLiteral()
    {
        var generated = GetCore(Single("string", "", "Blank"));

        ShouldContainField(generated, "OrderId", "Blank", "\"\"");
    }

    [Fact]
    public void Core_Should_EmitCharLiteral()
    {
        var generated = GetCore(Single("char", "A"));

        ShouldContainField(generated, "OrderId", "Sentinel", "'A'");
    }

    /// <summary>
    /// <c>char</c> 的合法文本就是「长度恰为 1」，与生成器里 <c>TryParse</c> 的判定同源。
    /// </summary>
    [Fact]
    public void Core_Should_SkipChar_WhenTextLengthIsNotOne()
    {
        var generated = GetCore(Single("char", "AB", "TooLong"));

        generated.Should().NotContain("TooLong");
    }

    #endregion

    #region Guid 与时间

    [Fact]
    public void Core_Should_EmitGuidLiteral()
    {
        var generated = GetCore(Single("Guid", "00000000-0000-0000-0000-000000000000", "Empty"));

        ShouldContainField(
            generated,
            "OrderId",
            "Empty",
            "new global::System.Guid(\"00000000-0000-0000-0000-000000000000\")");
    }

    /// <summary>
    /// <c>DateTime</c> 必须连 <c>Kind</c> 一起发射：只发 ticks 会把文本里的 <c>Z</c> 静默折成
    /// <see cref="DateTimeKind.Unspecified"/>，往返后不再相等。
    /// </summary>
    [Fact]
    public void Core_Should_EmitDateTime_WithKind()
    {
        var generated = GetCore(Single("DateTime", "1970-01-01T00:00:00Z", "Epoch"));

        ShouldContainField(
            generated,
            "OrderId",
            "Epoch",
            "new global::System.DateTime(621355968000000000L, global::System.DateTimeKind.Utc)");
    }

    /// <summary>
    /// <c>DateTimeOffset</c> 必须带偏移：只发 ticks 会把 <c>+08:00</c> 折成 UTC。
    /// </summary>
    [Fact]
    public void Core_Should_EmitDateTimeOffset_WithOffset()
    {
        var generated = GetCore(Single("DateTimeOffset", "2020-01-01T00:00:00+08:00", "PlusEight"));

        ShouldContainField(
            generated,
            "OrderId",
            "PlusEight",
            "new global::System.DateTimeOffset(637134336000000000L, new global::System.TimeSpan(288000000000L))");
    }

    [Fact]
    public void Core_Should_EmitTimeSpan_FromTicks()
    {
        var generated = GetCore(Single("TimeSpan", "01:02:03", "Duration"));

        ShouldContainField(generated, "OrderId", "Duration", "new global::System.TimeSpan(37230000000L)");
    }

    /// <summary>
    /// <c>DateOnly</c> 只接受规范形 <c>yyyy-MM-dd</c>，且必须校验真实日历（2021-02-29 不存在）。
    /// </summary>
    [Fact]
    public void Core_Should_EmitDateOnly_AndRejectImpossibleDate()
    {
        GetCore(Single("DateOnly", "2020-02-29", "Leap"))
            .Should().Contain("= Create(new global::System.DateOnly(2020, 2, 29));");

        GetCore(Single("DateOnly", "2021-02-29", "Impossible")).Should().NotContain("Impossible");
    }

    /// <summary>
    /// <c>TimeOnly</c> 的小数部分按 100ns 刻度右补零：<c>01:02:03.5</c> 是 3723 秒又半秒。
    /// </summary>
    [Fact]
    public void Core_Should_EmitTimeOnly_FromTicks()
    {
        var generated = GetCore(Single("TimeOnly", "01:02:03.5", "HalfPast"));

        ShouldContainField(generated, "OrderId", "HalfPast", "new global::System.TimeOnly(37235000000L)");
    }

    #endregion

    #region 大整数

    /// <summary>
    /// <c>Int128</c> / <c>UInt128</c> / <c>BigInteger</c> 在 netstandard2.0 上不存在，无法在编译期真正解析，
    /// 故生成运行期 <c>Parse</c>（入参已在编译期校验过）。
    /// </summary>
    [Fact]
    public void Core_Should_EmitInt128_AsRuntimeParse()
    {
        var generated = GetCore(Single("Int128", "170141183460469231731687303715884105727", "Max"));

        ShouldContainField(
            generated,
            "OrderId",
            "Max",
            "global::System.Int128.Parse(\"170141183460469231731687303715884105727\", global::System.Globalization.CultureInfo.InvariantCulture)");
    }

    [Fact]
    public void Core_Should_EmitUInt128_AsRuntimeParse()
    {
        var generated = GetCore(Single("UInt128", "340282366920938463463374607431768211455", "Max"));

        ShouldContainField(
            generated,
            "OrderId",
            "Max",
            "global::System.UInt128.Parse(\"340282366920938463463374607431768211455\", global::System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// 归一化会去掉前导零与正号，保证同一取值只有一种文本形。
    /// </summary>
    [Fact]
    public void Core_Should_NormalizeBigIntegerText()
    {
        var generated = GetCore(Single("System.Numerics.BigInteger", "+007", "Seven"));

        ShouldContainField(
            generated,
            "OrderId",
            "Seven",
            "global::System.Numerics.BigInteger.Parse(\"7\", global::System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// 128 位边界必须按位宽校验：<c>Int128.MaxValue + 1</c> 越界。
    /// </summary>
    [Fact]
    public void Core_Should_SkipInt128_WhenValueExceeds128Bits()
    {
        var generated = GetCore(Single("Int128", "170141183460469231731687303715884105728", "TooBig"));

        generated.Should().NotContain("TooBig");
    }

    #endregion

    #region 枚举

    [Fact]
    public void Core_Should_EmitEnumMember_AsUnderlyingValue()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public enum Status
            {
                Active,
                Suspended,
            }

            [StronglyTypedId]
            [StronglyTypedIdInstance("Active", "Active")]
            public partial record struct OrderId(Status Value);
            """;

        ShouldContainField(GetCore(code), "OrderId", "Active", "(global::Len.StronglyTypedId.Tests.Status)(0)");
    }

    /// <summary>
    /// <c>[Flags]</c> 枚举接受逗号分隔的位组合，按位或后发射。
    /// </summary>
    [Fact]
    public void Core_Should_EmitFlagsCombination_AsBitwiseOr()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
            }

            [StronglyTypedId]
            [StronglyTypedIdInstance("ReadWrite", "Read, Write")]
            public partial record struct OrderId(Access Value);
            """;

        ShouldContainField(GetCore(code), "OrderId", "ReadWrite", "(global::Len.StronglyTypedId.Tests.Access)(3)");
    }

    /// <summary>
    /// 非 <c>[Flags]</c> 枚举必须恰好命中某个成员：<c>3</c> 没有对应成员，与 <c>Enum.IsDefined</c> 同源。
    /// </summary>
    [Fact]
    public void Core_Should_SkipEnum_WhenValueIsUndefined()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public enum Status
            {
                Active,
                Suspended,
            }

            [StronglyTypedId]
            [StronglyTypedIdInstance("Weird", "3")]
            public partial record struct OrderId(Status Value);
            """;

        GetCore(code).Should().NotContain("Weird");
    }

    /// <summary>
    /// <c>[Flags]</c> 枚举允许未命名的位组合，但不允许出现未定义的位。
    /// </summary>
    [Fact]
    public void Core_Should_RejectUndefinedFlagBits()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
                Write = 2,
            }

            [StronglyTypedId]
            [StronglyTypedIdInstance("Bad", "8")]
            public partial record struct OrderId(Access Value);
            """;

        GetCore(code).Should().NotContain("Bad");
    }

    #endregion

    #region 成员形态与回归护栏

    /// <summary>
    /// 成员形态：<c>public static readonly</c> 字段 + 文档注释 + 两个生成标记特性，且排在 <c>Create</c> 之后。
    /// </summary>
    [Fact]
    public void Core_Should_EmitDocumentedStaticReadonlyField_AfterCreate()
    {
        var generated = GetCore(Single("int", "-1", "Unspecified"));

        var createIndex = generated.IndexOf("public static OrderId Create(", StringComparison.Ordinal);
        var fieldIndex = generated.IndexOf("public static readonly OrderId Unspecified", StringComparison.Ordinal);

        createIndex.Should().BeGreaterThan(0);
        fieldIndex.Should().BeGreaterThan(createIndex);

        generated.Should().Contain("    /// 获取 <c>OrderId</c> 的 <c>Unspecified</c> 实例。");
        generated.Should().Contain("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"StronglyTypedIdCodeGenerator\", ");
    }

    /// <summary>
    /// 多条实例保持声明顺序。
    /// </summary>
    [Fact]
    public void Core_Should_PreserveDeclarationOrder()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("First", "1")]
            [StronglyTypedIdInstance("Second", "2")]
            [StronglyTypedIdInstance("Third", "3")]
            public partial record struct OrderId(int Value);
            """;

        var generated = GetCore(code);

        generated.IndexOf("OrderId First", StringComparison.Ordinal)
            .Should().BeLessThan(generated.IndexOf("OrderId Second", StringComparison.Ordinal));
        generated.IndexOf("OrderId Second", StringComparison.Ordinal)
            .Should().BeLessThan(generated.IndexOf("OrderId Third", StringComparison.Ordinal));
    }

    /// <summary>
    /// 重名时先声明者胜出，后者既不生成也不影响前者。
    /// </summary>
    [Fact]
    public void Core_Should_KeepFirstDeclaration_WhenNamesCollide()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("Same", "1")]
            [StronglyTypedIdInstance("Same", "2")]
            public partial record struct OrderId(int Value);
            """;

        var generated = GetCore(code);

        generated.Should().Contain("= Create((int)(1));");
        generated.Should().NotContain("= Create((int)(2));");
    }

    /// <summary>
    /// 引用类型 Id 会额外生成私有的 <c>CompareValues</c> 比较辅助方法，实例取这个名字会撞 CS0102，
    /// 故整条跳过（分析器另报 STIA0020）。
    /// </summary>
    [Fact]
    public void Core_Should_SkipInstance_WhenNameCollidesWithGeneratedComparisonHelper()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("CompareValues", "0")]
            public partial record OrderId(int Value);
            """;

        var generated = GetCore(code);

        generated.Should().NotContain("public static readonly OrderId CompareValues");
        // 辅助方法本身照常产出，证明「跳过实例」不是因为整套比较逻辑都没生成。
        generated.Should().Contain("private static int CompareValues(OrderId? left, OrderId? right)");
    }

    /// <summary>
    /// 回归护栏：未声明实例时产物里不得出现任何实例成员，保证「不加特性 = 逐字节不变」。
    /// </summary>
    [Fact]
    public void Core_Should_OmitInstances_WhenNoneDeclared()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(int Value);
            """;

        var generated = GetCore(code);

        generated.Should().NotContain("public static readonly");
        generated.Should().NotContain("instance of <c>");
    }

    /// <summary>
    /// 前缀只影响文本形：实例声明的是基元值，<c>ToString()</c> 照常带前缀。
    /// </summary>
    [Fact]
    public void Core_Should_EmitPrimitiveValue_WhenIdHasPrefix()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "ord_")]
            [StronglyTypedIdInstance("Empty", "00000000-0000-0000-0000-000000000000")]
            public partial record struct OrderId(Guid Value);
            """;

        var generated = GetCore(code);

        ShouldContainField(
            generated,
            "OrderId",
            "Empty",
            "new global::System.Guid(\"00000000-0000-0000-0000-000000000000\")");
        generated.Should().NotContain("= Create(\"ord_");
    }

    /// <summary>
    /// 归一化在 <c>Create</c> 内部发生，故发射的仍是声明的原始文本。
    /// </summary>
    [Fact]
    public void Core_Should_EmitRawText_WhenIdHasNormalizer()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            [StronglyTypedIdInstance("Upper", "abc")]
            public partial record struct OrderId(string Value)
            {
                private static string Normalize(string value) => value.ToUpperInvariant();
            }
            """;

        ShouldContainField(GetCore(code), "OrderId", "Upper", "\"abc\"");
    }

    #endregion
}
