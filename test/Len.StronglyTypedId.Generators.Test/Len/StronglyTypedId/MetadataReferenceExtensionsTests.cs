// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit / GetAssemblyOrModuleSymbol 等均为测试内
// 同步调用，取消令牌无意义，统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="MetadataReferenceExtensions.GetModules"/> 的单元测试：覆盖两个实际可达分支
/// （CompilationReference 与 PortableExecutableReference/AssemblyMetadata）以及 PE 引用未登记进编译时
/// Assembly 为 null 的路径。
/// </summary>
/// <remarks>
/// 第三个防御性分支（返回空集合）作用于无法识别的引用类型（如 <see cref="PortableExecutableReference"/>
/// 之外、或 <see cref="PortableExecutableReference.GetMetadata"/> 不返回 <see cref="AssemblyMetadata"/> 的引用）。
/// 模块级（netmodule）引用虽会经 <see cref="PortableExecutableReference.GetMetadata"/> 返回被包裹的
/// <see cref="AssemblyMetadata"/>，但其模块不含程序集清单，已在 <see cref="MetadataReferenceExtensions.GetModules"/>
/// 内部按 <see cref="System.Reflection.Metadata.MetadataKind"/> 过滤为安全返回空集合，不再抛异常（见下方
/// <c>GetModules_Should_ReturnEmpty_ForModuleOnlyNetmoduleReference</c>）。
/// </remarks>
public class MetadataReferenceExtensionsTests
{
    #region 分支一：CompilationReference

    [Fact]
    public void GetModules_Should_ReturnModule_ForCompilationReference()
    {
        var referencedCompilation = CSharpCompilation.Create(
            "ReferencedAssembly",
            [CSharpSyntaxTree.ParseText("namespace ReferencedAssembly { public class Marker { } }")],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationReference = referencedCompilation.ToMetadataReference();

        var modules = compilationReference.GetModules(referencedCompilation);

        modules.Should().ContainSingle();
        var module = modules.Single();
        // 源编译产物模块名带 .dll 后缀，与 Assembly.Name（无后缀）不同。
        module.Name.Should().Be(referencedCompilation.Assembly.Name + ".dll");
        module.Version.Should().Be(referencedCompilation.Assembly.Identity.Version);
        module.MetadataReference.Should().BeSameAs(compilationReference);
        module.Assembly.Should().BeSameAs(referencedCompilation.Assembly);
    }

    #endregion

    #region 分支二：PortableExecutableReference（AssemblyMetadata）

    [Fact]
    public void GetModules_Should_ReturnModule_ForPortableExecutableReference()
    {
        var path = typeof(StronglyTypedIdAttribute).Assembly.Location;
        var peRef = MetadataReference.CreateFromFile(path);

        var consumerCompilation = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText("public class Consumer { }")],
            GetReferences().Add(peRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var modules = peRef.GetModules(consumerCompilation);

        modules.Should().ContainSingle();
        var module = modules.Single();
        module.Name.Should().Be(Path.GetFileName(path));
        module.Version.Should().Be(AssemblyName.GetAssemblyName(path).Version);
        module.MetadataReference.Should().BeSameAs(peRef);
        module.Assembly.Should().NotBeNull();
        module.Assembly!.Name.Should().Be(AssemblyName.GetAssemblyName(path).Name);
    }

    [Fact]
    public void GetModules_Should_ReturnModuleWhoseAssemblySymbolMatchesCompilationSymbol()
    {
        var path = typeof(StronglyTypedIdAttribute).Assembly.Location;
        var peRef = MetadataReference.CreateFromFile(path);

        var consumerCompilation = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText("public class Consumer { }")],
            GetReferences().Add(peRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var expectedAssembly = consumerCompilation.GetAssemblyOrModuleSymbol(peRef) as IAssemblySymbol;
        var modules = peRef.GetModules(consumerCompilation);

        modules.Single().Assembly.Should().BeSameAs(expectedAssembly);
    }

    [Fact]
    public void GetModules_Should_ReturnModule_WithNullAssembly_WhenReferenceNotInCompilation()
    {
        // 引用未在编译中登记时，GetAssemblyOrModuleSymbol 返回 null，但 GetModules 仍应从元数据读取模块。
        var path = typeof(StronglyTypedIdAttribute).Assembly.Location;
        var peRef = MetadataReference.CreateFromFile(path);

        var consumerCompilation = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText("public class Consumer { }")],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var modules = peRef.GetModules(consumerCompilation);

        modules.Should().ContainSingle();
        modules.Single().Assembly.Should().BeNull();
    }

    #endregion

    #region 缺陷回归：模块级（netmodule）引用

    [Fact]
    public void GetModules_Should_ReturnEmpty_ForModuleOnlyNetmoduleReference()
    {
        // 模块级（netmodule）引用不含程序集清单：修复前 GetModules 会抛
        // BadImageFormatException（"Metadata image doesn't represent an assembly"）。
        // 修复后应按 MetadataKind 过滤，安全返回空集合而非崩溃。
        var netmodulePath = Path.Combine(
            Path.GetTempPath(),
            "StiNetmodule." + Guid.NewGuid().ToString("N") + ".netmodule");

        var moduleCompilation = CSharpCompilation.Create(
            "StiNetmodule",
            [CSharpSyntaxTree.ParseText("public class ModuleMarker { }")],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.NetModule));

