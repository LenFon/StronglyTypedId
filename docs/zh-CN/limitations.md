---
layout: default
title: 已知限制
---

<a id="limitations"></a>

# 已知限制

有四件事生成器替你做不了。每一件都有绕法，也都不是 bug —— 它们都由「源码生成器被允许碰什么」决定。

<a id="value-is-public"></a>

## `Value` 恒为 public

该属性来自位置记录的主构造函数，源码生成器无法改变你亲手声明的成员的可见性。若要改成 `internal`，
就只能手写整个类型 —— 而那正是本库要消灭的一行声明。

请把 `Value` 当作逃生舱口：持久化映射与互操作时用它，但别让它出现在领域层的签名里。确实需要硬边界
时，把 Id 声明为 `internal`，只对外暴露应用真正需要的操作。

`STIA0014` 会在「Id 对外可见、其验证器可被 `new` 绕过」时给出提示。它默认关闭：多数代码库接受这个
取舍，而且在随手 `new` 一个 Id 的测试里，这条提示会很吵。

<a id="primitive-types"></a>

## 只支持列出的基元类型

内置清单是封闭的，见
[支持的基元类型](https://github.com/LenFon/StronglyTypedId/blob/main/README.zh-CN.md#supported-primitive-types)。
但它可以扩展：你自己写的类型只要实现 `IParsable<TSelf>`、`IComparable<TSelf>` 与非泛型 `IComparable`，
并暴露公开静态的 `Parse` / `TryParse`，就会被接受 —— 见
[自定义基元](https://github.com/LenFon/StronglyTypedId/blob/main/README.zh-CN.md#custom-primitives)。
不满足该契约的类型由 `STIA0008` 拒绝。

`Uri` 即使满足契约也不支持：`Uri.Equals` 忽略 fragment，并对大小写与默认端口做归一化，于是两个
不同的 URI 会比较相等、静默塌成同一个 Id —— 而这正是强类型 Id 要防的那类 bug。请把这类值包进受支持的
基元（通常是 `string`），在验证器里校验格式：

```csharp
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct ResourceUri(string Value)
{
    private static bool Validate(string value) => Uri.TryCreate(value, UriKind.Absolute, out _);
}
```

<a id="primary-constructor"></a>

## 主构造函数不受保护

`new OrderId(...)` 会同时绕过 `Normalizer` 与 `Validator`；只有 `Create`、`TryCreate`、`Parse` 与
`TryParse` 会执行它们 —— 见[值归一化](declaration.md#value-normalization)。源码生成器无法往你声明的
主构造函数里注入代码。

实践中这很少成为问题，因为跨边界的取值本来就走生成的入口：JSON、EF Core、Dapper、MVC 模型绑定与
用户输入最终都落在 `Create` 或 `TryParse` 上。唯一跳过校验的路径是在自己的领域代码里直接构造 ——
而这通常正是你想要的，因为那里恰恰是不变量确定成立的地方。

<a id="generic-containers"></a>

## 泛型容器里的 Id 拿不到「外部引用类」集成

`Outer<T>.InnerId` 本身会生成，但无法自动接线：特性实参不能引用携带容器类型参数的转换器（CS0416），
因此 `[JsonConverter]` 无法生成；`AddStronglyTypedIds` 入口也会跳过它，因为开放泛型不能作为
类型实参传入。转换器类本身是生成的 —— 请为你实际使用的闭合类型手工注册，见
[嵌套类型](declaration.md#nested-types)。

`[DebuggerTypeProxy]` 出于同样的原因被跳过，而且与转换器不同，它的代理类也不生成：代理类是私有的，
没有手工接线的可能，调试器里展开该 Id 时只会显示 record 自己的成员。

[<- 返回目录](index.md) · [English](../limitations.md)
