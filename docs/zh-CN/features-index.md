---
layout: default
title: 功能索引
---

# 功能索引

每个功能点，连同它的可运行示例与讲解页面。这一页回答「这个功能在哪儿能看到」；
「它是什么、为什么这样设计」请看[功能特性](features.md)。

表格由生成器自己的登记表渲染，并在每次构建时校验：某个功能没有示例、或它的文档锚点已不存在，
CI 会直接失败，而不是留下一行过期记录。

<!-- BEGIN GENERATED:feature-index -->
| 功能                                              | 类型       | 示例                                                                                                            | 文档                                                  |
| ------------------------------------------------- | ---------- | --------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------- |
| Create / TryCreate                                | 生成成员   | [`CoreDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| Parse / TryParse（string 与 span）                | 生成成员   | [`CoreDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| 格式化（IFormattable / ISpanFormattable）         | 生成成员   | [`CoreDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| UTF-8 span（IUtf8SpanParsable）                   | 生成成员   | [`CoreDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [declaration](declaration.md#trycreate-and-utf8)      |
| 比较与排序                                        | 生成成员   | [`CoreDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| string 基元的序数比较                             | 生成成员   | [`StringPrimitiveDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/StringPrimitiveDemo.cs)           | [generated-code](generated-code.md)                   |
| 解构                                              | 生成成员   | [`CoreDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| 调试器视图（DebuggerDisplay / DebuggerTypeProxy） | 生成成员   | [`DebuggerViewDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/DebuggerViewDemo.cs)                 | [generated-code](generated-code.md)                   |
| 反射助手                                          | 运行时 API | [`ReflectionDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/ReflectionDemo.cs)                     | [runtime](runtime.md#reflection-helpers)              |
| 泛型约束（IStronglyTypedId<TSelf>）               | 运行时 API | [`StructureDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/StructureDemo.cs)                       | [runtime](runtime.md#using-the-interface)             |
| 基元矩阵（23 种 + enum）                          | 基元       | [`PrimitiveMatrixDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/PrimitiveMatrixDemo.cs)           | [generated-code](generated-code.md)                   |
| 枚举基元（含 [Flags]）                            | 基元       | [`FlagsDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/FlagsDemo.cs)                               | [generated-code](generated-code.md#enum-backed-ids)   |
| 自定义基元                                        | 基元       | [`CustomPrimitiveDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/CustomPrimitiveDemo.cs)           | [limitations](limitations.md#primitive-types)         |
| 引用类型 Id（record class）                       | 基元       | [`ReferenceTypeDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/ReferenceTypeDemo.cs)               | [generated-code](generated-code.md)                   |
| 嵌套类型与泛型容器                                | 声明       | [`StructureDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/StructureDemo.cs)                       | [declaration](declaration.md#nested-types)            |
| 验证器（Validator）                               | 校验       | [`ValidationDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/ValidationDemo.cs)                     | [declaration](declaration.md#value-validation)        |
| 归一化（Normalizer）                              | 校验       | [`ValidationDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/ValidationDemo.cs)                     | [declaration](declaration.md#value-normalization)     |
| 自定义失败异常（ExceptionType）                   | 校验       | [`ExceptionDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/ExceptionDemo.cs)                       | [declaration](declaration.md#value-validation)        |
| 消息脱敏（RedactValidationMessage）               | 校验       | [`ExceptionDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/ExceptionDemo.cs)                       | [declaration](declaration.md#value-validation)        |
| 命名实例                                          | 配置       | [`InstanceDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/InstanceDemo.cs)                         | [declaration](declaration.md#named-instances)         |
| 文本前缀（Prefix / PrefixReadMode）               | 配置       | [`TextDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/TextDemo.cs)                                 | [serialization](serialization.md#serialization)       |
| JSON 形态（JsonShape）                            | 配置       | [`TextDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/TextDemo.cs)                                 | [serialization](serialization.md#serialization)       |
| TypeConverter                                     | 配置       | [`ConfigurationBindingDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/ConfigurationBindingDemo.cs) | [declaration](declaration.md#type-converter)          |
| 配置绑定（IConfiguration）                        | 配置       | [`ConfigurationBindingDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/ConfigurationBindingDemo.cs) | [declaration](declaration.md#type-converter)          |
| 集成过滤与白名单（Integrations）                  | 配置       | [`IntegrationsDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/IntegrationsDemo.cs)                 | [declaration](declaration.md#integrations-filter)     |
| 程序集级默认值（StronglyTypedIdDefaults）         | 配置       | [`Program`](../../samples/Len.StronglyTypedId.SampleDefaults/Program.cs)                                        | [declaration](declaration.md#assembly-defaults)       |
| 文化不变性                                        | 配置       | [`CultureDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/CultureDemo.cs)                           | [generated-code](generated-code.md)                   |
| System.Text.Json 字典键与边界                     | 序列化     | [`JsonEdgeCasesDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/JsonEdgeCasesDemo.cs)               | [serialization](serialization.md#serialization)       |
| 源生成 JsonSerializerContext                      | 序列化     | [`SourceGeneratedJsonDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/SourceGeneratedJsonDemo.cs)   | [aot](aot.md)                                         |
| Newtonsoft.Json 字典键                            | 序列化     | [`NewtonsoftJsonDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/NewtonsoftJsonDemo.cs)             | [serialization](serialization.md#serialization)       |
| 测试辅助（Any / AnyIdOptions / Conformance）      | 测试       | [`TestingDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/TestingDemo.cs)                           | [testing](testing.md#testing)                         |
| AutoFixture 与 Bogus 适配                         | 测试       | [`TestingDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/TestingDemo.cs)                           | [testing](testing.md#testing)                         |
| System.Text.Json                                  | 集成       | [`SystemTextJsonDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/SystemTextJsonDemo.cs)             | [serialization](serialization.md#serialization)       |
| Newtonsoft.Json                                   | 集成       | [`NewtonsoftJsonDemo`](../../samples/Len.StronglyTypedId.SampleConsole/Demos/NewtonsoftJsonDemo.cs)             | [serialization](serialization.md#serialization)       |
| Microsoft.EntityFrameworkCore                     | 集成       | [`EfCoreDemo`](../../samples/Len.StronglyTypedId.SampleIntegrations/EfCoreDemo.cs)                              | [integrations](integrations.md#entity-framework-core) |
| Swashbuckle.AspNetCore.SwaggerGen                 | 集成       | [`Program`](../../samples/Len.StronglyTypedId.Sample3/Program.cs)                                               | [integrations](integrations.md#swashbuckle)           |
| Dapper                                            | 集成       | [`DapperDemo`](../../samples/Len.StronglyTypedId.SampleIntegrations/DapperDemo.cs)                              | [integrations](integrations.md#dapper)                |
| MessagePack                                       | 集成       | [`BinarySerializationDemo`](../../samples/Len.StronglyTypedId.SampleIntegrations/BinarySerializationDemo.cs)    | [integrations](integrations.md#messagepack)           |
| MongoDB.Bson                                      | 集成       | [`BinarySerializationDemo`](../../samples/Len.StronglyTypedId.SampleIntegrations/BinarySerializationDemo.cs)    | [integrations](integrations.md#mongodb)               |
| HotChocolate.Types                                | 集成       | [`Program`](../../samples/Len.StronglyTypedId.SampleGraphQL/Program.cs)                                         | [integrations](integrations.md#hotchocolate)          |
| protobuf-net                                      | 集成       | [`ProtobufDemo`](../../samples/Len.StronglyTypedId.SampleIntegrations/ProtobufDemo.cs)                          | [integrations](integrations.md#protobuf-net)          |
| Microsoft.AspNetCore.OpenApi                      | 集成       | [`Program`](../../samples/Len.StronglyTypedId.Sample2/Program.cs)                                               | [integrations](integrations.md#aspnetcore-openapi)    |
| Microsoft.AspNetCore.Mvc                          | 集成       | [`Program`](../../samples/Len.StronglyTypedId.Sample1/Program.cs)                                               | [integrations](integrations.md#aspnet-core-mvc)       |
<!-- END GENERATED:feature-index -->

[<- 返回目录](index.md) · [English](../features-index.md)
