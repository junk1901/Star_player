using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// Pitch Zone Lite #5, #10 and #12a (ADR-008 FC-D8, proposal §10 / §10.1) on the real execution path: every check reads the confirmed commits of real engine games, and each check's failure path is shown
/// by a hand-made violation so a broken check cannot pass silently. #6 (JSON round trip) is in Infrastructure.Tests, since the harness may not reference Infrastructure.
/// </summary>
public class PzlCheckTests
{
    private static RunResult Run(bool automated, int games = 3, CohortInputs? cohort = null, string key = "fc2-dry-pzl") =>
        SimRunner.Run(H.Ok(RunConfig.Create("pzl", "dry", cohort ?? CalibrationCohorts.VirtualLeague(), H.Rules(automated), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: key)),
            Enumerable.Range(0, games).Select(i => 900_101UL + (ulong)i).ToList());

    private static PzlCheck Check(RunResult run, string id) => PzlChecks.Evaluate(run).Single(c => c.Id == id);

    private static RunResult WithGame(RunResult run, int index, Func<PzlGameObservation, PzlGameObservation> change) =>
        run with { Games = run.Games.Select((g, i) => i == index ? g with { Pzl = change(g.Pzl!) } : g).ToList() };

    private static readonly Lazy<RunResult> Automated = new(() => Run(true));
    private static readonly Lazy<RunResult> NonAutomated = new(() => Run(false, 6));

    /// <summary>A test-only cohort (never an official one: every official cohort's catchers have Framing 50, ADR-008 FC-D1/FC-D2) whose catchers have an extreme Framing of 95.</summary>
    private static CohortInputs ExtremeFraming() => H.Variant("test-framing-95", s => s.Ratings.Catcher is { } c
        ? H.WithRatings(s, s.Ratings.Hitter, s.Ratings.Pitcher, new StarPlayer.Domain.Players.CatcherRatings(H.Skill(95), c.Blocking, c.PopTime, c.GameCalling))
        : null);

    private static readonly Lazy<RunResult> AutomatedExtreme = new(() => Run(true, 6, ExtremeFraming(), "fc2-dry-pzl-x"));
    private static readonly Lazy<RunResult> NonAutomatedExtreme = new(() => Run(false, 6, ExtremeFraming(), "fc2-dry-pzl-x"));

    // ------------------------------------------------------------------ #5

    [Fact]
    public void Pzl5_every_committed_event_of_real_games_is_identified_by_its_location_and_no_event_id_collides()
    {
        foreach (var run in new[] { Automated.Value, NonAutomated.Value })
        {
            Assert.True(run.AllCompleted);
            var c = Check(run, "pzl-5");
            Assert.Equal(PzlChecks.Pass, c.Status);
            Assert.Equal(run.Games.Sum(g => (long)g.CommittedEvents), c.Counts["events"]);
            Assert.All(run.Games, g => Assert.Equal(g.CommittedEvents, g.Pzl!.Events));
            Assert.Equal(run.Aggregate.LocatedPitches, c.Counts["located_events"]);           // one located event per actual pitch (an in-play pitch is one event)
            Assert.Equal(c.Counts["located_events"], c.Counts["ef2_receipts"]);
            Assert.True(c.Counts["ef1_receipts"] > 0, "approach events carry no location and stay ef1");
            foreach (var zero in new[] { "version_mismatches", "fingerprint_mismatches", "foreign_event_ids", "duplicate_event_ids", "event_id_collisions", "receipt_log_mismatches", "duplicate_game_ids" })
                Assert.Equal(0, c.Counts[zero]);
        }
    }

