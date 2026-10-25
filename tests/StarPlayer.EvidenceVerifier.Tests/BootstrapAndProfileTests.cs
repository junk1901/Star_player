using System.Text;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// B1-T06a trust bootstrap through the registry (L2-L8, PATCH-04 on registry entries, T6 TRUST_CONFIG, R03-4 step 7) and B1-T06b Test Profile rules (§12.1-§12.3, A25) on TRX inputs.
/// </summary>
public sealed class BootstrapAndProfileTests(TestOnlyWorld w) : IClassFixture<TestOnlyWorld>
{
    private static string Cause(Action a) => Assert.Throws<EvidenceException>(a).Error.Cause;

    [Fact]
    public void The_approval_record_its_profiles_and_the_authorization_resolve_from_the_registry_as_test_only()
    {
        var t = new TestOnlyTrust(w);
        using var verifier = t.Open(RegistryRole.VerifierService, TestOnlyTrust.Verifier);
        var r = t.Resolve(verifier);
        Assert.Equal(ApprovalResolution.ResolvedTestOnly, r.Resolution);
        Assert.Equal(t.Approval.RawSha256, r.Approval.RawSha256);
        Assert.Equal("auth-test-1", Schema.Text(r.Authorization["authorization_id"]));
        TestProfiles.CheckRequired(r.TestProfile.Doc);
        TestProfiles.CheckFixtures(r.TestProfile.Doc, TestOnlyWorld.RepositoryRoot);
        foreach (var p in TestOnlyTrust.Projects)
            TestProfiles.CheckAssembly(r.Build.Doc, TestOnlyTrust.AssemblyPath(p), File.ReadAllBytes(Path.Combine(TestOnlyWorld.RepositoryRoot, TestOnlyTrust.AssemblyPath(p))));
        Assert.Null(ExecutionEnvironment.Mismatch(ExecutionEnvironment.Capture(), r.Environment.Doc));       // E2-4 against the registered (TEST_ONLY) profile
        Assert.Null(ExecutionEnvironment.GoldenMismatch(r.Environment.Doc));                                 // E2-5
    }

