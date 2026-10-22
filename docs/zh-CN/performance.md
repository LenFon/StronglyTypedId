---
layout: default
title: 性能
---

# 性能

有两笔账要分开算：**生成器**在构建期的成本，和**生成的代码**在运行期的成本。

## 运行期

这里没有数字可报，因为没有可比的对象：生成的成员就是你手写会写出的那些代码。`Create` 是一次静态调用
加一次你的验证器；`ToString()` 是一次对基元的调用；`Parse` 是一次对基元 `Parse` 的调用。没有反射、没有
装箱（`record struct`），除了基元自身要分配的内存之外没有额外分配——只有一个例外：带前缀的 Id 每次
`ToString()` 都要把前缀和值拼起来，这一条路径会分配一个字符串。

JSON 转换器是生成出来的类型而非反射式的，所以序列化一个 Id 与序列化它所包裹的基元等价。

## 构建期

生成器要遍历每个被引用程序集的类型来找出 Id，因此它的成本随**引用集**增长，而不是随你声明了多少个 Id
增长。`bench/Len.StronglyTypedId.Benchmarks` 里的四组基准分别量化各个环节：

| 基准组 | 测什么 |
| --- | --- |
| `GeneratorRunBenchmarks` | 在真实引用集上完整跑一次生成器，核心、System.Text.Json、Newtonsoft.Json、EF Core、Swagger 全部启用——它是衡量其他一切的「分母」 |
| `DiscoveryBenchmarks` | 热符号下 `StronglyTypedIdDiscovery.Discover` 的成本，对比一次调用（现状）、五次调用（重构前的形态：每个集成生成器各自发现一次）与类型表缓存方案 |
| `ColdDiscoverBenchmarks` | 一次运行中**第一次** `Discover` 的成本，此时符号尚未实现——真实构建每次都要付这一次 |
| `GetTypesBenchmarks` | `ModuleInfo.GetTypes()` 在引用集中最大的五个程序集上的成本 |

```shell
# 统计报告（BenchmarkDotNet），本地排查用
dotnet run -c Release --project bench/Len.StronglyTypedId.Benchmarks -- --filter *Discovery*

# 确定性门槛，CI 跑的就是这个
dotnet run -c Release --project bench/Len.StronglyTypedId.Benchmarks -- --gate
```

## 回归门槛

`--gate` 不走 BenchmarkDotNet。BDN 的价值在统计严谨，代价是单次跑几分钟；而 CI 跑在共享 runner 上，
墙钟抖动本身就常常超过 ±20%——拿它做门槛只会制造假红。门槛改成「跑固定工作量，把两个确定性数字与入库
基线比对」。

**分配字节数是主判据。** 同一份代码走同一条路径，分配量逐字节可复现，与机器快慢无关；而 Roslyn 侧
绝大多数性能退化（多枚举一遍符号、多拼一次字符串、多接一层 LINQ）都会体现在分配量上。

**耗时只作数量级兜底**（基线的 10 倍），用于捕捉「分配没变但复杂度炸了」这种罕见情形。

| 旋钮 | 取值 | 为什么 |
| --- | --- | --- |
| 分配量容忍 | +10% | 同一台机器连续跑，分配量相差约 0.005%，10% 已是观测噪声的两千倍。不能放宽到 20%：本仓库历史上一次真实退化（一次枚举变成五次）只让分配量涨了 16.3%，20% 会放过它 |
| 耗时容忍 | 基线的 10 倍 | 墙钟跨机器不可比：同一份代码同一份基线，开发机 13.97 ms、`windows-latest` 59.75 ms，而分配量只差 0.011%。原先取 4 倍时，CI 仅因 runner 较慢就被判红 |
| 轮次 | 预热 3 轮 + 测量 10 轮取中位数 | 每轮都重建编译与驱动器；复用驱动器会命中增量缓存，量到的就不再是一次构建 |

基线存放在 `bench/Len.StronglyTypedId.Benchmarks/baselines/generator-run.baseline.json`，与数字一起
记录了运行时版本。若你有意改变了性能特征，重写基线并连同改动一起提交：

```shell
dotnet run -c Release --project bench/Len.StronglyTypedId.Benchmarks -- --gate --update-baseline
```

门槛还会自检：每一轮产出的文件数必须与首轮一致，否则数字不可比——它会直接报错，而不是给出一个假的
差值。

[<- 返回目录](index.md) · [English](../performance.md)
