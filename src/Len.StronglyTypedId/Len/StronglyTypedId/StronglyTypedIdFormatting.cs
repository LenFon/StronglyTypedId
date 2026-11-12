using System.Globalization;

namespace Len.StronglyTypedId;

/// <summary>
/// 不装箱地格式化基元值，供生成代码调用。
/// </summary>
/// <remarks>
/// 生成代码需要按 <see cref="IFormattable"/> 的语义格式化基元，而基元可能把
/// <c>ToString(string?, IFormatProvider?)</c> 写成显式接口实现（<c>char</c> 与封装类型的常见写法），
/// 此时按类型名直呼会 CS1501，只能经接口调用。但接口强制转换对值类型会装箱 —— 每次调用一次分配。
/// <para>
/// 这里的泛型约束让编译器发出 <c>constrained.</c> 调用：值类型直接调它自己的实现，不装箱；
/// 引用类型走接口分派，与强制转换等价。生成代码因此不必再写 <c>((IFormattable)value).ToString(…)</c>。
/// </para>
/// </remarks>
public static class StronglyTypedIdFormatting
{
    /// <summary>
    /// 按不变文化把基元值写成规范文本。
    /// </summary>
    /// <typeparam name="T">基元类型。</typeparam>
    /// <param name="value">要格式化的基元值。</param>
    /// <returns>规范文本；基元返回 <see langword="null"/> 时为空字符串。</returns>
    public static string ToInvariantString<T>(T value)
        where T : IFormattable
        => value.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>
    /// 按指定格式与格式提供程序格式化基元值。
    /// </summary>
    /// <typeparam name="T">基元类型。</typeparam>
    /// <param name="value">要格式化的基元值。</param>
    /// <param name="format">格式字符串；<see langword="null"/> 表示默认格式。</param>
    /// <param name="formatProvider">格式提供程序；<see langword="null"/> 表示当前区域性。</param>
    /// <returns>格式化后的文本；基元返回 <see langword="null"/> 时为空字符串。</returns>
    /// <remarks>
    /// 与 <see cref="ToInvariantString{T}"/> 同口径兜底：<see cref="IFormattable"/> 的返回类型标注为非可空，
    /// 但自定义基元可以返回 <see langword="null"/>，而生成的 Id 的 <c>ToString(string?, IFormatProvider?)</c>
    /// 直接转发到这里、返回值同样标注为非可空。不兜底就会把 <see langword="null"/> 泄漏给调用方，
    /// 让 <c>id.ToString(format, provider).Length</c> 这类写法抛 <see cref="NullReferenceException"/>。
    /// </remarks>
    public static string Format<T>(T value, string? format, IFormatProvider? formatProvider)
        where T : IFormattable
        => value.ToString(format, formatProvider) ?? string.Empty;

    /// <summary>
    /// 把基元值格式化进字符跨度。
    /// </summary>
    /// <typeparam name="T">基元类型。</typeparam>
    /// <param name="value">要格式化的基元值。</param>
    /// <param name="destination">目标缓冲区。</param>
    /// <param name="charsWritten">写入的字符数。</param>
    /// <param name="format">格式字符串。</param>
    /// <param name="formatProvider">格式提供程序。</param>
    /// <returns>缓冲区足够大时为 <see langword="true"/>。</returns>
    public static bool TryFormat<T>(
        T value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? formatProvider)
        where T : ISpanFormattable
        => value.TryFormat(destination, out charsWritten, format, formatProvider);

    /// <summary>
    /// 把基元值格式化进 UTF-8 字节跨度。
    /// </summary>
    /// <typeparam name="T">基元类型。</typeparam>
    /// <param name="value">要格式化的基元值。</param>
    /// <param name="utf8Destination">目标缓冲区。</param>
    /// <param name="bytesWritten">写入的字节数。</param>
    /// <param name="format">格式字符串。</param>
    /// <param name="formatProvider">格式提供程序。</param>
    /// <returns>缓冲区足够大时为 <see langword="true"/>。</returns>
    public static bool TryFormatUtf8<T>(
        T value,
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? formatProvider)
        where T : IUtf8SpanFormattable
        => value.TryFormat(utf8Destination, out bytesWritten, format, formatProvider);
}
