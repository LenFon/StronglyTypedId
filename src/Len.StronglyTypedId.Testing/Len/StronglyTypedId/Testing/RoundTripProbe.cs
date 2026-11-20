using System.Reflection;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 对程序集里的每个强类型 Id 跑一遍库承诺的往返契约，把失败与跳过分开收集。
/// </summary>
/// <remarks>
/// 五项检查各自独立上报，因为它们的修法不同：文本往返失败要查 <c>ToString</c> / <c>Parse</c>，
/// <c>TryCreate</c> 往返失败要查 <c>Create</c> 的归一化，归一化器幂等失败要查归一化器本身，
/// JSON 往返失败要查转换器或 <c>JsonShape</c> 配置。
/// <para>
/// 造不出值的 Id 记为「跳过」而不是失败：<see cref="Any"/> 的启发式覆盖不了严格格式的自定义基元，
/// 而验证器恒拒绝的 Id 本来就是刻意不可构造的。跳过条目单独返回，避免「一条都没报」被误读成「全都查过了」。
/// </para>
/// </remarks>
internal static class RoundTripProbe
{
    /// <summary>
    /// <c>System.Text.Json</c> 的两个非泛型重载；运行时没有该程序集时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 用反射取而非直接引用：测试辅助包不该为了一个可选检查把 STJ 变成硬依赖，而「用户程序集有没有
    /// 引用 STJ」本身就是这条检查要不要跑的前提。
    /// </remarks>
    private static readonly Lazy<JsonMethods?> _jsonMethods = new(ResolveJsonMethods, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 跑完 <paramref name="assembly"/> 里的全部 Id。
    /// </summary>
    /// <param name="assembly">要检查的程序集。</param>
    /// <param name="generator">
    /// 造值用的生成器；为 <see langword="null"/> 时用 <see cref="Any"/> 的共享实例。
    /// 传入自己的实例可以避开共享实例的全局注册状态 —— 并行测试里 <see cref="Any.Reset"/> 会清掉注册。
    /// </param>
    public static RoundTripOutcome Run(Assembly assembly, AnyIdGenerator? generator)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var failures = new List<(Type IdType, string Message)>();
        var skipped = new List<(Type IdType, string Message)>();

        foreach (var type in AssemblyTypeReader.GetLoadableTypes(assembly))
        {
            if (type.GetPrimitiveIdType() is not { } primitiveType)
            {
                continue;
            }

            // 开放泛型（Box<T>.BoxedId）无法闭合构造，也就无法造值。
            if (type.ContainsGenericParameters)
            {
                skipped.Add((type, "the id is declared in an open generic container, so no value can be generated for it."));
                continue;
            }

            object id;

            try
            {
                id = generator is null ? Any.Create(type) : generator.Create(type);
            }
            catch (Exception exception) when (IsUnvaluable(exception))
            {
                skipped.Add((type, $"no value could be generated: {exception.Message}"));
                continue;
            }

            CheckTextRoundTrip(type, id, failures);
            CheckTryCreateRoundTrip(type, primitiveType, id, failures);
            CheckNormalizerIdempotence(type, primitiveType, id, failures);
            CheckJsonRoundTrip(type, id, failures);
        }

        return new RoundTripOutcome(failures, skipped);
    }

    /// <summary>
    /// 判断造值失败是否属于「这个 Id 造不出值」而非「库有 bug」。
    /// </summary>
    /// <remarks>
    /// 这四种正是 <see cref="AnyIdGenerator.Create"/> 文档化的失败形态：无法推断格式
    /// （<see cref="NotSupportedException"/>）、重试预算耗尽（<see cref="InvalidOperationException"/>）、
    /// 开放泛型（<see cref="ArgumentException"/>）、命名实例静态构造失败
    /// （<see cref="TypeInitializationException"/>）。其余异常（如验证器自身的
    /// <see cref="NullReferenceException"/>）是真 bug，必须冒泡。
    /// </remarks>
    private static bool IsUnvaluable(Exception exception)
        => exception is NotSupportedException
            or InvalidOperationException
            or ArgumentException
            or TypeInitializationException;

    /// <summary>
    /// <c>Parse(id.ToString())</c> 必须读回同一个 Id。
    /// </summary>
    private static void CheckTextRoundTrip(Type idType, object id, List<(Type IdType, string Message)> failures)
    {
        var text = id.ToString() ?? string.Empty;

        try
        {
            var parsed = InvokeStatic(idType, "Parse", [typeof(string), typeof(IFormatProvider)], text, null);

            if (!Equals(parsed, id))
            {
                failures.Add((idType, $"Parse(ToString()) returned '{parsed}' instead of '{id}' (text: '{text}')."));
            }
        }
        catch (Exception exception)
        {
            failures.Add((idType, $"Parse(ToString()) threw {Describe(exception)} (text: '{text}')."));
        }
    }

