---
layout: default
title: 运行时
---

# 运行时

<a id="using-the-interface"></a>

## 用接口约束泛型

每个生成的 Id 都实现了 `IStronglyTypedId<TSelf, TPrimitiveId>`，因此你可以编写适用于任意强类型 Id 的泛型代码：

```csharp
public class Repository<TId>
    where TId : IStronglyTypedId<TId, Guid>
{
    public TId CreateId(Guid value) => TId.Create(value);
}
```

每个 Id 同时实现了单参数的 `IStronglyTypedId<TSelf>`，因此约束可以只表达「某个强类型 Id」而不必指明
基元类型——日志、缓存、校验辅助方法这类把 Id 当作不透明值处理的场景很合用。该接口本身已继承
`IParsable<TSelf>` 与 `IEqualityOperators<TSelf, TSelf, bool>`，所以这样的约束依然能调用
`TId.Parse(...)` 并使用 `==` / `!=`，无需知道基元类型。`IFormattableStronglyTypedId<TSelf>` 在此基础上
进一步收窄到「基元可格式化」的 Id。

<a id="reflection-helpers"></a>

## 运行时反射

包还提供 `StronglyTypedIdExtensions`，用于在运行时检查类型：

- `bool IsStronglyTypedId(this Type type)`
- `Type? GetPrimitiveIdType(this Type type)`
- `bool TryGetPrimitiveIdType(this Type type, out Type? primitiveIdType)`

```csharp
if (typeof(OrderId).IsStronglyTypedId())
{
    Type primitive = typeof(OrderId).GetPrimitiveIdType()!; // typeof(Guid)
}
```

该判断基于所实现的接口，因此同样能识别由其它程序集生成的强类型 Id。接口、抽象类型、枚举与数组
永远不会被判定为强类型 Id。可空 Id（`OrderId?`）按其底层 Id 判定，`typeof(OrderId?)` 得到的基元
类型与 `typeof(OrderId)` 相同 —— 与 MVC 模型绑定器和 EF Core 转换器的口径一致。

[<- 返回目录](index.md) · [English](../runtime.md)
