namespace Len.StronglyTypedId;

/// <summary>
/// 生成器会写进 Id 类型体内的成员名，以及「类型名是否撞上它们」的判据。
/// </summary>
/// <remarks>
/// 类型名与成员名相同会触发 CS0542（成员名不能与它们的封闭类型相同）。这些成员由生成器产出，
/// 用户在声明处看不到，编译器报错也只指向生成文件，因此必须由分析器提前报出、生成器同步跳过。
/// <para>
/// 与 <see cref="InstanceDeclaration"/> 的保留名清单同源：那里判「命名实例叫这个名字会不会撞」，
/// 这里判「类型叫这个名字会不会撞」。两处判据都从这张表派生，避免新增成员时只改一处。
/// </para>
/// </remarks>
internal static class GeneratedMemberNames
{
    /// <summary>
    /// 核心生成器无条件产出的成员名（元数据名）。
    /// </summary>
    /// <remarks>
    /// 运算符在元数据里叫 <c>op_LessThan</c> 这类名字，而它们都是合法 C# 标识符，
    /// <c>SyntaxFacts.IsValidIdentifier</c> 会放行，故必须显式列出。
    /// </remarks>
    internal static readonly ImmutableHashSet<string> Core = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Create",
        "TryCreate",
        "Parse",
        "TryParse",
        "CompareTo",
        "op_LessThan",
        "op_GreaterThan",
        "op_LessThanOrEqual",
        "op_GreaterThanOrEqual");

    /// <summary>
    /// record 自身合成的成员名。
    /// </summary>
    /// <remarks>
    /// 这些名字作为类型名时，用户自己的声明就已经 CS0542（<c>record Equals</c> 与合成的 <c>Equals</c> 撞名），
    /// 与本库无关。纳入判据会报出一条指向错误原因的诊断，故只在此记录、不参与碰撞判定。
    /// <para>
    /// <c>ToString</c> 与 <c>Deconstruct</c> 也在其中：生成器虽然会重写 / 追加它们，但 record 本身已合成同名成员，
    /// 用户写 <c>record ToString</c> 时即便没有本库也编译不过。
    /// </para>
    /// </remarks>
    internal static readonly ImmutableHashSet<string> RecordSynthesized = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Equals",
        "GetHashCode",
        "PrintMembers",
        "EqualityContract",
        "ToString",
        "Deconstruct",
        "op_Equality",
        "op_Inequality",
        "Value");

    /// <summary>
    /// 引用类型 Id 才会产出的 null 安全比较辅助方法名。
    /// </summary>
    internal const string CompareValues = "CompareValues";

    /// <summary>
    /// 自定义基元且声明了合法命名实例时才会产出的私有工厂方法名。
    /// </summary>
    internal const string CreateInstance = "CreateInstance";

    /// <summary>
    /// 基元可 span 格式化时才会产出的成员名。
    /// </summary>
    internal const string TryFormat = "TryFormat";

    /// <summary>
    /// System.Text.Json 转换器的嵌套类型名。
    /// </summary>
    internal const string SystemTextJsonConverter = "SystemTextJsonConverter";

    /// <summary>
    /// Newtonsoft.Json 转换器的嵌套类型名。
    /// </summary>
    internal const string NewtonsoftJsonConverter = "NewtonsoftJsonConverter";

    /// <summary>
    /// 廉价前置判定：该名字是否可能撞上任何生成成员。
    /// </summary>
    /// <remarks>
    /// 绝大多数 Id 的名字不在此列，先用字符串比较挡掉，避免为每个 Id 构造 <see cref="StronglyTypedIdInfo"/>。
    /// </remarks>
    internal static bool MayCollide(string typeName)
        => Core.Contains(typeName)
            || typeName is CompareValues or CreateInstance or TryFormat
                or SystemTextJsonConverter or NewtonsoftJsonConverter;

    /// <summary>
    /// 判断该 Id 的类型名是否与生成器产出的成员撞名。
    /// </summary>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="hasSystemTextJsonConverter">本次编译是否引用了 System.Text.Json。</param>
    /// <param name="hasNewtonsoftJsonConverter">本次编译是否引用了 Newtonsoft.Json ≥ 13。</param>
    /// <remarks>
    /// 两个转换器的名字取决于是否引用了对应程序集，故由调用方传入；其余成员只由 Id 自身属性决定。
    /// </remarks>
    internal static bool CollidesWithGeneratedMember(
        StronglyTypedIdInfo idInfo,
        bool hasSystemTextJsonConverter,
        bool hasNewtonsoftJsonConverter)
        => Core.Contains(idInfo.Name)
            || (idInfo.TypeKindSuffix is null && idInfo.Name == CompareValues)
            || (idInfo.IsCustomPrimitive && idInfo.InstancesBlock.Length > 0 && idInfo.Name == CreateInstance)
            || ((idInfo.IsSpanFormattable || idInfo.IsUtf8SpanFormattable) && idInfo.Name == TryFormat)
            || (hasSystemTextJsonConverter && idInfo.Name == SystemTextJsonConverter)
            || (hasNewtonsoftJsonConverter && idInfo.Name == NewtonsoftJsonConverter);
}
