// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="DapperCodeGenerator"/> 的枚举基元分支。
/// </summary>
/// <remarks>
/// 枚举的 <c>Parse</c> 有三条路径（直通 / 文本 / 数值），每条都要做「已定义」校验。
/// 校验条件由 <see cref="StronglyTypedIdInfo.GetEnumValidityCondition"/> 给出，它在两种情况下返回
/// <see langword="null"/>：非 <c>[Flags]</c> 枚举走 <c>Enum.IsDefined</c>，而「全部位都已定义」的
/// <c>[Flags]</c> 枚举没有可校验的位。后者必须直接返回、不产出抛错分支，否则 <c>return</c> 之后的
/// <c>throw</c> 是不可达代码（CS0162）。
/// </remarks>
public class DapperCodeGeneratorTests
{
    #region 普通枚举：三条路径都追加 IsDefined 校验

    [Fact]
    public void Parse_Should_GuardEveryPath_ForPlainEnum()
    {
        var handler = GetHandler("""
            public enum Status { Active, Suspended }
            """);

        // 直通路径、文本路径、数值路径各一处校验。
        handler.Should().Contain("if (global::System.Enum.IsDefined(typeof(global::Probe.Status), primitiveValue))");
        handler.Should().Contain("&& global::System.Enum.IsDefined(typeof(global::Probe.Status), enumValue)");
        handler.Should().Contain("if (global::System.Enum.IsDefined(typeof(global::Probe.Status), enumValue))");
    }

    #endregion

    #region [Flags] 枚举：未定义位掩码

    [Fact]
    public void Parse_Should_UseUndefinedBitsMask_ForFlagsEnum()
    {
        var handler = GetHandler("""
            [System.Flags]
            public enum Access { None = 0, Read = 1, Write = 2 }
            """);

        // 默认底层类型是 int，掩码按 32 位裁剪。
        handler.Should().Contain("(unchecked((ulong)primitiveValue) & 0x00000000FFFFFFFCUL) == 0UL");
        handler.Should().Contain("(unchecked((ulong)enumValue) & 0x00000000FFFFFFFCUL) == 0UL");
    }

    /// <summary>
    /// 全部位都已定义的 <c>[Flags]</c> 枚举没有可校验的位：三条路径都必须直接返回，
    /// 不产出抛错分支（否则是不可达代码 CS0162）。
    /// </summary>
    [Fact]
    public void Parse_Should_ReturnDirectly_WhenFlagsEnumDefinesEveryBit()
    {
        var handler = GetHandler("""
            [System.Flags]
            public enum Access : byte { All = 0xFF }
            """);

        handler.Should().NotContain("& 0x");
        handler.Should().Contain("return global::Probe.OrderId.Create(primitiveValue);");
        handler.Should().Contain("return global::Probe.OrderId.Create(enumValue);");
    }

    #endregion

    #region 辅助

    private static string GetHandler(string enumDeclaration)
    {
        var source = $$"""
            using System;
            using Len.StronglyTypedId;

            namespace Probe;

            {{enumDeclaration}}

            [StronglyTypedId]
            public partial record struct OrderId({{EnumName(enumDeclaration)}} Value);
            """;

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union([typeof(StronglyTypedIdAttribute).Assembly])
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Where(assembly => assembly.GetName().Name is not
                ("Len.StronglyTypedId.TestBase"
                or "Microsoft.EntityFrameworkCore"
                or "Swashbuckle.AspNetCore.SwaggerGen"
                or "Microsoft.OpenApi"
                or "Microsoft.AspNetCore.Mvc"
                or "Microsoft.AspNetCore.Mvc.Core"
                or "Microsoft.AspNetCore.Mvc.Abstractions"
                or "Microsoft.AspNetCore.OpenApi"
                or "Microsoft.AspNetCore.Routing"
                or "MessagePack"
                or "MongoDB.Bson"))
            .Select(AssemblyReferenceCache.FromAssembly)
            .Append(SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return result.Results[0].GeneratedSources
            .Single(source => source.HintName == "Probe.OrderId.Dapper.g.cs")
            .SourceText.ToString();
    }

    /// <summary>
    /// 从枚举声明里取枚举名：声明形如 <c>public enum Status { ... }</c>。
    /// </summary>
    private static string EnumName(string declaration)
        => declaration[(declaration.IndexOf("enum ", StringComparison.Ordinal) + 5)..]
            .Split(' ', ':', '{')[0]
            .Trim();

    #endregion
}
