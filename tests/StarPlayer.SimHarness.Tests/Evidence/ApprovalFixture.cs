using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>A TEST_ONLY approval record satisfying the §13.2 catalogue (fixture values only; shared with the verifier tests).</summary>
public static class ApprovalFixture
{
    public const string Git = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static string Hex(string s) => Hashing.Sha256(s);

    private static JObject Unit(string id, string cohort, string rules, long planned, string family) =>
        J.O(("unit_id", J.S(id)), ("cohort_id", J.S(cohort)), ("rules_profile_id", J.S(rules)), ("planned_games", J.N(planned)),
            ("seed_manifest_path", J.S($"seeds/{family}/{id.ToLowerInvariant()}.json")), ("seed_manifest_sha256", J.S(Hex($"{family}/{id}"))));

    /// <summary>A TEST_ONLY approval record that satisfies the §13.2 catalogue (fixture values only).</summary>
    public static JObject Approval(int perCell = 2)
    {
        var cohorts = new[] { CalibrationCohorts.NeutralMixedId, NeutralCohort.OppositeId, NeutralCohort.Id, CalibrationCohorts.VirtualLeagueId }.Order(StringComparer.Ordinal);
        return J.O(
            ("format", J.S(ApprovalFormats.ApprovalFormat)), ("approval_record_id", J.S("apr-test-1")), ("validation_set_id", J.S("vs-test-1")), ("freeze_id", J.S("freeze-test-1")),
            ("test_only", J.B(true)), ("contracts", OfficialDocuments.SchemaHeader(EvidenceKind.Family)["contracts"]), ("git_commit_sha", J.S(Git)),
            ("build_provenance", J.O(("id", J.S("bp-test-1")), ("raw_sha256", J.S(Hex("bp"))))), ("environment_profile", J.O(("id", J.S("env-test-1")), ("raw_sha256", J.S(Hex("env"))))),
            ("pzl_test_profile", J.O(("id", J.S("pzl-profile-test-1")), ("raw_sha256", J.S(Hex("tp"))))),
            ("execution_policy", J.O(("id", J.S(OfficialProfile.Id)), ("hash", J.S(OfficialProfile.Hash())))),
            ("evaluation_policies", J.O(("calibration", J.O(("id", J.S(EvaluationPolicy.FcEvaluationV1.Id)), ("hash", J.S(EvaluationPolicy.FcEvaluationV1.Hash())))),
                ("dominance", J.O(("id", J.S(DominanceEvaluator.PolicyId)))))),
            ("dominance_games_per_cell", J.N(perCell)), ("integration_cohort_ids", J.A(J.S(NeutralCohort.Id))),
            ("cohort_inputs_hashes", J.A(cohorts.Select(c => (JNode)J.O(("cohort_id", J.S(c)), ("inputs_hash", J.S(CohortRegistry.Resolve(c).Value.InputsHash)))))),
            ("combination_policy_clauses", J.O(("calibration", J.S("clause-cal-test")), ("integration", J.S("clause-int-test")))),
            ("certification_allowances", J.O(("insufficient_sample", J.B(false)))),
            ("families", J.A(
                J.O(("family", J.S("fc3-cal")), ("run_id", J.S("run-cal-test")), ("units", J.A(CalibrationStrata.All.Select(s => (JNode)Unit(s.Id, s.CohortId, s.RulesProfileId, 2000, "fc3-cal"))))),
                J.O(("family", J.S("fc3-int")), ("run_id", J.S("run-int-test")), ("units", J.A(Unit("automated", NeutralCohort.Id, HarnessData.AutomatedProfile, 100, "fc3-int"),
                    Unit("non-automated", NeutralCohort.Id, HarnessData.NonAutomatedProfile, 100, "fc3-int")))),
                J.O(("family", J.S("fc3-dom")), ("run_id", J.S("run-dom-test")), ("units", J.A(DominanceMatrix.Cells.Select(c => (JNode)Unit(c.Id, c.CohortId, c.RulesProfileId, perCell, "fc3-dom"))))))),
            ("verifiers", J.A(J.O(("id", J.S("evidence-verifier")), ("version", J.S("1.0.0-test")), ("source_git_sha", J.S(Git)), ("binary_sha256", J.S(Hex("verifier")))))));
    }
}
