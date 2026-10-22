using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="StronglyTypedIdGenerator.RunGuarded"/> 的契约守卫：生成阶段抛出的异常必须变成可定位的诊断。
/// </summary>
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
