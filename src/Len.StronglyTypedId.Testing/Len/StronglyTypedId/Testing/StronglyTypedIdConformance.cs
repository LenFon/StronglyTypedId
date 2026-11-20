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

        foreach (var type in AssemblyTypeReader.GetLoadableTypes(assembly))
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
    /// 对 <paramref name="assembly"/> 内每个强类型 Id 跑一遍往返契约，返回所有违约条目。
    /// </summary>
    /// <param name="assembly">要检查的程序集。</param>
    /// <param name="generator">
    /// 造值用的生成器；为 <see langword="null"/> 时用 <see cref="Any"/> 的共享实例。
    /// </param>
    /// <returns>违约条目；全部通过时为空列表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 检查项：<c>Parse(ToString())</c> 读回同一个 Id、<c>TryCreate(Value)</c> 成功并读回同一个 Id、
    /// 归一化器幂等（配了才有）、以及 System.Text.Json 往返（生成了转换器才有）。
    /// <para>
    /// 造不出值的 Id 不在这里报：它们记在 <see cref="FindSkippedRoundTrips"/> 里。两者分开是因为
    /// 「这个 Id 的格式推断不出来」和「这个 Id 违约了」需要完全不同的处理，混在一个列表里只能靠
    /// 读消息区分。
    /// </para>
    /// <para>
    /// 需要为某个 Id 提供取值时，优先传自己的 <paramref name="generator"/> 并在它上面
    /// <see cref="AnyIdGenerator.Register(Type, Func{object})"/>：共享实例的注册是进程级状态，
    /// 并行跑的测试里任何一处 <see cref="Any.Reset"/> 都会把它清掉。
    /// </para>
    /// </remarks>
    public static IReadOnlyList<(Type IdType, string Message)> CheckRoundTrip(
        Assembly assembly,
        AnyIdGenerator? generator = null)
        => RoundTripProbe.Run(assembly, generator).Failures;

    /// <summary>
    /// 对 <paramref name="assembly"/> 内每个强类型 Id 跑一遍往返契约，返回无法造值因而没有检查的条目。
    /// </summary>
    /// <param name="assembly">要检查的程序集。</param>
    /// <param name="generator">
    /// 造值用的生成器；为 <see langword="null"/> 时用 <see cref="Any"/> 的共享实例。
    /// </param>
    /// <returns>跳过条目；全部 Id 都造出了值时为空列表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 跳过不是失败：严格格式的自定义基元（<c>ACCT-</c> 加 8 位数字之类）无法从常见文本形态推断，
    /// 验证器恒拒绝的 Id 也本来就不可构造。但「一条失败都没有」不等于「全都查过了」，
    /// 这个入口让那条区别可见 —— 需要覆盖时在 <paramref name="generator"/> 上注册工厂。
    /// </remarks>
    public static IReadOnlyList<(Type IdType, string Message)> FindSkippedRoundTrips(
        Assembly assembly,
        AnyIdGenerator? generator = null)
        => RoundTripProbe.Run(assembly, generator).Skipped;

    /// <summary>
    /// 对 <paramref name="assembly"/> 内每个强类型 Id 跑一遍往返契约，有违约时抛出列出全部条目的异常。
    /// </summary>
    /// <param name="assembly">要检查的程序集。</param>
    /// <param name="generator">
    /// 造值用的生成器；为 <see langword="null"/> 时用 <see cref="Any"/> 的共享实例。
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">存在违约的 Id。</exception>
    public static void AssertRoundTrip(Assembly assembly, AnyIdGenerator? generator = null)
    {
        var failures = CheckRoundTrip(assembly, generator);

        if (failures.Count == 0)
        {
            return;
        }

        var details = string.Join(
            Environment.NewLine,
            failures.Select(failure => $"  {failure.IdType.FullName}: {failure.Message}"));

        throw new InvalidOperationException(
            $"{failures.Count} strongly typed id type(s) failed the round-trip contract:{Environment.NewLine}{details}");
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
