using System.Globalization;

namespace Len.StronglyTypedId;

/// <summary>
/// README 表格单元格的双语文案。
/// </summary>
internal sealed class DocText(string en, string zh)
{
    /// <summary>
    /// 空文案：表示该行不参与渲染（例如不单独成行的登记项），或该列没有内容（例如无代码修复）。
    /// </summary>
    public static readonly DocText Empty = new(string.Empty, string.Empty);

    /// <summary>
    /// 英文文案。
    /// </summary>
    public string En { get; } = en;

    /// <summary>
    /// 简体中文文案。
    /// </summary>
    public string Zh { get; } = zh;

    /// <summary>
    /// 是否为空文案（以英文文案为准，两者始终成对填写）。
    /// </summary>
    public bool IsEmpty => En.Length == 0;

    /// <summary>
    /// 按目标区域性取文案：中文（<c>zh</c> 及其子区域性）取 <see cref="Zh"/>，其余一律取 <see cref="En"/>。
    /// </summary>
    public string For(CultureInfo culture)
        => culture.TwoLetterISOLanguageName == "zh" ? Zh : En;
}
