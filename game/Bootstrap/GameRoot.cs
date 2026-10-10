using Godot;
using StarPlayer.Client.UI;

namespace StarPlayer.Client.Bootstrap;

/// <summary>Main scene script: wires services into the shell and handles the diagnostic launch mode.</summary>
public partial class GameRoot : Node
{
    private MainMenu _menu = null!;

    public override void _Ready()
    {
        var services = CompositionRoot.Build();
        _menu = GD.Load<PackedScene>("res://UI/MainMenu/MainMenu.tscn").Instantiate<MainMenu>();
        AddChild(_menu);
        _menu.Initialize(services.Build, services.Diagnostics);

        var options = LaunchOptions.Parse(OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()));
        if (options.RunDiagnostics)
            _ = RunAutomaticDiagnostics(options);
    }

    private async Task RunAutomaticDiagnostics(LaunchOptions options)
    {
        var exitCode = 2;
        try
        {
            var passed = _menu.RunDiagnostics();
            var summary = _menu.ResultText + $"\nSTARPLAYER_DIAGNOSTICS={(passed ? "PASS" : "FAIL")}\n";
            GD.Print(summary);
            if (options.OutputPath is { } outputPath)
                await File.WriteAllTextAsync(outputPath, summary);

            if (options.ScreenshotPath is { } screenshotPath && DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var error = GetViewport().GetTexture().GetImage().SavePng(screenshotPath);
                GD.Print($"STARPLAYER_SCREENSHOT={error} size={GetViewport().GetVisibleRect().Size}");
            }

            exitCode = passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"STARPLAYER_DIAGNOSTICS_ERROR={exception}");
        }

        GetTree().Quit(exitCode);
    }
}
