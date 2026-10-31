---
layout: default
title: StronglyTypedId 中文文档
---

# StronglyTypedId

[English](../index.md) · **中文**

一个源码生成器、分析器与代码修复工具。给一行 `record` 声明加上特性，就得到一个功能完备的强类型 Id：
`IStronglyTypedId<TSelf, TPrimitiveId>` 实现、`IParsable<TSelf>` 支持，以及 JSON /
Entity Framework Core / Swagger 集成。`Guid` 与 `OrderId` 从此不会互相误传。

```csharp
[StronglyTypedId]
public partial record struct OrderId(Guid Value);
```

## 指南

| 页面 | 内容 |
| --- | --- |
| [功能特性](features.md) | 完整清单：每个生成成员、每个配置项、每个集成。 |
| [功能索引](features-index.md) | 每个功能点对应的可运行示例与讲解页面；表格由源码生成并在构建时校验。 |
| [NuGet 包](packages.md) | 五个包的分工、生成器为何单独成包，以及分析器如何抵达每个项目。 |
| [声明一个 Id](declaration.md) | 校验、归一化、命名实例、异常类型、程序集级默认、关闭集成、嵌套类型、`TryCreate` 与 UTF-8 接口、`TypeConverter`。 |
| [生成产物](generated-code.md) | 每个生成成员做什么：实现了哪些接口、`Parse` 与文化、`[Flags]` 枚举的收紧、`ToString()` 与集成入口。 |
| [序列化](serialization.md) | System.Text.Json、Newtonsoft.Json、字典键、JSON 形态、文本前缀、显式注册，以及序列化器不认识的基元。 |
| [集成](integrations.md) | Entity Framework Core、Swashbuckle、Dapper、MessagePack、MongoDB、HotChocolate（GraphQL）、protobuf-net、ASP.NET Core MVC、Minimal API、ASP.NET Core OpenAPI，以及如何手工接上 AutoMapper。 |
| [运行时](runtime.md) | 用 `IStronglyTypedId<,>` 约束泛型，以及运行时反射辅助。 |
| [原生 AOT 与裁剪](aot.md) | 裁剪后什么还能用、三个反射入口，以及 AOT 应用必须提供的 `JsonSerializerContext`。 |
| [测试](testing.md) | `Any.Id<T>()`、AutoFixture 与 Bogus 集成、`StronglyTypedIdConformance`，以及示例程序。 |
| [诊断](diagnostics.md) | 每一条规则、级别划分、是否带代码修复，以及如何配置命名后缀。 |
| [性能](performance.md) | 生成器在构建期的开销，以及生成代码在运行期的开销。 |
| [故障排查](troubleshooting.md) | 按症状索引：什么都没生成、集成缺失、分析器不刷新、解析出乎意料。 |
| [已知限制](limitations.md) | 源码生成器做不到的事（`Value` 的可见性、封闭的基元清单、不受保护的主构造函数）以及各自的绕法。 |
| [常见问题](faq.md) | 最常被问到的问题。 |
| [从 1.3.0 升级](migration.md) | 1.4.0 中四处需要动手的变更、四处能编译通过但会改变行为的变化，以及升级后可按需采用的能力。 |
| [未来规划](roadmap.md) | 暂无；计划中的方向已全部交付。 |

## 其他入口

- [README（中文）](https://github.com/LenFon/StronglyTypedId/blob/main/README.zh-CN.md) 介绍功能特性、
  快速开始、支持的基元类型与生成产物。
- [PACKAGE.md](https://github.com/LenFon/StronglyTypedId/blob/main/PACKAGE.md) 是随 NuGet 包发布的精简版。
- [CHANGELOG（中文）](https://github.com/LenFon/StronglyTypedId/blob/main/CHANGELOG.zh-CN.md) 记录变更。
- [贡献指南](https://github.com/LenFon/StronglyTypedId/blob/main/.github/CONTRIBUTING.md) 说明构建、测试与 CI 门禁。
- [安全策略](https://github.com/LenFon/StronglyTypedId/blob/main/.github/SECURITY.md) 列出支持版本、私有报告渠道，
  以及如何核验你装到的包。
- [NuGet 包](https://www.nuget.org/packages/Len.StronglyTypedId) 提供安装方式。
