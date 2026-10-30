// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="ModuleInfo"/>、<see cref="ModuleInfo.Comparer"/> 与 <see cref="ModuleInfoExtensions.HasModule"/>
/// 的单元测试。
/// </summary>
public class ModuleInfoTests
{
    #region HasModule：程序集名匹配

    [Fact]
    public void HasModule_Should_ReturnTrue_WhenNameMatchesAndMajorVersionReached()
    {
        ImmutableArray<ModuleInfo> modules = [CreateModule("Microsoft.EntityFrameworkCore.dll", "7.0.0.0")];

        modules.HasModule("Microsoft.EntityFrameworkCore.dll", 7).Should().BeTrue();
    }

    [Fact]
    public void HasModule_Should_ReturnTrue_WhenNameMatchesAndMajorVersionExceedsMinimum()
    {
        ImmutableArray<ModuleInfo> modules = [CreateModule("Microsoft.EntityFrameworkCore.dll", "10.3.1.0")];

        modules.HasModule("Microsoft.EntityFrameworkCore.dll", 7).Should().BeTrue();
    }

    [Fact]
    public void HasModule_Should_ReturnFalse_WhenMajorVersionBelowMinimum()
    {
        ImmutableArray<ModuleInfo> modules = [CreateModule("Microsoft.EntityFrameworkCore.dll", "6.9.9.9")];

        modules.HasModule("Microsoft.EntityFrameworkCore.dll", 7).Should().BeFalse();
    }

    /// <summary>
    /// 模块名取自元数据读取器，实际大小写可能与 NuGet 包名不完全一致，故比较必须忽略大小写。
    /// </summary>
    [Theory]
    [InlineData("microsoft.entityframeworkcore.dll")]
    [InlineData("MICROSOFT.ENTITYFRAMEWORKCORE.DLL")]
    public void HasModule_Should_IgnoreCase(string registeredName)
    {
        ImmutableArray<ModuleInfo> modules = [CreateModule(registeredName, "7.0.0.0")];

        modules.HasModule("Microsoft.EntityFrameworkCore.dll", 7).Should().BeTrue();
    }

    [Fact]
    public void HasModule_Should_ReturnFalse_WhenNameDiffers()
    {
        ImmutableArray<ModuleInfo> modules = [CreateModule("Microsoft.EntityFrameworkCore.dll", "7.0.0.0")];

        modules.HasModule("Swashbuckle.AspNetCore.SwaggerGen.dll", 6).Should().BeFalse();
    }

    [Fact]
    public void HasModule_Should_ReturnFalse_ForEmptyModules()
    {
        ImmutableArray<ModuleInfo> modules = [];

        modules.HasModule("Microsoft.EntityFrameworkCore.dll", 0).Should().BeFalse();
    }

    [Fact]
    public void HasModule_Should_ReturnTrue_WhenAnyModuleMatches()
    {
        ImmutableArray<ModuleInfo> modules =
        [
            CreateModule("Newtonsoft.Json.dll", "12.0.0.0"),
            CreateModule("Microsoft.EntityFrameworkCore.dll", "8.0.0.0"),
        ];

        modules.HasModule("Microsoft.EntityFrameworkCore.dll", 7).Should().BeTrue();
    }

    #endregion

    #region GetStronglyTypedIdTypes：Assembly 为 null 的模块不产出类型

    [Fact]
    public void GetStronglyTypedIdTypes_Should_ReturnEmpty_WhenAssemblyIsNull()
    {
        // PE 引用未登记进编译时 Assembly 为 null（见 MetadataReferenceExtensionsTests），
        // 此时不能抛异常，而应按「该模块没有可枚举的类型」处理。
        var module = CreateModule("Unregistered.dll", "1.0.0.0");

        module.Assembly.Should().BeNull();
        module.GetStronglyTypedIdTypes().Should().BeEmpty();
    }

    /// <summary>
    /// 正向对照：确保上一条用例的「空」是 Assembly 为 null 造成的，而不是恒为空。
    /// </summary>
    [Fact]
    public void GetStronglyTypedIdTypes_Should_EnumerateIds_WhenAssemblyIsPresent()
    {
        var module = ResolveModule("ReferencedAssembly", referencesRuntime: true);

        module.GetStronglyTypedIdTypes().Select(type => type.Name).Should().Contain(["OrderId", "ProductId"]);
    }

    /// <summary>
    /// 剪枝：看不见 <c>IStronglyTypedId&lt;,&gt;</c> 的模块不可能声明 Id，直接返回空，
    /// 不必遍历整棵类型树（<c>System.Private.CoreLib</c> 就有上万个类型）。
    /// </summary>
    [Fact]
    public void GetStronglyTypedIdTypes_Should_ReturnEmpty_WhenModuleCannotDeclareIds()
    {
        var module = ResolveModule("UnrelatedAssembly", referencesRuntime: false);

        module.MayDeclareIds.Should().BeFalse();
        module.GetStronglyTypedIdTypes().Should().BeEmpty();
    }

