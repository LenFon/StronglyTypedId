using System.Collections.Concurrent;
using System.Globalization;

namespace Len.StronglyTypedId;

/// <summary>
/// 把一个本地化资源键解析为 <c>Locales.{culture}.txt</c> 中对应区域性的文案。
/// </summary>
/// <remarks>
/// 分析器规范禁止直接读取 <c>CultureInfo.CurrentCulture</c>（RS1035，会破坏分析器的确定性），
/// 因此这里改为实现 <see cref="LocalizableString"/>：由 Roslyn 在格式化诊断时把目标区域性
/// 作为 <see cref="IFormatProvider"/> 传入，与编译器自身的诊断本地化行为保持一致。
/// </remarks>
internal sealed class LocalizedString(string key) : LocalizableString
{
    private readonly string _key = key;

    protected override string GetText(IFormatProvider? formatProvider)
        => LocalizationTexts.TryGetValue(_key, formatProvider as CultureInfo ?? CultureInfo.InvariantCulture, out var text)
            ? text
            : _key;

    protected override int GetHash() => StringComparer.Ordinal.GetHashCode(_key);

    protected override bool AreEqual(object? other)
        => other is LocalizedString otherString && StringComparer.Ordinal.Equals(_key, otherString._key);
}

/// <summary>
/// 读取并缓存内嵌的 <c>Locales.{culture}.txt</c> 资源文件。
/// </summary>
file static class LocalizationTexts
{
    private const string FallbackCultureName = "en";

    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _texts
        = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryGetValue(string key, CultureInfo culture, out string text)
        => GetTexts(culture).TryGetValue(key, out text!);

    private static Dictionary<string, string> GetTexts(CultureInfo culture)
    {
        if (_texts.TryGetValue(culture.Name, out var texts))
        {
            return texts;
        }

        texts = LoadTexts(culture);

        return _texts.GetOrAdd(culture.Name, texts);
    }

    private static Dictionary<string, string> LoadTexts(CultureInfo culture)
    {
        using var stream = OpenLocalizationStream(culture);
        using var reader = new StreamReader(stream);

        var texts = new Dictionary<string, string>();

        while (reader.ReadLine() is { } line)
        {
            var content = line.Trim();

            // 跳过空行与以 '#' 起始的注释行。
            if (content.Length == 0 || content.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = content.IndexOf('=');

            if (separatorIndex < 0)
            {
                continue;
            }

            texts[content[..separatorIndex].Trim()] = content[(separatorIndex + 1)..].Trim();
        }

        return texts;
    }

    private static Stream OpenLocalizationStream(CultureInfo culture)
    {
        var assembly = typeof(LocalizationTexts).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        var resourceName = FindResource(resourceNames, culture.Name)
            ?? FindResource(resourceNames, culture.TwoLetterISOLanguageName)
            ?? FindResource(resourceNames, FallbackCultureName)
            ?? throw new InvalidOperationException($"No embedded localization resource was found for culture '{culture.Name}'.");

        return assembly.GetManifestResourceStream(resourceName)!;
    }

    private static string? FindResource(string[] resourceNames, string cultureName)
        => resourceNames.FirstOrDefault(name => name.EndsWith($".Locales.{cultureName}.txt", StringComparison.OrdinalIgnoreCase));
}
