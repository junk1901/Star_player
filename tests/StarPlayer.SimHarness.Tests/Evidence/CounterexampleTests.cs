using System.Text;
using System.Text.RegularExpressions;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.SimHarness.Evidence;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>
/// B1-T10 direct counterexamples that were only covered by a shared path before: N01 (game-result-v1 recomputed from the document, restoring exactly three nulls), N02 (a completed game's regulation
/// error), N33 / N34 (the RNG null boundary of rows B / A), N05-c / N05-e (NSC-6 pitch coverage) and N35 (case C's first rng_before), on real-engine games.
/// </summary>
public sealed class CounterexampleTests(FamilyOfficialWorld fam, DominanceOfficialWorld dom) : IClassFixture<FamilyOfficialWorld>, IClassFixture<DominanceOfficialWorld>
{
    private static RunConfig Config(int budget = 3000) =>
        H.Ok(RunConfig.Create("b1-cx", "official", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Ok(HarnessData.NeutralPark()), "advancement-policy-v2", budget, competitionKey: "fc3-int-fx-a"));

    private static string? Row(JObject game, bool dominance)
    {
        try
        {
            EvidenceRules.GameRow(game, "/g", dominance);
            return null;
        }
        catch (EvidenceException e)
        {
            return e.Error.Cause;
        }
    }

