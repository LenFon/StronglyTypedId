---
layout: default
title: NuGet 包
---

<a id="packages"></a>

# NuGet 包

要装的只有一个包。另外三个确实存在，其中两个你可能用得上。

| 包 | 目标框架 | 内容 | 什么时候装 |
| --- | --- | --- | --- |
| `Len.StronglyTypedId` | `net8.0`、`net10.0` | 运行时库：`IStronglyTypedId<,>`、反射辅助，以及生成代码所依赖的类型 | 总是 —— 代码里引用的就是它 |
| `Len.StronglyTypedId.Generators` | `netstandard2.0` | 源码生成器、分析器与代码修复 | 永不直接装：主包已依赖它 |
| `Len.StronglyTypedId.Testing` | `net8.0`、`net10.0` | `Any.Id<T>()`、`Any.Ids`、`Any.Register`、`Any.Reset`、`AnyIdGenerator` 与 `AnyIdOptions` | 需要造可信 Id 的测试工程 —— 见[测试](testing.md#testing) |
| `Len.StronglyTypedId.Testing.AutoFixture` | `net8.0`、`net10.0` | `WithStronglyTypedIds()`，教 AutoFixture 填充 Id | 已经在用 AutoFixture 的测试工程 |

```shell
dotnet add package Len.StronglyTypedId
```

<a id="why-two-packages"></a>

## 为什么生成器要单独成包

分析器必须加载在编译器进程里，因此它目标框架是 `netstandard2.0`，并针对 **Roslyn 5.9**
（`Microsoft.CodeAnalysis` 5.9.0）编译。宿主编译器必须至少这么新：在更旧的 SDK 上构建会报 `CS9057`，
生成器被整体跳过，随后表现为「找不到 `Create` 方法」而不是一条清楚的版本错误。因此下限就是「自带
Roslyn 5.9 的那个 SDK」—— 已在 .NET SDK **10.0.401** 上实测通过；SDK 10.0.201（Roslyn 5.3）与
9.0.308（Roslyn 4.14）都会失败。而运行时库目标是 `net8.0`/`net10.0`，绝不能把 Roslyn —— 连同它带来的
`System.Collections.Immutable` 与 `System.Reflection.Metadata` 版本 —— 拖进你的依赖图。这两个要求没法
塞进同一个程序集，于是分成两个包。

主包声明了这条依赖，所以一条 `dotnet add package` 就够了。

<a id="transitive-analyzer"></a>

## 分析器是随包传递来的

由于依赖是传递的，**凡是引用主包的项目都会拿到分析器** —— 包括那些只消费 `IStronglyTypedId<,>`、
自己一个 Id 都不声明的项目。这是刻意的：下游项目里声明的 Id 同样需要生成器，而一个悄悄缺了生成器的
消费者只会得到一个莫名其妙的「找不到 `Create` 方法」，而不是一条诊断。

确实不想要任何诊断的项目，可以按项目关掉——用工程文件里的一个属性把整个分析器静音：

```xml
<PropertyGroup>
  <StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>
</PropertyGroup>
```

这条属性走 MSBuild → 分析器这条通道：SDK 把它写进编译用的 analyzer config 的全局段
（`build_property.StronglyTypedIdEnableAnalyzers`），分析器读到「否」就**一个诊断动作都不注册**——
26 条规则一条都不报，代码修复也随之消失（没有诊断就没有可修复的东西）。

三态语义：

| 属性取值 | 分析器 |
| --- | --- |
| `false`（大小写不敏感）、`0`、`no`、`off` | 完全静音 |
| `true` | 正常报诊断 |
| 不设 | 正常报诊断（默认如此） |

**只关诊断，不关代码生成。** 生成器与分析器是两个独立的组件，Id 的代码照常产出——静音的项目拿到的
仍然是完整可用的强类型 Id。

只想关掉某几条规则时，`.editorconfig` 仍然可用：

```ini
[*.cs]
dotnet_diagnostic.STIA0011.severity = none
```

或者在工程文件里（更粗暴，但不依赖 `.editorconfig` 是否存在）：

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);STIA0011;STIA0014</NoWarn>
</PropertyGroup>
```

> **为什么这个属性是「设了就能用」而不是「还要再配一步」**：SDK 不会把任意 MSBuild 属性写进
> `build_property.*`——只有它自己内建的那二十来条（`TargetFramework`、`RootNamespace` 等）才会。
> 要让自定义属性出现在那一段里，必须由 `<CompilerVisibleProperty Include="…" />` 显式放行，
> 而放行这一步由本库在 `Len.StronglyTypedId.Generators` 包的
> `buildTransitive/Len.StronglyTypedId.Generators.props` 里替使用者做了。
> 那份 props 随依赖链流到最终使用者，因此**装主包的项目直接设属性即可**，不需要额外配置。

<a id="symbols"></a>

## 调试符号单独发布

运行时包发布的是 `.snupkg`，而不是把 `.pdb` 塞进 `lib/`。符号按需从 nuget.org 的符号服务器拉取，
因此不会有使用者的还原目录里躺着一份永远用不上的 PDB。

<a id="versioning"></a>

## 版本

四个包的版本都取自 `version.props`，一起发布。主包把生成器包钉在同一版本上，因此生成器与运行时
不可能各走一路。

[<- 返回目录](index.md) · [English](../packages.md)
