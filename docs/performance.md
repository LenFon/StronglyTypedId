---
layout: default
title: Performance
---

# Performance

There are two costs worth separating: what the **generator** costs at build time, and what the
**generated code** costs at run time.

## Run time

Nothing is measured here because there is nothing to measure against: the generated members are the
code you would have written by hand. `Create` is a static call plus your validator; `ToString()` is one
call into the primitive; `Parse` is one call into the primitive's `Parse`. There is no reflection, no
boxing (`record struct`), and no allocation beyond what the primitive itself allocates — with one
exception: a prefixed id concatenates the prefix with the value on every `ToString()`, so that one path
allocates a string.

The JSON converters are generated types, not reflection-based ones, so serializing an id costs the same
as serializing the primitive it wraps.

## Build time

The generator walks every referenced assembly's types to find ids, so its cost scales with the reference
set rather than with the number of ids you declare. Four benchmark groups in
`bench/Len.StronglyTypedId.Benchmarks` quantify the pieces:

| Group | Measures |
| --- | --- |
| `GeneratorRunBenchmarks` | One full generator run against a realistic reference set, with the core, System.Text.Json, Newtonsoft.Json, EF Core and Swagger generators all active — the denominator everything else is judged against |
| `DiscoveryBenchmarks` | `StronglyTypedIdDiscovery.Discover` on warm symbols, comparing one call (today) with five calls (the pre-refactor shape, where each integration generator discovered independently) and with a type-table cache |
| `ColdDiscoverBenchmarks` | The **first** `Discover` in a run, where symbols are still unrealised — the cost a real build pays once |
| `GetTypesBenchmarks` | `ModuleInfo.GetTypes()` over the five largest assemblies in the reference set |

```shell
# Statistical report (BenchmarkDotNet), for local investigation
dotnet run -c Release --project bench/Len.StronglyTypedId.Benchmarks -- --filter *Discovery*

# Deterministic gate, what CI runs
dotnet run -c Release --project bench/Len.StronglyTypedId.Benchmarks -- --gate
```

## The regression gate

`--gate` does not use BenchmarkDotNet. BDN earns its keep on statistical rigour, but a single run takes
minutes, and on a shared CI runner wall-clock noise routinely exceeds ±20% — a threshold built on it
produces false reds. The gate instead runs a fixed workload and compares two deterministic numbers
against a checked-in baseline.

**Allocated bytes is the primary criterion.** The same code on the same path allocates the same number of
bytes, independent of how fast the machine is, and almost every Roslyn-side regression (enumerating
symbols twice, concatenating one more string, adding a LINQ layer) shows up as allocation.

**Elapsed time is only an order-of-magnitude backstop** (10× the baseline), there to catch "allocations
unchanged but complexity exploded".

| Knob | Value | Why |
| --- | --- | --- |
| Allocation tolerance | +10% | Consecutive runs on one machine differ by about 0.005%, so 10% is ~2000× the observed noise. It cannot go to 20%: reverting a real regression in this repository's history (one enumeration turned into five) raised allocations by only 16.3%, which a 20% threshold would have waved through |
| Elapsed tolerance | 10× baseline | Wall clock is not comparable across machines: the same code and baseline measured 13.97 ms on a developer machine and 59.75 ms on `windows-latest`, while allocations differed by 0.011%. A tighter 4× threshold failed CI purely because the runner was slower |
| Runs | 3 warm-up + 10 measured, median | The compilation and driver are rebuilt every iteration; reusing a driver would hit the incremental cache and stop measuring a build |

The baseline lives in `bench/Len.StronglyTypedId.Benchmarks/baselines/generator-run.baseline.json` and
records the runtime version alongside the numbers. If you change performance characteristics on purpose,
rewrite it and commit it with the change:

```shell
dotnet run -c Release --project bench/Len.StronglyTypedId.Benchmarks -- --gate --update-baseline
```

The gate also self-checks: every measured run must produce the same number of generated files as the
first, otherwise the numbers are not comparable and it fails loudly instead of reporting a delta.

[<- Back to the index](index.md) · [中文](zh-CN/performance.md)
