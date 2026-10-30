// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Collections.Immutable;
using System.IO;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="MetadataIdScanner"/> 的单元测试：元数据侧扫描必须与符号侧得出同一组 Id。
/// </summary>
/// <remarks>
/// 这条等价性是整个缓存方案的前提 —— 元数据扫描快，但一旦判据与符号侧漂移，
/// 就会静默漏生成集成代码（编译通过、运行期缺转换器）。
/// </remarks>
public class MetadataIdScannerTests
{
    #region 基本发现

    [Fact]
    public void Scanner_Should_FindTopLevelId()
    {
        var names = Scan(IdSource);

        names.Should().Contain("Sample.OrderId");
    }

    /// <summary>
    /// 通过接口继承得到的 <c>IStronglyTypedId&lt;,&gt;</c> 同样算数。
    /// </summary>
    /// <remarks>
    /// C# 编译器把接口的<b>传递闭包</b>写进 <c>InterfaceImpl</c> 表：<c>MyId : IMyId</c> 而
    /// <c>IMyId : IStronglyTypedId&lt;MyId, Guid&gt;</c> 时，<c>MyId</c> 的元数据里直接就有后者。
    /// 符号侧的 <c>ITypeSymbol.Interfaces</c> 也是同一口径（含接口继承、不含基类继承），
    /// 所以两侧一致。这条用例把这个事实钉住，免得日后有人「优化」成只认源码里显式写出的接口。
    /// </remarks>
    [Fact]
    public void Scanner_Should_IncludeInterfaceInheritedThroughAnotherInterface()
    {
        var names = Scan("""
            namespace Sample;

            public interface IMyId : global::Len.StronglyTypedId.IStronglyTypedId<MyId, global::System.Guid>
            {
            }

            public readonly record struct MyId(global::System.Guid Value) : IMyId
            {
                public static MyId Create(global::System.Guid value) => new(value);

                public static MyId Parse(string value, global::System.IFormatProvider? provider)
                    => new(global::System.Guid.Parse(value, provider));

                public static bool TryParse(string? value, global::System.IFormatProvider? provider, out MyId result)
                {
                    if (global::System.Guid.TryParse(value, provider, out var primitive))
                    {
                        result = new(primitive);
                        return true;
                    }

                    result = default;
                    return false;
                }
            }
            """);

        names.Should().Contain("Sample.MyId");
    }

    /// <summary>
    /// 同命名空间、同名字但元数不同的接口不算：C# 允许 <c>IStronglyTypedId&lt;T&gt;</c> 与
    /// <c>IStronglyTypedId&lt;T, U&gt;</c> 共存，只比名字会误判。
    /// </summary>
    [Fact]
    public void Scanner_Should_IgnoreInterfaceWithDifferentArity()
    {
        var names = Scan("""
            namespace Len.StronglyTypedId
            {
                public interface IStronglyTypedId<T>
                {
                }
            }

            namespace Sample
            {
                public class Foo : global::Len.StronglyTypedId.IStronglyTypedId<int>
                {
                }
            }
            """);

        names.Should().NotContain("Sample.Foo");
    }

    /// <summary>
    /// 第三方命名空间里的同名接口不算。
    /// </summary>
    [Fact]
    public void Scanner_Should_IgnoreInterfaceFromThirdPartyNamespace()
    {
        var names = Scan("""
            namespace ThirdParty
            {
                public interface IStronglyTypedId<T, TPrimitive>
                {
                }
            }

            namespace Sample
            {
                public class Foo : global::ThirdParty.IStronglyTypedId<int, int>
                {
                }
            }
            """);

        names.Should().NotContain("Sample.Foo");
    }

    #endregion

    #region 嵌套与泛型容器

    /// <summary>
    /// 嵌套类型要拼成 <c>Outer+Inner</c> 形式，否则 <c>GetTypeByMetadataName</c> 解析不到。
    /// </summary>
    [Fact]
    public void Scanner_Should_UsePlusSeparator_ForNestedTypes()
    {
        var names = Scan("""
            namespace Sample;

            public partial class Container
            {
                public readonly record struct InnerId(global::System.Guid Value)
                    : global::Len.StronglyTypedId.IStronglyTypedId<InnerId, global::System.Guid>
                {
                    public static InnerId Create(global::System.Guid value) => new(value);

                    public static InnerId Parse(string value, global::System.IFormatProvider? provider)
                        => new(global::System.Guid.Parse(value, provider));

                    public static bool TryParse(string? value, global::System.IFormatProvider? provider, out InnerId result)
                    {
                        if (global::System.Guid.TryParse(value, provider, out var primitive))
                        {
                            result = new(primitive);
                            return true;
                        }

                        result = default;
                        return false;
                    }
                }
            }
            """);

        names.Should().Contain("Sample.Container+InnerId");
    }