    /// <summary>
    /// 只返回 Id 类型，不返回同程序集里的普通类型：调用方要的正是这个子集。
    /// </summary>
    [Fact]
    public void GetStronglyTypedIdTypes_Should_ExcludeNonIdTypes()
    {
        var module = ResolveModule("MixedAssembly", referencesRuntime: true);

        module.GetStronglyTypedIdTypes().Select(type => type.Name)
            .Should().NotContain("PlainClass");
    }

    #endregion

    #region Comparer：按「程序集名 + 完整版本号」判定等价

    [Fact]
    public void Comparer_Should_ConsiderSameInstanceEqual()
    {
        var comparer = new ModuleInfo.Comparer();
        var module = CreateModule("Newtonsoft.Json.dll", "13.0.3.0");

        comparer.Equals(module, module).Should().BeTrue();
    }

    [Fact]
    public void Comparer_Should_ConsiderSeparateInstancesWithSameIdentityEqual()
    {
        var comparer = new ModuleInfo.Comparer();

        var left = CreateModule("Newtonsoft.Json.dll", "13.0.3.0");
        var right = CreateModule("Newtonsoft.Json.dll", "13.0.3.0");

        left.Should().NotBeSameAs(right);
        comparer.Equals(left, right).Should().BeTrue();
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    [Fact]
    public void Comparer_Should_DistinguishByVersion()
    {
        var comparer = new ModuleInfo.Comparer();

        // 同名但版本不同视为不同模块：启用闸门只看主版本，而去重必须区分完整版本。
        comparer.Equals(
            CreateModule("Newtonsoft.Json.dll", "13.0.3.0"),
            CreateModule("Newtonsoft.Json.dll", "12.0.3.0")).Should().BeFalse();
    }

    [Fact]
    public void Comparer_Should_DistinguishByName()
    {
        var comparer = new ModuleInfo.Comparer();

        comparer.Equals(
            CreateModule("Newtonsoft.Json.dll", "13.0.3.0"),
            CreateModule("Microsoft.EntityFrameworkCore.dll", "13.0.3.0")).Should().BeFalse();
    }

    [Fact]
    public void Comparer_Should_ReturnFalse_WhenEitherSideIsNull()
    {
        var comparer = new ModuleInfo.Comparer();
        var module = CreateModule("Newtonsoft.Json.dll", "13.0.0.0");

        comparer.Equals(null, module).Should().BeFalse();
        comparer.Equals(module, null).Should().BeFalse();
        comparer.Equals(null, null).Should().BeTrue();
    }

    /// <summary>
    /// 忽略 <see cref="ModuleInfo.Assembly"/> 的实例差异：同一程序集可能以不同引用形态出现，但去重时它们是同一个模块。
    /// </summary>
    [Fact]
    public void Comparer_Should_IgnoreAssemblyIdentity()
    {
        var comparer = new ModuleInfo.Comparer();

        var withDefaultReference = CreateModule("Newtonsoft.Json.dll", "13.0.3.0");
        var withAnotherReference = new ModuleInfo(
            "Newtonsoft.Json.dll",
            new Version(13, 0, 3, 0),
            null);

        comparer.Equals(withDefaultReference, withAnotherReference).Should().BeTrue();
    }

    #endregion

    #region 辅助

    // 运行时程序集的引用：ResolveModule 用它构造「能看见 IStronglyTypedId<,>」的编译。
    private static readonly MetadataReference _reference
        = AssemblyReferenceCache.FromAssembly(typeof(StronglyTypedIdAttribute).Assembly);

    private static ModuleInfo CreateModule(string name, string version)
        => new(name, Version.Parse(version), null);

    /// <summary>
    /// 按生产路径造一个模块：源码程序集（<see cref="CompilationReference"/>）经
    /// <see cref="ModuleIdentity.ResolveAssembly"/> 解析，从而覆盖 <see cref="ModuleInfo.MayDeclareIds"/> 的推导。
    /// </summary>
    /// <remarks>
    /// 源码里放两个真正实现 <c>IStronglyTypedId&lt;,&gt;</c> 的类型，外加一个普通类型：
    /// 前者验证「能发现」，后者验证「只返回 Id 类型」。
    /// </remarks>
    private static ModuleInfo ResolveModule(string assemblyName, bool referencesRuntime)
    {
        MetadataReference[] references = referencesRuntime
            ? [_reference, AssemblyReferenceCache.FromAssembly(typeof(object).Assembly)]
            : [AssemblyReferenceCache.FromAssembly(typeof(object).Assembly)];

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(referencesRuntime ? IdSource : PlainSource)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return new ModuleIdentity($"{assemblyName}.dll", new Version(1, 0), compilation.ToMetadataReference())
            .ResolveAssembly(compilation);
    }

    private const string PlainSource = """
        namespace Sample;

        public class OrderId
        {
        }

        public class ProductId
        {
        }
        """;

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

        public readonly record struct ProductId(int Value)
            : global::Len.StronglyTypedId.IStronglyTypedId<ProductId, int>
        {
            public static ProductId Create(int value) => new(value);

            public static ProductId Parse(string value, global::System.IFormatProvider? provider)
                => new(int.Parse(value, provider));

            public static bool TryParse(string? value, global::System.IFormatProvider? provider, out ProductId result)
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

        public class PlainClass
        {
        }
        """;

    #endregion
}
