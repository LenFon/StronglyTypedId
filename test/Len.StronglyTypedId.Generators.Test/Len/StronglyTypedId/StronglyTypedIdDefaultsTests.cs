// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="StronglyTypedIdDefaults"/> 的单元测试：未声明程序集默认时的「全空」形态，
/// 以及从程序集读取九项默认值的路径。
/// </summary>
/// <remarks>
/// <see cref="StronglyTypedIdDefaults.Empty"/> 是「没有程序集默认」时的哨兵值。它的每一项都必须是
/// 「未指定」而不是某个具体默认值 —— 否则逐类型未写该参数时会被程序集默认悄悄接管。
/// </remarks>
public class StronglyTypedIdDefaultsTests
{
    #region Empty：九项全部为「未指定」

    [Fact]
    public void Empty_Should_HaveNoDefaults()
    {
        var empty = StronglyTypedIdDefaults.Empty;

        empty.ValidatorName.Should().BeNull();
        empty.NormalizerName.Should().BeNull();
        empty.ExceptionTypeName.Should().BeNull();
        empty.Integrations.Should().BeNull();
        empty.JsonShape.Should().BeNull();
        empty.PrefixReadMode.Should().BeNull();
        empty.RedactValidationMessage.Should().BeFalse();
        empty.TypeConverter.Should().BeFalse();
    }

    /// <summary>
    /// 未声明任何程序集默认时，<see cref="StronglyTypedIdDefaults.Read"/> 必须返回与
    /// <see cref="StronglyTypedIdDefaults.Empty"/> 等价的值。
    /// </summary>
    [Fact]
    public void Read_Should_ReturnEmpty_WhenAssemblyHasNoDefaultsAttribute()
    {
        var assembly = CreateCompilation("namespace Probe { public class Marker { } }").Assembly;

        var defaults = StronglyTypedIdDefaults.Read(assembly);

        defaults.ValidatorName.Should().BeNull();
        defaults.Integrations.Should().BeNull();
        defaults.JsonShape.Should().BeNull();
        defaults.PrefixReadMode.Should().BeNull();
        defaults.RedactValidationMessage.Should().BeFalse();
        defaults.TypeConverter.Should().BeFalse();
    }

    #endregion

    #region Read：九项逐项读取

    [Fact]
    public void Read_Should_ReadEveryArgument_WhenDefaultsAttributeIsPresent()
    {
        var assembly = CreateCompilation("""
            using Len.StronglyTypedId;

            [assembly: StronglyTypedIdDefaults(
                Validator = "Validate",
                Normalizer = "Normalize",
                ExceptionType = typeof(System.InvalidOperationException),
                Integrations = StronglyTypedIdIntegrations.Dapper,
                RedactValidationMessage = true,
                JsonShape = JsonShape.Object,
                TypeConverter = true,
                PrefixReadMode = PrefixReadMode.Strict)]

            namespace Probe
            {
                public static class Rules
                {
                    public static bool Validate(int value) => true;

                    public static int Normalize(int value) => value;
                }
            }
            """).Assembly;

        var defaults = StronglyTypedIdDefaults.Read(assembly);

        defaults.ValidatorName.Should().Be("Validate");
        defaults.NormalizerName.Should().Be("Normalize");
        defaults.ExceptionTypeName.Should().Be("global::System.InvalidOperationException");
        defaults.Integrations.Should().Be(IntegrationFlags.Dapper);
        defaults.RedactValidationMessage.Should().BeTrue();
        defaults.JsonShape.Should().NotBeNull();
        defaults.TypeConverter.Should().BeTrue();
        defaults.PrefixReadMode.Should().NotBeNull();
    }

    /// <summary>
    /// 显式 <c>Integrations = Default</c>（0）归一为 <see cref="IntegrationFlags.All"/>，
    /// 与「没写」区分开：前者是「跟随引用」，后者是「回退到逐类型」。
    /// </summary>
    [Fact]
    public void Read_Should_NormalizeExplicitDefaultIntegrationsToAll()
    {
        var assembly = CreateCompilation("""
            using Len.StronglyTypedId;

            [assembly: StronglyTypedIdDefaults(Integrations = StronglyTypedIdIntegrations.Default)]

            namespace Probe
            {
                public class Marker
                {
                }
            }
            """).Assembly;

        StronglyTypedIdDefaults.Read(assembly).Integrations.Should().Be(IntegrationFlags.All);
    }

    /// <summary>
    /// <see cref="StronglyTypedIdDefaults.ReadExceptionTypeSymbol"/> 与 <see cref="StronglyTypedIdDefaults.Read"/>
    /// 读的是同一个实参，但返回符号而非显示名：分析器要校验它是不是异常类型。
    /// </summary>
    [Fact]
    public void ReadExceptionTypeSymbol_Should_ReturnSymbol_WhenConfigured()
    {
        var assembly = CreateCompilation("""
            using Len.StronglyTypedId;

            [assembly: StronglyTypedIdDefaults(ExceptionType = typeof(System.InvalidOperationException))]

            namespace Probe
            {
                public class Marker
                {
                }
            }
            """).Assembly;

        var symbol = StronglyTypedIdDefaults.ReadExceptionTypeSymbol(assembly);

        symbol.Should().NotBeNull();
        symbol!.ToDisplayString().Should().Be("System.InvalidOperationException");
    }

    [Fact]
    public void ReadExceptionTypeSymbol_Should_ReturnNull_WhenNotConfigured()
    {
        var assembly = CreateCompilation("namespace Probe { public class Marker { } }").Assembly;

        StronglyTypedIdDefaults.ReadExceptionTypeSymbol(assembly).Should().BeNull();
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 合成编译的引用集取 AppDomain 全集：程序集级特性要用到 <c>System.Runtime</c> 里的
    /// <c>Attribute</c> / <c>Enum</c>，只引 <c>object</c> 所在程序集会 CS0012。
    /// </summary>
    private static CSharpCompilation CreateCompilation(string sourceCode)
        => CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            [.. AppDomain.CurrentDomain.GetAssemblies()
                .Union([typeof(object).Assembly, typeof(StronglyTypedIdAttribute).Assembly])
                .Distinct()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                .Select(AssemblyReferenceCache.FromAssembly)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    #endregion
}
