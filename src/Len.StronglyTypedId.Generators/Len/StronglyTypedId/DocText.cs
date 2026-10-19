using System.Globalization;

namespace Len.StronglyTypedId;

/// <summary>
/// README 表格单元格的双语文案。
/// </summary>
/// <remarks>
/// <para>
/// 三张自动生成的表格（基元类型 / 集成触发条件 / 诊断）同时存在于 <c>README.md</c> 与
/// <c>README.zh-CN.md</c>，因此每个单元格都要有中英两份。
/// </para>
/// <para>
/// 文案与判定逻辑放在一起，是为了让「改了判定却忘了改文档」这件事在物理上不可能发生 ——
/// 判定改了而文案没跟上，README 会在下一次 <c>DocGen --verify</c> 时直接报错。
/// </para>
/// </remarks>
internal sealed class DocText(string en, string zh)
{
    /// <summary>
    /// 空文案：表示该行不参与渲染（例如不单独成行的登记项），或该列没有内容（例如无代码修复）。
    /// </summary>
    public static readonly DocText Empty = new(string.Empty, string.Empty);

    /// <summary>英文文案。</summary>
    public string En { get; } = en;

    /// <summary>简体中文文案。</summary>
    public string Zh { get; } = zh;

    /// <summary>是否为空文案（以英文文案为准，两者始终成对填写）。</summary>
    public bool IsEmpty => En.Length == 0;

    /// <summary>
    /// 按目标区域性取文案：中文（<c>zh</c> 及其子区域性）取 <see cref="Zh"/>，其余一律取 <see cref="En"/>。
    /// </summary>
    public string For(CultureInfo culture)
        => culture.TwoLetterISOLanguageName == "zh" ? Zh : En;
}
