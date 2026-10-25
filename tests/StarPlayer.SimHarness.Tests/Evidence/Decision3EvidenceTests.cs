using System.Text;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>
/// B1 OWNER decision 3 at the harness level: §5 the GAME_RESULTS bundle format and its content comparison, §2 the closed RAW_INVENTORY reader, §4 the read-only final-state capture of the T04a seam.
/// </summary>
public sealed class Decision3EvidenceTests
{
    private static readonly EvidenceContext Ctx = OfficialDocumentTests.Ctx;

    private static RunConfig Config(int budget = 3000) =>
        H.Ok(RunConfig.Create("b1-d3", "official", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Ok(HarnessData.NeutralPark()), "advancement-policy-v2", budget, competitionKey: "fc3-int-fx-a"));

    private static string Cause(Action a) => Assert.Throws<EvidenceException>(a).Error.Cause;

    private static List<string> Lines(byte[] b) => [.. Encoding.UTF8.GetString(b).TrimEnd('\n').Split('\n')];

    private static byte[] Join(IEnumerable<string> lines) => Encoding.UTF8.GetBytes(string.Concat(lines.Select(l => l + "\n")));

    /// <summary>A unit of four games (completed, never started, budget stop, completed) with its Unit PZL document and the parent rows the Family document would carry.</summary>
    private static (RunResult Run, JObject UnitDoc, JObject Technical, byte[] Bundle) Unit()
    {
        var config = Config();
        var (run, games) = UnitPzlEvidenceTests.Unit(config, 4, UnitPzlEvidenceTests.WithFailures(config));
        var unitDoc = UnitPzlEvidence.Document(Ctx, "fc3-int", "automated", new string('f', 40), run, games, []);
        var technical = J.O(("games", J.A(run.Games.Select(g => (JNode)OfficialDocuments.Game(g, dominance: false)))), ("games_executed", J.N(run.Games.Count)),
            ("games_completed", J.N(run.CompletedCount)), ("games_failed", J.N(run.FailedCount)));
        return (run, unitDoc, technical, GameResultsEvidence.Bundle(Ctx, "fc3-int", "automated", run));
    }

    // ---------------------------------------------------------------- §5 GAME_RESULTS

    [Fact]
    public void Decision3_5_the_bundle_holds_every_executed_game_s_game_result_as_the_parent_row_and_compares_equal()
    {
        var (run, unitDoc, technical, bytes) = Unit();
        var parsed = GameResultsEvidence.Read(bytes);
        Assert.Equal(4, parsed.Games.Count);                                                               // failed games included, in parent order
        Assert.Equal(run.Games.Select(g => g.Game.GameId), parsed.Games.Select(g => Schema.Text(g["game_id"])));
        Assert.Equal(("Failed", "Failed"), (Schema.Text(parsed.Games[1]["status"]), Schema.Text(parsed.Games[2]["status"])));
        Assert.Equal((2L, 4L, 2L), (Schema.Long(parsed.End["completed_games"]), Schema.Long(parsed.End["executed_games"]), Schema.Long(parsed.End["failed_games"])));
        var rows = ((JArray)technical["games"]).Items.Cast<JObject>().ToList();
        Assert.All(parsed.Games.Zip(rows), p => Assert.Equal(Jcs.Bytes(p.Second), Jcs.Bytes(p.First)));   // the `game` JCS = the parent row's JCS
        Assert.Null(GameResultsEvidence.Compare(parsed, unitDoc, technical, rows));
        Assert.Equal(bytes, GameResultsEvidence.Bundle(Ctx, "fc3-int", "automated", run));                // deterministic bytes
        Assert.Equal("simharness-game-results-v1", Schema.Text(parsed.Header["bundle"]));
    }

