using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// TEST_ONLY trust records for a <see cref="TestOnlyWorld"/>: an anchor (fixture SIDs), an environment profile and Test Profile built from THIS machine and the HEAD tree, a Build Provenance naming the real test
/// assemblies and the anchor, the world's approval record re-pointed at those three, a TEST_ONLY authorization, all registered by the anchor's owner in a local SQLite registry. None of it is an approved
/// profile, SID, Golden or registry; the local registry is never trusted (A17).
/// </summary>
public sealed class TestOnlyTrust
{
    public const string Owner = "S-1-5-21-1-2-3-1002", Verifier = "S-1-5-21-1-2-3-1003", InstanceId = "registry-test-1";

    public static readonly string[] Projects = ["StarPlayer.Domain.Tests", "StarPlayer.Infrastructure.Tests", "StarPlayer.SimHarness.Tests", "StarPlayer.Simulation.Tests"];

    public static string AssemblyPath(string project) => $"tests/{project}/bin/Release/net8.0/{project}.dll";

    public TrustAnchorV1 Anchor { get; }

    public byte[] Build { get; }

    public byte[] Environment { get; }

    public byte[] TestProfile { get; }

    public ApprovalRecordV1 Approval { get; }

    public byte[] Authorization { get; }

    public string DatabasePath { get; }

    public static byte[] AnchorBytes(string instance = InstanceId) => Jcs.Bytes(J.O(
        ("format", J.S(ApprovalFormats.AnchorFormat)), ("registry_instance_id", J.S(instance)), ("registry_pipe_name", J.S("starplayer-registry-test")),
        ("registry_account_sid", J.S("S-1-5-21-1-2-3-1001")), ("registry_service_sid", JNode.Null), ("owner_principal_sids", J.A(J.S(Owner))),
        ("verifier_principal_sid", J.S(Verifier)), ("evidence_reader_sids", J.A(J.S("S-1-5-21-1-2-3-1004")))));

