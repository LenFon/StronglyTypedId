namespace Len.StronglyTypedId;

/// <summary>
/// 诊断的文档补充信息：README 诊断表里「代码修复」一列。
/// </summary>
/// <remarks>
/// 规则 ID、级别与文案都能从 <see cref="Descriptors"/> 读出，唯独「代码修复」一列无法反射 ——
/// 修复动作写在 <c>StronglyTypedIdCodeFixProvider</c> 的 switch 里。故单独成表，并由
/// <c>ReadmeDocumentationTests</c> 断言与描述符一一对应。
/// </remarks>
internal static class DiagnosticDocumentation
{
    /// <summary>
    /// 全部诊断的文档条目，按规则 ID 升序。
    /// </summary>
    /// <remarks><see cref="DocText.Empty"/> 表示无代码修复（README 渲染为 <c>—</c>）；有修复的集合须与 <c>FixableDiagnosticIds</c> 相等。</remarks>
    internal static readonly ImmutableArray<DiagnosticDocEntry> Entries = ImmutableArray.Create(
        new DiagnosticDocEntry(Descriptors.TypeMustBeRecordId, new DocText("Convert to `record`", "转为 `record`")),
        new DiagnosticDocEntry(Descriptors.TypeMustBePartialId, new DocText("Add `partial`", "补上 `partial`")),
        new DiagnosticDocEntry(Descriptors.TypeCannotBeAbstractId, new DocText("Remove `abstract`", "去掉 `abstract`")),
        new DiagnosticDocEntry(Descriptors.TypeCannotBeGenericId, new DocText("Remove the type parameter list", "移除类型参数表")),
        new DiagnosticDocEntry(Descriptors.TypeMustHaveNamespaceId, new DocText("Wrap in a namespace", "包进命名空间")),
        new DiagnosticDocEntry(Descriptors.TypeMustHaveSingleParameterPrimaryConstructorId, new DocText("Add a primary constructor", "补主构造函数")),
        new DiagnosticDocEntry(Descriptors.ParameterCannotBeNullableId, new DocText("Remove the `?` suffix", "去掉 `?` 后缀")),
        new DiagnosticDocEntry(Descriptors.ParameterNameMustBeValueId, new DocText("Rename the parameter", "重命名参数")),
        new DiagnosticDocEntry(Descriptors.ParameterTypeIsInvalidId, new DocText("Change the type to `Guid`", "改为 `Guid`")),
        new DiagnosticDocEntry(Descriptors.ContainingTypeMustBePartialId, new DocText("Add `partial` to the container", "给包含类型补 `partial`")),
        new DiagnosticDocEntry(Descriptors.ValidatorReferenceInvalidId, new DocText("Remove the `Validator` argument", "删掉 `Validator` 实参")),
        new DiagnosticDocEntry(Descriptors.NormalizerReferenceInvalidId, new DocText("Remove the `Normalizer` argument", "删掉 `Normalizer` 实参")),
        new DiagnosticDocEntry(Descriptors.BypassCreateId, new DocText("Replace with `Create(...)`", "改用 `Create(...)`")),
        // 生成器侧诊断：故障发生在生成阶段，没有可机械修复的目标。
        new DiagnosticDocEntry(Descriptors.GeneratorFailureId, DocText.Empty),
        new DiagnosticDocEntry(Descriptors.InvalidPrefixId, new DocText("Remove the `Prefix` argument or give it a non-blank value", "删掉 `Prefix` 实参，或给它一个非空白值")),
        new DiagnosticDocEntry(Descriptors.ValidatorCanBeBypassedId, new DocText("Narrow the type to `internal`", "收窄为 `internal`")),
        // 命名类提示：正确地重命名一个类型意味着同时改掉所有引用，「这些 Id 看起来一样」也没有机械修法。
        new DiagnosticDocEntry(Descriptors.TypeNameShouldEndWithIdId, DocText.Empty),
        new DiagnosticDocEntry(Descriptors.DuplicatePrimitiveTypeId, DocText.Empty));

    /// <summary>
    /// 单条诊断的文档条目。
    /// </summary>
    internal sealed class DiagnosticDocEntry(string ruleId, DocText fix)
    {
        /// <summary>规则 ID（<c>STIA0000</c> 形式）。</summary>
        public string RuleId { get; } = ruleId;

        /// <summary>「代码修复」列的文案；<see cref="DocText.Empty"/> 表示无修复。</summary>
        public DocText Fix { get; } = fix;
    }
}
