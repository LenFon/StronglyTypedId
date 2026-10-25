# Changelog

> 📖 中文版：[CHANGELOG.zh-CN.md](CHANGELOG.zh-CN.md)

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Releases up to `1.3.0` are reconstructed from the repository tags: each entry summarises the shipped
change set rather than reproducing every commit.

## [Unreleased]

### Added

- `Len.StronglyTypedId.Testing`: deterministic id generation for tests — `Any.Id<T>()`,
  `Any.Ids<T>(count)`, seeded sequences, `Any.Register<T>(factory)` overrides and `Any.Reset()`.
  Values rejected by a generated validator are retried instead of surfacing as test failures. The
  retry recognises a custom `ExceptionType` as a rejection too, and lets any other exception from the
  validator bubble up.
- `Len.StronglyTypedId.Testing.AutoFixture`: `fixture.WithStronglyTypedIds()` fills strongly typed
  ids through the same generator, so AutoFixture-created object graphs respect validators.
- `IStronglyTypedId<TSelf>` — a single-parameter view of the id contract, so generic code can
  constrain on "some strongly typed id" without naming the primitive. `IFormattableStronglyTypedId<TSelf>`
  is generated when the primitive implements `IFormattable`.
- Nine more primitives — `char`, `Int128`, `UInt128`, `Half`, `float`, `double`, `DateOnly`, `TimeOnly`
  and `BigInteger` — bringing the total to 24 types including `enum`. `char`'s parsing and formatting
  members are explicit interface implementations, so the generator emits a length check and an interface
  call instead of `char.TryParse` / `char.ToString(format, provider)`.
- `Normalizer` — point it at a `static TPrimitiveId Normalize(TPrimitiveId value)` member to repair
  incoming values (trim, case-fold, drop separators) before they are validated and stored. Every
  entry point runs normalize → validate → construct, and `[assembly: StronglyTypedIdDefaults(Normalizer = ...)]`
  sets an assembly-wide default. A malformed reference is reported as `STIA0017`, with a code fix that
  generates a pass-through stub or drops the argument.
- `ExceptionType` — point it at a custom exception to change what `Create` throws when the validator
  rejects a value. The type must derive from `System.Exception`, be non-abstract and expose an accessible
  constructor taking a single `string` message; anything else is reported as `STIA0018`, with a code fix
  that drops the argument. `Parse` throws `FormatException` instead, because its failure also covers
  text that is not an id at all. `[assembly: StronglyTypedIdDefaults(ExceptionType = ...)]` sets an
  assembly-wide default.
- `RedactValidationMessage` — drop the rejected value from the validator rejection message, so it reads
  `The value is not a valid OrderId.` instead of `The value '...' is not a valid OrderId.` That text
  otherwise travels with the exception into logs, APM traces and error reports, and for an id wrapping
  `string` it is user input. `[assembly: StronglyTypedIdDefaults(RedactValidationMessage = true)]` turns
  it on for a whole assembly; a type-level `false` cannot undo that, because redaction is a safety
  policy rather than a per-type preference.
- `STIA0023` — a `Prefix` whose first character can also start the primitive's own text, so `Parse`
  cannot tell the prefix from the value. `STIA0024` — bits set in `Integrations` that match no known
  flag; they were silently ignored before.
- Text prefixes: `Prefix` writes a constant prefix in `ToString` and strips it in `Parse`;
  `PrefixReadMode` chooses between accepting a bare value (`Lenient`) and rejecting it (`Strict`).
- `JsonShape` selects the serialized shape: the bare primitive (`Primitive`) or
  `{ "value": ... }` (`Object`).
- Registration entry points for System.Text.Json (`StronglyTypedIds.ApplyTo(JsonSerializerOptions)`) and
  Newtonsoft.Json (`settings.AddStronglyTypedId()`), for callers that configure converters explicitly
  instead of relying on the generated `[JsonConverter]` attribute.
- A refactoring provider that turns an existing `record` into a strongly typed id.
- `Integrations` — narrows which integration code an id gets. It is a **filter, not a switch**: it can only
  remove integrations whose packages are already referenced. `None` suppresses every integration while
  keeping the core implementation, `Dapper | SystemTextJson` keeps exactly those, and `All` restores
  everything under an assembly default of `None`. `[assembly: StronglyTypedIdDefaults(Integrations = ...)]`
  sets the default, and an id declared in a referenced assembly honours **its own** assembly default.
  `TypeConverter` is deliberately not part of the mask. Requesting an integration whose assembly is not
  referenced is reported as `STIA0019`.
- MessagePack and MongoDB BSON integrations — a generated `IMessagePackFormatter<TId>` /
  `IBsonSerializer<TId>`, wired up by `StronglyTypedIds.ApplyTo(MessagePackSerializerOptions)` and
  `StronglyTypedIds.RegisterBsonSerializers()`. Both write the primitive value itself: the text prefix
  belongs to the text form (`ToString()` / JSON) and never reaches the payload.
