// xUnit1051：本文件中的 CSharpCompilation.Create 等为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="SupportedPrimitiveTypes.IsSupported"/> 的单元测试。
/// </summary>
public class SupportedPrimitiveTypesTests
{
    #region BCL 基元类型

    [Theory]
    [InlineData(SpecialType.System_String)]
    [InlineData(SpecialType.System_Char)]
    [InlineData(SpecialType.System_Byte)]
    [InlineData(SpecialType.System_SByte)]
    [InlineData(SpecialType.System_Int16)]
    [InlineData(SpecialType.System_Int32)]
    [InlineData(SpecialType.System_Int64)]
    [InlineData(SpecialType.System_UInt16)]
    [InlineData(SpecialType.System_UInt32)]
    [InlineData(SpecialType.System_UInt64)]
    public void IsSupported_Should_ReturnTrue_ForBclSpecialType(SpecialType specialType)
    {
        var type = _emptyCompilation.GetSpecialType(specialType);

        SupportedPrimitiveTypes.IsSupported(type).Should().BeTrue();
    }

    /// <summary>
    /// <see cref="System.Guid"/> 不是 C# 的 <see cref="SpecialType"/>，走的是「具名类型 + 命名空间」这一支。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnTrue_ForGuid()
    {
        var type = _emptyCompilation.GetTypeByMetadataName("System.Guid");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeTrue();
    }

    /// <summary>
    /// 时间型、decimal、扩展整型、浮点、大整数等新基元（非 C# 的 <see cref="SpecialType"/>，走「具名类型 + 命名空间」分支）。
    /// </summary>
    [Theory]
    [InlineData("System.DateTime")]
    [InlineData("System.DateTimeOffset")]
    [InlineData("System.TimeSpan")]
    [InlineData("System.Decimal")]
    [InlineData("System.DateOnly")]
    [InlineData("System.TimeOnly")]
    [InlineData("System.Single")]
    [InlineData("System.Double")]
    [InlineData("System.Half")]
    [InlineData("System.Int128")]
    [InlineData("System.UInt128")]
    [InlineData("System.Numerics.BigInteger")]
    public void IsSupported_Should_ReturnTrue_ForExtendedBclType(string metadataName)
    {
        var type = _emptyCompilation.GetTypeByMetadataName(metadataName);

        type.Should().NotBeNull($"测试前提：{metadataName} 必须可被合成编译解析");
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeTrue();
    }

