using System.Reflection;
using System.Runtime.CompilerServices;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 校验程序集内全部强类型 Id 的命名实例能否成功构造。
/// </summary>
/// <remarks>
/// 自定义基元的实例值只能在运行期校验（生成器跑在 netstandard2.0 上，无法执行用户的 <c>Parse</c>），
/// 而静态字段初始化遇第一个异常就中止 —— 一次只能看到一个坏实例。本类把「逐个跑一遍、收集全部失败」
/// 做成一行调用，让排查从「猜哪个坏了」变成「看清单」。
/// <para>
/// 内置基元的实例在编译期就已校验，这里对它们只会得到「全部通过」；本类不区分基元种类，
/// 因为「实例值写错了」这件事对使用者是同一个问题。
/// </para>
/// </remarks>
public static class StronglyTypedIdConformance
{
    /// <summary>
    /// 强制初始化 <paramref name="assembly"/> 内全部强类型 Id，返回所有失败的「类型 → 消息」。
    /// </summary>
    /// <param name="assembly">要检查的程序集。</param>
    /// <returns>失败条目；全部通过时为空列表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> 为 <see langword="null"/>。</exception>
    public static IReadOnlyList<(Type IdType, string Message)> FindBrokenInstances(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var failures = new List<(Type IdType, string Message)>();

        foreach (var type in assembly.GetTypes())
        {
            if (!IsStronglyTypedId(type))
            {
                continue;
            }

            try
            {
                // 静态构造只在首次访问时运行；已经跑过的类型会直接返回，不会重复执行。
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
            catch (TypeInitializationException exception)
            {
                // 内层异常才是真正的原因（生成代码抛的 InvalidOperationException，或基元自己的 Parse 异常）。
                failures.Add((type, exception.InnerException?.Message ?? exception.Message));
            }
        }

        return failures;
    }

    /// <summary>
    /// 强制初始化 <paramref name="assembly"/> 内全部强类型 Id，有失败时抛出列出全部条目的异常。
    /// </summary>
    /// <param name="assembly">要检查的程序集。</param>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">存在无法构造的命名实例。</exception>
    public static void AssertAllInstancesValid(Assembly assembly)
    {
        var failures = FindBrokenInstances(assembly);

        if (failures.Count == 0)
        {
            return;
        }

        var details = string.Join(
            Environment.NewLine,
            failures.Select(failure => $"  {failure.IdType.FullName}: {failure.Message}"));

        throw new InvalidOperationException(
            $"{failures.Count} strongly typed id type(s) failed to initialize their named instances:{Environment.NewLine}{details}");
    }

    /// <summary>
    /// 判断类型是否实现了 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>。
    /// </summary>
    /// <remarks>
    /// 按接口判定而非按特性：引用程序集里的 Id 同样能被发现，且不必加载生成器的特性类型。
    /// </remarks>
    private static bool IsStronglyTypedId(Type type)
        => type.GetInterfaces().Any(@interface =>
            @interface.IsGenericType
            && @interface.GetGenericTypeDefinition() == typeof(IStronglyTypedId<,>));
}
