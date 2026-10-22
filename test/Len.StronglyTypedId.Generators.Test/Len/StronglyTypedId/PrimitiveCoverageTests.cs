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
            .Select(pair => pair.Primitive.FullName!)
            .Where(name => !documented.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        undocumented.Should().BeEmpty("测试 Id 不应使用未登记的基元");
    }

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