    /// <summary>
    /// 枚举（无论命名空间、无论底层类型）一律支持：强类型 Id 的基元可以是任意枚举。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnTrue_ForEnum()
    {
        var compilation = CreateCompilation("""
            namespace Probe.Fake;

            public enum Status
            {
                Active,
                Inactive
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.Fake.Status");

        type.Should().NotBeNull("测试前提：枚举必须可被合成编译解析");
        type!.TypeKind.Should().Be(TypeKind.Enum);
        SupportedPrimitiveTypes.IsSupported(type).Should().BeTrue();
    }

    #endregion

    #region 不受支持的 BCL 类型

    [Theory]
    [InlineData(SpecialType.System_Boolean)]
    [InlineData(SpecialType.System_Object)]
    public void IsSupported_Should_ReturnFalse_ForUnsupportedBclType(SpecialType specialType)
    {
        var type = _emptyCompilation.GetSpecialType(specialType);

        SupportedPrimitiveTypes.IsSupported(type).Should().BeFalse();
    }

    #endregion

    #region UTF-8 解析须有公开的 TryParse 重载

    /// <summary>
    /// 生成代码按具体类型名调用 <c>TryParse(ReadOnlySpan&lt;byte&gt;, IFormatProvider, out T)</c>，故只有暴露了该公开重载的基元才生成
    /// UTF-8 解析。数值型 / <c>decimal</c> 都有；时间型没有 UTF-8 语义（连 <c>IUtf8SpanParsable</c> 都不实现），<c>BigInteger</c>
    /// 则把它实现为显式接口实现，两者都拿不到公开重载，故都不生成。
    /// </summary>
    [Theory]
    [InlineData("System.Int32", true)]
    [InlineData("System.Decimal", true)]
    [InlineData("System.Int128", true)]
    [InlineData("System.Double", true)]
    [InlineData("System.Numerics.BigInteger", false)]
    [InlineData("System.DateTime", false)]
    [InlineData("System.DateOnly", false)]
    public void HasPublicStaticTryParse_Should_DetectUtf8Overload(string metadataName, bool expected)
    {
        var type = _emptyCompilation.GetTypeByMetadataName(metadataName);

        type.Should().NotBeNull($"测试前提：{metadataName} 必须可被合成编译解析");
        StronglyTypedIdInfo.HasPublicStaticTryParse(type!, SpecialType.System_Byte).Should().Be(expected,
            $"{metadataName} 的公开 UTF-8 TryParse 判定应为 {expected}");
    }

    /// <summary>
    /// <c>Guid</c> 的公开 UTF-8 <c>TryParse</c> 自 .NET 10 起才有：net8.0 消费者下判定为假，生成产物因此不含 UTF-8
    /// 解析，同一份源码在两个目标框架下的产物本就允许不同。
    /// </summary>
    [Fact]
    public void HasPublicStaticTryParse_Should_DependOnTargetFramework_ForGuid()
    {
        var type = _emptyCompilation.GetTypeByMetadataName("System.Guid");

        type.Should().NotBeNull("测试前提：System.Guid 必须可被合成编译解析");
#if NET10_0_OR_GREATER
        StronglyTypedIdInfo.HasPublicStaticTryParse(type!, SpecialType.System_Byte).Should().BeTrue(
            "net10.0 下 Guid 暴露了公开的 UTF-8 TryParse");
#else
        StronglyTypedIdInfo.HasPublicStaticTryParse(type!, SpecialType.System_Byte).Should().BeFalse(
            "net8.0 下 Guid 尚无公开的 UTF-8 TryParse");
#endif
    }

    /// <summary>
    /// 使用者自定义的 <c>Probe.Fake.Double</c> 与 BCL <c>System.Double</c> 同名，命名空间校验必须拦截它。
    /// </summary>
    [Fact]
    public void HasPublicStaticTryParse_Should_ReturnFalse_ForUserDefinedSameNameType()
    {
        var compilation = CreateCompilation("""
            namespace Probe.Fake;

            public struct Double
            {
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.Fake.Double");

        type.Should().NotBeNull("测试前提：自定义同名类型必须可被合成编译解析");
        StronglyTypedIdInfo.HasPublicStaticTryParse(type!, SpecialType.System_Byte).Should().BeFalse(
            "自定义的同名 Double 不该被误判为具备公开的 UTF-8 TryParse");
    }

    #endregion

    #region 缺陷回归：与 BCL 基元同名、但不属于 System 命名空间的类型

    [Theory]
    [InlineData("Guid")]
    [InlineData("Int32")]
    [InlineData("String")]
    public void IsSupported_Should_ReturnFalse_ForUserDefinedTypeWithSameSimpleName(string simpleName)
    {
        var compilation = CreateCompilation($$"""
            namespace Probe.Fake;

            public struct {{simpleName}}
            {
            }
            """);

        var type = compilation.GetTypeByMetadataName($"Probe.Fake.{simpleName}");

        type.Should().NotBeNull($"测试前提：{simpleName} 必须是具名类型且与 BCL 基元同名");
        type!.Name.Should().Be(simpleName);
        SupportedPrimitiveTypes.IsSupported(type).Should().BeFalse();
    }

    /// <summary>
    /// 非具名类型（数组等）既没有 <c>ContainingNamespace</c>，也不可能承载强类型 Id。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnFalse_ForArrayType()
    {
        var type = _emptyCompilation.CreateArrayTypeSymbol(_emptyCompilation.GetSpecialType(SpecialType.System_Int32));

        SupportedPrimitiveTypes.IsSupported(type).Should().BeFalse();
    }

    #endregion

    #region 自定义基元（N12）

    /// <summary>
    /// 满足契约的自定义类型是合法基元，且 <see cref="SupportedPrimitiveTypes.IsCustomPrimitive"/> 为真。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnTrue_ForCustomPrimitive()
    {
        var type = GetCustomPrimitiveType();

        SupportedPrimitiveTypes.IsSupported(type).Should().BeTrue();
        SupportedPrimitiveTypes.IsCustomPrimitive(type).Should().BeTrue();
        SupportedPrimitiveTypes.IsBuiltIn((INamedTypeSymbol)type).Should().BeFalse();
    }

    /// <summary>
    /// 内置基元与枚举不得被当成自定义基元：它们由前两段判定接管，产物走各自既有的分支。
    /// </summary>
    [Theory]
    [InlineData("System.Guid")]
    [InlineData("System.Int32")]
    [InlineData("System.String")]
    public void IsCustomPrimitive_Should_ReturnFalse_ForBuiltInPrimitive(string metadataName)
    {
        var type = _emptyCompilation.GetTypeByMetadataName(metadataName);

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.IsCustomPrimitive(type!).Should().BeFalse();
    }

    /// <summary>
    /// 只实现泛型 <c>IComparable&lt;T&gt;</c> 不够：<c>IStronglyTypedId&lt;,&gt;</c> 的约束是非泛型
    /// <c>IComparable</c>，缺了它会 CS0315。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnFalse_WhenNonGenericComparableMissing()
    {
        var compilation = CreateCompilation("""
            using System;

            namespace Probe;

            public readonly record struct NoNonGeneric(string Value) : IParsable<NoNonGeneric>, IComparable<NoNonGeneric>
            {
                public static NoNonGeneric Parse(string s, IFormatProvider? provider) => new(s);

                public static bool TryParse(string? s, IFormatProvider? provider, out NoNonGeneric result)
                {
                    result = new NoNonGeneric(s ?? string.Empty);
                    return s is not null;
                }

                public int CompareTo(NoNonGeneric other) => 0;
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.NoNonGeneric");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeFalse();
        SupportedPrimitiveTypes.DescribeMissingContracts(type!).Should().Contain("non-generic `System.IComparable`");
    }

    /// <summary>
    /// 显式接口实现的 <c>Parse</c> 按类型名直呼会 CS1501，故不算满足契约。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnFalse_WhenParseIsExplicitImplementation()
    {
        var compilation = CreateCompilation("""
            using System;

            namespace Probe;

            public readonly record struct ExplicitImpl(string Value)
                : IParsable<ExplicitImpl>, IComparable<ExplicitImpl>, IComparable
            {
                static ExplicitImpl IParsable<ExplicitImpl>.Parse(string s, IFormatProvider? provider) => new(s);

                static bool IParsable<ExplicitImpl>.TryParse(string? s, IFormatProvider? provider, out ExplicitImpl result)
                {
                    result = new ExplicitImpl(s ?? string.Empty);
                    return s is not null;
                }

                public int CompareTo(ExplicitImpl other) => 0;

                public int CompareTo(object? obj) => 0;
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.ExplicitImpl");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeFalse();
        SupportedPrimitiveTypes.DescribeMissingContracts(type!).Should().Contain("does not expose `public static ExplicitImpl Parse");
    }

    /// <summary>
    /// <c>System.Uri</c> 满足契约但相等语义与 Id 冲突，必须显式排除并给出专门解释。
    /// </summary>
    /// <remarks>
    /// 在源码里声明一个同名类型不够：判定按 <c>System.Uri</c> 的完整显示名匹配，必须真的引用到 BCL 类型。
    /// <c>System.Uri</c> 在 <c>System.Private.Uri.dll</c> 而非 CoreLib，故单独构造一份带该引用的编译。
    /// </remarks>
    [Fact]
    public void IsSupported_Should_ReturnFalse_ForUri()
    {
        var compilation = CSharpCompilation.Create(
            "ProbeUri",
            [CSharpSyntaxTree.ParseText("namespace Probe { public class Marker { } }")],
            [
                AssemblyReferenceCache.FromAssembly(typeof(object).Assembly),
                AssemblyReferenceCache.FromAssembly(typeof(Uri).Assembly),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var type = compilation.GetTypeByMetadataName("System.Uri");

        type.Should().NotBeNull("测试前提：System.Uri 必须能被解析成符号");
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeFalse();
        SupportedPrimitiveTypes.DescribeMissingContracts(type!).Should().StartWith("System.Uri is not supported");
    }

    /// <summary>
    /// 开放泛型无法在生成代码里写 <c>TPrimitive.Parse(...)</c>。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnFalse_ForOpenGenericCustomPrimitive()
    {
        var compilation = CreateCompilation("""
            using System;

            namespace Probe;

            public readonly record struct Box<T>(T Value)
                : IParsable<Box<T>>, IComparable<Box<T>>, IComparable
                where T : notnull
            {
                public static Box<T> Parse(string s, IFormatProvider? provider) => new(default!);

                public static bool TryParse(string? s, IFormatProvider? provider, out Box<T> result)
                {
                    result = new Box<T>(default!);
                    return true;
                }

                public int CompareTo(Box<T> other) => 0;

                public int CompareTo(object? obj) => 0;
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.Box`1");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeFalse();
    }

    /// <summary>
    /// 抽象类无法实例化，不算合法基元。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnFalse_ForAbstractCustomPrimitive()
    {
        var compilation = CreateCompilation("""
            using System;

            namespace Probe;

            public abstract record class Base(string Value)
                : IParsable<Base>, IComparable<Base>, IComparable
            {
                public static Base Parse(string s, IFormatProvider? provider) => throw new NotSupportedException();

                public static bool TryParse(string? s, IFormatProvider? provider, out Base result)
                {
                    result = null!;
                    return false;
                }

                public int CompareTo(Base other) => 0;

                public int CompareTo(object? obj) => 0;
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.Base");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeFalse();
        SupportedPrimitiveTypes.DescribeMissingContracts(type!).Should().Be("It is abstract or static.");
    }

    /// <summary>
    /// 派生类不满足「类型实参就是自身」：<c>AllInterfaces</c> 里是 <c>IParsable&lt;Base&gt;</c>，
    /// 按它生成会让 <c>Derived.Parse</c> 返回 <c>Base</c> 而编译不过。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnFalse_ForDerivedCustomPrimitive()
    {
        var compilation = CreateCompilation("""
            using System;

            namespace Probe;

            public record class Base(string Value) : IParsable<Base>, IComparable<Base>, IComparable
            {
                public static Base Parse(string s, IFormatProvider? provider) => new(s);

                public static bool TryParse(string? s, IFormatProvider? provider, out Base result)
                {
                    result = new Base(s ?? string.Empty);
                    return s is not null;
                }

                public int CompareTo(Base other) => 0;

                public int CompareTo(object? obj) => 0;
            }

            public record class Derived(string Value) : Base(Value);
            """);

        var type = compilation.GetTypeByMetadataName("Probe.Derived");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeFalse();
    }

    /// <summary>
    /// 非具名类型（数组）没有可描述的契约缺口，返回空串而不是抛异常：
    /// 这条路径跑在 STIA0008 的消息渲染上。
    /// </summary>
    [Fact]
    public void DescribeMissingContracts_Should_ReturnEmpty_ForNonNamedType()
    {
        var arrayType = _emptyCompilation.CreateArrayTypeSymbol(_emptyCompilation.GetSpecialType(SpecialType.System_Int32));

        SupportedPrimitiveTypes.DescribeMissingContracts(arrayType).Should().BeEmpty();
    }

    /// <summary>
    /// 开放泛型无法在生成代码里写 <c>TPrimitive.Parse(...)</c>，必须给出专门说明。
    /// </summary>
    [Fact]
    public void DescribeMissingContracts_Should_ExplainOpenGeneric()
    {
        var compilation = CreateCompilation("""
            using System;

            namespace Probe;

            public readonly record struct Box<T>(T Value)
                : IParsable<Box<T>>, IComparable<Box<T>>, IComparable
                where T : notnull
            {
                public static Box<T> Parse(string s, IFormatProvider? provider) => new(default!);

                public static bool TryParse(string? s, IFormatProvider? provider, out Box<T> result)
                {
                    result = new Box<T>(default!);
                    return true;
                }

                public int CompareTo(Box<T> other) => 0;

                public int CompareTo(object? obj) => 0;
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.Box`1");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.DescribeMissingContracts(type!).Should().Be("It is an open generic type.");
    }

    /// <summary>
    /// <c>Nullable&lt;T&gt;</c> 不能作为基元。它带类型参数，因此先命中「开放泛型」那条判据 ——
    /// 两条判据的结论都是「不支持」，只是文案不同。这里锁住实际文案，避免日后调整判据顺序时无人察觉。
    /// </summary>
    [Fact]
    public void DescribeMissingContracts_Should_RejectNullable()
    {
        var nullable = _emptyCompilation.GetSpecialType(SpecialType.System_Nullable_T)
            .Construct(_emptyCompilation.GetSpecialType(SpecialType.System_Int32));

        SupportedPrimitiveTypes.IsSupported(nullable).Should().BeFalse();
        SupportedPrimitiveTypes.DescribeMissingContracts(nullable).Should().Be("It is an open generic type.");
    }

    /// <summary>
    /// 非 struct / class（接口、枚举、委托）不属于可承载的基元形状。
    /// </summary>
    [Fact]
    public void DescribeMissingContracts_Should_ExplainNonStructOrClass()
    {
        var compilation = CreateCompilation("""
            namespace Probe;

            public interface IMarker
            {
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.IMarker");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.DescribeMissingContracts(type!).Should().Be("It is not a struct or class.");
    }

    /// <summary>
    /// 取测试用的自定义基元类型符号。
    /// </summary>
    private static ITypeSymbol GetCustomPrimitiveType()
    {
        var compilation = CreateCompilation("""
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
            """);

        var type = compilation.GetTypeByMetadataName("Probe.Email");

        type.Should().NotBeNull("测试前提：自定义基元必须能被解析成符号");

        return type!;
    }

    #endregion

    #region 辅助

    private static readonly CSharpCompilation _emptyCompilation = CreateCompilation("namespace Probe { public class Marker { } }");

    private static CSharpCompilation CreateCompilation(string sourceCode)
        => CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            [
                AssemblyReferenceCache.FromAssembly(typeof(object).Assembly),
                // BigInteger 不在 CoreLib，而在 System.Runtime.Numerics；扩展基元测试需要解析它。
                AssemblyReferenceCache.FromAssembly(typeof(System.Numerics.BigInteger).Assembly),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    #endregion
}
