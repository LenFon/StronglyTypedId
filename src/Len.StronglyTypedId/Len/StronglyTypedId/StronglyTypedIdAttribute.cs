namespace Len.StronglyTypedId;

/// <summary>
/// Marks a partial record (or record struct) as a strongly typed id.
/// </summary>
/// <remarks>
/// The annotated type must declare exactly one primary constructor parameter, and that parameter
/// must be named <c>Value</c>. The source generator then emits the strongly typed id implementation,
/// the <c>IParsable&lt;TSelf&gt;</c> support and the JSON / Entity Framework Core / Swagger integrations.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public class StronglyTypedIdAttribute : Attribute
{
}