- `[StronglyTypedIdInstance]` declares named static instances — `Unknown`, `Unspecified`, `System` —
  as `public static readonly` fields. The value is written as the primitive's invariant text, validated
  and translated into a literal at compile time, and constructed through `Create`, so an instance always
  honours the normalizer and validator and `Parse(instance.ToString())` always reads back. A value the
  validator rejects surfaces as a `TypeInitializationException` on first use rather than silently
  producing an id that cannot exist.
- `STIA0012`–`STIA0019` diagnostics, including `STIA0013` (blank `Prefix`), `STIA0014` (an id with a
  validator or normalizer declared `public`, leaving the primary constructor reachable — `Info`, off by
  default), `STIA0015` (id name should follow the `...Id` convention), `STIA0016` (two ids over the same
  primitive in one assembly), `STIA0017` (malformed `Normalizer` reference), `STIA0018` (unusable
  `ExceptionType`) and `STIA0019` (requested integration not referenced).
- `STIA0020`–`STIA0022` diagnostics for named instances: `STIA0020` (a name that is not a valid
  identifier, repeats another instance, or collides with an existing or generated member), `STIA0021`
  (a value that is not a valid primitive value) and `STIA0022` (a name that is not PascalCase, `Info`).
- Runnable samples: `SampleConsole` (guided tour of the whole surface), `SampleIntegrations`
  (EF Core + Dapper against in-memory SQLite), `Sample1` (MVC + Swashbuckle 6 + Newtonsoft.Json),
  `Sample2` (Minimal API + built-in OpenAPI), `Sample3` (MVC + Swashbuckle 10 / Microsoft.OpenApi 2.x).
- Code fixes for `STIA0019`, `STIA0024`, `STIA0020` and `STIA0021`. `STIA0019` / `STIA0024` (a requested
  integration whose assembly is missing, or a mask bit that names nothing) now offer a fix that removes
  the `Integrations` argument; `STIA0020` / `STIA0021` (a `[StronglyTypedIdInstance]` with an unusable
  name or an unparseable value) offer a fix that removes the whole attribute. `STIA0022` (non-PascalCase
  instance name) still has none — renaming an instance would have to chase every reference, which a
  single-file fix cannot do safely. This brings the rules that ship a code fix from 17 to 21 of 26.
- `STIA0015` — the id naming convention is now configurable. Set `dotnet_diagnostic.STIA0015.suffix` in
  `.editorconfig` to check a different trailing token (e.g. `Key`); the suggestion and the message use the
  configured suffix. The default remains `Id`.

### Changed

- The generated type declaration no longer carries a `/// <summary>` comment. It restated what the
  declaration already says, and it was the only doc comment in the file that did not come from
  `/// <inheritdoc/>`, so it read as noise in every generated file.
- Remaining generated doc comments now follow the C# convention of putting `<summary>` on its own
  line, with the text on the next line and `</summary>` on a third. Previously `TryCreate`,
  `Deconstruct`, the null-safe comparison helper, the `TypeConverter`, and every integration
  registration method emitted a single-line `<summary>…</summary>`. Formatting only; the compiled
  output is unchanged.
- **Breaking:** `Parse` now throws `FormatException` instead of `ArgumentException` when the text is not
  a valid id, matching the `IParsable<TSelf>` convention that `int.Parse` / `Guid.Parse` follow. Code
  that caught `ArgumentException` around `Parse` must catch `FormatException`; a `catch (ArgumentException)`
  no longer sees the failure. `Create` still throws `ArgumentException` (or the configured `ExceptionType`)
  when a validator rejects a value, and `TryParse` is unchanged.
- **Breaking:** diagnostic ids were renamed from `STIAO###` (letter O) to `STIA0###` (digit zero).
  Existing `dotnet_diagnostic.STIAO011.severity` entries in `.editorconfig` stop matching; the
  rename happened before the first stable release.
- `Parse` / `TryParse` now fall back to the invariant culture when the format provider is `null`,
  matching what `ToString()` writes. A `null` provider used to mean the current culture, so
  `Parse(id.ToString())` silently produced wrong values under e.g. `de-DE` (`"1.5"` → `15`). An
  explicitly passed provider is still honoured.
- Dapper registration gained a parameterless `StronglyTypedIds.ApplyTo()`. Dapper keeps type handlers in a
  process-wide registry, so the `IDbConnection` argument never had anything to scope to; the existing
  `ApplyTo(IDbConnection)` overload is kept for source compatibility and now forwards to the new one.
  Calling either more than once stays harmless — Dapper replaces the handler for a type instead of
  appending, so the registry does not grow.
- The README reference tables (primitive types, integrations, diagnostics) are generated from the
  generator source by `tools/Len.StronglyTypedId.DocGen`; CI fails when they drift.
