using System.Text;
using Godot;
using StarPlayer.Application.Projections;
using StarPlayer.Application.Queries;
using StarPlayer.Client.UI;

namespace StarPlayer.Client.Bootstrap;

/// <summary>Main scene script: wires services into the shell, switches between the diagnostic screens and handles the automatic launch modes.</summary>
public partial class GameRoot : Node
{
    private MainMenu _menu = null!;
    private PlayerDiagnosticsScreen _playerScreen = null!;
    private GameDiagnosticsScreen _gameScreen = null!;

    public override void _Ready()
    {
        var options = LaunchOptions.Parse(OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()));
        var services = CompositionRoot.Build(options);

        _menu = GD.Load<PackedScene>("res://UI/MainMenu/MainMenu.tscn").Instantiate<MainMenu>();
        AddChild(_menu);
        _menu.Initialize(services.Build, services.Diagnostics);
        _menu.PlayerDiagnosticsRequested += () => ShowPlayerScreen(true);
        _menu.GameDiagnosticsRequested += () => ShowGameScreen(true);

        _playerScreen = GD.Load<PackedScene>("res://UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn").Instantiate<PlayerDiagnosticsScreen>();
        AddChild(_playerScreen);
        _playerScreen.Initialize(services.Players);
        _playerScreen.BackRequested += () => ShowPlayerScreen(false);
        ShowPlayerScreen(false);

        _gameScreen = GD.Load<PackedScene>("res://UI/GameDiagnostics/GameDiagnosticsScreen.tscn").Instantiate<GameDiagnosticsScreen>();
        AddChild(_gameScreen);
        _gameScreen.Initialize(services.Games);
        _gameScreen.BackRequested += () => ShowGameScreen(false);
        ShowGameScreen(false);

        if (options.RunGameDiagnostics)
            _ = RunAutomaticGameDiagnostics(options);
        else if (options.RunPlayerDiagnostics)
            _ = RunAutomaticPlayerDiagnostics(options);
        else if (options.RunDiagnostics)
            _ = RunAutomaticDiagnostics(options);
    }

    private void ShowPlayerScreen(bool visible)
    {
        _playerScreen.Visible = visible;
        _menu.Visible = !visible && !(_gameScreen is { Visible: true });
    }

    private void ShowGameScreen(bool visible)
    {
        _gameScreen.Visible = visible;
        _menu.Visible = !visible && !(_playerScreen is { Visible: true });
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

            if (options.ScreenshotPath is { } screenshotPath)
                await Capture(screenshotPath);

            exitCode = passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"STARPLAYER_DIAGNOSTICS_ERROR={exception}");
        }

        GetTree().Quit(exitCode);
    }

    /// <summary>
    /// Opens the player card, re-runs its self-check, "clicks" every listed player and dumps all text the viewer could read.
    /// The dump is scanned once more for hidden-development identifiers before the PASS marker is written.
    /// </summary>
    private async Task RunAutomaticPlayerDiagnostics(LaunchOptions options)
    {
        var exitCode = 2;
        try
        {
            ShowPlayerScreen(true);
            var checksPassed = _playerScreen.Refresh();
            var ids = _playerScreen.ListedPlayerIds;
            var dump = new StringBuilder();
            dump.AppendLine(_playerScreen.SummaryText).AppendLine().AppendLine(_playerScreen.CheckText);
            foreach (var id in ids)
                dump.AppendLine().AppendLine($"===== {id} =====").AppendLine(_playerScreen.Select(id));

            var leaks = HiddenDataGuard.FindLeaks(dump.ToString());
            var ownCards = ids.Count(id => _playerScreen.Select(id).Contains("[내 선수"));
            var passed = checksPassed && leaks.Count == 0 && ids.Count >= 10 && ownCards == 1;
            dump.AppendLine().AppendLine($"hidden_identifier_leaks={leaks.Count} listed_players={ids.Count} own_player_cards={ownCards}");
            dump.AppendLine($"STARPLAYER_PLAYER_DIAGNOSTICS={(passed ? "PASS" : "FAIL")}");
            GD.Print(dump.ToString());
            if (options.OutputPath is { } outputPath)
                await File.WriteAllTextAsync(outputPath, dump.ToString());

            if (options.ScreenshotPath is { } screenshotPath && ids.Count > 0)
            {
                _playerScreen.Select(ids[0]);
                await Capture(screenshotPath);
                _playerScreen.Select(ids[^1]);
                await Capture(Path.ChangeExtension(screenshotPath, null) + "-npc" + Path.GetExtension(screenshotPath));
            }

            exitCode = passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"STARPLAYER_PLAYER_DIAGNOSTICS_ERROR={exception}");
        }

        GetTree().Quit(exitCode);
    }

    /// <summary>
    /// Opens the game screen, "clicks" Next Outcome through every scripted game, then runs the full self-check (each game against its
    /// independent expected result) and dumps all text a viewer could read. Writes the PASS marker only if everything agrees.
    /// </summary>
    private async Task RunAutomaticGameDiagnostics(LaunchOptions options)
    {
        var exitCode = 2;
        try
        {
            ShowGameScreen(true);
            var ids = _gameScreen.ListedGameIds;
            var dump = new StringBuilder();
            dump.AppendLine(_gameScreen.CheckText);
            var finished = 0;
            for (var game = 0; game < ids.Count; game++)
            {
                _gameScreen.Start(ids[game]);
                var clicks = 0;
                while (_gameScreen.CurrentView is { IsOver: false } && clicks < 5000)
                {
                    _gameScreen.PressNext();
                    clicks++;
                    if (game == 0 && clicks == 24 && options.ScreenshotPath is { } midPath)
                        await Capture(midPath);
                }

                var view = _gameScreen.CurrentView;
                dump.AppendLine().AppendLine($"===== {ids[game]} ({clicks} clicks) =====");
                if (view is not null)
                {
                    dump.AppendLine(GameViewFormatter.Summary(view)).AppendLine(GameViewFormatter.LineScore(view)).AppendLine(GameViewFormatter.Box(view));
                    if (view.IsOver && view.BoxScore is not null) finished++;
                }

                if (game == 0 && options.ScreenshotPath is { } endPath)
                    await Capture(Path.ChangeExtension(endPath, null) + "-end" + Path.GetExtension(endPath));
            }

            var checksPassed = _gameScreen.SelfCheckPassed();
            var passed = checksPassed && ids.Count >= 4 && finished == ids.Count;
            dump.AppendLine().AppendLine($"games={ids.Count} finished_with_box_score={finished} self_check={(checksPassed ? "PASS" : "FAIL")}");
            dump.AppendLine($"STARPLAYER_GAME_DIAGNOSTICS={(passed ? "PASS" : "FAIL")}");
            GD.Print(dump.ToString());
            if (options.OutputPath is { } outputPath)
                await File.WriteAllTextAsync(outputPath, dump.ToString());
            exitCode = passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"STARPLAYER_GAME_DIAGNOSTICS_ERROR={exception}");
        }

        GetTree().Quit(exitCode);
    }

    private async Task Capture(string path)
    {
        if (DisplayServer.GetName() == "headless")
            return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"STARPLAYER_SCREENSHOT={error} path={path} size={GetViewport().GetVisibleRect().Size}");
    }
}