    /// <summary>
    /// <c>TryCreate(id.Value)</c> 必须成功并读回同一个 Id。
    /// </summary>
    private static void CheckTryCreateRoundTrip(
        Type idType,
        Type primitiveType,
        object id,
        List<(Type IdType, string Message)> failures)
    {
        try
        {
            var value = GetValue(idType, id);
            var method = FindStatic(idType, "TryCreate", [primitiveType, idType.MakeByRefType()]);

            if (method is null)
            {
                failures.Add((idType, "TryCreate(TPrimitiveId, out TSelf) was not found on the id type."));

                return;
            }

            object?[] arguments = [value, null];

            if (!(bool)method.Invoke(null, arguments)!)
            {
                failures.Add((idType, $"TryCreate(Value) returned false for the value '{value}' that Create accepted."));

                return;
            }

            if (!Equals(arguments[1], id))
            {
                failures.Add((idType, $"TryCreate(Value) returned '{arguments[1]}' instead of '{id}'."));
            }
        }
        catch (Exception exception)
        {
            failures.Add((idType, $"TryCreate(Value) threw {Describe(exception)}"));
        }
    }

    /// <summary>
    /// 配了归一化器时，<c>Create(Normalize(v))</c> 必须等于 <c>Create(v)</c>。
    /// </summary>
    /// <remarks>
    /// 生成器在 <c>Create</c> 里先跑归一化器再跑验证器，因此这条等价于「归一化器幂等」。
    /// 不幂等时，同一个基元值经 <c>Create</c> 与经 <c>Parse(ToString())</c> 会得到不同的 Id。
    /// </remarks>
    private static void CheckNormalizerIdempotence(
        Type idType,
        Type primitiveType,
        object id,
        List<(Type IdType, string Message)> failures)
    {
        if (GetNormalizerName(idType) is not { } normalizerName)
        {
            return;
        }

        var method = FindStatic(idType, normalizerName, [primitiveType], includeNonPublic: true);

        // 方法找不到时生成器已经报过 STIA0017，这里不重复报。
        if (method is null)
        {
            return;
        }

        try
        {
            var value = GetValue(idType, id);
            var normalized = method.Invoke(null, [value]);
            var fromOriginal = InvokeStatic(idType, "Create", [primitiveType], value);
            var fromNormalized = InvokeStatic(idType, "Create", [primitiveType], normalized);

            if (!Equals(fromOriginal, fromNormalized))
            {
                failures.Add((
                    idType,
                    $"the normalizer '{normalizerName}' is not idempotent: Create(Normalize('{value}')) = '{fromNormalized}' "
                    + $"but Create('{value}') = '{fromOriginal}'."));
            }
        }
        catch (Exception exception)
        {
            failures.Add((idType, $"the normalizer '{normalizerName}' threw {Describe(exception)}"));
        }
    }

    /// <summary>
    /// 生成了 System.Text.Json 转换器时，序列化再反序列化必须读回同一个 Id。
    /// </summary>
    /// <remarks>
    /// 只在该 Id 确实带 <c>[JsonConverter]</c> 时检查：没有转换器时 STJ 会退回反射序列化，
    /// 那条路径的往返与生成器无关，报出来只会误导。STJ 本身不可用时同样跳过。
    /// </remarks>
    private static void CheckJsonRoundTrip(Type idType, object id, List<(Type IdType, string Message)> failures)
    {
        if (!HasGeneratedJsonConverter(idType) || _jsonMethods.Value is not { } json)
        {
            return;
        }

        try
        {
            var payload = (string)json.Serialize.Invoke(null, [id, idType, null])!;
            var restored = json.Deserialize.Invoke(null, [payload, idType, null]);

            if (!Equals(restored, id))
            {
                failures.Add((idType, $"JSON round-trip returned '{restored}' instead of '{id}' (payload: {payload})."));
            }
        }
        catch (Exception exception)
        {
            failures.Add((idType, $"JSON round-trip threw {Describe(exception)}"));
        }
    }

    /// <summary>
    /// 取 Id 的 <c>Value</c> 属性值。
    /// </summary>
    private static object? GetValue(Type idType, object id)
        => idType.GetProperty("Value")!.GetValue(id);

