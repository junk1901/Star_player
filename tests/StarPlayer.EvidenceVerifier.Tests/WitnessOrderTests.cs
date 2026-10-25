using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;
using StarPlayer.SimHarness.Tests;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// A 48-cell Dominance run on the real engine with 4 games per cell (a TEST_ONLY fixture: test pins and a test sample size, never an approved one), large enough that no two compared cells have zero variance,
/// so both sides are evaluated and carry Witnesses — what the 2-game TEST_ONLY world cannot show.
/// </summary>
public sealed class WitnessWorld : IDisposable
{
    public const int GamesPerCell = 4;

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "simharness-tests", "b1-witness-" + Guid.NewGuid().ToString("N"));

    public DominanceOfficialResult Result { get; }

    public WitnessWorld()
    {
        var pins = DominanceMatrix.Cells.ToDictionary(c => DominanceOfficialWorld.SeedPath(c), c => DominanceOfficialWorld.Put(Root, DominanceOfficialWorld.SeedPath(c), DominanceOfficialWorld.SeedFile(c, GamesPerCell)),
            StringComparer.Ordinal);
        var approvals = new OfficialApprovals(DominanceOfficialWorld.Sha, OfficialProfile.Id, OfficialProfile.Hash(), pins,
            new Dictionary<string, string> { ["neutral-v1"] = NeutralCohort.Create().InputsHash, ["neutral-opp-v1"] = NeutralCohort.CreateOpposite().InputsHash }, true, GamesPerCell);
        var preflight = DominancePreflight.Run(new DominanceOfficialRequest(Root, "dom-witness", null), approvals, DominanceOfficialWorld.Git());
        Result = DominanceOfficial.Execute(preflight, "dom-witness", approvals, Root);
    }

    public void Dispose() => Directory.Delete(Root, true);
}

/// <summary>N07 (P-10 / R-7, §4): Witnesses keep the existing generation order; a reordered list is refused by the re-evaluation comparison.</summary>
public sealed class WitnessOrderTests(WitnessWorld w) : IClassFixture<WitnessWorld>
{
    [Fact]
    public void N07_a_reordered_witness_list_differs_from_the_re_evaluation_and_is_an_evaluation_mismatch()
    {
        var ctx = new EvidenceContext("vs-witness", "apr-witness", new string('a', 64), "freeze-witness", "bp-witness", null);
        var doc = OfficialDocuments.Dominance(w.Result, ctx, []);
        Assert.Null(EvidenceRules.Check(EvidenceKind.Dominance, Jcs.Bytes(doc)));
        var (side, index, count) = new[] { "batter", "pitcher" }
            .SelectMany(s => ((JArray)JEditT.Get(doc, $"/aggregate_evaluation/{s}/strategies")).Items.Select((st, i) => (s, i, ((JArray)((JObject)st)["witnesses"]).Items.Count)))
            .First(x => x.Item3 >= 2);
        Assert.True(count >= 2, "the fixture produced no strategy with two witnesses");
        var path = $"/aggregate_evaluation/{side}/strategies/{index}/witnesses";
        var witnesses = ((JArray)JEditT.Get(doc, path)).Items;
        var reordered = (JObject)JEditT.Set(doc, path, new JArray([witnesses[1], witnesses[0], .. witnesses.Skip(2)]));
        // the generation order is the evaluator's (rival / column order); the same content in another order is not the re-evaluation's document
        Verifier.Compare(doc, OfficialDocuments.Dominance(w.Result, ctx, []), DominanceMatrix.Family);
        Assert.Null(EvidenceRules.Check(EvidenceKind.Dominance, Jcs.Bytes(reordered)));                // the schema has no order to check (same content, no duplicate)
        var e = Assert.Throws<EvidenceException>(() => Verifier.Compare(reordered, doc, DominanceMatrix.Family)).Error;
        Assert.Equal(EvidenceCodes.EvaluationMismatch, e.Code);
        Assert.StartsWith(path, e.Pointer, StringComparison.Ordinal);
    }
}