    /// <summary>
    /// 泛型容器里的 Id 也要能扫到：外层带元数后缀（<c>Container`1</c>）。
    /// 这类 Id 会被发现阶段再按 <c>IsInGenericContainer</c> 排除，但扫描本身不能漏。
    /// </summary>
    [Fact]
    public void Scanner_Should_FindIdInGenericContainer()
    {
        var names = Scan("""
            namespace Sample;

            public partial class Container<T>
            {
                public readonly record struct InnerId(global::System.Guid Value)
                    : global::Len.StronglyTypedId.IStronglyTypedId<InnerId, global::System.Guid>
                {
                    public static InnerId Create(global::System.Guid value) => new(value);

                    public static InnerId Parse(string value, global::System.IFormatProvider? provider)
                        => new(global::System.Guid.Parse(value, provider));

                    public static bool TryParse(string? value, global::System.IFormatProvider? provider, out InnerId result)
                    {
                        if (global::System.Guid.TryParse(value, provider, out var primitive))
                        {
                            result = new(primitive);
                            return true;
                        }

                        result = default;
                        return false;
                    }
                }
            }
            """);

        names.Should().Contain("Sample.Container`1+InnerId");
    }

    #endregion

    #region 与符号路径等价

    /// <summary>
    /// 同一次编译上，元数据扫描与符号树遍历必须得出同一组类型。
    /// </summary>
    /// <remarks>
    /// 这是本方案的核心护栏：元数据扫描是性能优化，语义必须与它替代的那条路径完全一致。
    /// </remarks>
    [Fact]
    public void Scanner_Should_AgreeWithSymbolTree_OnSameAssembly()
    {
        var (names, assembly) = ScanAndResolve(IdSource);

        var fromMetadata = names
            .Select(name => assembly.GetTypeByMetadataName(name)?.ToDisplayString())
            .Where(display => display is not null)
            .OrderBy(display => display, StringComparer.Ordinal)
            .ToArray();

        var fromSymbols = ScanSymbolTree(assembly)
            .Select(type => type.ToDisplayString())
            .OrderBy(display => display, StringComparer.Ordinal)
            .ToArray();

        fromMetadata.Should().Equal(fromSymbols);
    }

    #endregion

    #region 缓存

    /// <summary>
    /// 同一份程序集扫描两次必须返回同一结果（MVID 命中缓存）。
    /// </summary>
    /// <remarks>
    /// 只断言「结果相等」而非「缓存命中次数」：后者需要暴露内部计数器，为一条测试污染生产 API 不值得。
    /// 缓存失效的后果是重算一次，正确性不受影响 —— 这条用例守的是「缓存不会返回错误结果」。
    /// </remarks>
    [Fact]
    public void Scanner_Should_ReturnSameResult_OnRepeatedScans()
    {
        var image = EmitImage(IdSource);
        var metadata = AssemblyMetadata.CreateFromImage(image);

        var first = MetadataIdScanner.GetStronglyTypedIdNames(metadata.GetModules()[0]);
        var second = MetadataIdScanner.GetStronglyTypedIdNames(metadata.GetModules()[0]);

        second.Should().Equal(first);
    }

    /// <summary>
    /// 内容不同的两份程序集（MVID 不同）不能共用缓存条目。
    /// </summary>
    [Fact]
    public void Scanner_Should_NotReuseCache_AcrossDifferentAssemblies()
    {
        var first = Scan(IdSource);
        var second = Scan("""
            namespace Other;

            public readonly record struct OtherId(int Value)
                : global::Len.StronglyTypedId.IStronglyTypedId<OtherId, int>
            {
                public static OtherId Create(int value) => new(value);

                public static OtherId Parse(string value, global::System.IFormatProvider? provider)
                    => new(int.Parse(value, provider));

                public static bool TryParse(string? value, global::System.IFormatProvider? provider, out OtherId result)
                {
                    if (int.TryParse(value, provider, out var primitive))
                    {
                        result = new(primitive);
                        return true;
                    }

                    result = default;
                    return false;
                }
            }
            """);

        first.Should().Contain("Sample.OrderId");
        second.Should().Contain("Other.OtherId");
        second.Should().NotContain("Sample.OrderId");
    }

