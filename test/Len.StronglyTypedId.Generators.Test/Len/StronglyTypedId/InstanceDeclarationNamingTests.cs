using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="InstanceDeclaration.IsPascalCase"/> 的单元测试：命名实例名的 PascalCase 判定与建议名。
/// </summary>
/// <remarks>
/// 判定刻意保守：只看首字符是否大写、是否含下划线；全大写缩写（<c>URL</c>）不算违规。
/// 空名与全下划线名给不出建议名，按合规处理以免报出空建议 —— 这两条是分析器 STIA0022 的边界，
/// 生成器层用例覆盖不到。
/// </remarks>
public class InstanceDeclarationNamingTests
{
    [Theory]
    [InlineData("Active")]
    [InlineData("URL")]
    [InlineData("VeryHigh")]
    public void IsPascalCase_Should_ReturnTrue_ForCompliantName(string name)
    {
        InstanceDeclaration.IsPascalCase(name, out var suggestion).Should().BeTrue();
        suggestion.Should().Be(name);
    }

    [Theory]
    [InlineData("active", "Active")]
    [InlineData("very_high", "VeryHigh")]
    [InlineData("_private", "Private")]
    public void IsPascalCase_Should_ReturnFalse_AndSuggestName_ForNonCompliantName(string name, string expected)
    {
        InstanceDeclaration.IsPascalCase(name, out var suggestion).Should().BeFalse();
        suggestion.Should().Be(expected);
    }

    /// <summary>
    /// 空名按合规处理：给不出建议名，报一条空建议只会让用户困惑。
    /// </summary>
    [Fact]
    public void IsPascalCase_Should_ReturnTrue_ForEmptyName()
    {
        InstanceDeclaration.IsPascalCase(string.Empty, out var suggestion).Should().BeTrue();
        suggestion.Should().BeEmpty();
    }

    /// <summary>
    /// 全下划线名去掉分隔符后什么都不剩，同样按合规处理。
    /// </summary>
    [Theory]
    [InlineData("_")]
    [InlineData("__")]
    public void IsPascalCase_Should_ReturnTrue_ForUnderscoreOnlyName(string name)
    {
        InstanceDeclaration.IsPascalCase(name, out var suggestion).Should().BeTrue();
        suggestion.Should().BeEmpty();
    }
}
