namespace Len.StronglyTypedId.Layered;

/// <summary>
/// 分层领域层里的 Id：声明它的程序集**不引用 Newtonsoft.Json**。
/// </summary>
/// <remarks>
/// 用来复现真实项目里最常见的一种分层：领域模型不依赖序列化基础设施，由宿主应用（引用了
/// Newtonsoft.Json）负责把 Id 写出去。
/// <para>
/// 由此产生两个必须成立的行为：
/// </para>
/// <list type="bullet">
/// <item>宿主应用能自己为它补一份 Newtonsoft 转换器 —— 内嵌转换器只能生成在声明 Id 的那个程序集里，
/// 消费方补一份内嵌的会撞 CS0426；</item>
/// <item>补出来的转换器写出的形态必须与 System.Text.Json 一致（字符串），否则同一份数据在两侧
/// 表示不同，编译无警告、运行不抛异常。</item>
/// </list>
/// </remarks>
[StronglyTypedId]
public partial record struct LayeredOrderId(Guid Value);

/// <summary>
/// 另一个领域层 Id，用于验证同名 Id 在不同程序集里的顶层转换器命名不会互相撞车。
/// </summary>
[StronglyTypedId]
public partial record struct LayeredInvoiceId(Guid Value);
