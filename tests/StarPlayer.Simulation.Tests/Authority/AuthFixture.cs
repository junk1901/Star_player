using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests.Authority;

/// <summary>Builders for the C3-A tests: a canonical game id, canonical event ids, an RNG snapshot with real draws and aggregates built only through the internal factories.</summary>
internal static class Auth
{
    public const string GameId = "game:test:g1";

    public static string Id(int sequence, string game = GameId) => $"{game}:e{sequence:D6}";

    public static GameState NewState(string gameId = GameId) =>
        GameReducer.Start(new GameStart(gameId, TestRules.Dh, Team("a", true), Team("h", true))).Value;

    /// <summary>A snapshot whose one stream has really drawn, so a "same reference / same hash" assertion is not trivially about an empty set.</summary>
    public static GameRandomSnapshot Rng(string context = GameId, ulong seed = 7)
    {
        var random = GameRandom.Create(seed, context).Value;
        var stream = random.Stream(RngStreamId.Create("game/runner", 1).Value);
        stream.NextBounded(1000);
        stream.NextBounded(1000);
        return random.Snapshot();
    }

    public static AuthoritativeGame New(string gameId = GameId) => Ok(AuthoritativeGame.StartNew(NewState(gameId), Rng(gameId)));

    public static GameCursor Cursor(AuthoritativeGame? game = null) => new(game ?? New());

    public static T Ok<T>(StarPlayer.Domain.Common.Result<T> result)
    {
        Assert.True(result.IsSuccess, $"{result.Error?.Code}: {result.Error?.Message}");
        return result.Value;
    }

    public static string Code<T>(StarPlayer.Domain.Common.Result<T> result)
    {
        Assert.False(result.IsSuccess);
        return result.Error!.Value.Code;
    }

    /// <summary>Stage and Publish one event on the cursor, asserting both succeed.</summary>
    public static CommitRecord Commit(GameCursor cursor, GameEvent gameEvent) =>
        Ok(cursor.Publish(Ok(CommitGate.Stage(cursor.Current, gameEvent))));

    public static ApproachEvent Approach(int sequence) => new(Id(sequence), ApproachKind.Normal);

    /// <summary>A pinch hitter for the away team's current batter; legal at any time in a standard fixture game.</summary>
    public static SubstitutionEvent PinchHit(int sequence, string player = "ab1") => new(Id(sequence), SubstitutionKind.PinchHit, TeamSide.Away, P(player));
}

/// <summary>
/// Drives the real reducer with canonical event ids (the shared <c>Script</c> numbers its events "e1", which are not official ids) so that a late-game state with
/// a genuine receipt history exists. It does not use the gate; its result is turned into an aggregate by the internal <c>Restore</c> factory.
/// </summary>
internal sealed class CanonScript
{
    private int _n;

    public GameState State { get; private set; } = Auth.NewState();

    public int Count => _n;

    public CanonScript Apply(Func<int, GameEvent> make)
    {
        var e = make(++_n);
        var r = GameReducer.Apply(State, e);
        Assert.True(r.IsSuccess, $"{e.GetType().Name} {e.Id}: {r.Error?.Code}");
        State = r.Value.State;
        return this;
    }

    public CanonScript Out() => Apply(n => new ApproachEvent(Auth.Id(n), ApproachKind.Normal))
        .Apply(n => new InPlayEvent(Auth.Id(n), BattedBallType.Fly, InPlayResult.Out, false, null, [Po(FieldPosition.LeftField)]));

    public CanonScript Outs(int count)
    {
        for (var i = 0; i < count; i++)
            Out();
        return this;
    }

    public CanonScript Triple() => Apply(n => new ApproachEvent(Auth.Id(n), ApproachKind.Normal))
        .Apply(n => new InPlayEvent(Auth.Id(n), BattedBallType.Line, InPlayResult.Triple, false, null));

    /// <summary>0-0 after eight and a half innings, home to bat in the ninth.</summary>
    public static CanonScript ToBottomOfNinthTied() => new CanonScript().Outs(51);

    public AuthoritativeGame ToAuthority(long revision = 0) =>
        Auth.Ok(AuthoritativeGame.Restore(State, Auth.Rng(), _n, revision));
}
