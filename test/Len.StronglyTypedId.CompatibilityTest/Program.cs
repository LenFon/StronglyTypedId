using Len.StronglyTypedId;

// 生成器没跑时，下面两行会直接编译失败（CS1929 / CS0117）—— 这正是本探针要抓的症状。
var id = Probe.OrderId.Create(Guid.NewGuid());
Console.WriteLine(id);
Console.WriteLine(Probe.OrderId.Parse(id.ToString(), null) == id);

namespace Probe
{
    [StronglyTypedId]
    public partial record struct OrderId(Guid Value);
}