- Performance: generator discovery results are reused across the pipeline, `TryFormat` no longer
  boxes, and the `CompilationProvider` dependency was pushed down.
- Build: `.editorconfig` + `Directory.Build.props` pin a zero-warning baseline (IDE/CA/xUnit rules
  promoted to warnings), package validation runs on `dotnet pack`, and CI gained a Native AOT smoke
  test, package-content assertions, a generator allocation gate and coverage thresholds.
- `STIA0011` now also reports target-typed `new(...)` and `default` / `default(T)` construction, not
  just `new Xxx(...)`, and its default severity was raised from `Info` to `Warning` so that bypassing
  a validator shows up in build output and CI. It also fires for ids that only declare a `Normalizer`,
  since a directly constructed id is then not in canonical form. A `default` that is only compared
  against (`id == default`) is not reported. Projects building with `TreatWarningsAsErrors` will now
  fail on a bypassed validator; restore the previous behaviour with
  `dotnet_diagnostic.STIA0011.severity = suggestion`.
- **Breaking for prefixed ids:** `ToString(string?, IFormatProvider?)` and `TryFormat(...)` now keep the
  text prefix for a non-empty `format` too. They used to fall back to the bare primitive, so
  `$"{id:N}"` printed `d8ac85d4…` while `id.ToString()` printed `ord_d8ac85d4…` — the same id in two
  shapes, and the prefixed one lost wherever a format string was involved. The prefix is part of the
  canonical text form, so it is now unconditional and `format` only reaches the primitive. Ids without
  a prefix are byte-for-byte unchanged.
- Generator discovery skips referenced assemblies that do not reference `Len.StronglyTypedId`. Such an
  assembly cannot declare an id (that needs `IStronglyTypedId<,>`), yet every module used to have its
  whole type tree walked — `System.Private.CoreLib` alone contributes tens of thousands of symbols.
  The check reads `ReferencedAssemblyNames`, which is pure metadata.

### Removed

- Per-id-type string interning. `Create` / `TryCreate` / `Parse` / `TryParse` no longer route string
  values through a per-type `ConcurrentDictionary`, so a string-backed id no longer promises that equal
  values share one instance. The cache only paid off when a small set of values repeated heavily, while
  charging a dictionary probe on every factory call, pinning up to 1024 strings per id type for the
  life of the process, and — before it was capped — growing without bound for unbounded value sets.
  Use `string.Intern` or a cache in your own layer if you need deduplication.

### Fixed

- `STIA0016` (duplicate primitive type) no longer degrades to O(N²). The namespace grouping was recomputed
  from scratch for every strongly typed id, so N ids sharing a namespace each rescanned the whole namespace
  (including nested types). The grouping is now cached per namespace and shared across the id's symbol
  actions, so the work is O(M) overall instead of O(N·M). Reported diagnostics and the "first member reports"
  rule are unchanged.
- `IsStronglyTypedId` / `GetPrimitiveIdType` / `TryGetPrimitiveIdType` no longer keep collectible
  `AssemblyLoadContext`s alive. Their result cache was keyed by `Type` in a static dictionary, and a `Type`
  roots its `Assembly`, which roots its load context — so every plugin load, hot reload or test host that
  reloads assemblies pinned one context and all of its types for the life of the process. The cache now
  uses `ConditionalWeakTable`, whose entries disappear together with their key. Nothing changes for
  ordinary application code; only hosts that unload assemblies notice.
- When a type implements more than one `IStronglyTypedId<TSelf, TPrimitiveId>`, the primitive is now taken
  from the interface whose `TSelf` is that type itself. Previously the first match won, so the reported
  primitive depended on `Type.GetInterfaces()` enumeration order — the same type could yield a different
  primitive across runtimes, and the model binder and EF converters pick their converter from that answer.
- OpenAPI documents now describe an id declared with `JsonShape = JsonShape.Object` as the object it
  actually serializes to — `{ "value": <primitive> }` with `value` required — instead of as a bare
  primitive. Both integrations were affected: Swashbuckle's `MapType` emitted `type: string, format: uuid`
  for such an id, and the ASP.NET Core OpenAPI transformer rewrote the framework's schema to the same bare
  primitive. A client generated from either document sent and accepted a plain string while the server
  read and wrote an object, so every call involving that id failed. The nested `value` property keeps the
  normal primitive mapping, including the `Prefix` rule.
- A `string`-backed id now compares ordinally, so ordering agrees with equality. `CompareTo` and the
  `<`, `>`, `<=`, `>=` operators used `string.CompareTo(string)`, which is culture-sensitive: it treats
  `"a\u00ADb"` and `"ab"` as equal even though `Equals` says they differ, and it orders `"I"` and `"i"`
  differently under `en-US` and `tr-TR`. `SortedSet<T>` and `SortedDictionary<TKey, TValue>` use
  `CompareTo` to detect duplicate keys, so two distinct ids collapsed into one entry — silent data loss
  with no exception and no log line. The generated comparison now uses `string.CompareOrdinal`, which
  matches the record's `Equals` and the invariant text every serializer writes. Ids backed by other
  primitives are unaffected: their `CompareTo` is already strongly typed and ordinal where it matters.