        using var peStream = new MemoryStream();
        var emitResult = moduleCompilation.Emit(peStream);
        emitResult.Success.Should().BeTrue(string.Join("\n", emitResult.Diagnostics));
        File.WriteAllBytes(netmodulePath, peStream.ToArray());

        try
        {
            var netmoduleRef = MetadataReference.CreateFromFile(netmodulePath);

            var consumerCompilation = CSharpCompilation.Create(
                "Consumer",
                [CSharpSyntaxTree.ParseText("public class Consumer { }")],
                GetReferences().Add(netmoduleRef),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var modules = netmoduleRef.GetModules(consumerCompilation);

            modules.Should().BeEmpty();
        }
        finally
        {
            File.Delete(netmodulePath);
        }
    }

    #endregion

    #region 分支三：元数据形态异常的 PE 引用

    /// <summary>
    /// <c>GetMetadata()</c> 返回的不是 <see cref="AssemblyMetadata"/> 时，<see cref="MetadataReferenceExtensions.GetModules"/>
    /// 必须安全返回空集合，而不是抛异常或返回 null。
    /// </summary>
    /// <remarks>
    /// 真实引用里走不到这条路径，故用 <see cref="StubbedPortableExecutableReference"/> 构造出该形态。
    /// </remarks>
    [Fact]
    public void GetModules_Should_ReturnEmpty_ForNonAssemblyMetadata()
    {
        var reference = new StubbedPortableExecutableReference(
            () => ModuleMetadata.CreateFromFile(typeof(StronglyTypedIdAttribute).Assembly.Location));

        reference.GetModules(CreateConsumerCompilation()).Should().BeEmpty();
    }

    /// <summary>
    /// 程序集元数据里含有「不含程序集清单」的模块（模块级 netmodule）时，该模块必须被跳过而非令整个调用崩溃。
    /// </summary>
    /// <remarks>
    /// 这与上方 <c>GetModules_Should_ReturnEmpty_ForModuleOnlyNetmoduleReference</c> 覆盖的是不同环节：
    /// 那条走真实文件引用、由 Roslyn 在 <c>AssemblyMetadata.GetModules()</c> 内部就把无清单模块滤掉；
    /// 这条则把无清单模块直接塞进 <see cref="AssemblyMetadata"/>，从而真正执行到
    /// <c>GetAssemblyDefinition()</c> 的失败兜底（<see cref="BadImageFormatException"/> /
    /// <see cref="InvalidOperationException"/>）——不同运行时抛其中一种。
    /// </remarks>
    [Fact]
    public void GetModules_Should_SkipModuleWithoutAssemblyManifest()
    {
        var netmoduleMetadata = ModuleMetadata.CreateFromImage(EmitNetmoduleImage());
        var reference = new StubbedPortableExecutableReference(() => AssemblyMetadata.Create(netmoduleMetadata));

        reference.GetModules(CreateConsumerCompilation()).Should().BeEmpty();
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 可控的 <see cref="PortableExecutableReference"/> 替身：<c>GetMetadata()</c> 的返回值由构造参数决定。
    /// </summary>
    /// <remarks>
    /// <see cref="MetadataReference"/> 的构造函数是 <c>internal</c>，测试程序集无法直接继承；
    /// <see cref="PortableExecutableReference"/> 的构造函数是 <c>protected</c>，可以继承并在
    /// <c>GetMetadataImpl</c> 里返回任意 <see cref="Metadata"/>。三个抽象成员中只有第一个参与本测试，
    /// 另外两个是基类契约要求的占位实现。
    /// </remarks>
    private sealed class StubbedPortableExecutableReference : PortableExecutableReference
    {
        private readonly Func<Metadata> _metadataFactory;

        public StubbedPortableExecutableReference(Func<Metadata> metadataFactory)
            : this(metadataFactory, MetadataReferenceProperties.Module)
        {
        }

        private StubbedPortableExecutableReference(Func<Metadata> metadataFactory, MetadataReferenceProperties properties)
            : base(properties)
        {
            _metadataFactory = metadataFactory;
        }

        protected override Metadata GetMetadataImpl() => _metadataFactory();

        protected override DocumentationProvider CreateDocumentationProvider() => DocumentationProvider.Default;

        protected override PortableExecutableReference WithPropertiesImpl(MetadataReferenceProperties properties)
            => new StubbedPortableExecutableReference(_metadataFactory, properties);
    }

    private static CSharpCompilation CreateConsumerCompilation()
        => CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText("public class Consumer { }")],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static ImmutableArray<byte> EmitNetmoduleImage()
    {
        var moduleCompilation = CSharpCompilation.Create(
            "StiNetmodule",
            [CSharpSyntaxTree.ParseText("public class ModuleMarker { }")],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.NetModule));

        using var stream = new MemoryStream();
        var emitResult = moduleCompilation.Emit(stream);
        emitResult.Success.Should().BeTrue(string.Join("\n", emitResult.Diagnostics));

        return [.. stream.ToArray()];
    }

    #endregion

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