    [Fact]
    public void N26_N27_a_different_anchor_or_registry_instance_and_a_non_owner_registrant_are_refused()
    {
        var t = new TestOnlyTrust(w);
        using var verifier = t.Open(RegistryRole.VerifierService, TestOnlyTrust.Verifier);
        // N26: an anchor whose bytes are not the Build Provenance's TRUST_CONFIG (same instance id, other content)
        var other = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(TestOnlyTrust.AnchorBytes()).Replace("starplayer-registry-test", "starplayer-registry-other"));
        Assert.Equal("Trust.anchor_mismatch", Cause(() => t.Resolve(verifier, new TrustAnchorV1(ApprovalFormats.ReadAnchor(other), other, Archive.Sha(other)))));
        // N27: the registry reports another instance id than the anchor
        using var elsewhere = new SqliteRegistry(t.DatabasePath, RegistryRole.VerifierService, TestOnlyTrust.Verifier, "registry-other");
        Assert.Equal("Trust.registry_mismatch", Cause(() => t.Resolve(elsewhere)));
        // L3: an approval record registered by someone who is not the anchor's owner
        var t2 = new TestOnlyTrust(w);
        using var impostor = new SqliteRegistry(Path.Combine(w.Root, $"impostor-{Guid.NewGuid():N}.db"), RegistryRole.Owner, "S-1-5-21-9-9-9-9", TestOnlyTrust.InstanceId);
        foreach (var (kind, id, bytes) in new[] { ("APPROVAL_RECORD", t2.Approval.Id, t2.Approval.Raw), ("EXECUTION_AUTHORIZATION", "auth-test-1", t2.Authorization), ("BUILD_PROVENANCE", "bp-test-1", t2.Build),
                     ("ENVIRONMENT_PROFILE", "env-test-1", t2.Environment), ("PZL_TEST_PROFILE", "pzl-profile-test-1", t2.TestProfile) })
            impostor.Register(kind, id, bytes);
        Assert.Equal("Trust.registrant_not_owner", Cause(() => t2.Resolve(impostor)));
        // L2 / L3: an unknown id, a SHA that is not the registered bytes'
        Assert.Equal("Trust.entry_not_found", Cause(() => TrustBootstrap.Resolve(t.Anchor, verifier, "apr-none", t.Approval.RawSha256, "auth-test-1", w.Root)));
        Assert.Equal("Trust.raw_sha_mismatch", Cause(() => TrustBootstrap.Resolve(t.Anchor, verifier, t.Approval.Id, new string('0', 64), "auth-test-1", w.Root)));
    }

    private static readonly JObject Profile = TestOnlyTrust.Profile();

    private static List<TestCaseResult> AllPassed() => TestProfiles.Required.Values.SelectMany(x => x).Distinct().Select(c => new TestCaseResult(c, "Passed")).ToList();

    [Fact]
    public void A25_the_evidence_is_the_verifier_s_own_results_and_any_gap_skip_or_extra_case_is_a_profile_mismatch()
    {
        var evidence = TestProfiles.Evidence(Profile, new string('d', 64), AllPassed());
        Assert.Equal(8, evidence.Count);
        Assert.All(evidence.Values, e => Assert.True(TestProfiles.AllPass(e)));
        var first = TestProfiles.Required["pzl-8"][0];
        string Code(IEnumerable<TestCaseResult> rs) => Assert.Throws<EvidenceException>(() => TestProfiles.Evidence(Profile, new string('d', 64), rs.ToList())).Error.Code;
        Assert.Equal(EvidenceCodes.TestProfileMismatch, Code(AllPassed().Where(r => r.CaseId != first)));                                         // a required case not executed
        Assert.Equal(EvidenceCodes.TestProfileMismatch, Code(AllPassed().Select(r => r.CaseId == first ? r with { Outcome = "NotExecuted" } : r)));   // skipped / not executed
        Assert.Equal(EvidenceCodes.TestProfileMismatch, Code([.. AllPassed(), new TestCaseResult(first, "Passed")]));                               // the same case twice
        var theory = TestProfiles.Required["pzl-7"][0];
        Assert.Equal(EvidenceCodes.TestProfileMismatch, Code([.. AllPassed(), new TestCaseResult(theory.Replace("\"p1\"", "\"zz\""), "Passed")]));  // a new row of a listed method
        var failed = TestProfiles.Evidence(Profile, new string('d', 64), AllPassed().Select(r => r.CaseId == first ? r with { Outcome = "Failed" } : r).ToList());
        Assert.False(TestProfiles.AllPass(failed["pzl-8"]));                                                                                       // a FAIL is a FAIL case, never hidden
        // the profile must contain every §12.2 case
        var missing = TestOnlyTrust.Profile(TestProfiles.Required.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)(kv.Key == "pzl-9" ? kv.Value.Skip(1).ToList() : kv.Value)));
        Assert.Equal("TestProfile.required_case_missing", Cause(() => TestProfiles.CheckRequired(missing)));
        // a test assembly that is not the provenance's bytes
        var t = new TestOnlyTrust(w);
        Assert.Equal("TestProfile.assembly_changed", Cause(() => TestProfiles.CheckAssembly(ApprovalFormats.Read(ApprovalFormats.BuildProvenance, t.Build), TestOnlyTrust.AssemblyPath(TestOnlyTrust.Projects[0]), [1, 2, 3])));
    }

    [Fact]
    public void A_vstest_trx_is_read_as_display_names_and_outcomes()
    {
        var trx = Encoding.UTF8.GetBytes("""
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>
            <UnitTestResult testName="A.B.C(id: &quot;p1&quot;)" outcome="Passed" />
            <UnitTestResult testName="A.B.D" outcome="NotExecuted" />
            </Results></TestRun>
            """);
        Assert.Equal([new TestCaseResult("P|A.B.C(id: \"p1\")", "Passed"), new TestCaseResult("P|A.B.D", "NotExecuted")], TestProfiles.Trx("P", trx));
        Assert.Equal("A.B.C", TestProfiles.Method("P|A.B.C(id: \"p1\")"));
    }
}
