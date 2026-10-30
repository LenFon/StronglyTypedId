using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId.Testing.Tests;

/// <summary>
/// <see cref="StronglyTypedIdConformance"/>：把「静态构造遇第一个异常就中止」变成「一次列出全部坏实例」。
/// </summary>
/// <remarks>
/// 自定义基元的命名实例值只能在运行期校验，而静态字段初始化是「第一个失败就停」——
/// 有 3 个坏实例时用户要改一次、跑一次、再发现一个。本类锁住聚合行为。
/// </remarks>
public class StronglyTypedIdConformanceTests
{
    /// <summary>
    /// 程序集里没有强类型 Id 时返回空列表。
    /// </summary>
    /// <remarks>
    /// 用 <c>Len.StronglyTypedId.Testing</c> 自身的程序集：它只提供造值工具，不声明任何 Id。
    /// 不能用本测试程序集 —— 它同时含有好实例与坏实例，断言「全好」必然失败。
    /// </remarks>
    [Fact]
    public void FindBrokenInstances_Should_ReturnEmpty_WhenAssemblyHasNoIds()
    {
        StronglyTypedIdConformance.FindBrokenInstances(typeof(StronglyTypedIdConformance).Assembly)
            .Should().BeEmpty();
    }

    /// <summary>
    /// 本程序集里那个合法实例不产生失败条目。
    /// </summary>
    [Fact]
    public void FindBrokenInstances_Should_NotReportValidInstance()
    {
        var failures = StronglyTypedIdConformance.FindBrokenInstances(typeof(ValidInstanceId).Assembly);

        failures.Select(failure => failure.IdType).Should().NotContain(typeof(ValidInstanceId));
    }

    [Fact]
    public void FindBrokenInstances_Should_ReportEveryBrokenType()
    {
        var failures = StronglyTypedIdConformance.FindBrokenInstances(typeof(BrokenInstanceId).Assembly);

        failures.Select(failure => failure.IdType).Should().Contain([typeof(BrokenInstanceId), typeof(SecondBrokenInstanceId)]);
    }

    /// <summary>
    /// 消息必须自带实例名与 Id 类型名 —— 这正是「难排查」的成因。
    /// </summary>
    [Fact]
    public void FindBrokenInstances_Should_IncludeInstanceNameInMessage()
    {
        var failure = StronglyTypedIdConformance.FindBrokenInstances(typeof(BrokenInstanceId).Assembly)
            .Single(entry => entry.IdType == typeof(BrokenInstanceId));

        failure.Message.Should().StartWith("StronglyTypedId: named instance 'Broken' of 'BrokenInstanceId'");
    }

    [Fact]
    public void AssertAllInstancesValid_Should_NotThrow_WhenAssemblyHasNoIds()
    {
        var act = () => StronglyTypedIdConformance.AssertAllInstancesValid(typeof(StronglyTypedIdConformance).Assembly);

        act.Should().NotThrow();
    }

    [Fact]
    public void AssertAllInstancesValid_Should_ListAllFailures()
    {
        var act = () => StronglyTypedIdConformance.AssertAllInstancesValid(typeof(BrokenInstanceId).Assembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*2 strongly typed id type(s) failed*BrokenInstanceId*SecondBrokenInstanceId*");
    }

    [Fact]
    public void FindBrokenInstances_Should_Throw_WhenAssemblyIsNull()
    {
        var act = () => StronglyTypedIdConformance.FindBrokenInstances(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// 只认实现了 <c>IStronglyTypedId&lt;,&gt;</c> 的类型：普通类型即使有静态构造也不该被强制初始化。
    /// </summary>
    [Fact]
    public void FindBrokenInstances_Should_IgnoreTypesThatAreNotStronglyTypedIds()
    {
        var failures = StronglyTypedIdConformance.FindBrokenInstances(typeof(NotAnId).Assembly);

        failures.Select(failure => failure.IdType).Should().NotContain(typeof(NotAnId));
    }
}

/// <summary>
/// 全部实例都合法的类型：出现在程序集里但不产生失败条目。
/// </summary>
[StronglyTypedId]
[StronglyTypedIdInstance("Zero", "0")]
public partial record struct ValidInstanceId(int Value);

/// <summary>
/// 实例值无法解析：静态构造抛 <see cref="TypeInitializationException"/>。
/// </summary>
/// <remarks>
/// 必须用自定义基元：内置基元的实例值在编译期就被 STIA0021 拦下，产不出运行期失败。
/// </remarks>
[StronglyTypedId]
[StronglyTypedIdInstance("Broken", "not-an-email")]
public partial record struct BrokenInstanceId(Email Value);

/// <summary>
/// 第二个坏实例：用来验证「一次列出全部」而不是「第一个就停」。
/// </summary>
[StronglyTypedId]
[StronglyTypedIdInstance("AlsoBroken", "also-not-an-email")]
public partial record struct SecondBrokenInstanceId(Email Value);

/// <summary>
/// 只接受含 <c>@</c> 的文本：让上面两个实例在运行期失败。
/// </summary>
public readonly record struct Email(string Value) : IParsable<Email>, IComparable<Email>, IComparable
{
    public static Email Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid Email.");

    public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
    {
        if (s is not null && s.Contains('@', StringComparison.Ordinal))
        {
            result = new Email(s);
            return true;
        }

        result = default;
        return false;
    }

    public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

    public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException("not an Email", nameof(obj));

    public override string ToString() => Value;
}

/// <summary>
/// 普通类型：有静态构造，但不是强类型 Id。
/// </summary>
public static class NotAnId
{
    static NotAnId() => throw new InvalidOperationException("Should never run.");
}
