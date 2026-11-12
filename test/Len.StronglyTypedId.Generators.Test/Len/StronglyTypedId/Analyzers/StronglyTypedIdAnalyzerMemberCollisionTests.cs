using Microsoft.CodeAnalysis.Testing;

namespace Len.StronglyTypedId.Analyzers;

/// <summary>
/// 用户自己声明的成员与生成器产出的成员撞名（STIA0029）。
/// </summary>
/// <remarks>
/// 撞名的后果是 CS0111（方法）或 CS0102（字段 / 嵌套类型），而编译器只把错误指向生成文件 ——
/// 用户既不能编辑那个文件，也不知道该改自己哪一行。类型名撞名已有 STIA0027，成员名撞名同样需要一条
/// 落在用户声明上的诊断。
/// <para>
/// 判据必须与生成器同源且按条件判定：<c>TryFormat</c> 只在基元可 span 格式化时生成、
/// <c>ToString(string, IFormatProvider)</c> 只在基元实现 <c>IFormattable</c> 时生成、
/// <c>CompareValues</c> 只在引用类型 Id 上生成。无条件判定会把「本来不存在的撞名」误报成 Error。
/// </para>
/// </remarks>
public class StronglyTypedIdAnalyzerMemberCollisionTests
{
    /// <summary>
    /// 无条件产出的成员：用户写同签名方法必须报告 STIA0029。
    /// </summary>
    [Theory]
    [InlineData("public static OrderId Create(Guid value) => new(value);", "Create")]
    [InlineData("public void Deconstruct(out Guid value) => value = Value;", "Deconstruct")]
    [InlineData("public int CompareTo(OrderId other) => 0;", "CompareTo")]
    [InlineData("public override string ToString() => \"custom\";", "ToString")]
    [InlineData("public static OrderId Parse(string value, IFormatProvider? provider) => default;", "Parse")]
    [InlineData("public static bool TryParse(string? value, IFormatProvider? provider, out OrderId result) { result = default; return false; }", "TryParse")]
    [InlineData("public static bool TryCreate(Guid value, out OrderId result) { result = default; return false; }", "TryCreate")]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenMemberCollidesWithUnconditionalGeneratedMember(string member, string name)
    {
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                {{member}}
            }
            """";
        // 方法符号的声明位置是标识符本身，不是整行声明。
        var column = 5 + member.IndexOf(name, StringComparison.Ordinal);
        var expected = Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
            .WithArguments(name, "OrderId")
            .WithSpan(8, column, 8, column + name.Length);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 比较运算符在元数据里叫 <c>op_LessThan</c> 这类名字，撞名同样要报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenComparisonOperatorIsDeclared()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                public static bool operator <(OrderId left, OrderId right) => true;

                public static bool operator >(OrderId left, OrderId right) => true;
            }
            """";
        // 运算符的声明位置落在运算符符号本身（`<` / `>`），不是整行声明。
        var expected = new[]
        {
            Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
                .WithArguments("op_LessThan", "OrderId")
                .WithSpan(8, 33, 8, 34),
            Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
                .WithArguments("op_GreaterThan", "OrderId")
                .WithSpan(10, 33, 10, 34),
        };
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>ToString(string, IFormatProvider)</c> 只在基元实现 <c>IFormattable</c> 时生成。
    /// </summary>
    /// <remarks>
    /// <c>Guid</c> 实现 <c>IFormattable</c>，故同签名方法会撞名。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenFormattableToStringIsDeclared()
    {
        const string Member = "public string ToString(string? format, IFormatProvider? provider) => Value.ToString(format, provider);";
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                {{Member}}
            }
            """";
        var column = 5 + Member.IndexOf("ToString", StringComparison.Ordinal);
        var expected = Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
            .WithArguments("ToString", "OrderId")
            .WithSpan(8, column, 8, column + "ToString".Length);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>TryFormat</c> 只在基元可 span 格式化时生成；<c>string</c> 基元不生成，同签名方法合法。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenStringPrimitiveDeclaresTryFormat()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct StringId(string Value)
            {
                public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                    => Value.TryFormat(destination, out charsWritten, format, provider);
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 可 span 格式化的基元上 <c>TryFormat</c> 确实生成，同签名方法必须报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenSpanFormattableDeclaresTryFormat()
    {
        const string Member = "public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => Value.TryFormat(destination, out charsWritten, format, provider);";
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                {{Member}}
            }
            """";
        var column = 5 + Member.IndexOf("TryFormat", StringComparison.Ordinal);
        var expected = Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
            .WithArguments("TryFormat", "OrderId")
            .WithSpan(8, column, 8, column + "TryFormat".Length);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>CompareValues</c> 只在引用类型 Id 上生成；值类型 Id 上同签名方法合法。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValueTypeDeclaresCompareValues()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                private static int CompareValues(OrderId left, OrderId right) => 0;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 引用类型 Id 上 <c>CompareValues</c> 确实生成，同签名方法必须报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenReferenceTypeDeclaresCompareValues()
    {
        const string Member = "private static int CompareValues(OrderId? left, OrderId? right) => 0;";
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value)
            {
                {{Member}}
            }
            """";
        var column = 5 + Member.IndexOf("CompareValues", StringComparison.Ordinal);
        var expected = Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
            .WithArguments("CompareValues", "OrderId")
            .WithSpan(8, column, 8, column + "CompareValues".Length);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 参数表不同的同名方法不撞名（C# 允许重载），不报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenSameNameHasDifferentSignature()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                public static OrderId Create(int value) => new(Guid.Empty);

                public int CompareTo(string other) => 0;

                public void Deconstruct(out Guid value, out string text)
                {
                    value = Value;
                    text = string.Empty;
                }
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 基元不受支持时生成器不产出任何成员，撞名无从谈起（那种写法由 STIA0008 报出），不报告 STIA0029。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenPrimitiveIsUnsupported()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(bool Value)
            {
                public static OrderId Create(bool value) => new(value);
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithArguments("bool", "It does not expose `public static Boolean Parse(string, IFormatProvider)`, does not expose `public static bool TryParse(string, IFormatProvider, out Boolean)`.")
            .WithSpan(6, 38, 6, 42);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 最小合法 Id（没有手写成员）不报告 STIA0029。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNoUserMembers()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    #region 非方法成员与漏掉的重载

    /// <summary>
    /// <c>Parse(ReadOnlySpan&lt;char&gt;, IFormatProvider)</c> 是无条件产出的重载，与它同签名必须报告。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾只比 <c>string</c> 与 <c>ReadOnlySpan&lt;byte&gt;</c> 两个重载，漏了 char-span 那个，
    /// 于是这条 CS0111 只能由编译器指向生成文件。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenCharSpanParseIsDeclared()
    {
        const string Member = "public static OrderId Parse(ReadOnlySpan<char> value, IFormatProvider? provider) => default;";
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                {{Member}}
            }
            """";
        var column = 5 + Member.IndexOf("Parse", StringComparison.Ordinal);
        var expected = Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
            .WithArguments("Parse", "OrderId")
            .WithSpan(8, column, 8, column + "Parse".Length);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 字段 / 属性 / 事件与生成方法同名同样撞 CS0102，必须报告。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：分析器曾只遍历 <c>IMethodSymbol</c>，这三类成员被整个过滤掉。
    /// 非方法成员只比名字：它们与生成成员共用一个声明空间，同名即冲突，与参数表无关。
    /// </remarks>
    [Theory]
    [InlineData("public static readonly int Create = 1;")]
    [InlineData("public static int Create => 1;")]
    [InlineData("public static event Action? Create;")]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNonMethodMemberCollidesWithGeneratedMember(string member)
    {
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                {{member}}
            }
            """";
        var column = 5 + member.IndexOf("Create", StringComparison.Ordinal);
        var expected = Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
            .WithArguments("Create", "OrderId")
            .WithSpan(8, column, 8, column + "Create".Length);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 用户声明的嵌套类型与生成的嵌套类型同名时撞 CS0102，必须报告。
    /// </summary>
    /// <remarks>
    /// <c>OrderIdDebugView</c> 无条件生成（非泛型容器），<c>OrderIdTypeConverter</c> 仅在
    /// <c>TypeConverter = true</c> 时生成，故后者显式打开开关。
    /// </remarks>
    [Theory]
    [InlineData("public sealed class OrderIdDebugView { }", "OrderIdDebugView", false)]
    [InlineData("public sealed class OrderIdTypeConverter { }", "OrderIdTypeConverter", true)]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNestedTypeCollidesWithGeneratedNestedType(
        string member,
        string name,
        bool typeConverter)
    {
        var attribute = typeConverter ? "[StronglyTypedId(TypeConverter = true)]" : "[StronglyTypedId]";
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            {{attribute}}
            public partial record struct OrderId(Guid Value)
            {
                {{member}}
            }
            """";
        var column = 5 + member.IndexOf(name, StringComparison.Ordinal);
        var expected = Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
            .WithArguments(name, "OrderId")
            .WithSpan(8, column, 8, column + name.Length);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 自定义基元 + 命名实例时生成器产出私有 <c>CreateInstance(string, string)</c>，同签名必须报告。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾只认 <see cref="GeneratedMemberNames.Core"/> 里的公开成员，漏了这个私有工厂。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenCustomPrimitiveDeclaresCreateInstance()
    {
        const string Member = "private static OrderId CreateInstance(string name, string text) => default;";
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            public readonly record struct Email(string Value) : IParsable<Email>, IComparable<Email>, IComparable
            {
                public static Email Parse(string s, IFormatProvider? provider) => new(s);

                public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
                {
                    result = new(s ?? string.Empty);
                    return s is not null;
                }

                public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

                public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();
            }

            [StronglyTypedId]
            [StronglyTypedIdInstance("Unknown", "unknown@example.com")]
            public partial record struct OrderId(Email Value)
            {
                {{Member}}
            }
            """";
        var column = 5 + Member.IndexOf("CreateInstance", StringComparison.Ordinal);
        var expected = Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
            .WithArguments("CreateInstance", "OrderId")
            .WithSpan(24, column, 24, column + "CreateInstance".Length);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    #endregion

    #region 合法重载（不报告）

    /// <summary>
    /// <c>record struct</c> 上的 <c>OrderId?</c> 是 <c>Nullable&lt;OrderId&gt;</c>，与生成的
    /// <c>CompareTo(OrderId)</c> 是不同类型，属合法重载。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾无条件剥掉末尾 <c>?</c>，把值类型的 <c>Nullable&lt;T&gt;</c> 当成可空标注，
    /// 于是这条能编译的写法被报成 Error。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValueTypeIdDeclaresNullableCompareTo()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                public int CompareTo(OrderId? other) => 0;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>Create(Guid?)</c> 的 <c>Guid?</c> 是 <c>Nullable&lt;Guid&gt;</c>，与生成的 <c>Create(Guid)</c> 是合法重载。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNullablePrimitiveCreateIsDeclared()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                public static OrderId Create(Guid? value) => default;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 泛型元数不同是合法重载：<c>Create&lt;T&gt;(Guid)</c> 与生成的 <c>Create(Guid)</c> 可以并存。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾只比参数表，不看类型参数个数。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenGenericArityDiffers()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                public static OrderId Create<T>(Guid value) => default;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>in</c> 修饰符与按值传递是不同签名，不撞名。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenParameterIsInModifier()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                public static OrderId Create(in Guid value) => default;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>out OrderId?</c> 与生成的 <c>out OrderId</c> 会让生成代码里的 <c>out var</c> 产生 CS0121 二义性，
    /// 必须报告。
    /// </summary>
    /// <remarks>
    /// 与按值传递的 <c>Nullable&lt;T&gt;</c> 相反：<c>out</c> 位置两个候选都能被 <c>out var</c> 推断，
    /// 编译器无法择优。故 <c>out</c> / <c>ref</c> 位置仍剥掉可空标注再比。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenOutParameterIsNullableValueType()
    {
        const string Member = "public static bool TryCreate(Guid value, out OrderId? result) { result = null; return false; }";
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                {{Member}}
            }
            """";
        var column = 5 + Member.IndexOf("TryCreate", StringComparison.Ordinal);
        var expected = Verify.Diagnostic(Descriptors.MemberCollidesWithGeneratedMember)
            .WithArguments("TryCreate", "OrderId")
            .WithSpan(8, column, 8, column + "TryCreate".Length);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    #endregion
}
