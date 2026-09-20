// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Collections.Immutable;
using System.IO;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="StronglyTypedIdInfo"/> 的单元测试，聚焦 <see cref="StronglyTypedIdInfo.PrimitiveIdTypeName"/>
/// 的解析来源。
/// </summary>
/// <remarks>
/// 强类型 Id 有两种来源：本次编译新声明的（尚未实现接口，接口由生成代码补充）与引用程序集中已生成的
/// （已实现接口）。后者必须以 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的第二个类型实参为准，
/// 而不能按构造函数形状推断，否则会同时暴露两类缺陷：没有单参数构造函数时取首个元素直接抛异常；
/// 引用类型 <c>record</c> 会合成 <c>Foo(Foo original)</c> 复制构造函数，主构造函数参数多于一个时
/// 会把该记录自身误判为基元类型。
/// </remarks>
public class StronglyTypedIdInfoTests
{
    #region 接口优先：引用程序集中已生成的强类型 Id

    [Fact]
    public void PrimitiveIdTypeName_Should_BeResolvedFromInterface_WhenNoSingleParameterConstructor()
    {
        // 缺陷回归：修复前构造函数分支先行（所有具名类型都满足 INamedTypeSymbol），
        // First(constructor.Parameters.Length == 1) 在类型没有单参数构造函数时抛
        // InvalidOperationException，导致整个编译的源码生成崩溃。
        var type = GetTypeSymbol(
            """
            namespace Referenced;

            public readonly struct LegacyId : Len.StronglyTypedId.IStronglyTypedId<LegacyId, int>
            {
                public LegacyId(int number, string tag)
                {
                    Number = number;
                    Tag = tag;
                }

                public int Number { get; }

                public string Tag { get; }

                public int Value => Number;

                public static LegacyId Create(int value) => new(value, string.Empty);

                public static LegacyId Parse(string value, System.IFormatProvider? provider)
                    => new(int.Parse(value, provider), string.Empty);

                public static bool TryParse(
                    string? value,
                    System.IFormatProvider? provider,
                    out LegacyId result)
                {
                    result = new(0, string.Empty);
                    return true;
                }

                public static bool operator ==(LegacyId left, LegacyId right)
                    => left.Number == right.Number && left.Tag == right.Tag;

                public static bool operator !=(LegacyId left, LegacyId right) => !(left == right);

                public override bool Equals(object? obj) => obj is LegacyId other && this == other;

                public override int GetHashCode() => System.HashCode.Combine(Number, Tag);
            }
            """,
            "Referenced.LegacyId");

        var info = new StronglyTypedIdInfo(type);

        info.PrimitiveIdTypeName.Should().Be("int");
    }

    [Fact]
    public void PrimitiveIdTypeName_Should_BeResolvedFromInterface_NotRecordCopyConstructor()
    {
        // 缺陷回归：引用类型 record 会额外合成 Foo(Foo original) 复制构造函数。主构造函数参数多于一个时，
        // 「首个单参数构造函数」命中的正是该复制构造函数，会把记录自身误判为基元类型——不抛异常，
        // 生成出来的转换器 / JSON 契约全部指向错误类型，属静默错误。
        var type = GetTypeSymbol(
            """
            namespace Referenced;

            public record TaggedId(int Number, string Tag) : Len.StronglyTypedId.IStronglyTypedId<TaggedId, int>
            {
                public int Value => Number;

                public static TaggedId Create(int value) => new(value, string.Empty);

                public static TaggedId Parse(string value, System.IFormatProvider? provider)
                    => new(int.Parse(value, provider), string.Empty);

                public static bool TryParse(
                    string? value,
                    System.IFormatProvider? provider,
                    out TaggedId result)
                {
                    result = new(0, string.Empty);
                    return true;
                }
            }
            """,
            "Referenced.TaggedId");

        var info = new StronglyTypedIdInfo(type);

        info.PrimitiveIdTypeName.Should().Be("int");
        info.PrimitiveIdTypeName.Should().NotBe(info.FullyQualifiedName);
    }

    #endregion

    #region 构造函数回退：本次编译新声明的强类型 Id

    [Fact]
    public void PrimitiveIdTypeName_Should_FallBackToSingleParameterConstructor_WhenInterfaceNotImplemented()
    {
        // 新声明的强类型 Id 在生成时尚未实现接口（接口由生成代码补充），此时只能按单参数构造函数推断。
        var type = GetTypeSymbol(
            """
            namespace Referenced;

            public partial record struct NewId(int Value);
            """,
            "Referenced.NewId");

        type.Interfaces.Should().NotContain(
            @interface => @interface.Name == StronglyTypedIdInfo.InterfaceName,
            "测试前提：新声明的强类型 Id 此时不应已实现接口，否则覆盖不到构造函数回退分支");

        var info = new StronglyTypedIdInfo(type);

        info.PrimitiveIdTypeName.Should().Be("int");
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 在合成编译中取出指定元数据名对应的类型符号。测试源码必须先真正编译通过，
    /// 否则接口未成功绑定、用例会退化为「什么都没测」。
    /// </summary>
    private static INamedTypeSymbol GetTypeSymbol(string sourceCode, string metadataName)
    {
        var compilation = CSharpCompilation.Create(
            "ReferencedAssembly",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        emitResult.Success.Should().BeTrue(string.Join("\n", emitResult.Diagnostics));

        var type = compilation.GetTypeByMetadataName(metadataName);
        type.Should().NotBeNull();

        return type!;
    }

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        var seed = new[] { typeof(object).Assembly, typeof(StronglyTypedIdAttribute).Assembly };

        return AppDomain.CurrentDomain.GetAssemblies()
            .Union(seed)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => (MetadataReference)MetadataReference.CreateFromFile(assembly.Location))
            .ToImmutableArray();
    }

    #endregion
}