    public static JObject EnvironmentProfile()
    {
        var c = ExecutionEnvironment.Capture();
        return J.O(("format", J.S(ApprovalFormats.EnvironmentFormat)), ("environment_profile_id", J.S("env-test-1")),
            ("os", J.O(("platform", J.S("Windows")), ("version", J.S(c.OsVersion)), ("architecture", J.S(c.OsArchitecture)))), ("process_architecture", J.S(c.ProcessArchitecture)),
            ("runtime", J.O(("framework_description", J.S(c.FrameworkDescription)), ("netcore_app_version", J.S(c.NetCoreAppVersion)), ("runtime_identifier", J.S(c.RuntimeIdentifier)))),
            ("native_math", J.O(("library", J.S("ucrtbase.dll")), ("file_version", J.S(c.UcrtFileVersion)), ("raw_sha256", J.S(c.UcrtSha256)))),
            ("runtime_files", J.A(c.RuntimeFiles.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => (JNode)J.O(("name", J.S(f.Key)), ("raw_sha256", J.S(f.Value)))))),
            ("dotnet_variables", J.O(("forbidden_prefixes", J.A(J.S("COMPlus_"), J.S("DOTNET_"))))),
            ("math_golden", J.O(("upper_quantile", J.A(ExecutionEnvironment.UpperQuantileBits().Select((b, i) => (JNode)J.O(("divisor", J.N(i + 1)), ("bits", J.S(b)))))),
                ("witness_fixture", J.S("dom-witness-fixture-v1")), ("witness_digest", J.S(ExecutionEnvironment.WitnessDigest())))));
    }

    public static JObject Profile(IReadOnlyDictionary<string, IReadOnlyList<string>>? cases = null) => J.O(
        ("format", J.S(ApprovalFormats.TestProfileFormat)), ("profile_id", J.S("pzl-profile-test-1")), ("source_git_sha", J.S(TestOnlyWorld.Head)), ("build_provenance_id", J.S("bp-test-1")),
        ("runner", J.O(("dotnet_sdk", J.S("8.0.425")), ("test_sdk", J.S("17.14.1")), ("xunit", J.S("2.9.3")), ("xunit_runner", J.S("2.8.2")))),
        ("fixtures", J.A(TestProfiles.Fixtures(TestOnlyWorld.RepositoryRoot, TestOnlyWorld.Head))),
        ("checks", J.A(ApprovalFormats.TestProfileChecks.Select(c => (JNode)J.O(("check_id", J.S(c)), ("cases", J.A((cases ?? TestProfiles.Required)[c].Select(x => (JNode)J.S(x)))))))));

    private readonly string _root;

    /// <summary>The TEST_ONLY Build Provenance (naming <paramref name="anchor"/> and the real test assemblies), Environment Profile and Test Profile bytes.</summary>
    public static (byte[] Build, byte[] Environment, byte[] TestProfile) Files(TrustAnchorV1 anchor, string verifierSha)
    {
        var environment = Jcs.Bytes(EnvironmentProfile());
        var testProfile = Jcs.Bytes(Profile());
        var artifacts = new List<JObject>
        {
            J.O(("role", J.S("TRUST_CONFIG")), ("relative_path", J.S("config/trust-anchor.json")), ("raw_sha256", J.S(anchor.RawSha256)), ("byte_length", J.N(anchor.Raw.Length))),
            J.O(("role", J.S("VERIFIER_BINARY")), ("relative_path", J.S("tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll")), ("raw_sha256", J.S(verifierSha)), ("byte_length", J.N(1))),
        };
        foreach (var p in Projects)
        {
            var bytes = File.ReadAllBytes(Path.Combine(TestOnlyWorld.RepositoryRoot, AssemblyPath(p)));
            artifacts.Add(J.O(("role", J.S("TEST_ASSEMBLY")), ("relative_path", J.S(AssemblyPath(p))), ("raw_sha256", J.S(Archive.Sha(bytes))), ("byte_length", J.N(bytes.Length))));
        }

        var build = Jcs.Bytes(J.O(("format", J.S(ApprovalFormats.BuildFormat)), ("build_provenance_id", J.S("bp-test-1")), ("source_git_sha", J.S(TestOnlyWorld.Head)),
            ("environment_profile", J.O(("id", J.S("env-test-1")), ("raw_sha256", J.S(Archive.Sha(environment))))), ("dotnet_sdk", J.S("8.0.425")), ("configuration", J.S("Release")),
            ("build_procedure_clause_id", J.S("b4-test")), ("artifacts", J.A(artifacts.OrderBy(a => Schema.Text(a["relative_path"]), StringComparer.Ordinal)))));
        return (build, environment, testProfile);
    }

    public static TrustAnchorV1 DefaultAnchor(string instance = InstanceId)
    {
        var bytes = AnchorBytes(instance);
        return new TrustAnchorV1(ApprovalFormats.ReadAnchor(bytes), bytes, Archive.Sha(bytes));
    }

    /// <summary>The record pointed at these three profile files (their ids and raw SHAs).</summary>
    public static JObject Repoint(JObject record, byte[] build, byte[] environment, byte[] testProfile)
    {
        JNode Ref(string id, byte[] b) => J.O(("id", J.S(id)), ("raw_sha256", J.S(Archive.Sha(b))));
        return (JObject)JEditT.Set(JEditT.Set(JEditT.Set(record, "/build_provenance", Ref("bp-test-1", build)), "/environment_profile", Ref("env-test-1", environment)),
            "/pzl_test_profile", Ref("pzl-profile-test-1", testProfile));
    }

    /// <summary>The original files a producer archives for this record (B1 T11 R2: APPROVAL_RECORD, BUILD_PROVENANCE, two INPUT_BUNDLE).</summary>
    public PinnedFiles Pinned => new(Approval.Raw, Build, Environment, TestProfile);

    /// <param name="anchor">Another anchor to name as TRUST_CONFIG (the pipe tests: the anchor of a real local service).</param>
    /// <param name="insufficientSampleAllowed">The record's §19 allowance (makes this world's SEEDS_EXHAUSTED Calibration certifiable as TEST_ONLY).</param>
    /// <param name="freezeId">Another freeze id makes another approval record of the same validation set (a "different validation run").</param>
    public TestOnlyTrust(TestOnlyWorld w, string? anchorInstance = null, TrustAnchorV1? anchor = null, bool insufficientSampleAllowed = false, string? freezeId = null)
    {
        _root = w.Root;
        Anchor = anchor ?? DefaultAnchor(anchorInstance ?? InstanceId);
        var verifierSha = Schema.Text(((JObject)Schema.Items(w.Approval.Record["verifiers"])[0])["binary_sha256"]);
        (Build, Environment, TestProfile) = Files(Anchor, verifierSha);
        var record = Repoint(w.Approval.Record, Build, Environment, TestProfile);
        record = (JObject)JEditT.Set(record, "/certification_allowances/insufficient_sample", J.B(insufficientSampleAllowed));
        if (freezeId is not null)
            record = (JObject)JEditT.Set(record, "/freeze_id", J.S(freezeId));
        Approval = ApprovalFormats.ReadApproval(Jcs.Bytes(record), w.Root);
        Authorization = Jcs.Bytes(J.O(("format", J.S(ApprovalFormats.AuthorizationFormat)), ("authorization_id", J.S("auth-test-1")), ("approval_record_id", J.S(Approval.Id)),
            ("approval_record_sha256", J.S(Approval.RawSha256)), ("test_only", J.B(true)), ("families", J.A(J.S("fc3-cal"), J.S("fc3-int"), J.S("fc3-dom")))));

        DatabasePath = Path.Combine(w.Root, $"registry-{Guid.NewGuid():N}.db");
        using var owner = Open(RegistryRole.Owner, Owner);
        owner.Register("APPROVAL_RECORD", Approval.Id, Approval.Raw);
        owner.Register("EXECUTION_AUTHORIZATION", "auth-test-1", Authorization);
        owner.Register("BUILD_PROVENANCE", "bp-test-1", Build);
        owner.Register("ENVIRONMENT_PROFILE", "env-test-1", Environment);
        owner.Register("PZL_TEST_PROFILE", "pzl-profile-test-1", TestProfile);
    }

    public SqliteRegistry Open(RegistryRole role, string principal) => new(DatabasePath, role, principal, InstanceId);

    public ResolvedApproval Resolve(IEvidenceRegistry registry, TrustAnchorV1? anchor = null) =>
        TrustBootstrap.Resolve(anchor ?? Anchor, registry, Approval.Id, Approval.RawSha256, "auth-test-1", _root);
}
