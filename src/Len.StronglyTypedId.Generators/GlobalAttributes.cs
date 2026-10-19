[assembly: InternalsVisibleTo("Len.StronglyTypedId.Generators.Test")]
[assembly: InternalsVisibleTo("Len.StronglyTypedId.TestBase")]
[assembly: InternalsVisibleTo("Len.StronglyTypedId.Benchmarks")]
// README 的三张表由 DocGen 直接从生成器内部的判定数据渲染，故需要访问 internal 成员。
[assembly: InternalsVisibleTo("Len.StronglyTypedId.DocGen")]