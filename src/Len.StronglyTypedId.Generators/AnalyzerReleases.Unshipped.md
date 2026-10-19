### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
STIA0012 | StronglyTypedIdGenerator | Error | Reported by the source generator itself when code generation throws.
STIA0013 | StronglyTypedIdAnalyzer | Warning | Reported when [StronglyTypedId(Prefix = ...)] is set to an empty or whitespace-only string.
STIA0014 | StronglyTypedIdAnalyzer | Disabled | Reported when an externally visible id declares a Validator, so its primary constructor can bypass it. Enable via .editorconfig.
STIA0015 | StronglyTypedIdAnalyzer | Info | Reported when the id type name does not end with 'Id'.
STIA0016 | StronglyTypedIdAnalyzer | Info | Reported once per namespace/primitive group when several ids in the same namespace wrap the same primitive type.
