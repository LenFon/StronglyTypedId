using System.Runtime.CompilerServices;

// Bogus 适配包复用 Testing 包内部的反射读取逻辑（AssemblyTypeReader）：同一份保护写两遍正是它曾经
// 在一处缺失的原因。两个包同属测试辅助，版本始终一致，故按程序集名授权。
[assembly: InternalsVisibleTo("Len.StronglyTypedId.Testing.Bogus")]
