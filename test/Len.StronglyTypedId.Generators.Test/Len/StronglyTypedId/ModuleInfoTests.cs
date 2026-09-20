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
/// <remarks>
/// <para>
/// 这三者共同决定「哪些代码生成器被启用」，以及增量管道里模块集合如何去重。
/// </para>
/// <para>
/// 注意两处版本语义并不相同，不能互相替代：<see cref="ModuleInfoExtensions.HasModule"/> 只看
/// <b>主版本号</b>（用于启用闸门），<see cref="ModuleInfo.Comparer"/> 则按<b>完整版本号</b>判定等价
/// （用于 <c>SelectMany(…).WithComparer(…)</c> 的去重）。本文件同时把守这两条语义。
/// </para>
/// </remarks>
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

    #region GetTypes：Assembly 为 null 的模块不产出类型

    [Fact]
    public void GetTypes_Should_ReturnEmpty_WhenAssemblyIsNull()
    {
        // PE 引用未登记进编译时 Assembly 为 null（见 MetadataReferenceExtensionsTests），
        // 此时不能抛异常，而应按「该模块没有可枚举的类型」处理。
        var module = CreateModule("Unregistered.dll", "1.0.0.0");

        module.Assembly.Should().BeNull();
        module.GetTypes().Should().BeEmpty();
    }

    /// <summary>
    /// 正向对照：确保上一条用例的「空」是 Assembly 为 null 造成的，而不是 GetTypes 恒为空。
    /// </summary>
    [Fact]
    public void GetTypes_Should_EnumerateTypes_WhenAssemblyIsPresent()
    {
        var compilation = CSharpCompilation.Create(
            "ReferencedAssembly",
            [CSharpSyntaxTree.ParseText("namespace Sample { public class OrderId { } public class ProductId { } }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var module = new ModuleInfo("ReferencedAssembly.dll", new Version(1, 0), compilation.ToMetadataReference(), compilation.Assembly);

        module.GetTypes().Select(type => type.Name).Should().Contain(["OrderId", "ProductId"]);
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
    /// 忽略 <see cref="ModuleInfo.MetadataReference"/> 与 <see cref="ModuleInfo.Assembly"/> 的实例差异：
    /// 同一程序集可能以不同引用形态出现，但去重时它们是同一个模块。
    /// </summary>
    [Fact]
    public void Comparer_Should_IgnoreMetadataReferenceAndAssemblyIdentity()
    {
        var comparer = new ModuleInfo.Comparer();

        var withDefaultReference = CreateModule("Newtonsoft.Json.dll", "13.0.3.0");
        var withAnotherReference = new ModuleInfo(
            "Newtonsoft.Json.dll",
            new Version(13, 0, 3, 0),
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            null);

        comparer.Equals(withDefaultReference, withAnotherReference).Should().BeTrue();
    }

    #endregion

    #region 辅助

    // 模块描述符本身不读取 MetadataReference 的内容，仅用于承载实例，取任一真实引用即可。
    private static readonly MetadataReference _reference
        = MetadataReference.CreateFromFile(typeof(StronglyTypedIdAttribute).Assembly.Location);

    private static ModuleInfo CreateModule(string name, string version)
        => new(name, Version.Parse(version), _reference, null);

    #endregion
}
