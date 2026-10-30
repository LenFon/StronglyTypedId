// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="InstanceValueEmitter.TryEmit"/> 的单元测试：命名实例的文本值如何在编译期被翻译成表达式，
/// 以及非法文本如何被逐条拒绝。
/// </summary>
/// <remarks>
/// 生成器层用例（<c>InstanceCodeGeneratorTests</c>）只断言「某条非法值被跳过」，覆盖不到「为什么被跳过」。
/// 这里直接打底层函数，把每个校验分支单独钉住 —— 判据放宽的后果是把非法文本发射成编译不过的字面量，
/// 而漏判的后果更隐蔽：编译通过、运行期 <c>Parse</c> 才抛。
/// </remarks>
public class InstanceValueEmitterTests
{
    #region 整型：合法与越界

    [Theory]
    [InlineData("byte", "255", "(byte)(255)")]
    [InlineData("sbyte", "-128", "(sbyte)(-128)")]
    [InlineData("short", "-32768", "(short)(-32768)")]
    [InlineData("ushort", "65535", "(ushort)(65535)")]
    [InlineData("int", "-2147483648", "(int)(-2147483648)")]
    [InlineData("uint", "4294967295", "(uint)(4294967295)")]
    [InlineData("long", "-9223372036854775808", "(long)(-9223372036854775808)")]
    [InlineData("ulong", "18446744073709551615", "(ulong)(18446744073709551615)")]
    public void TryEmit_Should_TranslateIntegral(string primitive, string text, string expected)
    {
        InstanceValueEmitter.TryEmit(GetPrimitive(primitive), text, out var expression).Should().BeTrue();

        expression.Should().Be(expected);
    }

    /// <summary>
    /// 越界值必须被拒：发射出去就是编译错误（<c>(byte)(256)</c> 是 CS0221）。
    /// </summary>
    [Theory]
    [InlineData("byte", "256")]
    [InlineData("sbyte", "128")]
    [InlineData("short", "40000")]
    [InlineData("ushort", "-1")]
    [InlineData("uint", "-1")]
    [InlineData("ulong", "-1")]
    public void TryEmit_Should_RejectOutOfRangeIntegral(string primitive, string text)
        => InstanceValueEmitter.TryEmit(GetPrimitive(primitive), text, out _).Should().BeFalse();

    /// <summary>
    /// 千位分隔符与小数不属于整型文本：<c>NumberStyles.Integer</c> 挡掉它们，与 <c>Parse</c> 的语义一致。
    /// </summary>
    [Theory]
    [InlineData("int", "1,000")]
    [InlineData("int", "1.5")]
    [InlineData("int", "abc")]
    public void TryEmit_Should_RejectNonIntegralText(string primitive, string text)
        => InstanceValueEmitter.TryEmit(GetPrimitive(primitive), text, out _).Should().BeFalse();

    #endregion

    #region 浮点与 decimal

    [Theory]
    [InlineData("float", "1.5", "1.5F")]
    [InlineData("double", "1.5", "1.5D")]
    [InlineData("decimal", "1.5", "1.5M")]
    public void TryEmit_Should_TranslateFloatingPoint(string primitive, string text, string expected)
    {
        InstanceValueEmitter.TryEmit(GetPrimitive(primitive), text, out var expression).Should().BeTrue();

        expression.Should().Be(expected);
    }

    /// <summary>
    /// <c>NaN</c> 与 <c>±Infinity</c> 是浮点的合法取值，必须发射成对应常量而不是字面量文本。
    /// </summary>
    [Theory]
    [InlineData("float", "NaN", "global::System.Single.NaN")]
    [InlineData("float", "Infinity", "global::System.Single.PositiveInfinity")]
    [InlineData("float", "-Infinity", "global::System.Single.NegativeInfinity")]
    [InlineData("double", "NaN", "global::System.Double.NaN")]
    [InlineData("double", "Infinity", "global::System.Double.PositiveInfinity")]
    [InlineData("double", "-Infinity", "global::System.Double.NegativeInfinity")]
    public void TryEmit_Should_EmitSpecialFloatingPointConstants(string primitive, string text, string expected)
    {
        InstanceValueEmitter.TryEmit(GetPrimitive(primitive), text, out var expression).Should().BeTrue();

        expression.Should().Be(expected);
    }

