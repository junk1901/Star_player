using System.Collections.Immutable;
using StarPlayer.Application.Ports;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>C43 / C41: a game with a hit carrying an explained error survives the JSON event contract (fingerprints, receipts, replay); the refused combinations never get a receipt.</summary>
public class HitErrorReplayTests
{
    private static TeamSetup Team(string prefix)
    {
        var positions = new[] { FieldPosition.CenterField, FieldPosition.Shortstop, FieldPosition.RightField, FieldPosition.FirstBase, FieldPosition.DesignatedHitter,
            FieldPosition.LeftField, FieldPosition.ThirdBase, FieldPosition.SecondBase, FieldPosition.Catcher };
        var slots = positions.Select((p, i) => new LineupSlot(i + 1, PlayerId.Create($"{prefix}{i + 1}").Value, p)).ToList();
        return new TeamSetup(slots, PlayerId.Create(prefix + "p").Value, Enumerable.Range(1, 5).Select(i => PlayerId.Create($"{prefix}b{i}").Value).ToList());
    }

    private static ImmutableList<GameEvent> Events()
    {
        var events = ImmutableList.CreateBuilder<GameEvent>();
        events.Add(new ApproachEvent("x1", ApproachKind.Normal));
        events.Add(new InPlayEvent("x2", BattedBallType.Line, InPlayResult.Double));                              // a1 on second
        events.Add(new ApproachEvent("x3", ApproachKind.Normal));
        for (var i = 0; i < 4; i++)
            events.Add(new PitchEvent($"x4-{i}", new PitchSpec(PitchOutcome.Ball)));                                 // a2 walks: a1 second, a2 first
        events.Add(new ApproachEvent("x5", ApproachKind.Normal));
        events.Add(new InPlayEvent("x6", BattedBallType.Ground, InPlayResult.Single, false,
            [new RunnerMove(2, 4, MoveCause.ErrorExtraBase), new RunnerMove(1, 2)], null, [FieldPosition.RightField]));   // a3 singles; the error is explained by a1's extra base
        return events.ToImmutable();
    }

    private static GameState Replay(ScriptedGame game, IEnumerable<GameEvent> events)
    {
        var state = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
        foreach (var e in events)
        {
            var step = GameReducer.Apply(state, e);
            Assert.True(step.IsSuccess, $"{e.Id}: {step.Error?.Code}");
            state = step.Value.State;
        }

        return state;
    }

    [Fact]
    public void A_single_with_an_explained_error_survives_the_json_event_contract_and_a_refused_hit_leaves_no_receipt()
    {
        var rules = GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles.json")).Value.Single(r => r.ProfileId == "test-dh-cap11");
        var game = new ScriptedGame("hx1", "hit-error", rules, Team("a"), Team("h"), [], null);
        var events = Events();

        var read = ScriptedGameJson.ReadEvents(ScriptedGameJson.WriteEvents(events));
        Assert.True(read.IsSuccess, read.Error?.Message);
        for (var i = 0; i < events.Count; i++)
            Assert.True(EventFingerprint.Receipt(events[i]) == EventFingerprint.Receipt(read.Value[i]), events[i].Id);
        var original = Replay(game, events);
        var restored = Replay(game, read.Value);

        Assert.Equal(original.AppliedEvents, restored.AppliedEvents);
        Assert.Equal(original.Stats, restored.Stats);
        Assert.Equal((1, 1, 1), (original.Stats.Where(e => e.Player.Value == "a3" && e.Key == StatKey.BatH).Sum(e => e.Amount), original.AwayRuns, original.Stats.Where(e => e.Key == StatKey.FldE).Sum(e => e.Amount)));

        // the refused combinations: error saved a forced runner / an error nothing explains with a runner on first
        var withApproach = Replay(game, events.Take(events.Count - 1));                 // up to and including the approach of the next batter, a3
        var saved = new InPlayEvent("y1", BattedBallType.Ground, InPlayResult.Single, false, [new RunnerMove(1, 2, MoveCause.ErrorSavedOut)], null, [FieldPosition.Shortstop]);
        var unclear = new InPlayEvent("y2", BattedBallType.Ground, InPlayResult.Single, false, [new RunnerMove(1, 2)], null, [FieldPosition.Shortstop]);
        Assert.NotNull(withApproach);
        foreach (var bad in new[] { saved, unclear })
            Assert.False(GameReducer.Apply(withApproach, bad).IsSuccess);
        Assert.DoesNotContain(withApproach.AppliedEvents, r => r.EventId is "y1" or "y2");
    }
}
