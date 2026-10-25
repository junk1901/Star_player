using System.Text;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>
/// B1-T04b: Unit PZL documents and GAME_EVENTS bundles of real-engine units (every executed game, failed ones with their commits), the existing PZL verdicts, the NSC-6 coverage, and the re-observation
/// comparison catching a replaced game (A07), a removed PreparedPitch (A08), forged draws (A09), a missing observation (N05), a changed digest (N14) and reordered rows (N17).
/// </summary>
public class UnitPzlEvidenceTests
{
    private static readonly EvidenceContext Ctx = OfficialDocumentTests.Ctx;
    private const string Git = "fa11fa11fa11fa11fa11fa11fa11fa11fa11fa11";

    private static RunConfig Config(bool automated, int budget = 3000) =>
        H.Ok(RunConfig.Create("b1-unit", "official", NeutralCohort.Create(), H.Rules(automated), H.Tuning, H.Ok(HarnessData.NeutralPark()), "advancement-policy-v2", budget,
            competitionKey: automated ? "fc3-int-fx-a" : "fc3-int-fx-n"));

    /// <summary>Plays the specs with one observer per game. <paramref name="variant"/> may run a game on another configuration (to make it fail) without changing the unit's config hash.</summary>
    internal static (RunResult Run, List<ObservedGame> Games) Unit(RunConfig config, int games, Func<GameSpec, RunConfig?>? variant = null)
    {
        var observers = new Dictionary<int, PzlEventObserver>();
        var specs = Enumerable.Range(1, games).Select(i => config.Spec(i, 700_000UL + (ulong)i)).ToList();
        var run = SimRunner.RunSpecs(config, specs, play: s =>
        {
            var o = observers[s.GameIndex] = new PzlEventObserver();
            return GameExecutor.Play(variant?.Invoke(s) ?? config, s, default, null, o);
        });
        return (run, run.Games.Select(g => new ObservedGame(g, observers[g.Game.GameIndex])).ToList());
    }

    /// <summary>Game 2 never starts (case A: its pins are broken) and game 3 stops after one commit (case C: budget 1).</summary>
    internal static Func<GameSpec, RunConfig?> WithFailures(RunConfig config) => s => s.GameIndex switch
    {
        2 => config with { Execution = config.Execution with { TuningHash = new string('0', 64) } },
        3 => config with { EventBudget = 1 },
        _ => null,
    };

    private static JObject Doc(RunResult run, List<ObservedGame> games, string unit = "automated") => UnitPzlEvidence.Document(Ctx, "fc3-int", unit, Git, run, games, []);

    private static EvidenceError? Validate(JNode doc) => EvidenceRules.Check(EvidenceKind.UnitPzl, Jcs.Bytes(doc));

    private static List<byte[]> Lines(byte[] bundle) => Encoding.UTF8.GetString(bundle).TrimEnd('\n').Split('\n').Select(Encoding.UTF8.GetBytes).ToList();

    private static byte[] Join(IEnumerable<byte[]> lines) => [.. lines.SelectMany(l => l.Append((byte)'\n'))];

    private static byte[] EditLine(byte[] bundle, int line, Func<JNode, JNode> edit)
    {
        var lines = Lines(bundle);
        lines[line] = Jcs.Bytes(edit(StrictJson.Parse(lines[line])));
        return Join(lines);
    }

    /// <summary>The first re-observation difference of a bundle against freshly re-executed games (null = every game matches).</summary>
    private static EvidenceError? Reobserve(byte[] bundle, JObject doc, List<ObservedGame> again)
    {
        var parsed = UnitPzlEvidence.ReadBundle(bundle);
        var docGames = ((JArray)doc["games"]).Items.Cast<JObject>().ToList();
        if (parsed.Games.Count < again.Count)
            return new EvidenceError(EvidenceCodes.PzlIncomplete, "Evidence.bundle_games_missing", "", "pzl_reobservation", "missing game");
        for (var i = 0; i < again.Count; i++)
            if (UnitPzlEvidence.CompareGame(parsed.Games[i], docGames[i], again[i]) is { } e)
                return e;
        return null;
    }

    [Theory]
    [InlineData(true, "automated")]
    [InlineData(false, "non-automated")]
    public void A_real_unit_document_passes_the_schema_and_keeps_the_existing_pzl_verdicts(bool automated, string unit)
    {
        var (run, games) = Unit(Config(automated), 4);
        var doc = Doc(run, games, unit);
        Assert.Null(Validate(doc));
        var existing = PzlChecks.Evaluate(run);
        var checks = ((JArray)doc["checks"]).Items.Cast<JObject>().ToList();
        Assert.Equal(existing.Select(c => (c.Id, c.Status, c.Reason)), checks.Select(c => (Schema.Text(c["id"]), Schema.Text(c["status"]), Schema.Text(c["reason"]))));
        Assert.Equal(automated ? "PASS" : "NOT_APPLICABLE", Schema.Text(checks[1]["status"]));
        Assert.Equal("DEFERRED", Schema.Text(checks[3]["status"]));
        Assert.Equal(PzlChecks.PinnedStreams, ((JArray)doc["registered_streams"]).Items.Select(Schema.Text));
    }