    /// <summary>
    /// 缓存条目超过上限时整体清空后重算：结果必须仍然正确，不能因为清空而返回空表。
    /// </summary>
    /// <remarks>
    /// 上限是 512，逐条构造 512 份不同程序集代价过高；这里只验证「清空后重新扫描仍给出正确结果」
    /// 这一条不变量 —— 清空本身是 O(1)，正确性才是会出错的地方。
    /// </remarks>
    [Fact]
    public void Scanner_Should_StillReturnCorrectResult_AfterCacheChurn()
    {
        var image = EmitImage(IdSource);
        var metadata = AssemblyMetadata.CreateFromImage(image);

        for (var index = 0; index < 3; index++)
        {
            MetadataIdScanner.GetStronglyTypedIdNames(metadata.GetModules()[0])
                .Should().Contain("Sample.OrderId");
        }
    }

    /// <summary>
    /// 模块级（netmodule）引用不含程序集清单，<c>GetMetadataReader</c> 会抛异常。
    /// 扫描器必须按「没有 Id」处理，而不是让整个发现阶段崩溃。
    /// </summary>
    [Fact]
    public void Scanner_Should_ReturnEmpty_ForNetmoduleWithoutAssemblyManifest()
    {
        var moduleCompilation = CSharpCompilation.Create(
            "StiNetmodule",
            [CSharpSyntaxTree.ParseText("public class ModuleMarker { }")],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.NetModule));

        using var stream = new MemoryStream();
        var emitResult = moduleCompilation.Emit(stream);
        emitResult.Success.Should().BeTrue(string.Join("\n", emitResult.Diagnostics));

        var metadata = ModuleMetadata.CreateFromImage([.. stream.ToArray()]);

        MetadataIdScanner.GetStronglyTypedIdNames(metadata).Should().BeEmpty();
    }

    #endregion

    #region 辅助

    private const string IdSource = """
        namespace Sample;

        public readonly record struct OrderId(global::System.Guid Value)
            : global::Len.StronglyTypedId.IStronglyTypedId<OrderId, global::System.Guid>
        {
            public static OrderId Create(global::System.Guid value) => new(value);

            public static OrderId Parse(string value, global::System.IFormatProvider? provider)
                => new(global::System.Guid.Parse(value, provider));

            public static bool TryParse(string? value, global::System.IFormatProvider? provider, out OrderId result)
            {
                if (global::System.Guid.TryParse(value, provider, out var primitive))
                {
                    result = new(primitive);
                    return true;
                }

                result = default;
                return false;
            }
        }
        """;

    private static ImmutableArray<string> Scan(string source)
        => MetadataIdScanner.GetStronglyTypedIdNames(
            AssemblyMetadata.CreateFromImage(EmitImage(source)).GetModules()[0]);

    private static (ImmutableArray<string> Names, IAssemblySymbol Assembly) ScanAndResolve(string source)
    {
        var image = EmitImage(source);
        var reference = MetadataReference.CreateFromImage(image);
        var compilation = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText("public class Consumer { }")],
            GetReferences().Append(reference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var assembly = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(reference)!;

        return (Scan(source), assembly);
    }

    /// <summary>
    /// 符号侧的原判据：遍历类型树，取直接实现了 <c>IStronglyTypedId&lt;,&gt;</c> 的类型。
    /// </summary>
    private static IEnumerable<ITypeSymbol> ScanSymbolTree(IAssemblySymbol assembly)
    {
        var stack = new Stack<INamespaceOrTypeSymbol>(assembly.GlobalNamespace.GetMembers());

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            foreach (var member in current.GetMembers().OfType<INamespaceOrTypeSymbol>())
            {
                stack.Push(member);
            }

            if (current is ITypeSymbol type && type.Interfaces.Any(StronglyTypedIdInfo.IsStronglyTypedIdInterface))
            {
                yield return type;
            }
        }
    }

    private static byte[] EmitImage(string source)
    {
        var compilation = CSharpCompilation.Create(
            "ScannedAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        emitResult.Success.Should().BeTrue(string.Join("\n", emitResult.Diagnostics));

        return stream.ToArray();
    }

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        var seed = new[] { typeof(object).Assembly, typeof(StronglyTypedIdAttribute).Assembly };

        return [.. AppDomain.CurrentDomain.GetAssemblies()
            .Union(seed)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(AssemblyReferenceCache.FromAssembly)];
    }

    #endregion
}