- A value rejected by a `Validator` now surfaces as the serializer's own exception during
  deserialization instead of escaping as `ArgumentException`. The generated converters called
  `Id.Create(...)`, whose `ArgumentException` (or the configured `ExceptionType`) passed straight
  through `JsonSerializer.Deserialize` / `JsonConvert.DeserializeObject` / `MessagePackSerializer` /
  `BsonSerializer` — none of which wrap converter exceptions. A caller following the documented
  contract, `catch (JsonException)`, therefore missed the rejection entirely. The converters now call
  `TryCreate` and throw `JsonException` / `JsonSerializationException` /
  `MessagePackSerializationException` / `BsonSerializationException` themselves, matching what the
  dictionary-key path already did. EF Core's `ValueConverter` still calls `Create`: materializing an
  entity from a database row is a data-integrity boundary where `ArgumentException` is the more precise
  report. A `BigInteger`-backed id read a non-numeric payload through `BigInteger.Parse`, so its
  `FormatException` leaked the same way; it now uses `TryParse` and reports `JsonException`.
- A JSON token of the wrong type now raises the serializer's own exception instead of
  `InvalidOperationException`. The generated converters threw `InvalidOperationException` from the
  `switch` arm that handles an unexpected token — a `string`-backed id read from a number, or
  (Newtonsoft.Json) a `null` handed to a value-type id — while every structural error in the same file
  already threw `JsonException` / `JsonSerializationException`. A caller following the documented
  contract, `catch (JsonException)`, therefore missed this path, and the exception arrived without the
  JSON path the serializer attaches to its own exception types. Both now throw `JsonException` /
  `JsonSerializationException`.
- A `BigInteger`-backed id lost its value under System.Text.Json. `BigInteger` ships no built-in
  converter, so the generated converter fell back to `JsonSerializer.Serialize(writer, primitiveId, options)`,
  which serialized the struct's public properties (`{"IsPowerOfTwo":false,…,"Sign":1}`) and read back as
  `default` — silent data loss on every round trip. It now writes and reads the invariant-culture text,
  the same text `ToString()` produces.
- `Int128`, `UInt128`, `Half`, `DateOnly` and `TimeOnly` ids threw `JsonWriterException: Unsupported type`
  under Newtonsoft.Json. `JsonWriter.WriteValue` has no `PrimitiveTypeCode` for these types, so writing the
  raw value failed outright. Their converters now write the invariant-culture text and read it back through
  `Id.TryParse`, which is what a prefixed id already did.
- `Create` is now emitted indented like every other member. The template placeholder sits on the raw
  string's dedent baseline, so the first line received no indentation and the expression-bodied overload
  came out flush left while the block-bodied one (used with `Normalizer`) was fine. Cosmetic only — the
  code compiled either way — but every generated file looked broken in the editor.
