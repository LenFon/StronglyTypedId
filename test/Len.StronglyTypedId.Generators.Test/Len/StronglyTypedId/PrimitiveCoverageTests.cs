using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 基元覆盖率的漂移防线：生成器登记的每种基元都必须在测试程序集里有对应的 Id 声明。
/// </summary>
/// <remarks>
/// 新增一种基元最容易漏的是「补一个用于测试的 Id」：生成器表改了、README 重算了、快照也过了，运行时行为却一行没跑到。
/// 这条用例把「登记表」与「测试夹具」绑在一起，漏了就红。
/// </remarks>
public class PrimitiveCoverageTests
{
    [Fact]
    public void EveryDocumentedPrimitive_Should_HaveATestId()
    {
        var documented = DocumentedPrimitiveNames();
        var covered = TestIdPrimitives().Select(pair => pair.Primitive.FullName!).ToHashSet(StringComparer.Ordinal);

        covered.Should().Contain(documented, "每个受支持的基元都必须有一个用于测试的 Id 声明");
    }

    [Fact]
    public void EveryTestIdPrimitive_Should_BeDocumentedOrAnEnum()
    {
        var documented = DocumentedPrimitiveNames();

        var undocumented = TestIdPrimitives()
            .Where(pair => !pair.Primitive.IsEnum)
            // 自定义基元不在登记表里，也不该在：它们由 IParsable + IComparable 契约判定，
            // 而不是由一份封闭清单判定。它们的覆盖由 CustomPrimitiveTests 单独守着。
            .Where(pair => !IsCustomPrimitive(pair.Primitive))
            .Select(pair => pair.Primitive.FullName!)
            .Where(name => !documented.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        undocumented.Should().BeEmpty("测试 Id 不应使用未登记的基元");
    }

    /// <summary>
    /// 判断类型是否满足自定义基元契约（与生成器同一套判据的运行时近似）。
    /// </summary>
    /// <remarks>
    /// 运行时没有 Roslyn 符号，只能按接口近似：实现 <c>IParsable&lt;TSelf&gt;</c> 且类型实参是自身。
    /// 这个近似足够区分「内置基元」与「测试里自定义的基元」，后者不可能是 BCL 类型。
    /// </remarks>
    private static bool IsCustomPrimitive(Type primitive)
        => primitive.GetInterfaces().Any(@interface =>
            @interface.IsGenericType
            && @interface.GetGenericTypeDefinition() == typeof(IParsable<>)
            && @interface.GetGenericArguments()[0] == primitive);

    /// <summary>
    /// 登记表里的基元全名（<c>System.Guid</c> / <c>System.Numerics.BigInteger</c>）。
    /// </summary>
    private static HashSet<string> DocumentedPrimitiveNames()
        => SupportedPrimitiveTypes.DocumentedTypes
            .Select(entry => $"{entry.Namespace}.{entry.ClrName}")
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 测试程序集里所有实现了 <see cref="IStronglyTypedId{TSelf, TPrimitiveId}"/> 的类型及其基元。
    /// </summary>
    private static IEnumerable<(Type Id, Type Primitive)> TestIdPrimitives()
        => typeof(GuidId).Assembly
            .GetTypes()
            .Where(type => !type.IsGenericTypeDefinition)
            .SelectMany(type => type
                .GetInterfaces()
                .Where(@interface => @interface.IsGenericType
                    && @interface.GetGenericTypeDefinition() == typeof(IStronglyTypedId<,>))
                .Select(@interface => (Id: type, Primitive: @interface.GetGenericArguments()[1])));
}