    /// <summary>An independent reconstruction of the existing <c>game-result-v1</c> text from the 20 document fields: only outcome, rng_hash and error_code turn null back into "-" (P-05).</summary>
    private static string ResultHash(JObject g)
    {
        static string R(JNode n) => n is JString s ? s.Value : "-";
        var counters = Schema.Obj(g["counters"]);
        var canonical = string.Concat(typeof(MetricCounters).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal)
            .Select(n => $"{n}={Schema.Long(counters[Regex.Replace(n, "(?<!^)([A-Z])", "_$1").ToLowerInvariant()])};"));
        var text = new StringBuilder("game-result-v1\n")
            .Append(Schema.Text(g["game_id"])).Append('|').Append(Schema.Text(g["seed"])).Append('|').Append(Schema.Text(g["status"])).Append('|').Append(R(g["outcome"])).Append('|')
            .Append(Schema.Long(g["away_runs"])).Append('-').Append(Schema.Long(g["home_runs"])).Append('|').Append(Schema.Long(g["final_inning"])).Append(Schema.Text(g["final_half"])).Append('|')
            .Append(Schema.Text(g["end_kind"])).Append('|').Append(Schema.Long(g["committed_events"])).Append('|').Append(Schema.Text(g["event_hash"])).Append('|').Append(R(g["rng_hash"])).Append('|')
            .Append(R(g["error_code"])).Append('|').Append(canonical);
        return Hashing.Sha256(text.ToString());
    }

    [Fact]
    public void N01_the_existing_result_hash_is_recomputed_from_the_document_with_only_three_nulls_restored()
    {
        var config = Config();
        var (run, _) = UnitPzlEvidenceTests.Unit(config, 4, UnitPzlEvidenceTests.WithFailures(config));   // rows A (completed), D (never started), C (budget stop)
        var rows = run.Games.Select(g => OfficialDocuments.Game(g, dominance: false)).ToList();
        Assert.IsType<JNull>(rows[1]["rng_hash"]);                                                        // row D: "-" is written as null
        Assert.IsType<JNull>(rows[1]["outcome"]);
        foreach (var (g, row) in run.Games.Zip(rows))
            Assert.Equal(g.ResultHash, ResultHash(row));
        foreach (var g in fam.IntResult.Units.SelectMany(u => u.Run.Games))
            Assert.Equal(g.ResultHash, ResultHash(OfficialDocuments.Game(g, dominance: false)));
        // the reconstruction is sensitive to a restored field
        Assert.NotEqual(run.Games[1].ResultHash, ResultHash((JObject)JEdit.Set(rows[1], "/rng_hash", J.S(new string('0', 64)))));
    }

    [Fact]
    public void N02_a_completed_dominance_game_may_carry_a_regulation_error_and_exactly_one_regulation_field_is_non_null()
    {
        var game = (JObject)JEdit.Get(OfficialDocuments.Dominance(dom.Result, OfficialDocumentTests.Ctx, []), "/units/0/technical/games/0");
        Assert.Null(Row(game, dominance: true));
        var error = (JObject)JEdit.Set(JEdit.Set(game, "/regulation_half_innings", JNode.Null), "/regulation_error_code", J.S("Dominance.regulation_incomplete"));
        Assert.Equal("Completed", Schema.Text(error["status"]));
        Assert.Null(Row(error, dominance: true));                                                         // completed + regulation error: allowed (§3.6 A)
        Assert.NotNull(Row((JObject)JEdit.Set(error, "/regulation_error_code", JNode.Null), dominance: true));   // both null
        Assert.NotNull(Row((JObject)JEdit.Set(game, "/regulation_error_code", J.S("Dominance.regulation_incomplete")), dominance: true));   // both non-null
    }

    [Fact]
    public void N33_N34_the_rng_hash_is_null_exactly_for_a_failure_before_start()
    {
        var config = Config();
        var (run, _) = UnitPzlEvidenceTests.Unit(config, 2, s => s.GameIndex == 2 ? config with { Execution = config.Execution with { TuningHash = new string('0', 64) } } : null);
        var a = OfficialDocuments.Game(run.Games[1], dominance: false);
        Assert.Equal("config", Schema.Text(JEdit.Get(a, "/failure/stage")));
        Assert.Null(Row(a, dominance: false));
        var h = J.S(new string('7', 64));
        // N34: case A with an RNG hash
        Assert.Equal("Schema.state_table", Row((JObject)JEdit.Set(JEdit.Set(a, "/rng_hash", h), "/failure/rng_hash", h), dominance: false));
        // N33: case B (started, failed before its first commit: stage advance / exception) needs its RNG hash; with it the row is legal
        var b = (JObject)JEdit.Set(a, "/failure/stage", J.S("advance"));
        Assert.Equal("Schema.state_table", Row(b, dominance: false));
        Assert.Null(Row((JObject)JEdit.Set(JEdit.Set(b, "/rng_hash", h), "/failure/rng_hash", h), dominance: false));
        Assert.Equal("Schema.state_table", Row((JObject)JEdit.Set(b, "/failure/stage", J.S("exception")), dominance: false));
    }

    [Fact]
    public void N05c_N05e_pitch_coverage_counts_the_pitch_events_of_every_executed_game_and_differs_from_the_technical_counter_legally()
    {
        var config = Config();
        Func<GameSpec, RunConfig?> failing = s => s.GameIndex == 2 ? config with { EventBudget = 40 } : null;   // game 2 stops after 40 commits, pitches among them
        var (run, games) = UnitPzlEvidenceTests.Unit(config, 3, failing);
        Assert.Equal(GameRunStatus.Failed, run.Games[1].Status);
        var failedPitches = games[1].Observer!.Events.Count(e => e.Pitch is not null);
        Assert.True(failedPitches > 0);
        var doc = UnitPzlEvidence.Document(OfficialDocumentTests.Ctx, "fc3-int", "automated", new string('f', 40), run, games, []);
        Assert.Null(EvidenceRules.Check(EvidenceKind.UnitPzl, Jcs.Bytes(doc)));
        var pitches = Schema.Long(JEdit.Get(doc, "/checks/0/coverage/pitches"));
        var events = Schema.Long(JEdit.Get(doc, "/checks/0/coverage/events"));
        // N05-e: the technical counter counts the completed games' pitches; the coverage also the failed game's — they differ and that alone is no refusal
        Assert.NotEqual(run.Aggregate.Pitches, pitches);
        Assert.Equal(games.Sum(g => g.Observer!.Events.Count(e => e.Pitch is not null)), pitches);
        var bundle = UnitPzlEvidence.Bundle(OfficialDocumentTests.Ctx, "fc3-int", run.Config.RunId, "automated", games);
        var (rerun, again) = UnitPzlEvidenceTests.Unit(config, 3, failing);
        var parsed = UnitPzlEvidence.ReadBundle(bundle);
        var docGames = ((JArray)doc["games"]).Items.Cast<JObject>().ToList();
        for (var i = 0; i < again.Count; i++)
            Assert.Null(UnitPzlEvidence.CompareGame(parsed.Games[i], docGames[i], again[i]));
        // N05-c: counting the non-pitch events too, or leaving out the failed game's pitch events. The document alone cannot tell (it holds no per-event pitch count), so the schema accepts it and
        // the verifier's re-projection from the re-observed games finds it (Verifier: Evidence.unit_pzl_differs → pzl_mismatch)
        var reprojected = UnitPzlEvidence.Document(OfficialDocumentTests.Ctx, "fc3-int", "automated", new string('f', 40), rerun, again, []);
        foreach (var claimed in new[] { events, pitches - failedPitches })
        {
            var tampered = (JObject)JEdit.Set(doc, "/checks/0/coverage/pitches", J.N(claimed));
            Assert.Null(EvidenceRules.Check(EvidenceKind.UnitPzl, Jcs.Bytes(tampered)));
            Assert.Equal("/checks/0/coverage/pitches", JDiff.First(EvidenceHash.M(tampered), EvidenceHash.M(reprojected)));
        }

        Assert.Null(JDiff.First(EvidenceHash.M(doc), EvidenceHash.M(reprojected)));
    }

    [Fact]
    public void N35_case_c_whose_first_rng_before_is_not_the_initial_snapshot_is_a_pzl_mismatch()
    {
        var config = Config();
        var (run, games) = UnitPzlEvidenceTests.Unit(config, 1);
        var doc = UnitPzlEvidence.Document(OfficialDocumentTests.Ctx, "fc3-int", "automated", new string('f', 40), run, games, []);
        var bundle = UnitPzlEvidence.Bundle(OfficialDocumentTests.Ctx, "fc3-int", run.Config.RunId, "automated", games);
        var lines = Encoding.UTF8.GetString(bundle).TrimEnd('\n').Split('\n').ToList();
        var first = (JObject)StrictJson.Parse(Encoding.UTF8.GetBytes(lines[2]));                          // header, game, first event
        Assert.Equal(games[0].Observer!.StartRng, Schema.Text(first["rng_before"]));                      // the real first rng_before is the initial snapshot
        lines[2] = Jcs.Text(JEdit.Set(first, "/rng_before", J.S(games[0].Observer!.FinalRng!)));
        var forged = UnitPzlEvidence.ReadBundle(Encoding.UTF8.GetBytes(string.Concat(lines.Select(l => l + "\n"))));
        var (_, again) = UnitPzlEvidenceTests.Unit(config, 1);
        Assert.Equal(EvidenceCodes.PzlMismatch, UnitPzlEvidence.CompareGame(forged.Games[0], ((JArray)doc["games"]).Items.Cast<JObject>().First(), again[0])!.Code);
    }

    /// <summary>A test policy that fails at the first decision: the game has begun (its RNG exists) but nothing is committed — §10.4 case B, stage <c>exception</c>, made by the real executor.</summary>
    private sealed class FailsBeforeFirstCommit(IGamePolicy inner) : IGamePolicy
    {
        public string Id => inner.Id;

        public PaDecision AtPlateAppearance(GameState state) => throw new InvalidOperationException("B-1 N32 fixture: fails before the first commit");

        public PitchIntent IntentFor(GameState state) => inner.IntentFor(state);
    }

    [Fact]
    public void N32_case_b_keeps_the_initial_rng_snapshot_and_a_final_rng_that_is_not_it_is_a_pzl_mismatch()
    {
        var config = Config();
        Func<GameSpec, RunConfig?> b = s => s.GameIndex == 2 ? config with { Policy = new FailsBeforeFirstCommit(config.Policy) } : null;
        var (run, games) = UnitPzlEvidenceTests.Unit(config, 2, b);
        var g = run.Games[1];
        Assert.Equal((GameRunStatus.Failed, "exception", 0L), (g.Status, g.Failure!.Stage, (long)g.CommittedEvents));
        Assert.NotEqual("-", g.RngHash);

        // the family row is the state table's case B (no outcome, end_kind None, RNG hash present and equal to failure.rng_hash)
        Assert.Null(Row(OfficialDocuments.Game(g, dominance: false), dominance: false));
        var doc = UnitPzlEvidence.Document(OfficialDocumentTests.Ctx, "fc3-int", "automated", new string('f', 40), run, games, []);
        Assert.Null(EvidenceRules.Check(EvidenceKind.UnitPzl, Jcs.Bytes(doc)));
        var row = (JObject)((JArray)doc["games"]).Items[1];
        var final = Schema.Text(row["final_rng_canonical"]);
        // §10.4 B: the verifier's own reconstruction through the production start path only — GameStarter.Begin(...).Current.Rng.Canonical()
        var (away, home) = config.Cohort.SetupFor(g.Game.GameIndex);
        var initial = GameStarter.Begin(new GameStart(g.Game.GameId, config.Rules, away, home, config.Execution), g.Game.Seed).Value.Current.Rng.Canonical();
        Assert.Equal(initial, final);
        Assert.Equal(Schema.Text(row["rng_hash"]), Hashing.Sha256(final));
        Assert.Single(final.Split('\n', StringSplitOptions.RemoveEmptyEntries));                         // RNGSET1 header only, no stream row
        Assert.Equal("sha256:ed666f17bb1a25a68a4457864105ec21fb879a61b7c3919422e1918b14e2ed6e", Schema.Text(row["events_digest"]));   // the empty-array digest

        var bundle = UnitPzlEvidence.ReadBundle(UnitPzlEvidence.Bundle(OfficialDocumentTests.Ctx, "fc3-int", run.Config.RunId, "automated", games));
        var (_, again) = UnitPzlEvidenceTests.Unit(config, 2, b);
        Assert.Null(UnitPzlEvidence.CompareGame(bundle.Games[1], row, again[1]));
        // N32: a final RNG that is not the initial reconstruction (here game 1's final state, its hash made consistent) is a pzl_mismatch
        var other = Schema.Text(((JObject)((JArray)doc["games"]).Items[0])["final_rng_canonical"]);
        var forged = (JObject)JEdit.Set(JEdit.Set(row, "/final_rng_canonical", J.S(other)), "/rng_hash", J.S(Hashing.Sha256(other)));
        Assert.Equal(EvidenceCodes.PzlMismatch, UnitPzlEvidence.CompareGame(bundle.Games[1], forged, again[1])!.Code);
    }
}