- Three more generated blocks are indented consistently. The `CompareValues` helper on a reference-type
  id sat at 20 spaces while its siblings were at 4, and the EF Core / Swagger `ApplyTo` bodies were
  indented with tabs (Swagger's opening brace also carried a trailing space) while every other generated
  line uses four spaces. Cosmetic only — the code compiled either way.
- An enum-backed id used as a `Dictionary` key ignored `JsonSerializerOptions.DictionaryKeyPolicy`, so a
  `CamelCase` policy wrote `{"Active":1}` where a native `Dictionary<Status, int>` writes `{"active":1}`.
  The generated `WriteAsPropertyName` now applies the policy, and `ReadAsPropertyName` parses
  case-insensitively so the rewritten key still round-trips. Non-enum primitives are deliberately left
  alone: their key text *is* the value, and rewriting it would make the key unreadable.
- Swagger and the built-in OpenAPI transformer now describe `decimal`, `double`, `float`, `Half`,
  `Int128`, `UInt128`, `BigInteger`, enums and the date/time primitives with the schema type the JSON
  actually carries. `decimal` used to be announced as `string` (Swagger) or `integer` (OpenAPI), enums
  as `string`, and floats fell through to the string default, so generated clients deserialized the
  wrong type. Both generators now share a single mapping table.
- `{id:OrderId}` route constraints no longer reject link generation. During
  `RouteDirection.UrlGeneration` the route value is the id instance rather than a string, and the
  constraint's `is not string` guard turned that into a permanent mismatch, so a URL for such a route
  could never be produced. The value is now coerced with `Convert.ToString(..., InvariantCulture)`
  first, matching the framework's own constraints.
- `Parse` / `TryParse` on an enum-backed id no longer accept undefined numeric values (`"999"`),
  comma combinations (`"1,2"`) or any other text `Enum.TryParse` happens to accept: the parsed value is
  additionally checked with `Enum.IsDefined`. A `[Flags]` enum cannot use that check — bit combinations
  are legal values — so it is validated against a mask of the bits no member occupies instead: `Read | Write`
  parses, `999` does not. Surrounding whitespace is still trimmed, as with the other BCL parsers.
- `[StronglyTypedId(TypeConverter = true)]` on an id nested inside another type no longer emits a
  `typeof(...)` that points at a type which does not exist. The attribute was built from the namespace
  plus the id name, dropping the containing-type chain, so `Aggregate.NestedId` produced
  `global::Ns.NestedId.NestedIdTypeConverter` instead of `global::Ns.Aggregate.NestedId.NestedIdTypeConverter`
  and the consumer project failed with CS0246.
- OpenAPI parameter schemas now carry the primitive `format` (`/orders/{id}` →
  `type: string, format: uuid`). Route and query parameters describe the primitive in
  `JsonTypeInfo`, so the schema transformer previously missed them; request and response bodies were
  already correct.
- Ids declared inside a generic container no longer emit `[JsonConverter(typeof(...))]`, which is
  illegal in an attribute argument (CS0416). The converter type is still generated and can be
  registered manually for a closed instantiation, and `AddStronglyTypedId` / `ApplyTo` skip such ids
  for the same reason (an open generic type cannot be passed as a type argument). The README previously
  claimed both serializers worked out of the box; it now documents the manual registration
  (`new Outer<int>.InnerId.SystemTextJsonConverter()`).
- JSON registration entry points are no longer generated for ids whose declaring assembly does not
  reference the JSON library, removing CS0426 in consumer projects.
- The EF Core, Dapper, Swagger, MVC and OpenAPI generators no longer emit an empty `StronglyTypedIds`
  registration class when there is nothing to register. Previously an assembly with no eligible ids
  still got a `StronglyTypedIds.*.g.cs` whose `ApplyTo` did nothing; now the file is skipped, matching
  what the JSON registration entry points already did.
- Dapper type handlers now override the non-nullable `SqlMapper.TypeHandler<TId>` signatures
  (`SetValue(IDbDataParameter, TId)` and `Parse(object?)`). The previous `TId?` signatures resolved to
  `Nullable<TId>` for struct ids and failed to compile with CS0115 / CS0508.
- Dapper type handlers for reference-type ids (`partial record`) now write the wrapped primitive to
  `DbParameter.Value` instead of the id object itself. Drivers such as SqlClient and SQLite reject the
  wrapper with "No mapping exists from object type ...", so every insert and update of a reference-type
  id failed at parameter binding. Value-type ids were unaffected.
- Dapper type handlers now convert the value the driver hands back instead of unboxing it. `Parse` cast
  `object` straight to the primitive, which only works when the driver returns that exact CLR type: SQLite
  returns `long` for an enum column, `string` for `char` and `Guid`, and `double` for `decimal`, so reading
  any of those four threw `InvalidCastException` (writing them was fine). The handler now takes the
  primitive directly when the driver already returns it and converts by primitive kind otherwise.
- The `STIA0008` code fix no longer leaves a bare `Guid` behind in a file without `using System;`. It
  writes `global::System.Guid` and lets the simplifier shorten it, so the fix no longer trades one
  diagnostic for a `CS0246`.
- The analyzer no longer reads the primitive type from `Constructors[0].Parameters[0]`, which assumed
  the primary constructor comes first and has a parameter. A type whose first constructor is
  parameterless threw `IndexOutOfRangeException`, which Roslyn surfaces as `AD0001` and which drops
  every `STIA` diagnostic for that type. It now scans the instance constructors for the single-parameter
  one, skipping the compiler-synthesised copy constructor.
- A missing embedded localization resource no longer throws from the descriptor message-rendering path.
- MessagePack and MongoDB BSON integrations now work under Native AOT for every primitive, not just the
  id wrapper. Two gaps surfaced when the AOT smoke test first ran in CI: MessagePack's AOT-safe resolver
  chain contains no enum resolver at all (the IL-emitting `DynamicEnumResolver` is excluded when dynamic
  code is unavailable), so an enum-backed id could not even be serialized; and BSON's
  `PrimitiveSerializationProvider` creates `Int32Serializer`, `EnumSerializer<T>` and friends via
  `Activator`, which throws `MissingMethodException` under AOT once the constructors are trimmed.
  `ApplyTo` now adds a `GenericEnumFormatter<TEnum>` for each distinct enum primitive (same underlying
  numeric payload as the dynamic resolver, so the wire format is unchanged), and `RegisterBsonSerializers`
  pre-registers default-configured serializers for every binary-path primitive — `Guid` stays excluded,
  because since driver 3.0 it has no global default and the app must choose the representation.
- The MongoDB BSON integration now also pre-registers BSON's dynamic types (`object`, `List<object>` and
  `ExpandoObject`). Every root serialization context takes `BsonDefaults.DynamicArraySerializer` and
  `BsonDefaults.DynamicDocumentSerializer`, and those properties look the types up in the registry on
  first use — where the driver would create them through reflection, which throws
  `MissingMethodException` under Native AOT. The registered instances are the same types the driver would
  have created, so behavior is unchanged; an existing registration for one of them is kept.
  The loader returns an empty table and the text falls back to the resource key, so a packaging mistake
  degrades the wording instead of crashing the analyzer.
