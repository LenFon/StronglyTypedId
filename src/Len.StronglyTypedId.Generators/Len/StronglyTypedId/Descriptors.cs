using System.Globalization;

namespace Len.StronglyTypedId;

internal static class Descriptors
{
    public const string ParameterCannotBeNullableId = "STIAO006";
    public const string ParameterNameMustBeValueId = "STIAO007";
    public const string ParameterTypeIsInvalidId = "STIAO008";
    public const string TypeCannotBeAbstractId = "STIAO002";
    public const string TypeCannotBeGenericId = "STIAO003";
    public const string TypeCannotBeNestedAndMustHaveNamespaceId = "STIAO004";
    public const string TypeMustBePartialId = "STIAO001";
    public const string TypeMustBeRecordId = "STIAO000";
    public const string TypeMustHavePrimaryConstructorWithExactlyHaveOneParameterId = "STIAO005";

    public static readonly DiagnosticDescriptor ParameterCannotBeNullable
        = new(ParameterCannotBeNullableId,
            "ParameterCannotBeNullableTitle".Translate(),
            "ParameterCannotBeNullableMessage".Translate(),
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor ParameterNameMustBeValue
        = new(ParameterNameMustBeValueId,
            "ParameterNameMustBeValueTitle".Translate(),
            "ParameterNameMustBeValueMessage".Translate(),
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor ParameterTypeIsInvalid
        = new(ParameterTypeIsInvalidId,
            "ParameterTypeIsInvalidTitle".Translate(),
            "ParameterTypeIsInvalidMessage".Translate(),
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeCannotBeAbstract
        = new(TypeCannotBeAbstractId,
            "TypeCannotBeAbstractTitle".Translate(),
            "TypeCannotBeAbstractMessage".Translate(),
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeCannotBeGeneric
        = new(TypeCannotBeGenericId,
            "TypeCannotBeGenericTitle".Translate(),
            "TypeCannotBeGenericMessage".Translate(),
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeCannotBeNestedAndMustHaveNamespace
        = new(TypeCannotBeNestedAndMustHaveNamespaceId,
            "TypeCannotBeNestedAndMustHaveNamespaceTitle".Translate(),
            "TypeCannotBeNestedAndMustHaveNamespaceMessage".Translate(),
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustBePartial
        = new(TypeMustBePartialId,
            "TypeMustBePartialTitle".Translate(),
            "TypeMustBePartialMessage".Translate(),
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustBeRecord
       = new(TypeMustBeRecordId,
            "TypeMustBeRecordTitle".Translate(),
            "TypeMustBeRecordMessage".Translate(),
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustHavePrimaryConstructorWithExactlyHaveOneParameter
        = new(TypeMustHavePrimaryConstructorWithExactlyHaveOneParameterId,
            "TypeMustHavePrimaryConstructorWithExactlyHaveOneParameterTitle".Translate(),
            "TypeMustHavePrimaryConstructorWithExactlyHaveOneParameterMessage".Translate(),
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static DiagnosticDescriptor[] All => new[]
    {
        TypeMustBeRecord,
        TypeMustBePartial,
        TypeCannotBeAbstract,
        TypeCannotBeGeneric,
        TypeCannotBeNestedAndMustHaveNamespace,
        TypeMustHavePrimaryConstructorWithExactlyHaveOneParameter,
        ParameterCannotBeNullable,
        ParameterNameMustBeValue,
        ParameterTypeIsInvalid,
    };
}

file static class StringExtensions
{
    private readonly static Lazy<Dictionary<string, string>> _translations = new(() => GetTranslations(), true);

    public static string Translate(this string key) => _translations.Value.ContainsKey(key) ? _translations.Value[key] : key;

    private static Dictionary<string, string> GetTranslations()
    {
        var translations = new Dictionary<string, string>();
        var assembly = typeof(StringExtensions).Assembly;
        var name = $".Locales.{CultureInfo.CurrentCulture.Name}.txt";
        var resourceNames = assembly.GetManifestResourceNames();
        var resourceName = resourceNames.FirstOrDefault(w => w.Contains(name));

        if (resourceName == null)
        {
            name = $".Locales.{CultureInfo.CurrentCulture.TwoLetterISOLanguageName}.txt";
            resourceName = resourceNames.FirstOrDefault(w => w.Contains(name));
        }

        if (resourceName == null)
        {
            name = ".Locales.en.txt";
            resourceName = resourceNames.FirstOrDefault(w => w.Contains(name));
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var streamReader = new StreamReader(stream);

        while (!streamReader.EndOfStream)
        {
            var line = streamReader.ReadLine();
            var isEmpty = string.IsNullOrWhiteSpace(line);
            var isComment = !isEmpty && line.Trim().StartsWith("#");
            var isKeyValuePair = !isEmpty && !isComment && line.Contains("=");

            if (isEmpty || isComment || !isKeyValuePair)
                continue;

            var kvp = line.Split(new[] { '=' }, 2);
            var key = kvp[0].Trim();
            var value = kvp[1].Trim();

            if (key != null && value != null)
            {
                translations.Add(key, value);
            }
        }

        return translations;
    }
}