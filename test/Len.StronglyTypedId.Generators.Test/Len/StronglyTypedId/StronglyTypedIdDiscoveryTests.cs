// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="StronglyTypedIdDiscovery.Discover"/> 的单元测试：确认只把运行时的
/// <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 纳入发现结果。
/// </summary>
/// <remarks>
/// 判据必须同时校验命名空间与类型实参个数。曾只比较接口简单名，于是使用者或第三方程序集里任何
/// 名为 <c>IStronglyTypedId</c> 的接口都会被纳入，随后解析基元类型失败并抛异常 —— 在真实编译里
/// 表现为一条 CS8785 警告加上该生成器的产出全部消失（正常的 <c>.g.cs</c> 也一并消失）。
/// </remarks>
public class StronglyTypedIdDiscoveryTests
{
    [Fact]
    public void Discover_Should_Ignore_WhenInterfaceComesFromThirdPartyNamespace()
    {
        var module = CreateModule("ThirdParty", """
            namespace ThirdParty;

            public interface IStronglyTypedId<T>
            {
            }

            public class Foo : IStronglyTypedId<int>
            {
            }
            """);

        var act = () => StronglyTypedIdDiscovery.Discover([], [module]).ToList();

        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    public void Discover_Should_Ignore_WhenInterfaceArityDiffersInRuntimeNamespace()
    {
        // 与本仓库接口同处 Len.StronglyTypedId 命名空间、但类型实参个数不同。
        // C# 允许同名的不同 arity 泛型类型共存，因此仅校验命名空间仍不足以排除它。
        var module = CreateModule("Narrow", """
            namespace Len.StronglyTypedId;

            public interface IStronglyTypedId<T>
            {
            }

            public class Foo : IStronglyTypedId<int>
            {
            }
            """);

        var act = () => StronglyTypedIdDiscovery.Discover([], [module]).ToList();

        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    public void Discover_Should_Include_WhenTypeImplementsStronglyTypedId()
    {
        // 正向对照：真正实现 IStronglyTypedId<TSelf, TPrimitiveId> 的类型必须仍被发现，
        // 否则收紧判据就会把合法输入一起挡掉。
        var module = CreateModule("AlreadyGenerated", """
            namespace Already.Generated;

            public readonly record struct OrderId(global::System.Guid Value)
                : global::Len.StronglyTypedId.IStronglyTypedId<OrderId, global::System.Guid>
            {
                public static OrderId Create(global::System.Guid value) => new(value);

                public static OrderId Parse(string value, global::System.IFormatProvider? provider)
                    => new(global::System.Guid.Parse(value, provider));

                public static bool TryParse(
                    string? value,
                    global::System.IFormatProvider? provider,
                    out OrderId result)
                {
                    if (global::System.Guid.TryParse(value, provider, out var primitiveId))
                    {
                        result = new OrderId(primitiveId);
                        return true;
                    }

                    result = default;
                    return false;
                }
            }
            """);

        var discovered = StronglyTypedIdDiscovery.Discover([], [module]).ToList();

        discovered.Should().ContainSingle();
        discovered[0].Name.Should().Be("OrderId");
        discovered[0].PrimitiveIdTypeName.Should().Be("global::System.Guid");
    }

    /// <summary>
    /// 把一个源码片段编译成 <see cref="ModuleInfo"/>。走 <see cref="CompilationReference"/> 路径，
    /// 这样 <see cref="ModuleInfo.Assembly"/> 上挂着被引用编译的程序集符号，
    /// <see cref="ModuleInfo.GetTypes"/> 才能枚举其中的类型。
    /// </summary>
    private static ModuleInfo CreateModule(string assemblyName, string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.ToMetadataReference().GetModules(compilation).Single();
    }

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        var seed = new[] { typeof(object).Assembly, typeof(StronglyTypedIdAttribute).Assembly };

        return AppDomain.CurrentDomain.GetAssemblies()
            .Union(seed)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => (MetadataReference)MetadataReference.CreateFromFile(assembly.Location))
            .ToImmutableArray();
    }
}
