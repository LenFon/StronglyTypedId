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
            Resources.ParameterCannotBeNullableTitle,
            Resources.ParameterCannotBeNullableMessage,
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor ParameterNameMustBeValue
        = new(ParameterNameMustBeValueId,
            Resources.ParameterNameMustBeValueTitle,
            Resources.ParameterNameMustBeValueMessage,
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor ParameterTypeIsInvalid
        = new(ParameterTypeIsInvalidId,
            Resources.ParameterTypeIsInvalidTitle,
            Resources.ParameterTypeIsInvalidMessage,
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeCannotBeAbstract
        = new(TypeCannotBeAbstractId,
            Resources.TypeCannotBeAbstractTitle,
            Resources.TypeCannotBeAbstractMessage,
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeCannotBeGeneric
        = new(TypeCannotBeGenericId,
            Resources.TypeCannotBeGenericTitle,
            Resources.TypeCannotBeGenericMessage,
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeCannotBeNestedAndMustHaveNamespace
        = new(TypeCannotBeNestedAndMustHaveNamespaceId,
            Resources.TypeCannotBeNestedAndMustHaveNamespaceTitle,
            Resources.TypeCannotBeNestedAndMustHaveNamespaceMessage,
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustBePartial
        = new(TypeMustBePartialId,
            Resources.TypeMustBePartialTitle,
            Resources.TypeMustBePartialMessage,
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustBeRecord
       = new(TypeMustBeRecordId,
            Resources.TypeMustBeRecordTitle,
            Resources.TypeMustBeRecordMessage,
            "StronglyTypedIdAnalyzer",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustHavePrimaryConstructorWithExactlyHaveOneParameter
        = new(TypeMustHavePrimaryConstructorWithExactlyHaveOneParameterId,
            Resources.TypeMustHavePrimaryConstructorWithExactlyHaveOneParameterTitle,
            Resources.TypeMustHavePrimaryConstructorWithExactlyHaveOneParameterMessage,
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