- The comparison operators on a reference-type id (`partial record`) no longer throw
  `NullReferenceException` when the left operand is `null`. They dereferenced `left.Value` unconditionally,
  while the same type's `CompareTo` already treated `null` as the smallest value; the operators now share
  that convention, so `null < id` is `true` and `null >= id` is `false`. Value-type ids are unchanged.
- An id declared inside a generic container no longer risks a duplicate hint name. Sanitising the full
  name stripped `<`, `>`, `,` and spaces, so `Ns.Outer<T>.Inner` collapsed onto a real `Ns.OuterT.Inner`;
  `AddSource` then threw on the duplicate and the whole id degraded to `STIA0012`. A name that actually
  lost characters now gets a stable FNV-1a suffix derived from the original full name, so the two stay
  distinct across builds and machines.
- A string-backed id used as a `Dictionary` key threw `ArgumentException` instead of `JsonException`
  when its validator rejected the key. `ReadAsPropertyName` called `Create`, so a rejected value escaped
  `JsonSerializer.Deserialize` as the raw `ArgumentException` (or the configured `ExceptionType`),
  breaking the converter contract that every failure is a `JsonException`. It now goes through
  `TryCreate` and throws `JsonException`, like the prefixed and non-string branches already did.
- A `Half`-backed id rejected named-instance values that `Half.Parse` accepts. The compile-time check ran
  through `float` and refused `NaN`, `±Infinity` and anything above `65504`, while `(65504, 65520)` merely
  rounds back to the largest finite value (`65519.9` → `65504`). `NaN` and `±Infinity` now emit the
  matching `Half` constants, and the range check uses the real overflow midpoint `65520`, so only values
  that would round to infinity are rejected.
- `Len.StronglyTypedId.Testing` never produced a negative `int` / `long` or a `ulong` above
  `long.MaxValue`: the built-in factory used the non-negative `Random.Next()` / `NextInt64()` overloads.
  Samples now span the full range, so the negative-value paths (formatting, ordering, database round
  trips) are actually exercised by `Any.Id<T>()`.
- The `STIA0005` code fix silently dropped parameters. Its title promised a single-parameter primary
  constructor, but the fix replaced the whole parameter list with `(Guid Value)`, so
  `record OrderId(Guid Value, string Name)` lost `Name` without a word. The fix still has to drop them —
  the diagnostic demands exactly one parameter — but the title now says so and names the replacement:
  "Replace with a single-parameter primary constructor (Guid Value); other parameters are removed".
- `STIA0019` was never reported for the `MessagePack` and `MongoDb` integrations. The analyzer's
  flag-to-assembly table listed only seven of the nine flags, so
  `[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.MessagePack)]` in a project that does not
  reference `MessagePack` generated nothing and said nothing — even though the generator gates both
  integrations on their assemblies and the documentation promises the diagnostic instead of silence.
  Both rows are now present, and a structural test asserts that the table covers every bit of
  `StronglyTypedIdIntegrations.All`, so a future integration cannot repeat the omission.
- The "Inline strongly typed id" refactoring no longer deletes an unrelated attribute that happens to be
  named `StronglyTypedId`. It decided by name alone, so `[Acme.StronglyTypedId]` — or a short
  `[StronglyTypedId]` imported from another namespace — was stripped from the declaration along with the
  real one, silently removing whatever the other library used it for. The attribute is now resolved
  semantically and only removed when it binds to `Len.StronglyTypedId.StronglyTypedIdAttribute`; a name
  that resolves to no attribute at all (shadowed by a namespace of the same name) still counts, so the
  refactoring keeps working in that case.
- `StronglyTypedIds.ApplyTo(MvcOptions)` is now idempotent. It inserted a fresh
  `StronglyTypedIdModelBinderProvider` on every call, so startup code that registered it from several
  modules — or a configuration callback that ran twice — left duplicate providers in
  `ModelBinderProviders`, each rebuilding the binder on every request. It now returns early when the
  provider is already present.
- The Dapper type handler now parses enum text case-insensitively and rejects values that name no
  member. `Enum.Parse` was case-sensitive, so a driver returning `ACTIVE` for a column stored as `Active`
  threw even though the value was valid; `Enum.TryParse` in its place accepts `"999"` and `"1,2"`, which
  produce an id matching no member. Non-`[Flags]` enums are now checked with `Enum.IsDefined`, and
  `[Flags]` enums against the undefined-bits mask, matching `Id.TryParse` and the JSON dictionary-key
  path. A rejected text throws `ArgumentException`, the same exception `Create` raises.
