namespace Len.StronglyTypedId;

/// <summary>
/// 诊断的文档补充信息：README 诊断表里「代码修复」一列。
/// </summary>
internal static class DiagnosticDocumentation
{
    /// <summary>
    /// 全部诊断的文档条目，按规则 ID 升序。
    /// </summary>
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
        new DiagnosticDocEntry(Descriptors.ExceptionTypeInvalidId, new DocText("Remove the `ExceptionType` argument", "删掉 `ExceptionType` 实参")),
        new DiagnosticDocEntry(Descriptors.BypassCreateId, new DocText("Replace with `Create(...)`", "改用 `Create(...)`")),
        // 生成器侧诊断：故障发生在生成阶段，没有可机械修复的目标。
        new DiagnosticDocEntry(Descriptors.GeneratorFailureId, DocText.Empty),
        new DiagnosticDocEntry(Descriptors.InvalidPrefixId, new DocText("Remove the `Prefix` argument or give it a non-blank value", "删掉 `Prefix` 实参，或给它一个非空白值")),
        // 无修复：删掉 Prefix 会改变规范文本形，已按带前缀格式落盘的数据将读不回来，只能由人换一个安全前缀。
        new DiagnosticDocEntry(Descriptors.AmbiguousPrefixId, DocText.Empty),
        new DiagnosticDocEntry(Descriptors.ValidatorCanBeBypassedId, new DocText("Narrow the type to `internal`", "收窄为 `internal`")),
        // 命名类提示：正确地重命名一个类型意味着同时改掉所有引用，「这些 Id 看起来一样」也没有机械修法。
        new DiagnosticDocEntry(Descriptors.TypeNameShouldEndWithIdId, DocText.Empty),
        new DiagnosticDocEntry(Descriptors.DuplicatePrimitiveTypeId, DocText.Empty),
        // 缺包不是代码问题：无法替用户添加包引用。但「删掉 Integrations 实参」是机器能安全做的（类型退化为不带该集成的普通 Id），
        // 把「补包 or 去掉标志」的取舍交给人，故给修复。
        new DiagnosticDocEntry(Descriptors.IntegrationUnavailableId, new DocText("Remove the 'Integrations' argument", "删除 'Integrations' 实参")),
        // 越界位同理：删掉 Integrations 实参即可去掉未定义的位，机器能安全做，故给修复。
        new DiagnosticDocEntry(Descriptors.UnknownIntegrationId, new DocText("Remove the 'Integrations' argument", "删除 'Integrations' 实参")),
        // 命名实例：名字非法（不是合法标识符 / 重名 / 撞名）或取值无法解析的两条，给「删掉整条 [StronglyTypedIdInstance]」修复 ——
        // 机器只能让诊断消失，补全业务语义留给人；PascalCase 改名会牵动所有引用点，不是单文件可安全完成的机械修复，故保持无修复。
        new DiagnosticDocEntry(Descriptors.InvalidInstanceNameId, new DocText("Remove the '[StronglyTypedIdInstance]' attribute", "删除 '[StronglyTypedIdInstance]' 特性")),
        new DiagnosticDocEntry(Descriptors.InvalidInstanceValueId, new DocText("Remove the '[StronglyTypedIdInstance]' attribute", "删除 '[StronglyTypedIdInstance]' 特性")),
        new DiagnosticDocEntry(Descriptors.InstanceNameNotPascalCaseId, DocText.Empty),
        new DiagnosticDocEntry(Descriptors.TypeCannotBeFileLocalId, new DocText("Remove the `file` modifier", "去掉 `file` 修饰符")));

    /// <summary>
    /// 单条诊断的文档条目。
    /// </summary>
    internal sealed class DiagnosticDocEntry(string ruleId, DocText fix)
    {
        /// <summary>
        /// 规则 ID（<c>STIA0000</c> 形式）。
        /// </summary>
        public string RuleId { get; } = ruleId;

        /// <summary>
        /// 「代码修复」列的文案；<see cref="DocText.Empty"/> 表示无修复。
        /// </summary>
        public DocText Fix { get; } = fix;
    }
}
