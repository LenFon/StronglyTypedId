---
layout: default
title: Diagnostics
---

<a id="diagnostics"></a>

# Diagnostics

The analyzer enforces the following rules; `STIA0012` is the one exception, reported by the generator
when code emission itself fails. Most are reported as a **compile error**, so an invalid declaration
will not compile; `STIA0011`, `STIA0013`, `STIA0019` and `STIA0024` are warnings, and the advisory ones
(`STIA0014`, `STIA0015`, `STIA0016`, `STIA0022`) are informational. Every level can be tuned per project
in `.editorconfig`. Diagnostics are localized for English and Chinese according to the compiler culture.

A few rules fire in narrower circumstances than their titles suggest: `STIA0011` needs the id to declare
a `Validator` or a `Normalizer` (without either, constructing it directly is not a bypass) and covers
`new Xxx(...)`, target-typed `new(...)` and `default` / `default(T)` alike — a `default` that is only
compared against (`id == default`) is not reported; `STIA0014` is **off by default** and must be enabled
explicitly with `dotnet_diagnostic.STIA0014.severity = suggestion`; and `STIA0016` reports once per group
of ids sharing a primitive, on the ordinally first member.

<!-- BEGIN GENERATED:diagnostics -->
| Rule ID  | Description                                                           | Code fix                                                  |
| -------- | --------------------------------------------------------------------- | --------------------------------------------------------- |
| STIA0000 | Type must be defined as a record                                      | Convert to `record`                                       |
| STIA0001 | Type must be defined as partial                                       | Add `partial`                                             |
| STIA0002 | Type cannot be declared abstract                                      | Remove `abstract`                                         |
| STIA0003 | Type cannot be declared as generic                                    | Remove the type parameter list                            |
| STIA0004 | Type must be declared in a namespace                                  | Wrap in a namespace                                       |
| STIA0005 | Type must have a primary constructor with exactly one parameter       | Add a primary constructor                                 |
| STIA0006 | Parameter must not be nullable                                        | Remove the `?` suffix                                     |
| STIA0007 | Parameter name should be ‘Value’.                                     | Rename the parameter                                      |
| STIA0008 | Parameter type is not valid                                           | Change the type to `Guid`                                 |
| STIA0009 | Containing type must be declared as partial                           | Add `partial` to the container                            |
| STIA0010 | Validator method must be a valid static bool method                   | Remove the `Validator` argument                           |
| STIA0011 | Strongly typed id constructed directly                                | Replace with `Create(...)`                                |
| STIA0012 | Strongly typed id code generation failed                              | —                                                         |
| STIA0013 | Prefix value is blank                                                 | Remove the `Prefix` argument or give it a non-blank value |
| STIA0014 | Validator can be bypassed                                             | Narrow the type to `internal`                             |
| STIA0015 | Type name should end with 'Id'                                        | —                                                         |
| STIA0016 | Several strongly typed ids share the same primitive type              | —                                                         |
| STIA0017 | Normalizer method must be a valid static method                       | Remove the `Normalizer` argument                          |
| STIA0018 | Exception type must be a concrete exception with a string constructor | Remove the `ExceptionType` argument                       |
| STIA0019 | Requested integration is not available                                | —                                                         |
| STIA0020 | Named instance name is not usable                                     | —                                                         |
| STIA0021 | Named instance value is not valid for the primitive type              | —                                                         |
| STIA0022 | Named instance name should be PascalCase                              | —                                                         |
| STIA0023 | Prefix overlaps the primitive text                                    | —                                                         |
| STIA0024 | Integration mask contains undefined bits                              | —                                                         |
<!-- END GENERATED:diagnostics -->

Sixteen of the twenty-five rules ship a code fix, and a few of those only when the repair is
unambiguous — `STIA0003` is offered only when the id itself is generic (removing the *containing*
type's type parameters would change your design), `STIA0005` only when the type declares no explicit
constructor (adding a primary constructor next to an existing one would collide with `CS0111`), and
`STIA0009` only when the containing type is actually missing `partial` (otherwise the fix would insert
a second `partial`). `STIA0023` offers none on purpose: the only mechanical repair is deleting `Prefix`,
which changes the canonical text form and makes data already written with that prefix unreadable —
picking a safe replacement is a decision only you can make. The two naming hints (`STIA0015`,
`STIA0016`) deliberately offer none: renaming a type correctly means renaming every reference to it,
and "these ids look alike" has no mechanical repair. The remaining nine have no fix at all: `STIA0019`
and `STIA0024` report a mismatch between the declaration and the compilation — an integration whose
assembly is not referenced, or a mask bit that names nothing — which you resolve by adding the package or
correcting the flag; `STIA0022` is the same style advice as the two naming hints; and `STIA0012`,
`STIA0020` and `STIA0021` are raised where no rewrite can guess the intent (`STIA0012` by the generator
rather than the analyzer).

Code fixes are offered through the usual IDE light bulb and support **Fix all occurrences in document /
project / solution**. A type may be declared across several `partial` blocks: type-level rules are
evaluated once for the whole type, and parameter-level rules only against the block that declares the
primary constructor, so adding members in a separate block is perfectly legal.

[<- Back to the index](index.md) · [中文](zh-CN/diagnostics.md)
