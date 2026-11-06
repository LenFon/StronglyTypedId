using System.Reflection;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 反射取类型表时的容错读取，供测试辅助包内部共用。
/// </summary>
/// <remarks>
/// 直接调 <see cref="Assembly.GetTypes"/> 在两种情况下会失败，而这两种情况在「扫描全部已加载程序集」
/// 这类场景里都很常见：
/// <list type="bullet">
/// <item>程序集引用了缺失的依赖 —— 抛 <see cref="ReflectionTypeLoadException"/>，但其余类型仍然可用；</item>
/// <item>动态程序集或受限程序集连类型表都取不到 —— 抛其它异常，跳过即可。</item>
/// </list>
/// 这层保护原本在两个包里各写了一份，结果一处有、一处没有：<c>FindBrokenInstances</c> 因此会在缺依赖的
/// 程序集上直接抛异常，而不是给出它本该给出的清单。抽成一处后不会再分叉。
/// </remarks>
internal static class AssemblyTypeReader
{
    /// <summary>
    /// 取程序集里能加载的类型；加载不了的部分跳过。
    /// </summary>
    /// <param name="assembly">要读取的程序集。</param>
    /// <returns>可加载的类型序列；一个都取不到时为空。</returns>
    public static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // 部分类型加载失败（缺依赖）时，其余类型仍然可用。
            return exception.Types.Where(static type => type is not null).Cast<Type>();
        }
        catch (Exception)
        {
            // 动态程序集与某些受限程序集连类型表都取不到；跳过即可，显式指定类型的那条路仍然可用。
            return [];
        }
    }
}
