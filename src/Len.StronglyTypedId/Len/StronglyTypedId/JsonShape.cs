namespace Len.StronglyTypedId;

/// <summary>
/// Specifies the shape a strongly typed id takes in JSON.
/// </summary>
public enum JsonShape
{
    /// <summary>
    /// Serialized as the wrapped primitive value itself, for example <c>"3f2c…"</c> or <c>42</c>.
    /// This is the default.
    /// </summary>
    Primitive = 0,

    /// <summary>
    /// Serialized as a single-property object <c>{ "value": … }</c>, whose property is named
    /// <c>value</c>.
    /// </summary>
    Object = 1,
}
