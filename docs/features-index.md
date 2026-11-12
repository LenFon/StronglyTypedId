---
layout: default
title: Feature index
---

# Feature index

Every feature, with a runnable sample and the page that explains it. This page answers "where can I
see this work?"; for "what is this and why is it designed that way?", read [Features](features.md).

The table is generated from the generator's own registration table and checked on every build: if a
feature has no sample or its documentation anchor no longer exists, CI fails instead of leaving a
stale row behind.

<!-- BEGIN GENERATED:feature-index -->
| Feature                                             | Kind             | Sample                                                                                                       | Documentation                                         |
| --------------------------------------------------- | ---------------- | ------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------- |
| Create / TryCreate                                  | Generated member | [`CoreDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| Parse / TryParse (string and span)                  | Generated member | [`CoreDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| Formatting (IFormattable / ISpanFormattable)        | Generated member | [`CoreDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| UTF-8 span (IUtf8SpanParsable)                      | Generated member | [`CoreDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [declaration](declaration.md#trycreate-and-utf8)      |
| Comparison and ordering                             | Generated member | [`CoreDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| Ordinal comparison for string primitives            | Generated member | [`StringPrimitiveDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/StringPrimitiveDemo.cs)           | [generated-code](generated-code.md)                   |
| Deconstruction                                      | Generated member | [`CoreDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/CoreDemo.cs)                                 | [generated-code](generated-code.md)                   |
| Debugger view (DebuggerDisplay / DebuggerTypeProxy) | Generated member | [`DebuggerViewDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/DebuggerViewDemo.cs)                 | [generated-code](generated-code.md)                   |
| Reflection helpers                                  | Runtime API      | [`ReflectionDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/ReflectionDemo.cs)                     | [runtime](runtime.md#reflection-helpers)              |
| Generic constraint (IStronglyTypedId<TSelf>)        | Runtime API      | [`StructureDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/StructureDemo.cs)                       | [runtime](runtime.md#using-the-interface)             |
| Primitive matrix (23 types + enum)                  | Primitive        | [`PrimitiveMatrixDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/PrimitiveMatrixDemo.cs)           | [generated-code](generated-code.md)                   |
| Enum primitives (including [Flags])                 | Primitive        | [`FlagsDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/FlagsDemo.cs)                               | [generated-code](generated-code.md#enum-backed-ids)   |
| Custom primitives                                   | Primitive        | [`CustomPrimitiveDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/CustomPrimitiveDemo.cs)           | [limitations](limitations.md#primitive-types)         |
| Reference-type ids (record class)                   | Primitive        | [`ReferenceTypeDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/ReferenceTypeDemo.cs)               | [generated-code](generated-code.md)                   |
| Nested types and generic containers                 | Declaration      | [`StructureDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/StructureDemo.cs)                       | [declaration](declaration.md#nested-types)            |
| Value validation (Validator)                        | Validation       | [`ValidationDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/ValidationDemo.cs)                     | [declaration](declaration.md#value-validation)        |
| Value normalization (Normalizer)                    | Validation       | [`ValidationDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/ValidationDemo.cs)                     | [declaration](declaration.md#value-normalization)     |
| Custom failure type (ExceptionType)                 | Validation       | [`ExceptionDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/ExceptionDemo.cs)                       | [declaration](declaration.md#value-validation)        |
| Redacted messages (RedactValidationMessage)         | Validation       | [`ExceptionDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/ExceptionDemo.cs)                       | [declaration](declaration.md#value-validation)        |
| Named instances                                     | Configuration    | [`InstanceDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/InstanceDemo.cs)                         | [declaration](declaration.md#named-instances)         |
| Text prefix (Prefix / PrefixReadMode)               | Configuration    | [`TextDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/TextDemo.cs)                                 | [serialization](serialization.md#serialization)       |
| JSON shape (JsonShape)                              | Configuration    | [`TextDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/TextDemo.cs)                                 | [serialization](serialization.md#serialization)       |
| TypeConverter                                       | Configuration    | [`ConfigurationBindingDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/ConfigurationBindingDemo.cs) | [declaration](declaration.md#type-converter)          |
| Configuration binding (IConfiguration)              | Configuration    | [`ConfigurationBindingDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/ConfigurationBindingDemo.cs) | [declaration](declaration.md#type-converter)          |
| Integration filter (Integrations)                   | Configuration    | [`IntegrationsDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/IntegrationsDemo.cs)                 | [declaration](declaration.md#integrations-filter)     |
| Assembly-level defaults (StronglyTypedIdDefaults)   | Configuration    | [`Program`](../samples/Len.StronglyTypedId.SampleDefaults/Program.cs)                                        | [declaration](declaration.md#assembly-defaults)       |
| Culture invariance                                  | Configuration    | [`CultureDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/CultureDemo.cs)                           | [generated-code](generated-code.md)                   |
| System.Text.Json dictionary keys and edge cases     | Serialization    | [`JsonEdgeCasesDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/JsonEdgeCasesDemo.cs)               | [serialization](serialization.md#serialization)       |
| Source-generated JsonSerializerContext              | Serialization    | [`SourceGeneratedJsonDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/SourceGeneratedJsonDemo.cs)   | [aot](aot.md)                                         |
| Newtonsoft.Json dictionary keys                     | Serialization    | [`NewtonsoftJsonDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/NewtonsoftJsonDemo.cs)             | [serialization](serialization.md#serialization)       |
| Test helpers (Any / AnyIdOptions / Conformance)     | Testing          | [`TestingDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/TestingDemo.cs)                           | [testing](testing.md#testing)                         |
| AutoFixture and Bogus adapters                      | Testing          | [`TestingDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/TestingDemo.cs)                           | [testing](testing.md#testing)                         |
| System.Text.Json                                    | Integration      | [`SystemTextJsonDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/SystemTextJsonDemo.cs)             | [serialization](serialization.md#serialization)       |
| Newtonsoft.Json                                     | Integration      | [`NewtonsoftJsonDemo`](../samples/Len.StronglyTypedId.SampleConsole/Demos/NewtonsoftJsonDemo.cs)             | [serialization](serialization.md#serialization)       |
| Microsoft.EntityFrameworkCore                       | Integration      | [`EfCoreDemo`](../samples/Len.StronglyTypedId.SampleIntegrations/EfCoreDemo.cs)                              | [integrations](integrations.md#entity-framework-core) |
| Swashbuckle.AspNetCore.SwaggerGen                   | Integration      | [`Program`](../samples/Len.StronglyTypedId.Sample3/Program.cs)                                               | [integrations](integrations.md#swashbuckle)           |
| Dapper                                              | Integration      | [`DapperDemo`](../samples/Len.StronglyTypedId.SampleIntegrations/DapperDemo.cs)                              | [integrations](integrations.md#dapper)                |
| MessagePack                                         | Integration      | [`BinarySerializationDemo`](../samples/Len.StronglyTypedId.SampleIntegrations/BinarySerializationDemo.cs)    | [integrations](integrations.md#messagepack)           |
| MongoDB.Bson                                        | Integration      | [`BinarySerializationDemo`](../samples/Len.StronglyTypedId.SampleIntegrations/BinarySerializationDemo.cs)    | [integrations](integrations.md#mongodb)               |
| HotChocolate.Types                                  | Integration      | [`Program`](../samples/Len.StronglyTypedId.SampleGraphQL/Program.cs)                                         | [integrations](integrations.md#hotchocolate)          |
| protobuf-net                                        | Integration      | [`ProtobufDemo`](../samples/Len.StronglyTypedId.SampleIntegrations/ProtobufDemo.cs)                          | [integrations](integrations.md#protobuf-net)          |
| Microsoft.AspNetCore.OpenApi                        | Integration      | [`Program`](../samples/Len.StronglyTypedId.Sample2/Program.cs)                                               | [integrations](integrations.md#aspnetcore-openapi)    |
| Microsoft.AspNetCore.Mvc                            | Integration      | [`Program`](../samples/Len.StronglyTypedId.Sample1/Program.cs)                                               | [integrations](integrations.md#aspnet-core-mvc)       |
<!-- END GENERATED:feature-index -->

[<- Back to the index](index.md) · [中文](zh-CN/features-index.md)
