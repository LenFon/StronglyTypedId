---
layout: default
title: 常见问题
---

<a id="faq"></a>

# 常见问题

**问：我应该安装哪个包？**

答：只装 `Len.StronglyTypedId` 即可。它依赖 `Len.StronglyTypedId.Generators`，后者包含生成器、分析器
与代码修复；而代码中引用的是前者这个运行时库。另外三个包都是可选的 —— 见 [NuGet 包](packages.md#packages)。

**问：强类型 Id 能直接作为 `Dictionary` / `HashSet` 的键吗？**

答：可以。`record` 与 `record struct` 都会基于被包装的 `Value` 生成结构化相等性、`GetHashCode`、
`==` 与 `!=`。以 Id 为键的字典同样支持序列化——见[字典键](serialization.md#serialization)。

**问：可以对 Id 排序，或放进 `SortedSet<T>` 吗？**

答：可以。每个 Id 都实现了 `IComparable<TSelf>` 与 `<`、`>`、`<=`、`>=`（比较的都是被包装的值），
因此 `OrderBy`、`SortedSet<T>` 与区间判断都无需自定义比较器。引用类型 Id 把 `null` 视为最小、
排在最前，与 `Comparer<T>.Default` 的约定一致。

**问：`ToString()` 输出什么？**

答：输出被包装基元值的**不变文化**文本（`"42"`、`"3f2c…"`、`"1.5"`），而不是 record 默认的
`OrderId { Value = 42 }` 形式，因此在日志里更易读。它刻意与区域设置无关，从而与序列化写出的形态一致，
并且 `Parse(id.ToString())` 必定往返——`Parse` / `TryParse` 把 `null` provider 当作不变文化，正是为了这一点。
需要按特定文化输出时，请调用 `ToString(string?, IFormatProvider?)`。注意字符串插值不走 `ToString()`：
`$"{id}"` 走 `IFormattable.ToString(null, null)`，数值基元会按当前区域格式化。以上两点只对**基元实现了
`IFormattable`** 的 Id 成立——`string` 与枚举基元的 Id 两者都没有，插值也回落到不变文化的 `ToString()`。

**问：设置了 `Validator`，但 `new OrderId(...)` 仍能传入非法值？**

答：主构造函数属于你自己的声明，源码生成器无法往里面注入代码，因此只有生成的入口会校验。JSON、
EF Core 与用户输入都是经由 `Create` / `Parse` / `TryParse` 进入的，校验在实际用到的地方生效——
详见[值校验](declaration.md#value-validation)。

想按症状排查而不是问概念？见[故障排查](troubleshooting.md#troubleshooting)。

[<- 返回目录](index.md) · [English](../faq.md)