    [Fact]
    public void The_bundle_holds_every_executed_game_and_a_fresh_re_execution_matches_it_byte_for_byte()
    {
        var config = Config(true);
        var (run, games) = Unit(config, 4, WithFailures(config));
        var bundle = UnitPzlEvidence.Bundle(Ctx, "fc3-int", run.Config.RunId, "automated", games);
        var parsed = UnitPzlEvidence.ReadBundle(bundle);
        Assert.Equal(4, parsed.Games.Count);                                              // the failed games are in the bundle
        Assert.Equal(games.Select(g => (long)g.Result.CommittedEvents), parsed.Games.Select(g => (long)g.EventLines.Count));
        Assert.Empty(parsed.Games[1].EventLines);                                  // case A
        Assert.Single(parsed.Games[2].EventLines);                                  // case C: the commit published before the budget stop
        Assert.Equal("sha256:ed666f17bb1a25a68a4457864105ec21fb879a61b7c3919422e1918b14e2ed6e", Schema.Text(parsed.Games[1].Game["events_digest"]));   // GV-EV0

        var (_, again) = Unit(config, 4, WithFailures(config));
        var doc = Doc(run, games);
        Assert.Null(Validate(doc));
        Assert.Null(Reobserve(bundle, doc, again));
        Assert.Equal(bundle, UnitPzlEvidence.Bundle(Ctx, "fc3-int", run.Config.RunId, "automated", again));   // deterministic bytes

        var row = (JObject)((JArray)doc["games"]).Items[1];
        Assert.IsType<JNull>(row["rng_hash"]);                                              // §10.4 A: both null together
        Assert.IsType<JNull>(row["final_rng_canonical"]);
        var c = (JObject)((JArray)doc["games"]).Items[2];
        Assert.Equal(Schema.Text(c["rng_hash"]), Hashing.Sha256(Schema.Text(c["final_rng_canonical"])));
    }

    [Fact]
    public void Coverage_counts_the_unit_s_executed_games_including_failed_commits_and_twelve_b_counts_nothing()
    {
        var config = Config(true);
        var (run, games) = Unit(config, 4, WithFailures(config));
        var doc = Doc(run, games);
        Assert.Null(Validate(doc));
        var cov = (JObject)((JObject)((JArray)doc["checks"]).Items[0])["coverage"];
        var events = games.Sum(g => g.Observer!.Events.Count);
        var pitches = games.Sum(g => g.Observer!.Events.Count(e => e.Pitch is not null));
        Assert.Equal((2L, (long)events, (long)pitches), (Schema.Long(cov["completed_games"]), Schema.Long(cov["events"]), Schema.Long(cov["pitches"])));
        Assert.True(games[2].Observer!.Events.Count > 0 && games[2].Result.Status == GameRunStatus.Failed);   // the failed game's commit counts
        Assert.Equal(0, ((JObject)((JArray)doc["checks"]).Items[3])["coverage"] is JObject z ? EvidenceShapes.CoverageFields.Sum(f => Schema.Long(z[f])) : -1);

        // N05-a / N05-b / A25-a: a coverage that is not the unit's executed-game scope
        Assert.Equal(EvidenceCodes.PzlMismatch, Validate(JEdit.Set(doc, "/checks/0/coverage/events", J.N(events - 1)))!.Code);
        Assert.Equal(EvidenceCodes.PzlMismatch, Validate(JEdit.Set(doc, "/checks/1/coverage/completed_games", J.N(3)))!.Code);
        Assert.Equal(EvidenceCodes.PzlMismatch, Validate(JEdit.Set(doc, "/checks/3/coverage/events", J.N(1)))!.Code);
        Assert.Equal(EvidenceCodes.PzlMismatch, Validate(JEdit.Set(doc, "/checks/2/coverage/called_pitch_steps", J.N(0)))!.Code);   // N05-d
    }

