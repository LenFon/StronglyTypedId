### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
STIA0012 | StronglyTypedIdGenerator | Error | Reported by the source generator itself when code generation throws.
STIA0013 | StronglyTypedIdAnalyzer | Warning | Reported when [StronglyTypedId(Prefix = ...)] is set to an empty or whitespace-only string.
STIA0014 | StronglyTypedIdAnalyzer | Disabled | Reported when an externally visible id declares a Validator, so its primary constructor can bypass it. Enable via .editorconfig.
STIA0015 | StronglyTypedIdAnalyzer | Info | Reported when the id type name does not end with 'Id'.
STIA0016 | StronglyTypedIdAnalyzer | Info | Reported once per namespace/primitive group when several ids in the same namespace wrap the same primitive type.
STIA0017 | StronglyTypedIdAnalyzer | Error | Reported when [StronglyTypedId(Normalizer = ...)] points at a method that is not a static `static TPrimitiveId Name(TPrimitiveId value)`.
STIA0018 | StronglyTypedIdAnalyzer | Error | Reported when [StronglyTypedId(ExceptionType = ...)] points at a type that is not a non-abstract System.Exception subclass with an accessible single-string constructor.
STIA0019 | StronglyTypedIdAnalyzer | Warning | Reported once per missing integration per compilation when an integration is explicitly enabled via [StronglyTypedId(Integrations = ...)] but the compilation does not reference that integration's assembly; every requester is listed in the message.
STIA0020 | StronglyTypedIdAnalyzer | Error | Reported when a [StronglyTypedIdInstance] name is not a valid identifier, repeats an earlier instance, or collides with the id type itself, an existing member, or a generated member.
STIA0021 | StronglyTypedIdAnalyzer | Error | Reported when a [StronglyTypedIdInstance] value cannot be parsed as the id's primitive type.
STIA0022 | StronglyTypedIdAnalyzer | Info | Reported when a [StronglyTypedIdInstance] name is not in PascalCase.
STIA0023 | StronglyTypedIdAnalyzer | Error | Reported when [StronglyTypedId(Prefix = ...)] can also begin the text form of the id's primitive type, so unprefixed text is silently stripped and read as a different value.
STIA0024 | StronglyTypedIdAnalyzer | Warning | Reported when [StronglyTypedId(Integrations = ...)] (or the assembly-level default) sets bits that no integration uses; those bits are ignored and generate nothing.
STIA0025 | StronglyTypedIdAnalyzer | Error | Reported when the id itself is declared as a file-local type: the generator must reopen its partial declaration in another file, which a file-local type cannot allow.
STIA0026 | StronglyTypedIdAnalyzer | Error | Reported when the id type is named 'var': that name shadows the implicit-typing keyword, so the generated 'out var' / 'var' patterns stop compiling (CS1503 / CS8508).
STIA0027 | StronglyTypedIdAnalyzer | Error | Reported when the id type name equals a member the generator emits into it (e.g. 'Create', 'Parse', 'TryFormat', 'SystemTextJsonConverter'): C# forbids a member sharing its enclosing type's name (CS0542), so the generated code cannot compile.
STIA0028 | StronglyTypedIdAnalyzer | Warning | Reported when the id is declared inside a generic container: it still compiles and gets its core members, but every integration is skipped (open generics cannot be registered) and the [JsonConverter] / [TypeConverter] / [DebuggerTypeProxy] attributes cannot be emitted (CS0416). Not reported when every integration is explicitly turned off.
STIA0029 | StronglyTypedIdAnalyzer | Error | Reported when a member the user declares on the id collides with a member the generator emits into it: a method with the same name and parameter list (CS0111), or a field, property, event or nested type sharing a generated member's name (CS0102). The compiler only points at the generated file.
STIA0030 | StronglyTypedIdAnalyzer | Warning | Reported once per requested integration per compilation when the integration's assembly is referenced but its major version is below the generator's minimum (e.g. EF Core 6 with a 7.0.0 floor), so no code is generated for it. Mutually exclusive with STIA0019.
STIA0031 | StronglyTypedIdAnalyzer | Warning | Reported when [StronglyTypedId(JsonShapePropertyName = ...)] cannot take effect: JsonShape is not JsonShape.Object, or the name is empty, whitespace-only, or contains control characters. The generator falls back to 'value'.
STIA0032 | StronglyTypedIdAnalyzer | Warning | Reported on each [StronglyTypedIdInstance] attribute whose type has no [StronglyTypedId] attribute, so no instance member is generated and the attribute is silently ignored until a CS0117 at run time. Not reported when the type has [StronglyTypedId] but an invalid shape; the shape rules cover that case.
STIA0033 | StronglyTypedIdAnalyzer | Warning | Reported when [JsonNumberHandling] is applied to a strongly typed id type, or to a property, field or indexer whose type is one (including Nullable<T> and ids inside List<T>-style containers). System.Text.Json validates that attribute against the CLR type rather than the converter, so serializing the id throws InvalidOperationException. Use JsonSerializerOptions.NumberHandling instead.

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|--------------------
STIA0011 | StronglyTypedIdAnalyzer | Warning | StronglyTypedIdAnalyzer | Info | Raised so that bypassing a validator is visible in build output and CI. Projects that relied on the advisory level can restore it with `dotnet_diagnostic.STIA0011.severity = suggestion`.