    [Fact]
    public void Pzl5_the_recorder_counts_a_wrong_version_a_forged_receipt_a_foreign_or_misnumbered_id_a_repeat_a_collision_and_a_different_log()
    {
        const string game = "game:fc2-dry-pzl:t-000001";
        string Id(int n, string g = game) => $"{g}:e{n:D6}";
        var located = new PitchSpec(PitchOutcome.Ball, Location: PitchLocation.FromZone(ZoneModelVersion.Pzl1, PitchZoneId.Create("in.r1c1").Value, AppliedBatterSide.Right).Value);
        GameEvent Pitch(string id) => new PitchEvent(id, located);
        GameEvent Approach(string id) => new ApproachEvent(id, ApproachKind.Normal);

        PzlGameObservation Observe(Action<PzlRecorder> feed, IReadOnlyList<EventReceipt>? applied = null)
        {
            var r = new PzlRecorder(game);
            feed(r);
            return r.Finish(applied, null);
        }

        // the honest sequence: both versions, all zero
        var e1 = Approach(Id(1));
        var e2 = Pitch(Id(2));
        var honest = Observe(r => { r.Observe(e1, EventFingerprint.Receipt(e1), 1, null, null); r.Observe(e2, EventFingerprint.Receipt(e2), 2, null, null); },
            [EventFingerprint.Receipt(e1), EventFingerprint.Receipt(e2)]);
        Assert.Equal((2L, 1L, 1L), (honest.Events, honest.LocatedEvents, honest.Ef2Receipts));
        Assert.Equal(0, honest.VersionMismatches + honest.FingerprintMismatches + honest.ForeignEventIds + honest.DuplicateEventIds + honest.EventIdCollisions + honest.ReceiptLogMismatches);

        var real = EventFingerprint.Receipt(e2);
        var asEf1 = Observe(r => r.Observe(e2, real with { FingerprintVersion = "ef1" }, 2, null, null), [real with { FingerprintVersion = "ef1" }]);
        Assert.Equal((1L, 1L), (asEf1.VersionMismatches, asEf1.FingerprintMismatches));
        var located1 = EventFingerprint.Receipt(e1);
        Assert.Equal(1, Observe(r => r.Observe(e1, located1 with { FingerprintVersion = "ef2" }, 1, null, null), [located1 with { FingerprintVersion = "ef2" }]).VersionMismatches);
        Assert.Equal(1, Observe(r => r.Observe(e2, real with { Fingerprint = new string('0', 64) }, 2, null, null), [real with { Fingerprint = new string('0', 64) }]).FingerprintMismatches);

        var foreign = Pitch(Id(2, "game:fc2-dry-pzl:t-000002"));
        Assert.Equal(1, Observe(r => r.Observe(foreign, EventFingerprint.Receipt(foreign), 2, null, null), [EventFingerprint.Receipt(foreign)]).ForeignEventIds);
        Assert.Equal(1, Observe(r => r.Observe(e2, real, 3, null, null), [real]).ForeignEventIds);                       // id says sequence 2, the commit says 3

        var twice = Observe(r => { r.Observe(e2, real, 2, null, null); r.Observe(e2, real, 2, null, null); }, [real, real]);
        Assert.Equal((1L, 0L), (twice.DuplicateEventIds, twice.EventIdCollisions));
        var other = new PitchEvent(Id(2), located with { Outcome = PitchOutcome.CalledStrike });
        var clash = Observe(r => { r.Observe(e2, real, 2, null, null); r.Observe(other, EventFingerprint.Receipt(other), 2, null, null); }, [real, EventFingerprint.Receipt(other)]);
        Assert.Equal(1, clash.EventIdCollisions);

        Assert.Equal(1, Observe(r => r.Observe(e2, real, 2, null, null), []).ReceiptLogMismatches);                    // the final log lost the commit
        Assert.Equal(1, Observe(r => r.Observe(e2, real, 2, null, null), [located1]).ReceiptLogMismatches);            // the final log holds another receipt
    }

    [Fact]
    public void Pzl5_any_violation_or_a_repeated_game_id_fails_the_run_check_and_no_events_is_not_evaluated()
    {
        var run = Automated.Value;
        foreach (var change in new Func<PzlGameObservation, PzlGameObservation>[]
                 {
                     o => o with { VersionMismatches = 1 }, o => o with { FingerprintMismatches = 1 }, o => o with { ForeignEventIds = 1 }, o => o with { DuplicateEventIds = 1 },
                     o => o with { EventIdCollisions = 1 }, o => o with { ReceiptLogMismatches = 1 },
                 })
            Assert.Equal(PzlChecks.Fail, Check(WithGame(run, 1, change), "pzl-5").Status);

        var repeated = run with { Games = [.. run.Games, run.Games[0]] };
        var c = Check(repeated, "pzl-5");
        Assert.Equal((PzlChecks.Fail, 1L), (c.Status, c.Counts["duplicate_game_ids"]));
        Assert.Equal(PzlChecks.NotEvaluated, Check(run with { Games = run.Games.Select(g => g with { Pzl = PzlGameObservation.None }).ToList() }, "pzl-5").Status);
    }

    // ------------------------------------------------------------------ #10

