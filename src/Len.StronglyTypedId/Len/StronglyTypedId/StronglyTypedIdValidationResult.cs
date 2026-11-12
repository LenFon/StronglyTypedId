namespace Len.StronglyTypedId;

/// <summary>
/// 验证器返回的结果：通过，或带原因与可选错误码的拒绝。
/// </summary>
/// <remarks>
/// <para>
/// <see langword="default"/> 必须是<b>通过</b>，这样验证器可以写 <c>return default;</c> 表示接受。
/// 状态用 <c>_isFailure</c> 而非 <c>_isValid</c> 存储，正是为了这个方向：结构体的默认值所有字段为零，
/// 只有「零表示成功」才能让默认值有意义。
/// </para>
/// <para>
/// 拒绝原因只经 <c>TryCreate</c> 的三参重载与 <c>Create</c> 抛出的异常消息传递。序列化集成（JSON、
/// EF Core、Dapper、MessagePack、BSON、protobuf）各自有自己的异常契约，不把 <see cref="Message"/>
/// 塞进框架异常。
/// </para>
/// </remarks>
public readonly struct StronglyTypedIdValidationResult
{
    private StronglyTypedIdValidationResult(bool isFailure, string? message, string? code)
    {
        IsFailure = isFailure;
        Message = message;
        Code = code;
    }

    /// <summary>
    /// 取值是否被接受。
    /// </summary>
    public bool IsValid => !IsFailure;

    /// <summary>
    /// 拒绝原因；通过时为 <see langword="null"/>。
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// 可选的机器可读错误码（如 <c>"E_EMAIL_FORMAT"</c>）；通过或未提供时为 <see langword="null"/>。
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// 是否被拒绝。存「失败」而非「成功」，<see langword="default"/> 才是通过。
    /// </summary>
    private bool IsFailure { get; }

    /// <summary>
    /// 表示「通过」的结果，也是 <see langword="default"/>。
    /// </summary>
    public static StronglyTypedIdValidationResult Success => default;

    /// <summary>
    /// 表示「拒绝」的结果。
    /// </summary>
    /// <param name="message">拒绝原因，会进入 <c>Create</c> 抛出的异常消息。</param>
    /// <param name="code">可选的机器可读错误码。</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> 为 <see langword="null"/>。</exception>
    public static StronglyTypedIdValidationResult Failure(string message, string? code = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new StronglyTypedIdValidationResult(true, message, code);
    }
}
