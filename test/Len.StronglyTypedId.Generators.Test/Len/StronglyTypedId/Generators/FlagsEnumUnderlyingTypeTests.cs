// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <c>[Flags]</c> 枚举的「未定义位掩码」按底层类型宽度裁剪的契约。
/// </summary>
/// <remarks>
/// 掩码由全部成员常量按位或取反得出，必须按底层宽度裁剪：<c>sbyte</c> 的 <c>-1</c> 在 <see cref="ulong"/> 里是满位，不裁剪会把掩码整个吃掉。
/// 每种底层类型都要单独验证：<c>GetIntegralBitWidth</c> 与 <c>ToBits</c> 各有一张分派表，漏一个分支的表现是某种底层类型的 <c>[Flags]</c> 永远拒绝合法位组合。
/// </remarks>
public class FlagsEnumUnderlyingTypeTests
{
    #region 辅助：驱动生成器并捕获核心产物

    private static CSharpCompilation CreateCompilation(string sourceCode)
    {
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
            "Len.StronglyTypedId.FlagsEnum.Test",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static Dictionary<string, string> GetGeneratedCode(string sourceCode)
    {
        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(CreateCompilation(sourceCode))
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return result.Results[0].GeneratedSources
            .ToDictionary(source => source.HintName, source => source.SourceText.ToString());
    }

    /// <summary>
    /// 排除会触发其它集成生成器的门控程序集，避免本用例被它们带偏。
    /// </summary>
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
            or "Dapper"
            or "MessagePack"
            or "MongoDB.Bson");

    private static string Source(string underlyingType, string members)
        => $$"""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [Flags]
            public enum Access : {{underlyingType}}
            {
            {{members}}
            }

            [StronglyTypedId]
            public partial record struct AccessId(Access Value);
            """;

    private static string GetCore(string sourceCode)
        => GetGeneratedCode(sourceCode)
            .Single(pair => pair.Key.EndsWith(".AccessId.g.cs", StringComparison.Ordinal))
            .Value;

    #endregion

    #region 掩码按底层宽度裁剪

    /// <summary>
    /// <c>A = 1, B = 2</c> 只占用低两位，其余位都未定义 —— 掩码就是「宽度内除低两位外全 1」。
    /// </summary>
    [Theory]
    [InlineData("byte", "0x00000000000000FCUL")]
    [InlineData("sbyte", "0x00000000000000FCUL")]
    [InlineData("short", "0x000000000000FFFCUL")]
    [InlineData("ushort", "0x000000000000FFFCUL")]
    [InlineData("uint", "0x00000000FFFFFFFCUL")]
    [InlineData("ulong", "0xFFFFFFFFFFFFFFFCUL")]
    public void FlagsMask_Should_BeClippedToUnderlyingWidth(string underlyingType, string expectedMask)
    {
        var generated = GetCore(Source(underlyingType, "    None = 0,\n    Read = 1,\n    Write = 2,"));

        generated.Should().Contain($"(unchecked((ulong)val) & {expectedMask}) == 0UL");
    }

    /// <summary>
    /// <c>All = -1</c> 意味着宽度内每一位都有定义，掩码为 0 —— 此时必须整条省掉位校验，
    /// 产物与「加入该能力之前」逐字节一致，否则所有取值都会被拒。
    /// </summary>
    /// <remarks>
    /// 无符号底层类型不能写 <c>-1</c>（CS0031），只能写该宽度的全 1 值。
    /// </remarks>
    [Theory]
    [InlineData("sbyte", "-1")]
    [InlineData("byte", "255")]
    [InlineData("short", "-1")]
    [InlineData("uint", "4294967295")]
    [InlineData("ulong", "18446744073709551615")]
    public void FlagsMask_Should_BeOmitted_WhenEveryBitIsDefined(string underlyingType, string allValue)
    {
        var generated = GetCore(Source(underlyingType, $"    All = {allValue},"));

        generated.Should().NotContain("unchecked((ulong)val)");
    }

    /// <summary>
    /// 负值成员按补码展开后由宽度掩码裁掉高位：<c>High = -128</c> 在 <c>sbyte</c> 里是 <c>0x80</c>，
    /// 与 <c>Low = 1</c> 合起来只占两位，掩码应为 <c>0x7E</c>。
    /// </summary>
    [Fact]
    public void FlagsMask_Should_ExpandNegativeMemberAsTwosComplement()
    {
        var generated = GetCore(Source("sbyte", "    Low = 1,\n    High = -128,"));

        generated.Should().Contain("(unchecked((ulong)val) & 0x000000000000007EUL) == 0UL");
    }

    #endregion
}
