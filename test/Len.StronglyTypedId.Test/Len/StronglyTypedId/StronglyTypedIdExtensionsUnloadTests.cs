// xUnit1051：本文件中的 Roslyn 编译与 GC 等待均为测试内同步调用，取消令牌无意义。
#pragma warning disable xUnit1051
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="StronglyTypedIdExtensions"/> 的缓存不得阻止可回收 <see cref="AssemblyLoadContext"/> 卸载。
/// </summary>
/// <remarks>
/// 缓存键是 <see cref="Type"/>，而 <c>Type</c> 持有 <c>Assembly</c>，<c>Assembly</c> 持有 ALC。
/// 用普通字典做缓存时，插件热重载或测试宿主反复加载程序集，每加载一次就永久钉住一个 ALC 及其全部类型。
/// 这类泄漏不会让任何功能出错，只有观察「<c>Unload()</c> 后 ALC 是否真的被回收」才能发现。
/// </remarks>
public class StronglyTypedIdExtensionsUnloadTests
{
/// <summary>
    /// 插件里的一份最小 Id 实现：只要能被 <c>IsStronglyTypedId</c> 认出来即可，不需要生成器的产物。
    /// </summary>
    private const string PluginIdSource = """
        using System;
        using Len.StronglyTypedId;

        namespace PluginIds;

        public readonly struct PluginId : IStronglyTypedId<PluginId, Guid>
        {
            public PluginId(Guid value) => Value = value;

            public Guid Value { get; }

            public static PluginId Create(Guid value) => new(value);

            public static PluginId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));

            public static bool TryParse(string? s, IFormatProvider? provider, out PluginId result)
            {
                if (Guid.TryParse(s, out var value))
                {
                    result = new(value);
                    return true;
                }

                result = default;
                return false;
            }

            public static bool operator ==(PluginId left, PluginId right) => left.Value == right.Value;

            public static bool operator !=(PluginId left, PluginId right) => left.Value != right.Value;

            public override bool Equals(object? obj) => obj is PluginId other && Value == other.Value;

            public override int GetHashCode() => Value.GetHashCode();
        }
        """;

    /// <summary>
    /// 把插件源码编译成内存镜像。引用取自可信平台程序集清单，再加上本库自身。
    /// </summary>
    private static byte[] CompilePluginImage()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(PluginIdSource);

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(StronglyTypedIdAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "Len.StronglyTypedId.PluginProbe",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);

        emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("探针程序集必须能编译，否则下面的卸载断言测的是别的东西");

        return stream.ToArray();
    }

    /// <summary>
    /// 在独立的可回收 ALC 里加载插件、调用反射辅助方法、<c>Unload()</c>，然后只交出 ALC 的长弱引用。
    /// </summary>
    /// <remarks>
    /// 三点缺一不可，否则用例会静默失去判别力：
    /// <list type="bullet">
    /// <item>必须显式 <c>Unload()</c>：不调用时 ALC 只是不可达，短弱引用照样先死。</item>
    /// <item>必须用 <c>trackResurrection: true</c> 的长弱引用：短弱引用在终结器排队时就报 false。</item>
    /// <item>加载、触碰、卸载必须关在同一个 <see cref="MethodImplOptions.NoInlining"/> 方法里，GC 留在外面，
    /// 否则 Debug 构建下 JIT 会延长局部变量生命周期，断言就成了空转。</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Alc, bool Recognised) LoadProbeAndUnload(byte[] image)
    {
        var alc = new CollectiblePluginLoadContext();

        var assembly = alc.LoadFromStream(new MemoryStream(image));
        var idType = assembly.GetType("PluginIds.PluginId", throwOnError: true)!;

        // 这一步正是把 Type 写进缓存的地方，修复前它会成为静态字典的强键。
        var recognised = idType.IsStronglyTypedId() && idType.GetPrimitiveIdType() == typeof(Guid);

        var alcReference = new WeakReference(alc, trackResurrection: true);

        alc.Unload();

        return (alcReference, recognised);
    }

    [Fact]
    public void Cache_Should_Not_Prevent_CollectibleLoadContext_FromUnloading()
    {
        var image = CompilePluginImage();

        var (alc, recognised) = LoadProbeAndUnload(image);

        recognised.Should().BeTrue("前置条件：插件里的 Id 必须被识别出来，缓存才会被写入");

        // 卸载后反复 GC，直到 ALC 被回收或次数用尽。
        for (var attempt = 0; attempt < 10 && alc.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        alc.IsAlive.Should().BeFalse(
            "缓存键是 Type，而 Type 持有 Assembly 与 ALC；静态强引用键会让每次插件加载都泄漏一个 ALC");
    }

    /// <summary>
    /// 插件 ALC：自身不解析任何依赖，全部回落到默认上下文，这样插件里的 Id 与本库的
    /// <c>IStronglyTypedId&lt;,&gt;</c> 是同一个类型标识，接口判定才有意义。
    /// </summary>
    private sealed class CollectiblePluginLoadContext() : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
