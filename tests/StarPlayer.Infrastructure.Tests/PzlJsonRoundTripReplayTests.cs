using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.SimHarness;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Pitch Zone Lite #6 "legacy and new JSON formats both replay" — the F-C2 check of proposal §10: the harness may not reference Infrastructure, so this test plays a pre-declared sample of games through
/// the production engine (the harness executor), writes each game's confirmed events in the format-2 (<c>ef2</c>) JSON, reads them back and replays them; every receipt and the final state must be
/// the same as the game the engine played. The official sample (for example the first 10 <c>fc3-int</c> games of each mode) cannot be declared before the Final Execution Freeze fixes those seeds, so the
/// sample here is the non-official dry stand-in: the first 10 games of the <c>fc2-dry-cal-va</c> / <c>fc2-dry-cal-vn</c> pilot strata (virtual-league-v1, each called-pitch mode, seeds 900101-900110 and
/// 900151-900160). The legacy half of the clause stays covered by <see cref="Ef1CorpusPinTests"/> and <see cref="ReplayContractTests"/>.
/// </summary>
public class PzlJsonRoundTripReplayTests
{
    public static TheoryData<string, string, ulong> Sample => new()
    {
        { HarnessData.AutomatedProfile, "fc2-dry-cal-va", 900_101UL },
        { HarnessData.NonAutomatedProfile, "fc2-dry-cal-vn", 900_151UL },
    };

    private static T Ok<T>(Result<T> r)
    {
        Assert.True(r.IsSuccess, $"{r.Error?.Code}: {r.Error?.Message}");
        return r.Value;
    }

    [Theory]
    [MemberData(nameof(Sample))]
    public void Ten_engine_games_of_each_mode_round_trip_through_the_version_2_json_and_replay_to_the_same_receipts_and_final_state(string rulesProfile, string key, ulong firstSeed)
    {
        var config = Ok(RunConfig.Create("pzl6", "dry", CalibrationCohorts.VirtualLeague(), Ok(HarnessData.LoadRules(rulesProfile)), Ok(HarnessData.LoadTuningV4()), Ok(HarnessData.NeutralPark()),
            "advancement-policy-v2", 3000, competitionKey: key));
        long located = 0;
        for (var index = 1; index <= 10; index++)
        {
            var spec = config.Spec(index, firstSeed + (ulong)(index - 1));
            var events = new List<GameEvent>();
            var played = GameExecutor.Play(config, spec, default, commit => events.Add(commit.Event));
            Assert.True(played.Completed, $"{spec.GameId}: {played.Failure?.Code}");
            Assert.Equal(played.CommittedEvents, events.Count);

            // format 2 round trip: every event, location and receipt survives, and writing again gives the same bytes
            var json = ScriptedGameJson.WriteEventsV2(events);
            var read = Ok(ScriptedGameJson.ReadEventsV2(json));
            Assert.Equal(events.Count, read.Count);
            for (var i = 0; i < events.Count; i++)
            {
                Assert.Equal(GameEventLocation.Of(events[i]), GameEventLocation.Of(read[i]));
                Assert.Equal(EventFingerprint.Receipt(events[i]), EventFingerprint.Receipt(read[i]));
            }

            Assert.Equal(json, ScriptedGameJson.WriteEventsV2(read));
            located += read.Count(e => GameEventLocation.Of(e) is not null);

            // replay from the same start: the original list and the list read back give the same state, and that state is the game the engine played
            var (away, home) = config.Cohort.SetupFor(spec.GameIndex);
            GameState Replay(IEnumerable<GameEvent> list)
            {
                var state = Ok(GameReducer.Start(new GameStart(spec.GameId, config.Rules, away, home, config.Execution)));
                foreach (var e in list)
                    state = Ok(GameReducer.Apply(state, e)).State;
                return state.Status == GameStatus.Final ? Ok(GameReducer.Finalize(state)).State : state;   // a game-ending event is finalized once, as the commit gate does
            }

            var original = Replay(events);
            var again = Replay(read);
            Assert.Equal(original.AppliedEvents, again.AppliedEvents);
            Assert.Equal(original.Stats, again.Stats);
            Assert.Equal((original.Status, original.Final?.Outcome, original.AwayRuns, original.HomeRuns, original.Inning, original.Half),
                (again.Status, again.Final?.Outcome, again.AwayRuns, again.HomeRuns, again.Inning, again.Half));
            Assert.Equal(original.AwayLine, again.AwayLine);
            Assert.Equal(original.HomeLine, again.HomeLine);

            Assert.Equal(GameStatus.Final, again.Status);
            Assert.Equal((original.Final!.Outcome, original.Final.AwayRuns, original.Final.HomeRuns, original.Final.Execution), (again.Final!.Outcome, again.Final.AwayRuns, again.Final.HomeRuns, again.Final.Execution));
            Assert.Equal(config.Execution, again.Final.Execution);
            Assert.Equal(played.EventHash, Hash(again.AppliedEvents));                     // the harness' event hash (EventId|Kind|version|fingerprint of every receipt)
            Assert.Equal((played.Outcome, played.AwayRuns, played.HomeRuns, played.FinalInning, played.FinalHalf), (again.Final?.Outcome, again.AwayRuns, again.HomeRuns, again.Inning, again.Half));
            Assert.Equal(played.CommittedEvents, again.AppliedEvents.Count);
            Assert.Equal(again.AppliedEvents.Count(r => r.FingerprintVersion == EventFingerprint.VersionV2), read.Count(e => GameEventLocation.Of(e) is not null));

            // the receipts survive their own JSON too
            Assert.Equal(again.AppliedEvents, Ok(EventReceiptJson.Read(EventReceiptJson.Write(again.AppliedEvents))));
        }

        Assert.True(located > 0);
    }

    [Fact]
    public void A_located_engine_game_cannot_be_written_in_the_legacy_format_and_its_version_2_json_is_never_read_as_legacy()
    {
        var config = Ok(RunConfig.Create("pzl6", "dry", CalibrationCohorts.VirtualLeague(), Ok(HarnessData.LoadRules(HarnessData.AutomatedProfile)), Ok(HarnessData.LoadTuningV4()),
            Ok(HarnessData.NeutralPark()), "advancement-policy-v2", 3000, competitionKey: "fc2-dry-cal-va"));
        var events = new List<GameEvent>();
        Assert.True(GameExecutor.Play(config, config.Spec(1, 900_101UL), default, commit => events.Add(commit.Event)).Completed);
        Assert.Equal("Fixture.location_requires_v2", Assert.Throws<GameJsonException>(() => ScriptedGameJson.WriteEvents(events)).Code);   // no silent location drop
        Assert.False(ScriptedGameJson.ReadEvents(ScriptedGameJson.WriteEventsV2(events)).IsSuccess);   // no promotion between formats
    }

    private static string Hash(IEnumerable<EventReceipt> receipts) =>
        Hashing.Sha256(string.Join('\n', receipts.Select(r => $"{r.EventId}|{r.Kind}|{r.FingerprintVersion}|{r.Fingerprint}")));
}
