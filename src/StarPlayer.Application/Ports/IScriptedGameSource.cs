using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;

namespace StarPlayer.Application.Ports;

/// <summary>
/// Expected results written down independently of the engine (by the generator's own scorer or by hand). Only strings and
/// numbers: it is compared with the engine's output, never produced from it.
/// </summary>
internal sealed record ExpectedGame(
    string Outcome,
    int AwayRuns,
    int HomeRuns,
    IReadOnlyList<int> AwayLine,
    IReadOnlyList<int> HomeLine,
    int AwayHits,
    int HomeHits,
    int AwayErrors,
    int HomeErrors,
    int AwayLeftOnBase,
    int HomeLeftOnBase,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Players,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Catchers,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Batteries,
    IReadOnlyDictionary<string, string> Decisions,
    IReadOnlyList<string> Appearances);

/// <summary>A complete game as data: rules, both rosters and the ordered events, optionally with independent expected results.</summary>
internal sealed record ScriptedGame(
    string Id,
    string Title,
    GameRuleSnapshot Rules,
    TeamSetup Away,
    TeamSetup Home,
    IReadOnlyList<GameEvent> Events,
    ExpectedGame? Expected);

/// <summary>Internal port for scripted diagnostic/test games. Implementations return a typed failure for bad content.</summary>
internal interface IScriptedGameSource
{
    Result<IReadOnlyList<ScriptedGame>> Load();
}