    /// <summary>
    /// 在 Id 类型上查找静态方法。
    /// </summary>
    /// <param name="idType">Id 类型。</param>
    /// <param name="name">方法名。</param>
    /// <param name="parameterTypes">形参类型表。</param>
    /// <param name="includeNonPublic">
    /// 是否连非公开成员一起找。生成器产出的 <c>Parse</c> / <c>Create</c> / <c>TryCreate</c> 都是公开的，
    /// 但用户写的归一化器与验证器通常是 <c>private static</c> —— 生成器在同一个类型里调用它们，
    /// 反射检查也必须看得到。
    /// </param>
    /// <remarks>
    /// 必须在具体类型上反射，不能在 <c>IParsable&lt;TSelf&gt;</c> 这类接口上找：静态抽象成员是抽象的，
    /// <see cref="MethodInfo"/> 的 <c>Invoke</c> 会抛 <see cref="InvalidOperationException"/>。
    /// </remarks>
    private static MethodInfo? FindStatic(Type idType, string name, Type[] parameterTypes, bool includeNonPublic = false)
        => idType.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static | (includeNonPublic ? BindingFlags.NonPublic : BindingFlags.Default),
            binder: null,
            parameterTypes,
            modifiers: null);

    /// <summary>
    /// 调 Id 类型上的公开静态方法。
    /// </summary>
    private static object? InvokeStatic(Type idType, string name, Type[] parameterTypes, params object?[] arguments)
        => FindStatic(idType, name, parameterTypes)!.Invoke(null, arguments);

    /// <summary>
    /// 取生效的归一化器方法名：逐类型优先，其次程序集级默认。优先级与生成器一致。
    /// </summary>
    private static string? GetNormalizerName(Type idType)
        => idType.GetCustomAttribute<StronglyTypedIdAttribute>()?.Normalizer
            ?? idType.Assembly.GetCustomAttribute<StronglyTypedIdDefaultsAttribute>()?.Normalizer;

    /// <summary>
    /// 判断该 Id 是否带生成的 <c>[JsonConverter]</c>。
    /// </summary>
    /// <remarks>
    /// 按特性全名判定而非引用 <c>JsonConverterAttribute</c>：测试辅助包不引用 STJ，
    /// 且泛型容器里的 Id 刻意不带这个特性（CS0416），按特性判定正好把那种情况排除掉。
    /// </remarks>
    private static bool HasGeneratedJsonConverter(Type idType)
        => idType.GetCustomAttributesData().Any(data =>
            data.AttributeType.FullName == "System.Text.Json.Serialization.JsonConverterAttribute");

    /// <summary>
    /// 把反射调用抛出的 <see cref="TargetInvocationException"/> 拆成内层异常，消息才有意义。
    /// </summary>
    private static string Describe(Exception exception)
    {
        var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;

        return $"{cause.GetType().Name}: {cause.Message}";
    }

    /// <summary>
    /// 反射取 <c>JsonSerializer.Serialize(object?, Type, JsonSerializerOptions?)</c> 与对应的
    /// <c>Deserialize</c>；STJ 不可用时返回 <see langword="null"/>。
    /// </summary>
    private static JsonMethods? ResolveJsonMethods()
    {
        var serializer = Type.GetType("System.Text.Json.JsonSerializer, System.Text.Json", throwOnError: false);
        var options = Type.GetType("System.Text.Json.JsonSerializerOptions, System.Text.Json", throwOnError: false);

        if (serializer is null || options is null)
        {
            return null;
        }

        var serialize = serializer.GetMethod("Serialize", [typeof(object), typeof(Type), options]);
        var deserialize = serializer.GetMethod("Deserialize", [typeof(string), typeof(Type), options]);

        return serialize is null || deserialize is null ? null : new JsonMethods(serialize, deserialize);
    }

    /// <summary>
    /// <c>JsonSerializer</c> 的两个非泛型重载。
    /// </summary>
    private sealed record JsonMethods(MethodInfo Serialize, MethodInfo Deserialize);
}

/// <summary>
/// 一次往返检查的结果：失败条目与跳过条目分开。
/// </summary>
/// <param name="Failures">违约的 Id 与原因。</param>
/// <param name="Skipped">无法造值、因而没有检查的 Id 与原因。</param>
internal readonly record struct RoundTripOutcome(
    IReadOnlyList<(Type IdType, string Message)> Failures,
    IReadOnlyList<(Type IdType, string Message)> Skipped);
