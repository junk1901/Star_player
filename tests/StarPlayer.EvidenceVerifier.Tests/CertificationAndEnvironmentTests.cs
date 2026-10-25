using System.Collections;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// B1-T09 / §18 / §19 rules without a world: the certification table (A26, A27, NOT_EVALUATED / INCOMPLETE refused, INSUFFICIENT_SAMPLE only with the allowance), receipt target rules (N11), a registry that
/// fails (A14 → pending) or holds other bytes (A16 → conflict), the E2-1 forbidden variables (N16, N29, N29-a), the profile comparison (N30-a) and the bit Golden (N31).
/// </summary>
public sealed class CertificationAndEnvironmentTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "simharness-tests", "cert-" + Guid.NewGuid().ToString("N"));

    public CertificationAndEnvironmentTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, true);
    }

    private static JObject Doc(string runStatus, string gate, bool dominance = false)
    {
        var unit = J.O(("technical", J.O(("run_status", J.S(runStatus)))));
        var schema = J.O(("id", J.S(EvidenceShapes.SchemaId(dominance ? EvidenceKind.Dominance : EvidenceKind.Family))));
        var g = J.O(("status", J.S(gate)), ("reason", J.S("x")));
        return J.O(("schema", schema), ("units", J.A(unit)), ("aggregate_evaluation", dominance ? J.O(("gate", g)) : g));
    }

    private static string Cause(Action a) => Assert.Throws<EvidenceException>(a).Error.Cause;

    [Fact]
    public void Section19_a_verified_fail_is_issued_a_technical_failure_or_unevaluated_gate_is_not()
    {
        Assert.Equal(("COMPLETED", "FAIL"), Receipts.Eligibility(Doc("COMPLETED", "FAIL"), false));                       // A26: evidence of a failure, never a Gate PASS
        Assert.Equal(("COMPLETED", "PASS"), Receipts.Eligibility(Doc("COMPLETED", "PASS", dominance: true), false));
        Assert.Equal("Certification.technical", Cause(() => Receipts.Eligibility(Doc("METRICS_ERROR", "FAIL"), false)));   // A27: a metrics failure dressed as a statistical FAIL
        Assert.Equal("Certification.technical", Cause(() => Receipts.Eligibility(Doc("GAMES_FAILED", "FAIL"), false)));
        Assert.Equal("Certification.statistical", Cause(() => Receipts.Eligibility(Doc("COMPLETED", "NOT_EVALUATED"), true)));
        Assert.Equal("Certification.statistical", Cause(() => Receipts.Eligibility(Doc("COMPLETED", "INCOMPLETE", dominance: true), true)));
        Assert.Equal("Certification.statistical", Cause(() => Receipts.Eligibility(Doc("COMPLETED", "INSUFFICIENT_SAMPLE"), false)));
        Assert.Equal(("COMPLETED", "INSUFFICIENT_SAMPLE"), Receipts.Eligibility(Doc("COMPLETED", "INSUFFICIENT_SAMPLE"), true));
        Assert.Equal("Certification.statistical", Cause(() => Receipts.Eligibility(Doc("COMPLETED", "INSUFFICIENT_SAMPLE", dominance: true), true)));
    }

    private static readonly string Sha = new('a', 64), Git = new('b', 40);

    private static JObject Target(string kind, string? stat, bool runId = true) => J.O(("kind", J.S(kind)), ("run_id", runId ? J.S("run-1") : JNode.Null), ("evidence_id", J.S("vs.x")),
        ("evidence_hash", J.S("sha256:" + Sha)), ("technical_status", J.S("COMPLETED")), ("statistical_status", stat is null ? JNode.Null : J.S(stat)));

    internal static JObject Receipt(string id, params JObject[] targets) => J.O(
        ("format", J.S(Receipts.Format)), ("receipt_id", J.S(id)), ("validation_set_id", J.S("vs-test-1")),
        ("approvals", J.O(("approval_record_id", J.S("apr-test-1")), ("approval_record_sha256", J.S(Sha)), ("freeze_id", J.S("freeze-test-1")))),
        ("contracts", OfficialDocuments.SchemaHeader(EvidenceKind.Family)["contracts"]),
        ("verifier", J.O(("id", J.S("evidence-verifier")), ("version", J.S("1.0.0-test")), ("source_git_sha", J.S(Git)), ("binary_sha256", J.S(Sha)))),
        ("build_provenance", J.O(("id", J.S("bp-test-1")), ("raw_sha256", J.S(Sha)))), ("environment_profile", J.O(("id", J.S("env-test-1")), ("raw_sha256", J.S(Sha)))),
        ("pzl_test_profile", J.O(("id", J.S("tp-test-1")), ("raw_sha256", J.S(Sha)))), ("execution_authorization_id", J.S("auth-test-1")), ("test_only", J.B(true)),
        ("verification_decision", J.S("ELIGIBLE")), ("pzl_status", J.S("REQUIRED_CHECKS_SATISFIED")), ("targets", J.A(targets)), ("checks", J.A()), ("source_files", J.A()),
        ("child_receipt_ids", targets.Length == 4 ? J.A(Enumerable.Range(0, 3).Select(_ => (JNode)J.S(Guid.NewGuid().ToString("D")))) : J.A()));

    [Fact]
    public void N11_a_phase_target_has_no_statistical_state_and_family_targets_keep_their_allowed_set()
    {
        var family = new[] { Target("CALIBRATION", "PASS"), Target("INTEGRATION", "FAIL"), Target("DOMINANCE", "PASS"), Target("PHASE", null, runId: false) };
        Receipts.Validate(Jcs.Bytes(Receipt(Guid.NewGuid().ToString("D"), family)));
        Assert.Equal("Receipt.statistical_status", Cause(() => Receipts.Validate(Jcs.Bytes(Receipt(Guid.NewGuid().ToString("D"), family[0], family[1], family[2], Target("PHASE", "PASS", runId: false))))));
        Assert.Equal("Receipt.statistical_status", Cause(() => Receipts.Validate(Jcs.Bytes(Receipt(Guid.NewGuid().ToString("D"), Target("DOMINANCE", "INSUFFICIENT_SAMPLE"))))));
        Assert.Equal("Receipt.targets", Cause(() => Receipts.Validate(Jcs.Bytes(Receipt(Guid.NewGuid().ToString("D"), family[0], family[1])))));
        Assert.Equal("Schema.format", Cause(() => Receipts.Validate(Jcs.Bytes(Receipt("NOT-A-UUID", family[0])))));
    }

    private sealed class Unreachable : IEvidenceRegistry
    {
        public bool Trusted => true;

        public string InstanceId => "registry-unreachable";

        public RegistryEntry? Get(string kind, string id) => throw new IOException("pipe broken");

        public IReadOnlyList<RegistryEntry> GetByScope(string kind, string scope) => throw new IOException("pipe broken");

        public (RegisterOutcome Outcome, long Sequence) Register(string kind, string id, byte[] raw) => throw new IOException("pipe broken");
    }

    [Fact]
    public void A14_A16_a_failed_registration_stays_pending_and_other_bytes_under_one_id_conflict_in_the_registry()
    {
        var receipt = Receipt(Guid.NewGuid().ToString("D"), Target("DOMINANCE", "FAIL"));
        Assert.Equal(EvidenceCodes.RegistrationPending, Assert.Throws<EvidenceException>(() => Receipts.Issue(receipt, _dir, new Unreachable(), () => null)).Error.Code);
        using var registry = new SqliteRegistry(Path.Combine(_dir, "r.db"), RegistryRole.VerifierService, "S-1-5-21-1-2-3-1003");
        var id = Guid.NewGuid().ToString("D");
        registry.Register("VERIFICATION_RECEIPT", id, Jcs.Bytes(Receipt(id, Target("DOMINANCE", "FAIL"))));
        Assert.Equal(EvidenceCodes.RegistrationConflict, Assert.Throws<EvidenceException>(() => registry.Register("VERIFICATION_RECEIPT", id, Jcs.Bytes(Receipt(id, Target("DOMINANCE", "PASS"))))).Error.Code);
    }

    [Fact]
    public void E2_1_forbids_dotnet_and_complus_variables_case_insensitively_and_the_digest_is_order_independent()
    {
        Assert.Equal(["DOTNET_TieredPGO", "complus_gcServer"], ExecutionEnvironment.ForbiddenVariables(new Hashtable { ["DOTNET_TieredPGO"] = "0", ["complus_gcServer"] = "1", ["PATH"] = "x" }));   // N16 / N29
        Assert.Empty(ExecutionEnvironment.ForbiddenVariables(new Hashtable { ["PATH"] = "x", ["MY_DOTNET_X"] = "1" }));   // N29-a: only the prefixes, no profile needed
        Assert.Equal(ExecutionEnvironment.EnvironmentDigest(new Hashtable { ["b"] = "2", ["A"] = "1" }), ExecutionEnvironment.EnvironmentDigest(new Hashtable { ["A"] = "1", ["B"] = "2" }));
        Assert.NotEqual(ExecutionEnvironment.EnvironmentDigest(new Hashtable { ["A"] = "1" }), ExecutionEnvironment.EnvironmentDigest(new Hashtable { ["A"] = "2" }));
    }

    private static JObject Profile(EnvironmentSnapshot c, IReadOnlyList<string> bits, string witness) => J.O(
        ("os", J.O(("version", J.S(c.OsVersion)), ("architecture", J.S(c.OsArchitecture)))), ("process_architecture", J.S(c.ProcessArchitecture)),
        ("runtime", J.O(("framework_description", J.S(c.FrameworkDescription)), ("netcore_app_version", J.S(c.NetCoreAppVersion)), ("runtime_identifier", J.S(c.RuntimeIdentifier)))),
        ("native_math", J.O(("file_version", J.S(c.UcrtFileVersion)), ("raw_sha256", J.S(c.UcrtSha256)))),
        ("runtime_files", J.A(c.RuntimeFiles.Select(f => (JNode)J.O(("name", J.S(f.Key)), ("raw_sha256", J.S(f.Value)))))),
        ("math_golden", J.O(("upper_quantile", J.A(bits.Select((b, i) => (JNode)J.O(("divisor", J.N(i + 1)), ("bits", J.S(b)))))), ("witness_digest", J.S(witness)))));

    [Fact]
    public void N30a_N31_the_snapshot_is_compared_field_by_field_and_one_golden_bit_differing_is_a_mismatch()
    {
        var c = ExecutionEnvironment.Capture();
        var bits = ExecutionEnvironment.UpperQuantileBits();
        var witness = ExecutionEnvironment.WitnessDigest();
        Assert.Equal(36, bits.Count);
        Assert.StartsWith("sha256:", witness, StringComparison.Ordinal);
        var profile = Profile(c, bits, witness);
        Assert.Null(ExecutionEnvironment.Mismatch(c, profile));
        Assert.Null(ExecutionEnvironment.GoldenMismatch(profile));
        Assert.StartsWith("runtime.framework_description", ExecutionEnvironment.Mismatch(c with { FrameworkDescription = "other" }, profile), StringComparison.Ordinal);
        var oneBit = Profile(c, [.. bits.Take(35), "0000000000000000"], witness);
        Assert.Equal("upper_quantile j=36", ExecutionEnvironment.GoldenMismatch(oneBit));
        Assert.Equal("witness_digest", ExecutionEnvironment.GoldenMismatch(Profile(c, bits, "sha256:" + new string('0', 64))));
    }
}
