using System.Runtime.CompilerServices;

// Bogus 适配包复用 Testing 包内部的反射读取逻辑（AssemblyTypeReader）：同一份保护写两遍正是它曾经
// 在一处缺失的原因。两个包同属测试辅助，版本始终一致，故按程序集名授权。
[assembly: InternalsVisibleTo("Len.StronglyTypedId.Testing.Bogus")]

// 单元测试要直接验证 DeterministicRandom 的区间语义（退化区间、满区间）：这些边界无法经公开 API
// 触发 —— PrimitiveValueFactory 的所有调用点都传非退化区间，故只能直接测内部类型。
[assembly: InternalsVisibleTo("Len.StronglyTypedId.Testing.Test")]
