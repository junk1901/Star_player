using System.Reflection;
using StarPlayer.Application.Ports;
using StarPlayer.Application.Projections;
using StarPlayer.Domain.Common;

namespace StarPlayer.Infrastructure.Fixtures;

/// <summary>
/// Supplies scripted games and GameRules profiles from JSON text. The default instance reads the profiles and the diagnostic games
/// embedded in this assembly. Internal like the Phase 1 source: a client gets only <see cref="GameDiagnosticsComposition"/>.
/// </summary>
internal sealed class ScriptedGameSource : IScriptedGameSource
{
    public const string RulesResource = "game_rules_profiles.json";
    public const string GamesResource = "phase2_diagnostic_games.json";

    private readonly Func<string> _rules;
    private readonly Func<string> _games;

    public ScriptedGameSource() : this(() => ReadResource(RulesResource), () => ReadResource(GamesResource))
    {
    }

    public ScriptedGameSource(string rulesJson, string gamesJson) : this(() => rulesJson, () => gamesJson)
    {
    }

    private ScriptedGameSource(Func<string> rules, Func<string> games)
    {
        _rules = rules;
        _games = games;
    }

    public Result<IReadOnlyList<ScriptedGame>> Load()
    {
        try
        {
            var rules = GameRulesJson.Read(_rules());
            if (!rules.IsSuccess)
                return Result<IReadOnlyList<ScriptedGame>>.Failure(rules.Error!.Value);
            return ScriptedGameJson.Read(_games(), rules.Value);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            return Result<IReadOnlyList<ScriptedGame>>.Failure(new Error("Fixture.unreadable", $"진단 경기 데이터를 읽을 수 없습니다: {exception.Message}"));
        }
    }

    public static string ReadResource(string name)
    {
        using var stream = typeof(ScriptedGameSource).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"내장 데이터 '{name}'이(가) 어셈블리에 없습니다.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>The only public way to obtain the Phase 2 game diagnostic service; it returns public views only.</summary>
public static class GameDiagnosticsComposition
{
    public static GameDiagnosticsService CreateFictionalPhase2() => new(new ScriptedGameSource());
}
