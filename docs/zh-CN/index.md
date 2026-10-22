---
layout: default
title: StronglyTypedId 中文文档
---

# StronglyTypedId

[English](../index.md) · **中文**

一个源生成器 + 分析器 + 代码修复器：把一行 `record` 声明变成一个功能完整的强类型 Id。它会生成
`IStronglyTypedId<TSelf, TPrimitiveId>` 实现、`IParsable<TSelf>` 支持，以及 JSON / Entity Framework Core /
Swagger 集成——于是 `Guid` 不能出现在本该是 `OrderId` 的位置，反之亦然。

```csharp
[StronglyTypedId]
public partial record struct OrderId(Guid Value);
```

## 指南

| 页面 | 内容 |
| --- | --- |
| [NuGet 包](packages.md) | 四个包的分工、生成器为何单独成包，以及分析器如何抵达每个项目。 |
| [声明一个 Id](declaration.md) | 校验、归一化、命名实例、异常类型、程序集级默认、关闭集成、嵌套类型、`TryCreate` 与 UTF-8 接口、`TypeConverter`。 |
| [生成产物](generated-code.md) | 每个生成成员做什么：实现了哪些接口、`Parse` 与文化、`[Flags]` 枚举的收紧、`ToString()` 与集成入口。 |
| [序列化](serialization.md) | System.Text.Json、Newtonsoft.Json、字典键、JSON 形态、文本前缀、显式注册，以及序列化器不认识的基元。 |
| [集成](integrations.md) | Entity Framework Core、Swashbuckle、Dapper、MessagePack、MongoDB、ASP.NET Core MVC、Minimal API、ASP.NET Core OpenAPI。 |
| [运行时](runtime.md) | 用 `IStronglyTypedId<,>` 约束泛型，以及运行时反射辅助。 |
| [原生 AOT 与裁剪](aot.md) | 裁剪后什么还能用、反射辅助方法，以及 AOT 应用必须提供的 `JsonSerializerContext`。 |
| [测试](testing.md) | `Any.Id<T>()`、AutoFixture 集成，以及示例程序。 |
| [诊断](diagnostics.md) | 每一条规则、级别划分，以及是否带代码修复。 |
| [性能](performance.md) | 生成器在构建期的开销、生成代码在运行期的开销，以及按分配量判定的回归门禁。 |
| [故障排查](troubleshooting.md) | 按症状索引：什么都没生成、集成缺失、分析器不刷新、解析出乎意料。 |
| [已知限制](limitations.md) | 源码生成器做不到的事 —— `Value` 的可见性、封闭的基元清单、不受保护的主构造函数 —— 以及各自的绕法。 |
| [常见问题](faq.md) | 最常被问到的问题。 |

## 其他入口

- [README（中文）](https://github.com/LenFon/StronglyTypedId/blob/main/README.zh-CN.md) —— 功能特性、
  环境要求、快速开始、支持的基元类型与生成产物。
- [PACKAGE.md](https://github.com/LenFon/StronglyTypedId/blob/main/PACKAGE.md) —— 随 NuGet 包发布的精简版。
- [CHANGELOG（中文）](https://github.com/LenFon/StronglyTypedId/blob/main/CHANGELOG.zh-CN.md) —— 变更记录。
- [贡献指南](https://github.com/LenFon/StronglyTypedId/blob/main/CONTRIBUTING.md) —— 构建、测试与 CI 门禁。
- [安全策略](https://github.com/LenFon/StronglyTypedId/blob/main/SECURITY.md) —— 支持版本、私有报告渠道，
  以及如何核验你装到的包。
- [NuGet 包](https://www.nuget.org/packages/Len.StronglyTypedId) —— 安装方式。