    [Fact]
    public void Pzl10_an_automated_run_has_no_non_neutral_framing_factor_while_the_non_automated_control_shows_the_counter_working()
    {
        foreach (var run in new[] { Automated.Value, AutomatedExtreme.Value })
        {
            var a = Check(run, "pzl-10");
            Assert.Equal(PzlChecks.Pass, a.Status);
            Assert.True(a.Counts["called_pitch_steps"] > 0);
            Assert.Equal((0L, 0L, 0L), (a.Counts["framing_non_neutral"], a.Counts["framing_entries"], a.Counts["framing_inputs"]));   // ABS: Framing is not even an input, even at 95
        }

        // official cohorts have Framing 50, so umpire calls read a neutral factor: entries but no effect
        var neutral = Check(NonAutomated.Value, "pzl-10");
        Assert.Equal(PzlChecks.NotApplicable, neutral.Status);
        Assert.True(neutral.Counts["framing_inputs"] > 0 && neutral.Counts["framing_entries"] > 0);
        Assert.Equal(0, neutral.Counts["framing_non_neutral"]);

        // negative control: the same test cohort with umpire calls reads Framing 95 and changes Borderline calls, so the counter is looking at the right place
        var n = Check(NonAutomatedExtreme.Value, "pzl-10");
        Assert.Equal(PzlChecks.NotApplicable, n.Status);
        Assert.True(n.Counts["framing_non_neutral"] > 0, "the extreme-Framing non-automated control must show non-neutral Framing factors");
    }

    [Fact]
    public void Pzl10_a_non_neutral_factor_fails_an_automated_run_and_no_called_pitch_is_not_evaluated()
    {
        var run = Automated.Value;
        var c = Check(WithGame(run, 0, o => o with { FramingNonNeutral = 1, FramingEntries = 1 }), "pzl-10");
        Assert.Equal(PzlChecks.Fail, c.Status);
        Assert.Equal(PzlChecks.NotEvaluated, Check(run with { Games = run.Games.Select(g => g with { Pzl = g.Pzl! with { CalledPitchSteps = 0 } }).ToList() }, "pzl-10").Status);
        // an injected count on a non-automated run is still not a #10 verdict (the clause judges the ABS mode only)
        Assert.Equal(PzlChecks.NotApplicable, Check(WithGame(NonAutomated.Value, 0, o => o with { FramingNonNeutral = 99 }), "pzl-10").Status);
    }

    // ------------------------------------------------------------------ #12a / #12b

    [Fact]
    public void Pzl12a_the_registered_streams_are_exactly_the_pinned_game_set_and_real_runs_draw_only_from_them()
    {
        // the Simulation registry, read by reflection so a stream added there fails here (the harness sources themselves never name the RNG types)
        var registered = typeof(GameRngStreams).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(f => f.FieldType == typeof(RngStreamId))
            .Select(f => ((RngStreamId)f.GetValue(null)!).Canonical).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(PzlChecks.PinnedStreams, registered);
        Assert.All(registered, s => Assert.StartsWith("game/", s));
        Assert.DoesNotContain(registered, s => s.Contains("presentation") || s.Contains("bcl") || s.Contains("lmv"));
        foreach (var run in new[] { Automated.Value, NonAutomated.Value })
        {
            var c = Check(run, "pzl-12a");
            Assert.Equal(PzlChecks.Pass, c.Status);
            Assert.Equal(0, c.Counts["drawn_unregistered"]);
            var drawn = run.Games.SelectMany(g => g.Pzl!.DrawnStreams.Split(',')).ToHashSet();
            Assert.True(new[] { "game/target@1", "game/location@1", "game/pitch@1" }.All(drawn.Contains), string.Join(",", drawn));   // target, location, then the outcome (ROADMAP 1.14)
        }
    }

    [Fact]
    public void Pzl12a_a_presentation_stream_in_the_registry_or_in_a_game_fails_and_12b_is_always_deferred_never_pass()
    {
        var games = Automated.Value.Games.Select(g => g.Pzl!).ToList();
        Assert.Equal(PzlChecks.Fail, PzlChecks.Clause12a(games, [.. PzlChecks.PinnedStreams, "presentation/bcl@1"]).Status);
        Assert.Equal(PzlChecks.Fail, PzlChecks.Clause12a(games, PzlChecks.PinnedStreams.Where(s => s != "game/pitch@1").ToList()).Status);
        Assert.Equal(PzlChecks.Fail, PzlChecks.Clause12a([.. games, games[0] with { DrawnStreams = "game/pitch@1,presentation/lmv@1" }], PzlChecks.PinnedStreams).Status);
        Assert.Equal(PzlChecks.Fail, PzlChecks.Clause12a([games[0] with { DrawnStreams = "game/pitch@2" }], PzlChecks.PinnedStreams).Status);
        Assert.Equal(PzlChecks.NotEvaluated, PzlChecks.Clause12a([PzlGameObservation.None], PzlChecks.PinnedStreams).Status);

        foreach (var run in new[] { Automated.Value, NonAutomated.Value })
            Assert.Equal(PzlChecks.Deferred, Check(run, "pzl-12b").Status);
    }

