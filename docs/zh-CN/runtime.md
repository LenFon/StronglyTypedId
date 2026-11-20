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

`Create` 在 `Validator` 拒绝取值时抛异常。接口同时声明了 `TryCreate`，因此泛型代码不必知道具体 Id 类型
也能走非抛异常路径：

```csharp
public bool TryCreateId<TId>(Guid value, out TId id)
    where TId : IStronglyTypedId<TId, Guid>
    => TId.TryCreate(value, out id);
```

每个 Id 同时实现了单参数的 `IStronglyTypedId<TSelf>`，因此约束可以只表达「某个强类型 Id」而不必指明
基元类型，日志、缓存、校验辅助方法这类把 Id 当作不透明值处理的场景很合用。该接口本身已继承
`IParsable<TSelf>`、`ISpanParsable<TSelf>`、`IEquatable<TSelf>`、`IComparable<TSelf>` 与
`IComparisonOperators<TSelf, TSelf, bool>`，所以这样的约束依然能从 `string` 或 `ReadOnlySpan<char>`
调用 `TId.Parse(...)`、用类型安全的 `Equals` 比较、使用 `==` / `!=`，并用 `CompareTo` 或 `<` / `>`
排序，无需知道基元类型：

```csharp
public static TId Max<TId>(IEnumerable<TId> ids)
    where TId : IStronglyTypedId<TId>
    => ids.Max()!;                       // IComparable<TSelf> 就够 LINQ 用

public static bool IsBefore<TId>(TId left, TId right)
    where TId : IStronglyTypedId<TId>
    => left < right;                     // IComparisonOperators<TSelf, TSelf, bool>

public static bool Same<TId>(TId left, TId right)
    where TId : IStronglyTypedId<TId>
    => left.Equals(right);               // IEquatable<TSelf>：不经 object.Equals 装箱

public static TId FromSpan<TId>(ReadOnlySpan<char> text)
    where TId : IStronglyTypedId<TId>
    => TId.Parse(text, null);            // ISpanParsable<TSelf>：不产生中间 string
```

把 `IEquatable<TSelf>` 与 `ISpanParsable<TSelf>` 声明在接口上，泛型代码才能直接调到这两个成员。
否则编译器会把 `left.Equals(right)` 绑定到 `object.Equals(object)`，值类型 Id 每次比较都要装箱；
span 也只能先 `ToString()` 再解析。

两者都带**默认接口实现** —— span 成员转发到 `IParsable<TSelf>`，`Equals` 转发到 `object.Equals` ——
因此手写实现该接口只需提供 `IParsable` 的两个成员，原样就能编译。生成的 Id 用真正的 span 解析与结构
相等覆盖它们。报告拒绝原因的四参 `TryParse` 是同样安排的 `static virtual` 成员：默认实现转发到两参
`TryParse` 并返回一条通用消息，生成的 Id 用验证器的真实结果覆盖它。

`IFormattableStronglyTypedId<TSelf>` 在此基础上进一步收窄到「基元可格式化」的 Id。

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
类型与 `typeof(OrderId)` 相同，与 MVC 模型绑定器和 EF Core 转换器的口径一致。

判定结果按 `Type` 缓存，且缓存对键持弱引用。因此加载进可回收 `AssemblyLoadContext` 的类型（插件、
热重载、会重复加载程序集的测试宿主）不会因此失去可回收性：一旦插件不再被其它地方引用，该上下文
及其类型会和对应的缓存记录一起被回收。若一个类型同时实现多份 `IStronglyTypedId<TSelf, TPrimitiveId>`，
以 `TSelf` 等于该类型自身的那份为准，报告的基元类型不会随接口枚举顺序变化。

[<- 返回目录](index.md) · [English](../runtime.md)
