using Godot;
using StarPlayer.Application.Diagnostics;

namespace StarPlayer.Client.UI;

/// <summary>Thin presenter for the diagnostic shell (Phase 0 storage probe, Phase 1 player-card entry). No gameplay, no direct storage access.</summary>
public partial class MainMenu : Control
{
    [Signal]
    public delegate void PlayerDiagnosticsRequestedEventHandler();

    private Label _titleLabel = null!;
    private Label _versionLabel = null!;
    private Label _resultLabel = null!;
    private DiagnosticsService? _diagnostics;

    public string ResultText => _resultLabel.Text;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("%TitleLabel");
        _versionLabel = GetNode<Label>("%VersionLabel");
        _resultLabel = GetNode<Label>("%ResultLabel");
        GetNode<Button>("%StartButton").Pressed += () => RunDiagnostics();
        GetNode<Button>("%PlayerDiagnosticsButton").Pressed += () => EmitSignal(SignalName.PlayerDiagnosticsRequested);
        GetNode<Button>("%QuitButton").Pressed += () => GetTree().Quit();
    }

    public void Initialize(BuildInfo build, DiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
        _titleLabel.Text = build.ProductName;
        _versionLabel.Text = $"빌드 버전: {build.Version}";
    }

    /// <summary>Runs the probe, shows the Korean result text and returns whether every step passed.</summary>
    public bool RunDiagnostics()
    {
        if (_diagnostics is null)
        {
            _resultLabel.Text = "진단 서비스가 연결되지 않았습니다.";
            return false;
        }

        var report = _diagnostics.Run();
        var passedCount = report.Steps.Count(step => step.Passed);
        var lines = report.Steps.Select(step => $"[{(step.Passed ? "통과" : "실패")}] {step.Name} — {step.Detail}");
        _resultLabel.Text = $"진단 결과: {(report.Passed ? "통과" : "실패")} ({passedCount}/{report.Steps.Count} 단계)\n" + string.Join("\n", lines);
        return report.Passed;
    }
}