    [Fact]
    public void Pzl12a_the_harness_and_its_result_hashes_depend_on_no_presentation_or_infrastructure_assembly()
    {
        var references = typeof(GameExecutor).Assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("StarPlayer", StringComparison.Ordinal)).Order(StringComparer.Ordinal);
        Assert.Equal(new[] { "StarPlayer.Domain", "StarPlayer.Simulation" }, references);
        // the observation is derived data beside the hashes: replaying the same games gives the same hashes and the same observation
        var run = Automated.Value;
        var again = Run(true);
        Assert.Equal(run.Games.Select(g => (g.ResultHash, g.EventHash, g.RngHash)), again.Games.Select(g => (g.ResultHash, g.EventHash, g.RngHash)));
        Assert.Equal(run.RunResultHash, again.RunResultHash);
        Assert.Equal(run.Games.Select(g => g.Pzl), again.Games.Select(g => g.Pzl));                                    // and the observation is itself deterministic
    }

    // ------------------------------------------------------------------ reporting

    [Fact]
    public void The_summary_carries_every_check_and_the_text_names_them_without_changing_the_run_result()
    {
        var run = Automated.Value;
        var summary = JsonDocument.Parse(JsonSerializer.Serialize(RunReport.Summary(run), RunReport.Json)).RootElement;
        var checks = summary.GetProperty("pzl_checks").GetProperty("checks").EnumerateArray().ToList();
        Assert.Equal(new[] { "pzl-5", "pzl-10", "pzl-12a", "pzl-12b" }, checks.Select(c => c.GetProperty("id").GetString()));
        Assert.Equal(new[] { "PASS", "PASS", "PASS", "DEFERRED" }, checks.Select(c => c.GetProperty("status").GetString()));
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("regulation_runs_diagnostic").ValueKind);                 // σ_g is reported for stop-aware (dry) runs only
        Assert.Contains("pzl-5 PASS  pzl-10 PASS  pzl-12a PASS  pzl-12b DEFERRED", RunReport.Text(run));
    }

    [Fact]
    public void A_dry_run_summary_reports_sigma_g_of_the_innings_1_to_8_game_observation_as_a_diagnostic_only()
    {
        var config = H.Ok(RunConfig.Create("pzl-dry", "dry", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Park(), "advancement-policy-v2", 3000, DominancePolicy.Of(BatterStrategy.Normal, PitcherStrategy.Balanced),
            "fc2-dry-dom-sa-nb"));
        var plan = new StopPlan("fc2-dry", 4, SeedManifestReader.DryMaxPa, Enumerable.Range(1, 8).Select(i => config.Spec(i, 910_000UL + (ulong)i)).ToList(), "seeds/x.json", new string('a', 64));
        var run = SimRunner.RunStopAware(config, plan);
        Assert.Equal(StopReason.SeedsExhausted, run.Stop!.Reason);
        var expected = CellSample.Of(run.Games.Select(g => g.Regulation!.Total));
        var d = JsonDocument.Parse(JsonSerializer.Serialize(RunReport.Summary(run), RunReport.Json)).RootElement.GetProperty("regulation_runs_diagnostic");
        Assert.Equal(8, d.GetProperty("games").GetInt32());
        Assert.Equal(0, d.GetProperty("regulation_errors").GetInt32());
        Assert.Equal(expected.TotalRuns, d.GetProperty("total_runs_innings_1_8").GetInt64());
        Assert.Equal(expected.SumOfSquares, d.GetProperty("sum_of_squares").GetInt64());
        Assert.Equal(Math.Sqrt(expected.Variance!.Value), d.GetProperty("sigma_g_game_observation_sd").GetDouble(), 12);
        Assert.Equal(expected.Mean, d.GetProperty("mean_runs_per_half_inning").GetDouble(), 12);
        Assert.False(d.TryGetProperty("n_games", out _));                                                             // no sample size is derived from it (Final Execution Freeze candidate)
        Assert.Equal("NOT_EVALUATED", JsonDocument.Parse(JsonSerializer.Serialize(RunReport.Summary(run), RunReport.Json)).RootElement.GetProperty("calibration_evaluation").GetProperty("gate_status").GetString());
    }
}
