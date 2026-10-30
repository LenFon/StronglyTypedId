using System.Globalization;
using Len.StronglyTypedId;

// 程序集级默认值：一次写清「本程序集里所有 Id 的默认行为」，逐类型标注只用于覆盖。
// 这里把能设的选项全开一遍，好让每个选项的效果都能在运行输出里看到。
//
// 优先级：逐类型 > 程序集默认 > 内置默认。
//   - Validator / Normalizer / ExceptionType：逐类型写了就用逐类型的。
//   - JsonShape / PrefixReadMode / Integrations：枚举型，逐类型显式写回默认值也能覆盖程序集默认。
//   - TypeConverter / RedactValidationMessage：bool 型，只能单向打开——程序集打开后逐类型关不回去。
[assembly: StronglyTypedIdDefaults(
    Validator = nameof(Len.StronglyTypedId.SampleDefaults.DefaultRules.Validate),
    Normalizer = nameof(Len.StronglyTypedId.SampleDefaults.DefaultRules.Normalize),
    ExceptionType = typeof(Len.StronglyTypedId.SampleDefaults.DefaultRuleViolationException),
    RedactValidationMessage = true,
    JsonShape = JsonShape.Object,
    TypeConverter = true,
    PrefixReadMode = PrefixReadMode.Strict,
    Integrations = StronglyTypedIdIntegrations.SystemTextJson)]

namespace Len.StronglyTypedId.SampleDefaults;

/// <summary>
/// 程序集默认验证器拒绝取值时抛出的异常。
/// </summary>
public sealed class DefaultRuleViolationException(string message) : Exception(message);

/// <summary>
/// 程序集默认的验证与归一化规则。
/// </summary>
/// <remarks>
/// 程序集默认值只写方法名，因此每个 Id 仍需提供同名静态方法（否则 STIA0010）。
/// 这里用 <c>using static</c> 把共享规则引入每个 Id，避免逐类型复制粘贴。
/// </remarks>
public static class DefaultRules
{
    /// <summary>
    /// 归一化：去掉首尾空白并统一小写。
    /// </summary>
    public static string Normalize(string value) => value.Trim().ToLowerInvariant();

    /// <summary>
    /// 验证：非空即可。
    /// </summary>
    public static bool Validate(string value) => value.Length > 0;
}

/// <summary>
/// 完全依赖程序集默认值的 Id：验证器、归一化、异常类型、脱敏、JSON 形态、TypeConverter 全部来自程序集。
/// </summary>
[StronglyTypedId]
public partial record struct TenantId(string Value)
{
    private static string Normalize(string value) => DefaultRules.Normalize(value);

    private static bool Validate(string value) => DefaultRules.Validate(value);
}

/// <summary>
/// 逐类型覆盖验证器：程序集默认的 <c>Validate</c> 被这个 Id 自己的规则取代。
/// </summary>
[StronglyTypedId(Validator = nameof(IsEmail))]
public partial record struct EmailId(string Value)
{
    private static string Normalize(string value) => DefaultRules.Normalize(value);

    private static bool IsEmail(string value) => value.Contains('@');
}

/// <summary>
/// 逐类型把 JSON 形态写回 <c>Primitive</c>：枚举型默认值可以双向覆盖。
/// </summary>
[StronglyTypedId(JsonShape = JsonShape.Primitive)]
public partial record struct PrimitiveShapeId(string Value)
{
    private static string Normalize(string value) => DefaultRules.Normalize(value);

    private static bool Validate(string value) => DefaultRules.Validate(value);
}

/// <summary>
/// 带前缀的 Id：程序集默认的 <c>PrefixReadMode.Strict</c> 生效，裸值不再被接受。
/// </summary>
[StronglyTypedId(Prefix = "tn_")]
public partial record struct PrefixedTenantId(string Value)
{
    private static string Normalize(string value) => DefaultRules.Normalize(value);

    private static bool Validate(string value) => DefaultRules.Validate(value);
}

/// <summary>
/// 逐类型把前缀读回方式写回 <c>Lenient</c>：同为枚举型，可以覆盖程序集默认的严格模式。
/// </summary>
[StronglyTypedId(Prefix = "lg_", PrefixReadMode = PrefixReadMode.Lenient)]
public partial record struct LenientPrefixedId(string Value)
{
    private static string Normalize(string value) => DefaultRules.Normalize(value);

    private static bool Validate(string value) => DefaultRules.Validate(value);
}

/// <summary>
/// 逐类型指定异常类型：程序集默认的 <c>DefaultRuleViolationException</c> 被替换。
/// </summary>
public sealed class TenantSuspendedException(string message) : Exception(message);

[StronglyTypedId(ExceptionType = typeof(TenantSuspendedException))]
public partial record struct SuspendedTenantId(string Value)
{
    private static string Normalize(string value) => DefaultRules.Normalize(value);

    private static bool Validate(string value) => value != "suspended";
}

/// <summary>
/// 逐类型把集成范围写回 <c>None</c>：程序集默认只开 System.Text.Json，这个 Id 连它也不要。
/// </summary>
/// <remarks>
/// <c>Integrations</c> 是枚举型，因此「逐类型写回默认值」也能覆盖程序集默认——与 <c>JsonShape</c>、
/// <c>PrefixReadMode</c> 同一规则，而不是 <c>TypeConverter</c> 那种单向开关。
/// </remarks>
[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
public partial record struct CoreOnlyTenantId(string Value)
{
    private static string Normalize(string value) => DefaultRules.Normalize(value);

    private static bool Validate(string value) => DefaultRules.Validate(value);
}
