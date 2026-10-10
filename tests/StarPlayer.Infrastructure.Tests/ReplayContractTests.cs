using System.Collections.Immutable;
using StarPlayer.Application.Ports;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// The resumable-game fixture contract (docs/game-engine.md section 3a): a game is its start data, its ordered events (JSON) and the event
/// receipts (id, kind, fingerprint version, fingerprint). Replaying the same events gives the same state; the receipts survive JSON; the
/// fingerprints do not change when an event is written and read again. No SQLite mapping exists before Phase 4.
/// </summary>
public class ReplayContractTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    private static IReadOnlyList<ScriptedGame> Hundred()
    {
        var loaded = new ScriptedGameSource(File.ReadAllText(Path.Combine(Root, "content", "rules", "game_rules_profiles.json")),
            File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase2_scripted_games_100.json"))).Load();
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        return loaded.Value;
    }

    private static GameState Replay(ScriptedGame game, IEnumerable<GameEvent> events)
    {
        var state = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
        foreach (var e in events)
        {
            var step = GameReducer.Apply(state, e);
            Assert.True(step.IsSuccess, $"{game.Id} {e.Id}: {step.Error?.Code}");
            state = step.Value.State;
        }

        return state;
    }

    [Fact]
    public void Writing_and_reading_every_event_of_the_hundred_games_keeps_its_fingerprint_and_the_replay_identical()
    {
        foreach (var game in Hundred())
        {
            var read = ScriptedGameJson.ReadEvents(ScriptedGameJson.WriteEvents(game.Events));
            Assert.True(read.IsSuccess, read.Error?.Message);
            Assert.Equal(game.Events.Count, read.Value.Count);
            for (var i = 0; i < game.Events.Count; i++)
                Assert.True(EventFingerprint.Receipt(game.Events[i]) == EventFingerprint.Receipt(read.Value[i]), $"{game.Id} {game.Events[i].Id}: 지문이 JSON 왕복 후 달라짐");

            var original = Replay(game, game.Events);
            var again = Replay(game, read.Value);
            Assert.Equal(original.AppliedEvents, again.AppliedEvents);
            Assert.Equal((original.AwayRuns, original.HomeRuns, original.Stats.Count), (again.AwayRuns, again.HomeRuns, again.Stats.Count));
            Assert.Equal(original.Stats, again.Stats);
        }
    }

    [Fact]
    public void Retrying_events_that_went_through_json_is_a_duplicate_and_a_changed_one_is_a_conflict()
    {
        var game = Hundred()[3];
        var state = Replay(game, game.Events);
        var roundTripped = ScriptedGameJson.ReadEvents(ScriptedGameJson.WriteEvents(game.Events)).Value;

        foreach (var e in roundTripped)
        {
            var retry = GameReducer.Apply(state, e);
            Assert.True(retry.IsSuccess && retry.Value.WasDuplicate, e.Id);
            Assert.Same(state, retry.Value.State);
        }

        var first = (ApproachEvent)game.Events.First(e => e is ApproachEvent);
        var changed = first with { Approach = first.Approach == ApproachKind.Aggressive ? ApproachKind.Patient : ApproachKind.Aggressive };
        Assert.Equal("Game.event_id_conflict", GameReducer.Apply(state, changed).Error!.Value.Code);
    }

    [Fact]
    public void Event_receipts_survive_json_and_say_which_event_each_id_belongs_to()
    {
        var game = Hundred()[0];
        var state = Replay(game, game.Events);

        var json = EventReceiptJson.Write(state.AppliedEvents);
        var back = EventReceiptJson.Read(json);

        Assert.True(back.IsSuccess, back.Error?.Message);
        Assert.Equal(state.AppliedEvents, back.Value);
        Assert.Contains("\"fingerprint_version\": \"ef1\"", json);
        Assert.Equal(game.Events.Select(e => e.Id), back.Value.Select(r => r.EventId));
        Assert.Equal(game.Events.Select(EventFingerprint.KindName), back.Value.Select(r => r.Kind));
        Assert.All(back.Value, r => Assert.Matches("^[0-9a-f]{64}$", r.Fingerprint));
        Assert.False(EventReceiptJson.Read("[{\"event_id\":\"a\",\"kind\":\"approach\",\"fingerprint_version\":\"ef1\",\"fingerprint\":\"x\"},{\"event_id\":\"a\",\"kind\":\"approach\",\"fingerprint_version\":\"ef1\",\"fingerprint\":\"y\"}]").IsSuccess);
        Assert.False(EventReceiptJson.Read("[{\"event_id\":\"a\"}]").IsSuccess);
    }

    // ---------------------------------------------------------------- DH state survives the fixture contract

    private static TeamSetup Team(string prefix)
    {
        var positions = new[] { FieldPosition.CenterField, FieldPosition.Shortstop, FieldPosition.RightField, FieldPosition.FirstBase, FieldPosition.DesignatedHitter,
            FieldPosition.LeftField, FieldPosition.ThirdBase, FieldPosition.SecondBase, FieldPosition.Catcher };
        var slots = positions.Select((p, i) => new LineupSlot(i + 1, PlayerId.Create($"{prefix}{i + 1}").Value, p)).ToList();
        return new TeamSetup(slots, PlayerId.Create(prefix + "p").Value, Enumerable.Range(1, 5).Select(i => PlayerId.Create($"{prefix}b{i}").Value).ToList());
    }

    [Fact]
    public void The_dh_state_of_each_team_is_part_of_the_replayable_game_and_survives_a_json_round_trip()
    {
        var rules = GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles.json")).Value.Single(r => r.ProfileId == "test-dh-forfeit");
        Assert.True(rules.DhMayBeForfeited);
        var game = new ScriptedGame("dh1", "dh", rules, Team("a"), Team("h"), [], null);
        ImmutableList<GameEvent> events =
        [
            new SubstitutionEvent("s1", SubstitutionKind.DhToField, TeamSide.Home, Slot: 3),     // the home team loses its DH while fielding
            new SubstitutionEvent("s2", SubstitutionKind.PitchingChange, TeamSide.Home, PlayerId.Create("h6").Value),
        ];

        var read = ScriptedGameJson.ReadEvents(ScriptedGameJson.WriteEvents(events));
        Assert.True(read.IsSuccess, read.Error?.Message);
        var original = Replay(game, events);
        var restored = Replay(game, read.Value);

        Assert.True(original.Away.DhActive);
        Assert.False(original.Home.DhActive);
        Assert.Equal((original.Away.DhActive, original.Home.DhActive), (restored.Away.DhActive, restored.Home.DhActive));
        Assert.Equal(original.Home.Lineup, restored.Home.Lineup);
        Assert.Equal(original.AppliedEvents, restored.AppliedEvents);
        Assert.Equal(original.Substitutions, restored.Substitutions);
        Assert.Contains("dh_to_field", ScriptedGameJson.WriteEvents(events));
    }

    [Fact]
    public void A_fielder_going_to_the_mound_names_the_dh_slot_explicitly_and_that_request_replays_identically_after_a_json_round_trip()
    {
        // OBR 5.11(a)(14) with the KBO-oriented profile: who fills the vacated position is stated by the event (Slot), never chosen by the engine
        var rules = GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles.json")).Value.Single(r => r.ProfileId == "kbo-oriented-unverified-v0");
        Assert.True(rules.FielderToMoundForfeitsDh);
        var game = new ScriptedGame("dh2", "dh", rules, Team("a"), Team("h"), [], null);
        var unspecified = new SubstitutionEvent("s1", SubstitutionKind.PitchingChange, TeamSide.Home, PlayerId.Create("h6").Value);
        var start = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
        Assert.Equal("Game.dh_vacated_position_unspecified", GameReducer.Apply(start, unspecified).Error!.Value.Code);

        ImmutableList<GameEvent> events = [unspecified with { Id = "s2", Slot = 5 }];
        var read = ScriptedGameJson.ReadEvents(ScriptedGameJson.WriteEvents(events));
        Assert.True(read.IsSuccess, read.Error?.Message);
        var original = Replay(game, events);
        var restored = Replay(game, read.Value);

        Assert.False(original.Home.DhActive);
        Assert.Equal(("h5", FieldPosition.LeftField), (original.Home.SlotByOrder(5).Player.Value, original.Home.SlotByOrder(5).Position));
        Assert.Equal(original.Home.Lineup, restored.Home.Lineup);
        Assert.Equal(original.AppliedEvents, restored.AppliedEvents);
        Assert.NotEqual(EventFingerprint.Receipt(unspecified with { Id = "s2" }).Fingerprint, EventFingerprint.Receipt(events[0]).Fingerprint);   // the slot is part of the event identity
    }

    [Fact]
    public void A_single_whose_batter_is_put_out_later_replays_identically_after_a_json_round_trip_with_the_same_fingerprints()
    {
        var rules = GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles.json")).Value.Single(r => r.ProfileId == "test-dh-cap11");
        var game = new ScriptedGame("pr1", "post reach", rules, Team("a"), Team("h"), [], null);
        PositionCredit Po(FieldPosition p) => new(p, CreditKind.Putout);
        PositionCredit As(FieldPosition p) => new(p, CreditKind.Assist);
        ImmutableList<PositionCredit> Out2B = [As(FieldPosition.RightField), Po(FieldPosition.SecondBase)];
        ImmutableList<PositionCredit> Fly = [Po(FieldPosition.LeftField)];
        ImmutableList<GameEvent> events =
        [
            new ApproachEvent("e1", ApproachKind.Normal),
            new InPlayEvent("e2", BattedBallType.Line, InPlayResult.Triple),                                              // a1 on third
            new ApproachEvent("e3", ApproachKind.Normal),
            new InPlayEvent("e4", BattedBallType.Fly, InPlayResult.Out, Moves: [new RunnerMove(3, 3)], BatterCredits: Fly),
            new ApproachEvent("e5", ApproachKind.Normal),
            new InPlayEvent("e6", BattedBallType.Fly, InPlayResult.Out, Moves: [new RunnerMove(3, 3)], BatterCredits: Fly),    // two outs
            new ApproachEvent("e7", ApproachKind.Normal),
            new InPlayEvent("e8", BattedBallType.Line, InPlayResult.Single, Moves:
                [new RunnerMove(3, 4, Sequence: 1), new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, Out2B, 2, 2, null)]),
        ];

        var original = Replay(game, events);
        var json = ScriptedGameJson.WriteEvents(events);
        var read = ScriptedGameJson.ReadEvents(json);
        Assert.True(read.IsSuccess, read.Error?.Message);
        var restored = Replay(game, read.Value);

        Assert.Contains("\"out_at\": 2", json);
        for (var i = 0; i < events.Count; i++)
            Assert.Equal(EventFingerprint.Receipt(events[i]), EventFingerprint.Receipt(read.Value[i]));
        Assert.Equal(original.AppliedEvents, restored.AppliedEvents);
        Assert.Equal(original.Stats, restored.Stats);
        Assert.Equal((1, 0), (restored.AwayRuns, restored.HomeRuns));                 // the run before the third out counts: worked out by hand
        Assert.Equal((InningHalf.Bottom, 1), (restored.Half, restored.Inning));
        var batter = restored.Plays.Last().Moves.Single(m => m.From == 0);
        Assert.Equal((OutClassification.NonForce, 2, 2), (batter.Classification, batter.OutAt, batter.Sequence));
    }

    // ---------------------------------------------------------------- what the 100 games really exercise

    [Fact]
    public void The_hundred_games_exercise_voided_runs_counted_runs_before_a_third_out_and_walk_off_left_on_base()
    {
        int voidedByForce = 0, voidedByBatter = 0, voidedAfterNonForce = 0, countedBeforeNonForce = 0, walkOffWithLob = 0, orderedPlays = 0;
        int postReachOuts = 0, postReachThirdOutRunCounted = 0, postReachThirdOutRunVoided = 0;
        foreach (var game in Hundred())
        {
            var state = Replay(game, game.Events);
            foreach (var play in state.Plays)
            {
                var postReach = play.Moves.FirstOrDefault(m => m.From == 0 && m.To == 0 && m.Classification == OutClassification.NonForce);
                if (postReach is not null)
                {
                    postReachOuts++;
                    if (play.OutsAfter == state.Rules.OutsPerHalfInning)
                    {
                        postReachThirdOutRunCounted += play.Moves.Count(m => m.To == 4 && !m.RunVoided);
                        postReachThirdOutRunVoided += play.Moves.Count(m => m.To == 4 && m.RunVoided);
                    }
                }

                if (play.OutsAfter != state.Rules.OutsPerHalfInning)
                    continue;
                var thirdOut = play.Moves.Where(m => m.Classification is not null && m.To == 0).OrderBy(m => m.Sequence).LastOrDefault();
                if (thirdOut is null)
                    continue;
                var crossed = play.Moves.Where(m => m.To == 4).ToList();
                if (crossed.Count > 0)
                    orderedPlays++;
                foreach (var m in crossed.Where(m => m.RunVoided))
                    switch (thirdOut.Classification)
                    {
                        case OutClassification.Force: voidedByForce++; break;
                        case OutClassification.BatterRunner: voidedByBatter++; break;
                        default: voidedAfterNonForce++; break;
                    }

                countedBeforeNonForce += crossed.Count(m => !m.RunVoided && thirdOut.Classification == OutClassification.NonForce);
            }

            var home = state.Stats.Where(e => e.Key == StatKey.TeamLOB && e.Team == TeamSide.Home).Sum(e => e.Amount);
            if (game.Expected!.Outcome == "home_win" && state.Plays.Last().OutsAfter < 3 && home > 0)
                walkOffWithLob++;
        }

        Assert.True(voidedByForce > 0, "포스아웃 제3아웃으로 무효가 된 득점이 100경기에 없음");
        Assert.True(voidedByBatter > 0, "타자주자 제3아웃으로 무효가 된 득점이 100경기에 없음");
        Assert.True(voidedAfterNonForce > 0, "비포스 제3아웃 뒤의 무효 득점이 100경기에 없음");
        Assert.True(countedBeforeNonForce > 0, "비포스 제3아웃 앞에 인정된 득점이 100경기에 없음");
        Assert.True(orderedPlays >= 10, "순서가 지정된 제3아웃+득점 플레이가 너무 적음");
        Assert.True(walkOffWithLob > 0, "잔루가 남은 끝내기 경기가 100경기에 없음");
        Assert.True(postReachOuts >= 10, "안타 뒤 주루사(1루 도달 후 아웃)가 100경기에 너무 적음");
        Assert.True(postReachThirdOutRunCounted > 0 && postReachThirdOutRunVoided > 0, "안타 뒤 주루사가 제3아웃이면서 득점이 인정/무효가 된 경우가 모두 있어야 함");
    }
}