- `RegisterBsonSerializers` now documents what happens when a serializer for the same id type is already
  registered: the driver's `TryRegisterSerializer` returns `false` and keeps the existing one, so the
  conflicting registration silently wins. The behaviour is unchanged — overwriting would break the
  documented idempotency — but the generated XML remark and the integration guide now say so instead of
  leaving it to be discovered.
- An id (or a containing type, a type parameter, or a named instance) whose name is a C# keyword no
  longer breaks the build. The generator wrote `ISymbol.Name` — which does not escape keywords — into
  type positions, so `partial record struct class :` had its name parsed as the `class` keyword, the
  members fell out of the type into the global namespace and the file produced some eighty errors
  (`CS0260` / `CS1520` / `CS0101` / `CS9348`); the display format also put the escaping `@` into the
  hint name, so `AddSource` threw and the whole id was discarded as `STIA0012` — a failure message
  unrelated to what the user actually wrote. Every type position now uses the escaped name (including
  contextual keywords such as `var`, since the generated code itself declares `out var id`), and the
  hint name keeps only letters, digits, `_` and `.`.
- An id declared `file partial record struct` is now reported as `STIA0025`, with a code fix that
  removes the `file` modifier. The generator used to reopen the partial declaration in a file of its
  own — a second type with the same name, since every part of a file-local type must carry `file` —
  producing `CS0535` with no STIA diagnostic to explain it. Worse, that declaration-level error made
  csc discard every analyzer diagnostic in the compilation unit, so unrelated ids in the same file
  silently lost their type-safety checks. The analyzer and the generator now apply the same rule.
- The System.Text.Json and Newtonsoft.Json converters now reject enum values that name no member on
  the *value* path, not just the dictionary-key path. `JsonSerializer.Deserialize<StatusId>("999")`
  used to produce an id whose value is `(Status)999` — `Enum.TryParse` and the Dapper paths already
  refused it, so the same id could be rejected as text and accepted as JSON. It now throws
  `JsonException` / `JsonSerializationException`, and `[Flags]` enums are checked against the
  undefined-bits mask.

## [1.3.0] - 2026-10-07

The modernisation release: the generator moved to an incremental pipeline and the surface grew from
"an id that parses" to a full DDD primitive.

### Added

- 14 primitive types plus any `enum`: `Guid`, `string`, `byte`, `sbyte`, `short`, `ushort`, `int`,
  `uint`, `long`, `ulong`, `decimal`, `DateTime`, `DateTimeOffset` and `TimeSpan`.
- Factories and parsing: `Create` / `TryCreate`, `Parse` / `TryParse` (string and `ReadOnlySpan<char>`),
  `IParsable<TSelf>`, and — when the primitive supports them — `IFormattable`, `ISpanFormattable`
  and the UTF-8 span interfaces.
- Comparison operators, `Deconstruct`, and an opt-in `TypeConverter` (`TypeConverter = true`).
- `Validator` on `[StronglyTypedId]`: one rule shared by `Create`, `TryCreate` and `TryParse`;
  assembly-level `StronglyTypedIdDefaults` supplies it for every id in the assembly.
- Ids may be declared in nested types and in generic containers.
- Integrations: EF Core (`ApplyTo(ModelConfigurationBuilder)` and `ApplyTo(ModelBuilder)`), Dapper
  (`ApplyTo(IDbConnection)`), Swashbuckle and ASP.NET Core OpenAPI schema mapping, MVC model binding
  and `{id:OrderId}` route constraints, System.Text.Json and Newtonsoft.Json converters.
- Per-id-type string interning: equal string-backed ids share one underlying instance without
  leaking into the global intern pool.
- Diagnostics `STIA0000`–`STIA0011` with code fixes, localised (en / zh-CN).

### Changed

- Incremental generator pipeline; deterministic builds, public API baseline, package validation and
  symbol packages in CI.
- `Len.StronglyTypedId` ships as a single package that pulls in the generator package.

### Fixed

- Swashbuckle 7/8/9 are no longer misdetected as Microsoft.OpenApi 2.x, and `Guid`-backed ids
  regained `format: uuid`.
- Ids with the same name in different namespaces no longer collide in EF Core converters.
- Multi-part `partial` declarations no longer raise `STIA0005`.
- The code fix no longer leaves stray `where` constraints or drops attributes and comments.
- `GetModules` returns safely for module-level (`netmodule`) references instead of throwing.

## [1.3.0-preview.11] - 2026-10-02

### Fixed

- Publishing a symbol package twice no longer fails the release: `dotnet nuget push` now passes
  `--skip-duplicate`, turning the 409 into a warning.

## [1.3.0-preview.10] - 2026-10-02

### Changed