    [Theory]
    [InlineData("float", "abc")]
    [InlineData("double", "abc")]
    [InlineData("decimal", "abc")]
    public void TryEmit_Should_RejectUnparseableFloatingPoint(string primitive, string text)
        => InstanceValueEmitter.TryEmit(GetPrimitive(primitive), text, out _).Should().BeFalse();

    #endregion

    #region 文本与字符

    [Fact]
    public void TryEmit_Should_TranslateStringWithEscaping()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("string"), "a\"b", out var expression).Should().BeTrue();

        expression.Should().Be("\"a\\\"b\"");
    }

    [Fact]
    public void TryEmit_Should_TranslateSingleChar()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("char"), "A", out var expression).Should().BeTrue();

        expression.Should().Be("'A'");
    }

    [Theory]
    [InlineData("")]
    [InlineData("AB")]
    public void TryEmit_Should_RejectCharWhoseLengthIsNotOne(string text)
        => InstanceValueEmitter.TryEmit(GetPrimitive("char"), text, out _).Should().BeFalse();

    #endregion

    #region Guid 与时间

    [Fact]
    public void TryEmit_Should_TranslateGuid()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("Guid"), "00000000-0000-0000-0000-000000000000", out var expression)
            .Should().BeTrue();

        expression.Should().Be("new global::System.Guid(\"00000000-0000-0000-0000-000000000000\")");
    }

    [Fact]
    public void TryEmit_Should_RejectInvalidGuid()
        => InstanceValueEmitter.TryEmit(GetPrimitive("Guid"), "not-a-guid", out _).Should().BeFalse();

    [Fact]
    public void TryEmit_Should_TranslateDateTimeWithKind()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("DateTime"), "1970-01-01T00:00:00Z", out var expression)
            .Should().BeTrue();

        expression.Should().Be("new global::System.DateTime(621355968000000000L, global::System.DateTimeKind.Utc)");
    }

    [Fact]
    public void TryEmit_Should_RejectInvalidDateTime()
        => InstanceValueEmitter.TryEmit(GetPrimitive("DateTime"), "not-a-date", out _).Should().BeFalse();

    [Fact]
    public void TryEmit_Should_TranslateDateTimeOffsetWithOffset()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("DateTimeOffset"), "2020-01-01T00:00:00+08:00", out var expression)
            .Should().BeTrue();

        expression.Should().Be("new global::System.DateTimeOffset(637134336000000000L, new global::System.TimeSpan(288000000000L))");
    }

    [Fact]
    public void TryEmit_Should_RejectInvalidDateTimeOffset()
        => InstanceValueEmitter.TryEmit(GetPrimitive("DateTimeOffset"), "not-a-date", out _).Should().BeFalse();

    [Fact]
    public void TryEmit_Should_TranslateTimeSpan()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("TimeSpan"), "01:02:03", out var expression).Should().BeTrue();

        expression.Should().Be("new global::System.TimeSpan(37230000000L)");
    }

    [Fact]
    public void TryEmit_Should_RejectInvalidTimeSpan()
        => InstanceValueEmitter.TryEmit(GetPrimitive("TimeSpan"), "not-a-span", out _).Should().BeFalse();

    #endregion

    #region DateOnly：规范形与真实日历

    [Fact]
    public void TryEmit_Should_TranslateDateOnly()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("DateOnly"), "2020-02-29", out var expression).Should().BeTrue();

        expression.Should().Be("new global::System.DateOnly(2020, 2, 29)");
    }

    /// <summary>
    /// 非规范形（位数不足、分隔符位置不对、非数字）一律拒绝：<c>DateOnly.Parse</c> 也接受
    /// <c>2020-1-1</c>，但实例声明必须只有一种文本形。
    /// </summary>
    [Theory]
    [InlineData("2020-1-1")]
    [InlineData("2020/01/01")]
    [InlineData("20200101")]
    [InlineData("2020-13-01")]
    [InlineData("2020-00-01")]
    [InlineData("2020-01-00")]
    [InlineData("2021-02-29")]
    [InlineData("0000-01-01")]
    public void TryEmit_Should_RejectNonCanonicalDateOnly(string text)
        => InstanceValueEmitter.TryEmit(GetPrimitive("DateOnly"), text, out _).Should().BeFalse();

    #endregion

    #region TimeOnly：HH:mm 与小数刻度

    [Theory]
    [InlineData("01:02", "new global::System.TimeOnly(37200000000L)")]
    [InlineData("01:02:03", "new global::System.TimeOnly(37230000000L)")]
    [InlineData("01:02:03.5", "new global::System.TimeOnly(37235000000L)")]
    public void TryEmit_Should_TranslateTimeOnly(string text, string expected)
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("TimeOnly"), text, out var expression).Should().BeTrue();

        expression.Should().Be(expected);
    }

    [Theory]
    [InlineData("1:02")]
    [InlineData("24:00")]
    [InlineData("01:60")]
    [InlineData("01:02:60")]
    [InlineData("01:02:03.")]
    [InlineData("01:02:03.12345678")]
    [InlineData("01-02-03")]
    public void TryEmit_Should_RejectInvalidTimeOnly(string text)
        => InstanceValueEmitter.TryEmit(GetPrimitive("TimeOnly"), text, out _).Should().BeFalse();

    #endregion

    #region Half：量程与特殊值

    [Fact]
    public void TryEmit_Should_TranslateHalf()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("Half"), "1.5", out var expression).Should().BeTrue();

        expression.Should().Be("(global::System.Half)(1.5F)");
    }

    [Theory]
    [InlineData("NaN", "global::System.Half.NaN")]
    [InlineData("Infinity", "global::System.Half.PositiveInfinity")]
    [InlineData("-Infinity", "global::System.Half.NegativeInfinity")]
    public void TryEmit_Should_EmitHalfSpecialConstants(string text, string expected)
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("Half"), text, out var expression).Should().BeTrue();

        expression.Should().Be(expected);
    }

    /// <summary>
    /// 溢出阈值是舍入中点 65520 而不是最大有限值 65504：<c>65510</c> 会舍入回 65504，仍属合法。
    /// </summary>
    [Theory]
    [InlineData("65510", true)]
    [InlineData("65520", false)]
    [InlineData("70000", false)]
    public void TryEmit_Should_ApplyHalfRoundingThreshold(string text, bool expected)
        => InstanceValueEmitter.TryEmit(GetPrimitive("Half"), text, out _).Should().Be(expected);

    [Fact]
    public void TryEmit_Should_RejectUnparseableHalf()
        => InstanceValueEmitter.TryEmit(GetPrimitive("Half"), "abc", out _).Should().BeFalse();

    #endregion

    #region 128 位与大整数：归一化与位宽

    [Theory]
    [InlineData("Int128", "170141183460469231731687303715884105727", true)]
    [InlineData("Int128", "170141183460469231731687303715884105728", false)]
    [InlineData("UInt128", "340282366920938463463374607431768211455", true)]
    [InlineData("UInt128", "340282366920938463463374607431768211456", false)]
    public void TryEmit_Should_Enforce128BitRange(string primitive, string text, bool expected)
        => InstanceValueEmitter.TryEmit(GetPrimitive(primitive), text, out _).Should().Be(expected);

    /// <summary>
    /// 归一化去掉前导零与正号，保证同一取值只有一种文本形。
    /// </summary>
    [Fact]
    public void TryEmit_Should_NormalizeBigIntegerText()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("System.Numerics.BigInteger"), "+007", out var expression)
            .Should().BeTrue();

        expression.Should().Be("global::System.Numerics.BigInteger.Parse(\"7\", global::System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// 全零文本归一成 <c>"0"</c>，而不是空串。
    /// </summary>
    [Fact]
    public void TryEmit_Should_NormalizeAllZeroTextToZero()
    {
        InstanceValueEmitter.TryEmit(GetPrimitive("System.Numerics.BigInteger"), "0000", out var expression)
            .Should().BeTrue();

        expression.Should().Be("global::System.Numerics.BigInteger.Parse(\"0\", global::System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// 无符号基元拒绝负号：<c>UInt128.Parse("-1")</c> 在运行期抛，必须在编译期挡掉。
    /// </summary>
    [Fact]
    public void TryEmit_Should_RejectNegativeForUnsignedBigIntegerLike()
        => InstanceValueEmitter.TryEmit(GetPrimitive("UInt128"), "-1", out _).Should().BeFalse();

    /// <summary>
    /// 只有符号没有数字、含非数字字符、含空白都拒绝。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("+")]
    [InlineData("1a")]
    [InlineData(" 1")]
    [InlineData("1 000")]
    public void TryEmit_Should_RejectMalformedBigIntegerText(string text)
        => InstanceValueEmitter.TryEmit(GetPrimitive("System.Numerics.BigInteger"), text, out _).Should().BeFalse();

    #endregion

    #region 枚举：成员名、数值、位组合

    [Fact]
    public void TryEmit_Should_TranslateEnumMemberName()
    {
        InstanceValueEmitter.TryEmit(GetEnum("Access", "int"), "Read", out var expression).Should().BeTrue();

        expression.Should().Be("(global::Probe.Access)(1)");
    }

    [Fact]
    public void TryEmit_Should_TranslateEnumNumeric()
    {
        InstanceValueEmitter.TryEmit(GetEnum("Access", "int"), "2", out var expression).Should().BeTrue();

        expression.Should().Be("(global::Probe.Access)(2)");
    }

    /// <summary>
    /// <c>[Flags]</c> 枚举接受逗号分隔的位组合，按位或后发射。
    /// </summary>
    [Fact]
    public void TryEmit_Should_TranslateFlagsCombination()
    {
        InstanceValueEmitter.TryEmit(GetEnum("Access", "int", flags: true), "Read, Write", out var expression)
            .Should().BeTrue();

        expression.Should().Be("(global::Probe.Access)(3)");
    }

    /// <summary>
    /// 非 <c>[Flags]</c> 枚举必须恰好命中某个成员：<c>3</c> 没有对应成员。
    /// </summary>
    [Fact]
    public void TryEmit_Should_RejectUndefinedEnumValue()
        => InstanceValueEmitter.TryEmit(GetEnum("Access", "int"), "3", out _).Should().BeFalse();

    /// <summary>
    /// <c>[Flags]</c> 枚举允许未命名的位组合，但不允许出现未定义的位。
    /// </summary>
    [Fact]
    public void TryEmit_Should_RejectUndefinedFlagBits()
        => InstanceValueEmitter.TryEmit(GetEnum("Access", "int", flags: true), "8", out _).Should().BeFalse();

    /// <summary>
    /// 逗号组合里出现空段（<c>"Read,,Write"</c>、尾随逗号）必须拒绝，而不是静默跳过。
    /// </summary>
    [Theory]
    [InlineData("Read,,Write")]
    [InlineData("Read,")]
    [InlineData(",Read")]
    public void TryEmit_Should_RejectEmptyEnumSegment(string text)
        => InstanceValueEmitter.TryEmit(GetEnum("Access", "int", flags: true), text, out _).Should().BeFalse();

    /// <summary>
    /// 无法解析成数值、且不是成员名的文本必须拒绝。
    /// </summary>
    [Fact]
    public void TryEmit_Should_RejectUnknownEnumText()
        => InstanceValueEmitter.TryEmit(GetEnum("Access", "int"), "Bogus", out _).Should().BeFalse();

    /// <summary>
    /// 有符号底层类型的数值必须落在该宽度内：<c>sbyte</c> 枚举的 <c>128</c> 越界。
    /// </summary>
    [Theory]
    [InlineData("sbyte", "128")]
    [InlineData("sbyte", "-129")]
    [InlineData("byte", "256")]
    [InlineData("byte", "-1")]
    public void TryEmit_Should_RejectEnumNumericOutsideUnderlyingRange(string underlying, string text)
        => InstanceValueEmitter.TryEmit(GetEnum("Access", underlying), text, out _).Should().BeFalse();

    /// <summary>
    /// 无符号底层类型不接受负号。
    /// </summary>
    [Fact]
    public void TryEmit_Should_RejectNegativeEnumNumericForUnsignedUnderlying()
        => InstanceValueEmitter.TryEmit(GetEnum("Access", "ulong"), "-1", out _).Should().BeFalse();

    /// <summary>
    /// 无符号底层类型超出宽度同样拒绝。
    /// </summary>
    [Fact]
    public void TryEmit_Should_RejectEnumNumericAboveUnsignedWidth()
        => InstanceValueEmitter.TryEmit(GetEnum("Access", "byte"), "256", out _).Should().BeFalse();

    /// <summary>
    /// 有符号 64 位枚举的边界值必须按 <see cref="long"/> 解析，不能按 32 位截断。
    /// </summary>
    [Fact]
    public void TryEmit_Should_TranslateSigned64BitEnumBoundary()
    {
        var type = GetEnum("Access", "long", extraMember: "Min = -9223372036854775808");

        InstanceValueEmitter.TryEmit(type, "-9223372036854775808", out var expression).Should().BeTrue();

        expression.Should().Be("(global::Probe.Access)(-9223372036854775808)");
    }

    #endregion

    #region 自定义基元与不支持的类型

    /// <summary>
    /// 自定义基元编译期不做值校验，只把原文搬进产物，由运行期 <c>Parse</c> 还原。
    /// </summary>
    [Fact]
    public void TryEmit_Should_DelegateCustomPrimitiveToRuntimeParse()
    {
        var compilation = CreateCompilation(CustomPrimitiveSource);
        var type = compilation.GetTypeByMetadataName("Probe.Email");

        type.Should().NotBeNull();

        InstanceValueEmitter.TryEmit(type!, "a@b.c", out var expression).Should().BeTrue();

        expression.Should().Be("global::Probe.Email.Parse(\"a@b.c\", global::System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// 非具名类型（数组）既不是枚举也没有 <c>SpecialType</c>，必须安全返回 <see langword="false"/>。
    /// </summary>
    [Fact]
    public void TryEmit_Should_ReturnFalse_ForNonNamedType()
    {
        var compilation = CreateCompilation("namespace Probe { public class Marker { } }");
        var arrayType = compilation.CreateArrayTypeSymbol(compilation.GetSpecialType(SpecialType.System_Int32));

        InstanceValueEmitter.TryEmit(arrayType, "1", out _).Should().BeFalse();
    }

    /// <summary>
    /// 既不在 <c>System</c> / <c>System.Numerics</c> 下、也不满足自定义基元契约的类型，无法翻译。
    /// </summary>
    [Fact]
    public void TryEmit_Should_ReturnFalse_ForUnsupportedNamedType()
    {
        var compilation = CreateCompilation("namespace Probe { public struct Plain { } }");
        var type = compilation.GetTypeByMetadataName("Probe.Plain");

        type.Should().NotBeNull();

        InstanceValueEmitter.TryEmit(type!, "1", out _).Should().BeFalse();
    }

    #endregion

    #region 辅助

    private static ITypeSymbol GetPrimitive(string primitive)
    {
        // 通过一份声明了该基元的源码取符号：特型基元与 BCL 具名类型都能被解析。
        var compilation = CreateCompilation($$"""
            using System;

            namespace Probe;

            public partial record struct OrderId({{primitive}} Value);
            """);

        var idType = compilation.GetTypeByMetadataName("Probe.OrderId");

        idType.Should().NotBeNull("测试前提：OrderId 必须能被解析成符号");

        return idType!.InstanceConstructors
            .SelectMany(constructor => constructor.Parameters)
            .First(parameter => !SymbolEqualityComparer.Default.Equals(parameter.Type, idType))
            .Type;
    }

    private static ITypeSymbol GetEnum(string name, string underlying, bool flags = false, string? extraMember = null)
    {
        var attribute = flags ? "[System.Flags]" : string.Empty;
        var extra = extraMember is null ? string.Empty : $",\n    {extraMember}";

        var compilation = CreateCompilation($$"""
            namespace Probe;

            {{attribute}}
            public enum {{name}} : {{underlying}}
            {
                None = 0,
                Read = 1,
                Write = 2{{extra}},
            }
            """);

        var type = compilation.GetTypeByMetadataName($"Probe.{name}");

        type.Should().NotBeNull("测试前提：枚举必须能被解析成符号");

        return type!;
    }

    private static CSharpCompilation CreateCompilation(string sourceCode)
        => CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            [
                AssemblyReferenceCache.FromAssembly(typeof(object).Assembly),
                AssemblyReferenceCache.FromAssembly(typeof(System.Numerics.BigInteger).Assembly),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private const string CustomPrimitiveSource = """
        using System;

        namespace Probe;

        public readonly record struct Email(string Value) : IParsable<Email>, IComparable<Email>, IComparable
        {
            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
            {
                result = new Email(s ?? string.Empty);
                return s is not null;
            }

            public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

            public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();
        }
        """;

    #endregion
}