    [Fact]
    public void A_unit_whose_games_never_committed_has_zero_events_and_pzl5_not_evaluated_and_equal_empty_digests_pass()
    {
        var config = Config(true);
        var broken = config with { Execution = config.Execution with { TuningHash = new string('0', 64) } };
        var (run, games) = Unit(config, 2, _ => broken);
        var doc = Doc(run, games);
        Assert.Null(Validate(doc));                                                         // N36: two equal empty digests, told apart by index / id / seed / hashes
        var five = (JObject)((JArray)doc["checks"]).Items[0];
        Assert.Equal(("NOT_EVALUATED", 0L), (Schema.Text(five["status"]), Schema.Long(((JObject)five["coverage"])["events"])));   // N36-a
        var claimed = JEdit.Set(doc, "/checks/0/status", J.S("PASS"));
        Assert.Null(Validate(claimed));                                                     // the schema allows PASS here ...
        Assert.Equal("/checks/0/status", JDiff.First(claimed, Doc(run, games)));            // ... the re-projection does not (pzl_mismatch in T06b)
    }

    [Fact]
    public void A_missing_observation_or_prepared_pitch_is_incomplete_and_a_replaced_or_forged_game_differs()
    {
        var config = Config(true);
        var (run, games) = Unit(config, 3);
        var doc = Doc(run, games);
        var bundle = UnitPzlEvidence.Bundle(Ctx, "fc3-int", run.Config.RunId, "automated", games);
        var (_, again) = Unit(config, 3);

        // N05: a game without an observer is never written as an empty one
        Assert.Equal(EvidenceCodes.PzlIncomplete, Assert.Throws<EvidenceException>(() => Doc(run, [games[0], games[1] with { Observer = null }, games[2]])).Error.Code);

        var first = Lines(bundle);
        var game1 = 1;
        var events1 = (int)Schema.Long(((JObject)StrictJson.Parse(first[game1]))["committed_events"]);
        var pitchLine = Enumerable.Range(game1 + 1, events1).First(i => ((JObject)StrictJson.Parse(first[i]))["pitch"] is JObject);

        // A08: a pitch row without its PreparedPitch
        Assert.Equal(EvidenceCodes.PzlIncomplete, Reobserve(EditLine(bundle, pitchLine, r => JEdit.Set(r, "/pitch", JNode.Null)), doc, again)!.Code);
        // A09: H1, a roll and the raw draws forged together
        var forged = EditLine(bundle, pitchLine, r => JEdit.Set(JEdit.Set(JEdit.Set(r, "/pitch/steps/0/roll", J.N(1)), "/pitch/steps/0/raw_draws", J.S("2")), "/pitch/h1_rng_canonical",
            JEdit.Get(r, "/rng_before")));
        Assert.Equal(EvidenceCodes.PzlMismatch, Reobserve(forged, doc, again)!.Code);
        // N17: two event rows swapped
        var swapped = Lines(bundle);
        (swapped[game1 + 1], swapped[game1 + 2]) = (swapped[game1 + 2], swapped[game1 + 1]);
        Assert.Equal(("Evidence.pzl_mismatch", "Evidence.event_sequence"), (Reobserve(Join(swapped), doc, again)!.Code, Reobserve(Join(swapped), doc, again)!.Cause));
        // N14: the document's digest changed
        Assert.Equal("Evidence.events_digest_mismatch", Reobserve(bundle, (JObject)JEdit.Set(doc, "/games/0/events_digest", J.S("sha256:" + new string('0', 64))), again)!.Cause);
        // A07: game 2's rows and digest replaced by another game's (same shape, other identity)
        var other = Unit(config with { CompetitionKey = "fc3-int-fx-other" }, 3).Games;
        var replaced = UnitPzlEvidence.Bundle(Ctx, "fc3-int", run.Config.RunId, "automated", [games[0], other[1], games[2]]);
        Assert.Equal(EvidenceCodes.PzlMismatch, Reobserve(replaced, doc, again)!.Code);
    }

    [Fact]
    public void The_bundle_format_refuses_cr_a_missing_final_lf_an_empty_line_and_a_non_canonical_record()
    {
        var (run, games) = Unit(Config(true), 1);
        var bundle = UnitPzlEvidence.Bundle(Ctx, "fc3-int", run.Config.RunId, "automated", games);
        EvidenceError Err(byte[] b) => Assert.Throws<EvidenceException>(() => UnitPzlEvidence.ReadBundle(b)).Error;
        Assert.Equal(EvidenceCodes.ArchiveInvalid, Err(bundle[..^1]).Code);
        Assert.Equal(EvidenceCodes.ArchiveInvalid, Err(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bundle).Replace("\n", "\r\n"))).Code);
        Assert.Equal(EvidenceCodes.ArchiveInvalid, Err([.. bundle, (byte)'\n']).Code);
        var lines = Lines(bundle);
        lines[0] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(lines[0]).Replace("{\"bundle\"", "{ \"bundle\""));
        Assert.Equal("Bundle.not_canonical", Err(Join(lines)).Cause);
        var last = Lines(bundle);
        last[^1] = Jcs.Bytes(JEdit.Set(StrictJson.Parse(last[^1]), "/total_events", J.N(0)));
        Assert.Equal("Bundle.count", Err(Join(last)).Cause);
    }
}