- Test coverage for the ASP.NET Core OpenAPI generator, including its accessibility branches.

## [1.3.0-preview.9] - 2026-10-02

_Retag of `1.3.0-preview.8`; no content changes._

## [1.3.0-preview.8] - 2026-10-02

### Added

- `STIA0011` code fix, `Deconstruct` support and a console sample.
- `PACKAGE.md` split out of the README as the NuGet package readme.

## [1.3.0-preview.7] - 2026-09-20

### Added

- ASP.NET Core MVC model binding and `{id:OrderId}` route constraints.

## [1.3.0-preview.6] - 2026-09-20

### Added

- `TryCreate`, UTF-8 span formatting and parsing, `TypeConverter`, Dapper and OpenAPI integrations,
  `STIA0010` / `STIA0011`, assembly-level defaults, ids in nested types, comparison / formatting /
  dictionary-key support and validator hooks.

### Changed

- Public API baseline, package validation, symbol packages and deterministic builds.

### Fixed

- Around twenty generator, analyzer and code-fix defects, including symbol-level primitive
  detection, cross-namespace EF Core converter collisions, missing `uuid` formats in Swagger,
  third-party interfaces suppressing generation, and multi-part `partial` false positives.

## [1.3.0-preview.5] - 2026-09-13

### Fixed

- `Len.StronglyTypedId` now declares its dependency on `Len.StronglyTypedId.Generators`.

## [1.3.0-preview.4] - 2026-09-13

_Retag of `1.3.0-preview.2`; no content changes._

## [1.3.0-preview.3] - 2026-09-13

_Retag of `1.3.0-preview.2`; no content changes._

## [1.3.0-preview.2] - 2026-09-13

### Fixed

- The publish workflow installs both the .NET 8 and .NET 10 SDKs, and the `OutputPath` trailing
  slash that produced NU5019.

## [1.3.0-priview.1] - 2026-09-13

_Published with this spelling; later releases use `preview`._

### Added

- Repository modernisation: .NET 10 target and an expanded test matrix.

### Changed

- Releases use NuGet trusted publishing (OIDC) instead of a long-lived API key.

## [1.2.1] - 2024-02-26

### Added

- `net7.0` target.

### Fixed

- `ModuleInfo.GetTypes` no longer caches, fixing stale results.

## [1.2.0] - 2023-11-27

### Changed

- Targets `net8.0`.

## [1.1.3] - 2023-10-08

### Changed

- Documentation only.

## [1.1.2] - 2023-10-08

### Changed

- `GetTypes` results are cached.

### Fixed

- Namespaces in the generated `InvalidOperationException` message are fully qualified.

## [1.1.1] - 2023-05-20

### Fixed

- Analyzer messages are localisable again.

## [1.1.0] - 2023-05-11

### Added

- Incremental generator pipeline and a syntax analyzer.
- Swagger and EF Core `ValueConverter` generators.

### Fixed

- Warnings emitted for `record`-based ids.

## [1.0.7] - 2023-04-26

### Added

- Localised generator resources (zh-CN).

## [1.0.6] - 2023-04-25

### Added

- Generator diagnostics for invalid id declarations.
- Localised generator error messages.

## [1.0.5] - 2023-04-23

### Added

- `IParsable<TSelf>` and a System.Text.Json converter on generated ids.
- `byte`, `sbyte`, `short` and `ushort` primitives.

### Removed

- `Len.StronglyTypedId.AspNetCore`, superseded by the generated integrations.

### Fixed

- Calling `AddStronglyTypedId` twice no longer registers duplicate resolvers.

[Unreleased]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0...HEAD
[1.3.0]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.11...1.3.0
[1.3.0-preview.11]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.10...1.3.0-preview.11
[1.3.0-preview.10]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.9...1.3.0-preview.10
[1.3.0-preview.9]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.8...1.3.0-preview.9
[1.3.0-preview.8]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.7...1.3.0-preview.8
[1.3.0-preview.7]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.6...1.3.0-preview.7
[1.3.0-preview.6]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.5...1.3.0-preview.6
[1.3.0-preview.5]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.2...1.3.0-preview.5
[1.3.0-preview.4]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.2...1.3.0-preview.4
[1.3.0-preview.3]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.2...1.3.0-preview.3
[1.3.0-preview.2]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-priview.1...1.3.0-preview.2
[1.3.0-priview.1]: https://github.com/lenfon/StronglyTypedId/compare/v1.2.1...1.3.0-priview.1
[1.2.1]: https://github.com/lenfon/StronglyTypedId/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.3...v1.2.0
[1.1.3]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/lenfon/StronglyTypedId/compare/v1.0.7...v1.1.0
[1.0.7]: https://github.com/lenfon/StronglyTypedId/compare/v1.0.6...v1.0.7
[1.0.6]: https://github.com/lenfon/StronglyTypedId/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/lenfon/StronglyTypedId/releases/tag/v1.0.5