    [Fact]
    public void Decision3_5_the_format_refuses_framing_shape_order_and_count_violations()
    {
        var (_, _, _, bytes) = Unit();
        var lines = Lines(bytes);
        string Read(byte[] b) => Cause(() => GameResultsEvidence.Read(b));
        Assert.Equal("GameResults.framing", Read(bytes[..^1]));                                            // no final LF
        Assert.Equal("GameResults.framing", Read(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\n", "\r\n"))));   // CR
        Assert.Equal("GameResults.framing", Read([.. bytes, (byte)'\n']));                                 // an empty line
        Assert.Equal("GameResults.record_invalid", Read([0xEF, 0xBB, 0xBF, .. bytes]));                    // BOM
        Assert.Equal("GameResults.order", Read(Join([lines[0], lines[2], lines[1], .. lines.Skip(3)])));  // two games swapped
        Assert.Equal("GameResults.order", Read(Join(lines.Where((_, i) => i != 2))));                      // a game left out
        Assert.Equal("GameResults.count", Read(Join([.. lines.Take(lines.Count - 1), lines[^1].Replace("\"executed_games\":4", "\"executed_games\":5")])));
        Assert.Equal("GameResults.record_invalid", Read(Join([lines[0].Replace("{\"bundle\"", "{\"extra\":1,\"bundle\""), .. lines.Skip(1)])));   // a closed header
        Assert.Equal("GameResults.record_invalid", Read(Join([lines[0], lines[1].Replace("\"record\":\"game\"", "\"record\":\"game\",\"zz\":1"), .. lines.Skip(2)])));
        Assert.Equal("GameResults.not_canonical", Read(Join([lines[0].Replace("{\"bundle\"", "{ \"bundle\""), .. lines.Skip(1)])));
        var big = new string('x', UnitPzlEvidence.MaxRecordBytes + 1);
        Assert.Equal("GameResults.record_too_large", Read(Join([lines[0], big, .. lines.Skip(1)])));
    }

    [Fact]
    public void Decision3_5_a_changed_row_result_hash_header_or_end_count_is_refused_by_the_content_comparison()
    {
        var (_, unitDoc, technical, bytes) = Unit();
        var rows = ((JArray)technical["games"]).Items.Cast<JObject>().ToList();
        var parsed = GameResultsEvidence.Read(bytes);
        EvidenceError? Compare(GameResultsEvidence.Parsed p) => GameResultsEvidence.Compare(p, unitDoc, technical, rows);
        var changed = (JObject)JEdit.Set(parsed.Games[0], "/home_runs", J.N(99));
        Assert.Equal(("Evidence.replay_mismatch", "GameResults.game_differs"), (Compare(parsed with { Games = [changed, .. parsed.Games.Skip(1)] })!.Code,
            Compare(parsed with { Games = [changed, .. parsed.Games.Skip(1)] })!.Cause));
        var hash = (JObject)JEdit.Set(parsed.Games[0], "/result_hash", J.S(new string('0', 64)));
        Assert.Equal("GameResults.game_differs", Compare(parsed with { Games = [hash, .. parsed.Games.Skip(1)] })!.Cause);
        Assert.Equal(("Evidence.reference_invalid", "GameResults.header"), (Compare(parsed with { Header = (JObject)JEdit.Set(parsed.Header, "/unit_id", J.S("non-automated")) })!.Code,
            Compare(parsed with { Header = (JObject)JEdit.Set(parsed.Header, "/unit_evidence_id", J.S("vs-b1-test.fc3-int.pzl.other")) })!.Cause));
        Assert.Equal("GameResults.end", Compare(parsed with { End = (JObject)JEdit.Set(parsed.End, "/failed_games", J.N(1)) })!.Cause);
        Assert.Equal("GameResults.count", Compare(parsed with { Games = parsed.Games.Take(3).ToList() })!.Cause);
    }

    // ---------------------------------------------------------------- §2 RAW_INVENTORY

    [Fact]
    public void Decision3_2_the_closed_raw_inventory_reader_accepts_only_the_exact_format()
    {
        var a = Archive.Reference("a", "DIAGNOSTICS", null, null, "a.json", [1]);
        var b = Archive.Reference("b", "SEED_MANIFEST", "u", null, "b.json", [2]);
        var inv = Archive.Inventory([b, a]);
        Assert.Equal(["a", "b"], Archive.ReadInventory(inv).Select(x => Schema.Text(x["artifact_id"])));
        string Read(JObject o) => Cause(() => Archive.ReadInventory(Jcs.Bytes(o)));
        var doc = (JObject)StrictJson.Parse(inv);
        Assert.Equal("Inventory.schema", Read((JObject)JEdit.Add(doc, "", "extra", J.N(1))));                                     // exactly two members
        Assert.Equal("Inventory.schema", Read((JObject)JEdit.Set(doc, "/format", J.S("simharness-raw-inventory-v2"))));          // the format id
        Assert.Equal("Inventory.schema", Read((JObject)JEdit.Remove(doc, "/format")));
        Assert.Equal("Inventory.order", Read((JObject)JEdit.Set(doc, "/artifacts", J.A(b, a))));                                  // unsorted
        Assert.Equal("Inventory.order", Read((JObject)JEdit.Set(doc, "/artifacts", J.A(a, a))));                                  // duplicate
        Assert.Equal("Archive.self_reference", Read((JObject)JEdit.Set(doc, "/artifacts", J.A(a, Archive.Reference("z", "CANONICAL_PAYLOAD", null, null, "z.json", [3])))));
        Assert.Equal("Inventory.schema", Read((JObject)JEdit.Set(doc, "/artifacts/0", JEdit.Add(a, "", "origin", J.S("ORIGINAL")))));   // ArtifactRef 7 fields only
        Assert.Equal("Inventory.not_canonical", Cause(() => Archive.ReadInventory(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(inv).Replace("{\"artifacts\"", "{ \"artifacts\"")))));
    }

    // ---------------------------------------------------------------- §4 the read-only final-state capture

    [Fact]
    public void Decision3_4_the_observer_records_the_production_final_state_read_only_and_never_for_a_game_that_did_not_start()
    {
        var config = Config();
        var spec = config.Spec(1, 700_001UL);
        var plain = GameExecutor.Play(config, spec);
        var observer = new PzlEventObserver();
        var observed = GameExecutor.Play(config, spec, default, null, observer);
        Assert.Equal((plain.ResultHash, plain.EventHash, plain.RngHash), (observed.ResultHash, observed.EventHash, observed.RngHash));   // invisible to every hash
        Assert.NotNull(observer.FinalState);
        Assert.Equal(observed.CommittedEvents, observer.FinalState!.AppliedEvents.Count);
        Assert.Equal((observed.AwayRuns, observed.HomeRuns), (observer.FinalState.AwayRuns, observer.FinalState.HomeRuns));
        Assert.NotNull(observer.FinalState.Final);                                                         // the official record of a completed game
        var stopped = new PzlEventObserver();
        GameExecutor.Play(config with { EventBudget = 1 }, spec, default, null, stopped);
        Assert.Single(stopped.FinalState!.AppliedEvents);                                                  // a stopped game: the state it ended in
        var never = new PzlEventObserver();
        GameExecutor.Play(config with { Execution = config.Execution with { TuningHash = new string('0', 64) } }, spec, default, null, never);
        Assert.Null(never.FinalState);
    }
}
