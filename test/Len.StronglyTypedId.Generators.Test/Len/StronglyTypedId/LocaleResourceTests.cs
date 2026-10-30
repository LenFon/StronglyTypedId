using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Len.StronglyTypedId;

/// <summary>
/// 内嵌语言资源文件的契约守卫：两种语言的键集合一致、源码引用的键都配了文案、文案在两种语言下都能解析。
/// </summary>
/// <remarks>
/// 这些断言补的是 <see cref="LocalizedString"/> 的静默回退：键缺失或漏翻时它把键名原样显示给使用者，
/// 不抛异常也不报错。只有把「键集合」本身钉住，那种退化才会在构建期暴露。
/// </remarks>
public partial class LocaleResourceTests
{
    private const string EnglishCulture = "en";
    private const string ChineseCulture = "zh-CN";

    /// <summary>
    /// 显式构造：<c>new LocalizedString("Key")</c>。
    /// </summary>
    [GeneratedRegex("""new\s+LocalizedString\s*\(\s*"([^"]+)"\s*\)""")]
    private static partial Regex ExplicitKeyPattern();

    /// <summary>
    /// 目标类型推断构造：<c>private static readonly LocalizedString _x = new("Key");</c>。
    /// </summary>
    [GeneratedRegex("""LocalizedString\s+\w+\s*=\s*new\s*\(\s*"([^"]+)"\s*\)""")]
    private static partial Regex TargetTypedKeyPattern();

    /// <summary>
    /// 两种语言的键集合必须完全相同。漏翻一条时 <see cref="LocalizedString"/> 会静默回退成键名，
    /// 使用者看到的是一串英文标识符，而不是「这条没翻译」。
    /// </summary>
    [Fact]
    public void LocaleFiles_Should_DeclareTheSameKeys()
    {
        var english = ReadLocale(EnglishCulture);
        var chinese = ReadLocale(ChineseCulture);

        english.Should().NotBeEmpty();
        chinese.Keys.Should().BeEquivalentTo(
            english.Keys,
            "两种语言的键集合必须一致，否则漏翻的键会静默回退成键名");
    }

    [Fact]
    public void LocaleFiles_Should_NotContainEmptyTexts()
    {
        using var scope = new AssertionScope();

        foreach (var culture in new[] { EnglishCulture, ChineseCulture })
        {
            foreach (var (key, text) in ReadLocale(culture))
            {
                text.Should().NotBeNullOrWhiteSpace($"{culture} 的 '{key}' 不能是空文案");
            }
        }
    }

    /// <summary>
    /// 源码里每一处 <see cref="LocalizedString"/> 引用的键，都必须在每种语言的资源文件里存在。
    /// 这条守的是「新增诊断时忘了加文案」——那种情况下诊断照常报出，只是标题变成键名。
    /// </summary>
    [Fact]
    public void EveryKeyUsedInSource_Should_ExistInEveryLocale()
    {
        var keys = FindKeysInSource();

        keys.Should().NotBeEmpty("源码里应当至少有一处 LocalizedString");

        using var scope = new AssertionScope();

        foreach (var culture in new[] { EnglishCulture, ChineseCulture })
        {
            var texts = ReadLocale(culture);

            foreach (var key in keys)
            {
                texts.Should().ContainKey(key, $"{culture} 缺少源码引用的资源键 '{key}'");
            }
        }
    }

    /// <summary>
    /// 反向覆盖：语言文件里不应留下没有任何代码引用的键。这类键通常是改名后忘记删除的残留，
    /// 留着会让人误以为那条文案还在生效。
    /// </summary>
    [Fact]
    public void LocaleFiles_Should_NotContainUnusedKeys()
    {
        var keys = FindKeysInSource();

        using var scope = new AssertionScope();

        foreach (var (key, _) in ReadLocale(EnglishCulture))
        {
            keys.Should().Contain(key, $"'{key}' 在语言文件里存在，但源码里没有任何 LocalizedString 引用它");
        }
    }

    /// <summary>
    /// 端到端：每个键在两种语言下都必须解析成文案，而不是回退成键名。
    /// 与 <see cref="EveryKeyUsedInSource_Should_ExistInEveryLocale"/> 的分工是——那条查文件内容，
    /// 这条查加载器与回退链的实际行为。
    /// </summary>
    [Fact]
    public void EveryKeyUsedInSource_Should_ResolveInBothCultures()
    {
        using var scope = new AssertionScope();

        foreach (var key in FindKeysInSource())
        {
            foreach (var culture in new[] { EnglishCulture, ChineseCulture })
            {
                var text = new LocalizedString(key).ToString(CultureInfo.GetCultureInfo(culture));

                text.Should().NotBe(key, $"{culture} 下 '{key}' 必须解析成文案，而不是回退成键名");
                text.Should().NotBeNullOrWhiteSpace();
            }
        }
    }

    /// <summary>
    /// 读取内嵌的语言文件。刻意不复用生产侧的解析器：本类要验证的是文件内容本身，
    /// 复用加载器会让加载器的 bug 同时掩盖文件里的 bug。
    /// </summary>
    private static Dictionary<string, string> ReadLocale(string culture)
    {
        var assembly = typeof(LocalizedString).Assembly;
        var suffix = $".Locales.{culture}.txt";
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);

        while (reader.ReadLine() is { } line)
        {
            var content = line.Trim();

            if (content.Length == 0 || content.StartsWith('#'))
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

    /// <summary>
    /// 扫描生成器工程源码，取出全部 <see cref="LocalizedString"/> 的键字面量。
    /// 两种构造写法都要覆盖：<c>new LocalizedString("Key")</c> 与 <c>LocalizedString _x = new("Key")</c>。
    /// </summary>
    private static IReadOnlyList<string> FindKeysInSource()
    {
        var root = System.IO.Path.Combine(RepositoryRoot.Path, "src", "Len.StronglyTypedId.Generators");
        var separator = System.IO.Path.DirectorySeparatorChar;
        var keys = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                || file.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            foreach (Match match in ExplicitKeyPattern().Matches(text))
            {
                keys.Add(match.Groups[1].Value);
            }

            foreach (Match match in TargetTypedKeyPattern().Matches(text))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return [.. keys];
    }
}
