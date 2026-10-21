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

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|--------------------
STIA0011 | StronglyTypedIdAnalyzer | Warning | StronglyTypedIdAnalyzer | Info | Raised so that bypassing a validator is visible in build output and CI. Projects that relied on the advisory level can restore it with `dotnet_diagnostic.STIA0011.severity = suggestion`.
