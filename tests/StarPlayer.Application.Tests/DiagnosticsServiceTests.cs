using StarPlayer.Application.Diagnostics;
using StarPlayer.Application.Ports;

namespace StarPlayer.Application.Tests;

public class DiagnosticsServiceTests
{
    private sealed class FakeStorage(Func<IReadOnlyList<DiagnosticStep>> run) : IStorageDiagnostics
    {
        public IReadOnlyList<DiagnosticStep> RunChecks() => run();
    }

    [Fact]
    public void Report_passes_only_when_every_step_passed()
    {
        var steps = new[] { new DiagnosticStep("a", true, ""), new DiagnosticStep("b", true, "") };

        Assert.True(new DiagnosticReport(steps).Passed);
        Assert.False(new DiagnosticReport([.. steps, new DiagnosticStep("c", false, "")]).Passed);
        Assert.False(new DiagnosticReport([]).Passed);
    }

    [Fact]
    public void Run_returns_the_ports_steps()
    {
        var service = new DiagnosticsService(new FakeStorage(() => [new DiagnosticStep("a", true, "ok")]));

        var report = service.Run();

        Assert.True(report.Passed);
        Assert.Equal("a", Assert.Single(report.Steps).Name);
    }

    [Fact]
    public void Run_converts_an_unexpected_port_exception_into_a_failed_step()
    {
        var service = new DiagnosticsService(new FakeStorage(() => throw new IOException("disk gone")));

        var report = service.Run();

        Assert.False(report.Passed);
        var step = Assert.Single(report.Steps);
        Assert.Equal(DiagnosticsService.StorageStepName, step.Name);
        Assert.Contains("disk gone", step.Detail);
    }
}
