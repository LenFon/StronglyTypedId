using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="StronglyTypedIdGenerator.RunGuarded"/> 的契约守卫：生成阶段抛出的异常必须变成可定位的诊断。
/// </summary>
/// <remarks>
/// <para>
/// 生成器内部抛异常时，Roslyn 会丢弃该生成器本次的全部产出，使用者只看到一条 CS8785 警告 ——
/// 无位置、无原因、无堆栈，表现为「生成的代码凭空消失」。这条兜底此前完全没有测试把守，
/// 而它恰恰是最难靠人工复现的一类故障。
/// </para>
/// <para>
/// 兜底逻辑被拆成 <see cref="Action"/> + <see cref="Action{T}"/> 两个委托，正是为了能在测试里真实触发
/// 失败路径：<see cref="SourceProductionContext"/> 无法在测试中构造，若直接吃它，这段代码就只能靠
/// 「真的让某个生成器炸掉」来验证。
/// </para>
/// </remarks>
public class GeneratorFailureTests
{
    [Fact]
    public void RunGuarded_Should_ReportGeneratorFailure_WhenGeneratorThrows()
    {
        var diagnostics = new List<Diagnostic>();

        StronglyTypedIdGenerator.RunGuarded(
            "ThrowingGenerator",
            () => throw new InvalidOperationException("boom"),
            diagnostics.Add,
            CancellationToken.None);

        var diagnostic = diagnostics.Should().ContainSingle().Subject;

        diagnostic.Id.Should().Be(Descriptors.GeneratorFailureId);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Location.Should().Be(Location.None);
        diagnostic.GetMessage().Should().Contain("ThrowingGenerator").And.Contain("boom");
    }

    [Fact]
    public void RunGuarded_Should_NotReport_WhenGeneratorSucceeds()
    {
        var diagnostics = new List<Diagnostic>();
        var ran = false;

        StronglyTypedIdGenerator.RunGuarded(
            "HealthyGenerator",
            () => ran = true,
            diagnostics.Add,
            CancellationToken.None);

        ran.Should().BeTrue();
        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// 取消是控制流而不是故障：必须原样上抛，否则生成器会被当成「已完成」而留下不完整的产出。
    /// </summary>
    [Fact]
    public void RunGuarded_Should_Rethrow_WhenCancellationWasRequested()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => StronglyTypedIdGenerator.RunGuarded(
            "CancelledGenerator",
            () => throw new OperationCanceledException(),
            _ => { },
            cancellation.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    /// <summary>
    /// 未被取消时抛出的 <see cref="OperationCanceledException"/> 是真故障，必须报出来而不是被静默吞掉。
    /// </summary>
    [Fact]
    public void RunGuarded_Should_Report_WhenCancellationWasNotRequested()
    {
        var diagnostics = new List<Diagnostic>();

        StronglyTypedIdGenerator.RunGuarded(
            "OddGenerator",
            () => throw new OperationCanceledException(),
            diagnostics.Add,
            CancellationToken.None);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(Descriptors.GeneratorFailureId);
    }
}
