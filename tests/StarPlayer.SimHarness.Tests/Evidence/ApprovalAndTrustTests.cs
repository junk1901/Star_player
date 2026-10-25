using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Text;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>
/// B1-T06a: approval record v1 (catalogue, §13.3 refusals, local §13.4 sources, mapping), execution authorization binding, PATCH-04 (N28), the trust anchor (N24, N25, role overlap, AT-14n), NSC-4 option B
/// on security descriptors, and NSC-5 <c>WINDOWS_LOCAL_ABS_PATH</c>. Every record here is TEST_ONLY; no official seed, pin, SID or approval is created.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ApprovalAndTrustTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "simharness-tests", "trust-" + Guid.NewGuid().ToString("N"));

    public ApprovalAndTrustTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private const string Git = ApprovalFixture.Git;

    private static string Hex(string s) => Hashing.Sha256(s);

    internal static JObject Approval(int perCell = 2) => ApprovalFixture.Approval(perCell);

    private static EvidenceError Refuse(JNode record)
    {
        var e = Assert.Throws<EvidenceException>(() => ApprovalFormats.ReadApproval(Jcs.Bytes(record), null));
        return e.Error;
    }

    [Fact]
    public void A_catalogue_complete_test_only_record_maps_to_a_verifier_only_approvals_instance_and_current_stays_closed()
    {
        var a = ApprovalFormats.ReadApproval(Jcs.Bytes(Approval()), null);
        Assert.True(a.TestOnly);
        var mapped = ApprovalFormats.ToOfficialApprovals(a);
        Assert.False(mapped.ExecutionPathReady);                                          // AMPV candidate: never ready (§14.6)
        Assert.Equal(54, mapped.SeedManifestPins.Count);
        Assert.Equal(2, mapped.DominanceGamesPerCell);
        Assert.Equal((null, false, 0), (OfficialApprovals.Current.GitCommitSha, OfficialApprovals.Current.ExecutionPathReady, OfficialApprovals.Current.CohortInputsHashes.Count));
    }

    [Theory]
    [InlineData("/families/2/run_id", "run-cal-test", "Evidence.approval_mismatch")]                     // §13.3 run id reused
    [InlineData("/families/1/units/1/seed_manifest_path", "seeds/fc3-int/automated.json", "Evidence.approval_mismatch")]   // seed path reused
    [InlineData("/integration_cohort_ids/0", "no-such-cohort", "Evidence.approval_mismatch")]
    [InlineData("/execution_policy/hash", "0000000000000000000000000000000000000000000000000000000000000000", "Evidence.approval_mismatch")]
    [InlineData("/cohort_inputs_hashes/0/inputs_hash", "1111111111111111111111111111111111111111111111111111111111111111", "Evidence.approval_mismatch")]
    [InlineData("/families/0/units/0/cohort_id", "virtual-league-v1", "Evidence.approval_mismatch")]        // catalogue
    [InlineData("/families/1/units/0/planned_games", "n99", "Evidence.approval_mismatch")]                  // integration = 100
    [InlineData("/contracts/hash", "2", "Evidence.contract_not_approved")]
    [InlineData("/families/0/family", "fc3-int", "Evidence.approval_mismatch")]
    [InlineData("/validation_set_id", "v234567890123456789012345678901234567890123456789012345678901234x", "Evidence.schema_invalid")]   // > 64
    public void Each_section_13_refusal(string pointer, string value, string code)
    {
        JNode v = value.StartsWith('n') && long.TryParse(value[1..], out var n) ? J.N(n) : J.S(value);
        Assert.Equal(code, Refuse(JEdit.Set(Approval(), pointer, v)).Code);
    }

    [Fact]
    public void A_cohort_set_that_is_not_the_units_union_and_a_duplicate_verifier_are_refused()
    {
        var extra = JEdit.Update(Approval(), "/cohort_inputs_hashes", n => J.A([.. Schema.Items(n), J.O(("cohort_id", J.S("zz-unused")), ("inputs_hash", J.S(new string('2', 64))))]));
        Assert.Equal("Approval.cohort_set", Refuse(extra).Cause);
        var v = (JObject)JEdit.Get(Approval(), "/verifiers/0");
        var dup = JEdit.Set(Approval(), "/verifiers", J.A(v, JEdit.Set(v, "/version", J.S("2.0.0-test"))));
        Assert.Equal("Approval.verifier_duplicate", Refuse(dup).Cause);
    }

    [Fact]
    public void The_execution_authorization_must_name_this_record_and_a_subsequence_of_its_families()
    {
        var a = ApprovalFormats.ReadApproval(Jcs.Bytes(Approval()), null);
        JObject Auth(string sha, bool testOnly, params string[] fams) => J.O(("format", J.S(ApprovalFormats.AuthorizationFormat)), ("authorization_id", J.S("auth-test-1")),
            ("approval_record_id", J.S(a.Id)), ("approval_record_sha256", J.S(sha)), ("test_only", J.B(testOnly)), ("families", J.A(fams.Select(f => (JNode)J.S(f)))));
        ApprovalFormats.ReadAuthorization(Jcs.Bytes(Auth(a.RawSha256, true, "fc3-cal", "fc3-dom")), a);
        Assert.Equal(EvidenceCodes.AuthorizationMissing, Assert.Throws<EvidenceException>(() => ApprovalFormats.ReadAuthorization(Jcs.Bytes(Auth(new string('0', 64), true, "fc3-cal")), a)).Error.Code);
        Assert.Equal(EvidenceCodes.AuthorizationMissing, Assert.Throws<EvidenceException>(() => ApprovalFormats.ReadAuthorization(Jcs.Bytes(Auth(a.RawSha256, false, "fc3-cal")), a)).Error.Code);
        Assert.Equal(EvidenceCodes.SchemaInvalid, Assert.Throws<EvidenceException>(() => ApprovalFormats.ReadAuthorization(Jcs.Bytes(Auth(a.RawSha256, true, "fc3-dom", "fc3-cal")), a)).Error.Code);
    }

    [Fact]
    public void N28_patch04_binds_the_three_profiles_and_an_environment_profile_has_no_source_git_sha()
    {
        var a = ApprovalFormats.ReadApproval(Jcs.Bytes(Approval()), null);
        JObject Build(string git) => J.O(("format", J.S(ApprovalFormats.BuildFormat)), ("build_provenance_id", J.S("bp-test-1")), ("source_git_sha", J.S(git)),
            ("environment_profile", J.O(("id", J.S("env-test-1")), ("raw_sha256", J.S(Hex("env"))))), ("dotnet_sdk", J.S("8.0.425")), ("configuration", J.S("Release")),
            ("build_procedure_clause_id", J.S("b4-test")), ("artifacts", J.A(J.O(("role", J.S("VERIFIER_BINARY")), ("relative_path", J.S("bin/v.dll")), ("raw_sha256", J.S(Hex("verifier"))), ("byte_length", J.N(1))))));
        var env = J.O(("environment_profile_id", J.S("env-test-1")));
        JObject Profile(string git, string bp) => J.O(("profile_id", J.S("pzl-profile-test-1")), ("source_git_sha", J.S(git)), ("build_provenance_id", J.S(bp)));
        ApprovalFormats.BuildProvenance(Build(Git), "");
        ApprovalFormats.CheckProfiles(a, (Build(Git), Hex("bp")), (env, Hex("env")), (Profile(Git, "bp-test-1"), Hex("tp")));
        string Cause(Action x) => Assert.Throws<EvidenceException>(x).Error.Cause;
        Assert.Equal("Approval.build_provenance", Cause(() => ApprovalFormats.CheckProfiles(a, (Build(new string('b', 40)), Hex("bp")), (env, Hex("env")), (Profile(Git, "bp-test-1"), Hex("tp")))));
        Assert.Equal("Approval.pzl_test_profile", Cause(() => ApprovalFormats.CheckProfiles(a, (Build(Git), Hex("bp")), (env, Hex("env")), (Profile(new string('b', 40), "bp-test-1"), Hex("tp")))));
        Assert.Equal("Approval.environment_profile", Cause(() => ApprovalFormats.CheckProfiles(a, (Build(Git), Hex("bp")), (env, Hex("other")), (Profile(Git, "bp-test-1"), Hex("tp")))));
        Assert.Equal("Schema.unknown_field", Assert.Throws<EvidenceException>(() => ApprovalFormats.EnvironmentProfile(J.O(("format", J.S(ApprovalFormats.EnvironmentFormat)), ("source_git_sha", J.S(Git))), "")).Error.Cause);
    }

    // ---------------------------------------------------------------- trust anchor (§16 T1/T2, R03-1, NSC-4)

    private static JObject Anchor(bool readers = true)
    {
        var a = J.O(
            ("format", J.S(ApprovalFormats.AnchorFormat)), ("registry_instance_id", J.S("registry-test-1")), ("registry_pipe_name", J.S("starplayer-registry-test")),
            ("registry_account_sid", J.S("S-1-5-21-1-2-3-1001")), ("registry_service_sid", JNode.Null), ("owner_principal_sids", J.A(J.S("S-1-5-21-1-2-3-1002"))),
            ("verifier_principal_sid", J.S("S-1-5-21-1-2-3-1003")), ("evidence_reader_sids", J.A(J.S("S-1-5-21-1-2-3-1004"))));
        return readers ? a : (JObject)JEdit.Remove(a, "/evidence_reader_sids");
    }

    private string Put(JNode n)
    {
        var p = Path.Combine(_dir, "anchor-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllBytes(p, Jcs.Bytes(n));
        return p;
    }

    private TrustAnchorV1 ReadAnchor(string path, string? acl = null, bool elevated = false) => TrustAnchorReader.Read(path, H.Root, Path.Combine(_dir, "evidence"), _ => acl, () => elevated);

    private string AnchorCause(Action a) => Assert.Throws<EvidenceException>(a).Error.Cause;

    [Fact]
    public void The_anchor_is_read_once_outside_the_repository_and_evidence_roots_and_refused_on_any_trust_doubt()
    {
        var good = Put(Anchor());
        var a = ReadAnchor(good);
        Assert.Equal(Archive.Sha(File.ReadAllBytes(good)), a.RawSha256);
        Assert.Equal("Trust.anchor_missing", AnchorCause(() => ReadAnchor(Path.Combine(_dir, "none.json"))));                    // N24
        Assert.Equal("Trust.anchor_untrusted", AnchorCause(() => ReadAnchor(good, acl: "C:\\: TCB 밖 쓰기")));                    // N25 / NSC-4
        Assert.Equal("Trust.anchor_untrusted", AnchorCause(() => ReadAnchor(good, elevated: true)));                              // NSC-4 6
        var inEvidence = Path.Combine(_dir, "evidence", "anchor.json");
        Directory.CreateDirectory(Path.GetDirectoryName(inEvidence)!);
        File.Copy(good, inEvidence);
        Assert.Equal("Trust.anchor_untrusted", AnchorCause(() => ReadAnchor(inEvidence)));                                        // N25: inside the evidence root
        var overlap = Put(JEdit.Set(Anchor(), "/verifier_principal_sid", J.S("S-1-5-21-1-2-3-1004")));
        Assert.Equal("Trust.anchor_untrusted", AnchorCause(() => ReadAnchor(overlap)));                                           // reader = verifier
        Assert.Equal(EvidenceCodes.SchemaInvalid, Assert.Throws<EvidenceException>(() => ReadAnchor(Put(Anchor(readers: false)))).Error.Code);   // AT-14n: no [] default
        Assert.Equal(EvidenceCodes.SchemaInvalid, Assert.Throws<EvidenceException>(() => ReadAnchor(Put(JEdit.Set(Anchor(), "/owner_principal_sids", J.A(J.S("S-1-5-32-544")))))).Error.Code);   // a group SID
    }

    [Theory]
    [InlineData("O:SYG:SYD:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FR;;;AU)", AclTarget.AnchorFile, null)]
    [InlineData("O:SYG:SYD:P(A;;FA;;;S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464)(A;;0x1200a9;;;BU)", AclTarget.AnchorFile, null)]
    [InlineData("O:S-1-5-21-1-2-3-1001G:SYD:P(A;;FA;;;SY)", AclTarget.Ancestor, "소유자")]           // owner outside the TCB, on every component
    [InlineData("O:SYG:SYD:NO_ACCESS_CONTROL", AclTarget.Ancestor, "NULL DACL")]
    [InlineData("O:SYG:SYD:P(A;;GW;;;WD)", AclTarget.AnchorFile, "S-1-1-0")]                         // Everyone, generic write
    [InlineData("O:SYG:SYD:P(A;;GA;;;S-1-5-21-1-2-3-1003)", AclTarget.AnchorFile, "S-1-5-21-1-2-3-1003")]   // the verifier account
    [InlineData("O:SYG:SYD:P(A;ID;0x2;;;AU)", AclTarget.AnchorFile, "상속")]                          // an inherited ACE applies to the object
    [InlineData("O:SYG:SYD:P(D;;FA;;;WD)(A;;0x100;;;WD)", AclTarget.AnchorFile, "S-1-1-0")]          // a deny ACE never relaxes
    [InlineData("O:SYG:SYD:P(A;;0x40;;;AU)", AclTarget.AnchorDirectory, "S-1-5-11")]                 // delete child on the anchor directory
    [InlineData("O:SYG:SYD:P(A;;0x40;;;AU)", AclTarget.AnchorFile, null)]                            // the same bit means nothing on a file
    [InlineData("O:SYG:SYD:P(A;;0x2000000;;;AU)", AclTarget.AnchorFile, "S-1-5-11")]                 // MAXIMUM_ALLOWED
    [InlineData("O:SYG:SYD:P(A;;WD;;;CO)", AclTarget.Ancestor, "모호")]                               // CREATOR OWNER on an ACE that applies here
    [InlineData("O:SYG:SYD:P(A;;FR;;;S-1-3-4)", AclTarget.AnchorFile, "모호")]                        // OWNER RIGHTS likewise
    [InlineData("O:SYG:SYD:P(OA;;RP;;;AU)", AclTarget.AnchorFile, "ACE")]                            // an object ACE
    [InlineData("O:SYG:SYD:P(A;OICIIO;GA;;;AU)", AclTarget.Ancestor, null)]                          // INHERIT_ONLY: no right on the object itself (judged on the child)
    [InlineData("O:SYG:SYD:P(A;OICIIO;GA;;;AU)", AclTarget.AnchorDirectory, null)]
    [InlineData("O:SYG:SYD:P(A;;LC;;;AU)", AclTarget.Ancestor, null)]                                // ancestor: adding a subdirectory cannot displace the path
    [InlineData("O:SYG:SYD:P(A;CI;DCLCRPCR;;;BU)", AclTarget.Ancestor, null)]                        // ancestor: add file / subdirectory, EA, attributes
    [InlineData("O:SYG:SYD:P(A;;LC;;;AU)", AclTarget.AnchorDirectory, "0x4")]                        // the anchor directory itself: no addition
    [InlineData("O:SYG:SYD:P(A;;DT;;;AU)", AclTarget.Ancestor, "0x40")]                              // ancestor: delete child (rename / remove a component)
    [InlineData("O:SYG:SYD:P(A;;SD;;;AU)", AclTarget.Ancestor, "0x10000")]                           // ancestor: delete
    [InlineData("O:SYG:SYD:P(A;;WD;;;AU)", AclTarget.Ancestor, "0x40000")]                           // ancestor: change the DACL
    [InlineData("O:SYG:SYD:P(A;;WO;;;AU)", AclTarget.Ancestor, "0x80000")]                           // ancestor: take ownership
    [InlineData("O:SYG:SYD:P(A;;GW;;;AU)", AclTarget.Ancestor, null)]                                // generic write maps to data / EA / attributes only
    [InlineData("O:SYG:SYD:P(A;;GA;;;AU)", AclTarget.Ancestor, "S-1-5-11")]                          // generic all includes delete / DACL / owner
    public void Nsc4_judges_the_rights_that_apply_to_each_component(string sddl, AclTarget target, string? reason)
    {
        var why = AnchorAcl.Violation(new RawSecurityDescriptor(sddl), target);
        if (reason is null)
            Assert.Null(why);
        else
            Assert.Contains(reason, why, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("O:SYG:SYD:P(A;OICIIO;GA;;;AU)", "O:SYG:SYD:AI", false, "S-1-5-11")]               // an inherit-only ACE is not ignored: it would reach an unprotected child file
    [InlineData("O:SYG:SYD:P(A;OICIIO;GA;;;AU)", "O:SYG:SYD:PAI", false, null)]                     // ... nothing is inherited into a protected DACL
    [InlineData("O:SYG:SYD:P(A;CIIO;GA;;;AU)", "O:SYG:SYD:AI", false, null)]                        // container-inherit only never reaches a file
    [InlineData("O:SYG:SYD:P(A;OIIO;GA;;;AU)", "O:SYG:SYD:AI", true, null)]                         // object-inherit only never applies to a subdirectory itself
    [InlineData("O:SYG:SYD:P(A;CIIO;SD;;;AU)", "O:SYG:SYD:AI", true, "S-1-5-11")]                   // ... container-inherit does
    [InlineData("O:SYG:SYD:P(A;OICIIO;GA;;;CO)", "O:SYG:SYD:AI", false, null)]                      // CREATOR OWNER becomes the child's (TCB) owner
    [InlineData("O:SYG:SYD:P(A;OICIIO;GA;;;CO)", "O:S-1-5-21-1-2-3-1001G:SYD:AI", false, "S-1-5-21-1-2-3-1001")]   // ... or a non-TCB owner
    [InlineData("O:SYG:SYD:P(A;OICIIO;GA;;;CG)", "O:SYG:BUD:AI", false, "S-1-5-32-545")]            // CREATOR GROUP becomes the child's group (Users)
    [InlineData("O:SYG:SYD:P(A;OICIIO;GA;;;CG)", "O:SYG:SYD:AI", false, null)]
    [InlineData("O:SYG:SYD:P(A;OICI;0x1200a9;;;BU)", "O:SYG:SYD:AI", false, null)]                  // read only
    public void Nsc4_judges_what_a_component_would_inherit_from_its_parent(string parent, string child, bool childIsDirectory, string? reason)
    {
        var why = AnchorAcl.Inherits(new RawSecurityDescriptor(parent), new RawSecurityDescriptor(child), childIsDirectory, childIsDirectory ? AclTarget.AnchorDirectory : AclTarget.AnchorFile);
        if (reason is null)
            Assert.Null(why);
        else
            Assert.Contains(reason, why, StringComparison.Ordinal);
    }

    /// <summary>The protected anchor directory proposed for operation (NSC-4 decision 2): owner Administrators, protected DACL, full control only for the TCB, read for the verifier account.</summary>
    public const string ProtectedDirectory = "O:BAG:SYD:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1200a9;;;S-1-5-21-1-2-3-1003)";

    private const string AnchorInherited = "O:BAG:SYD:AI(A;ID;FA;;;SY)(A;ID;FA;;;BA)(A;ID;0x1200a9;;;S-1-5-21-1-2-3-1003)";

    /// <summary>The real descriptors of C:\ and C:\ProgramData on this machine, then the candidate directory and anchor file (synthetic: creating them needs an administrator, not done here).</summary>
    private static string? Candidate(string directory, string file) => AnchorAcl.CheckChain([
        (@"C:\", AnchorAcl.Read(@"C:\", directory: true)), (@"C:\ProgramData", AnchorAcl.Read(@"C:\ProgramData", directory: true)),
        (@"C:\ProgramData\StarPlayerTrust", new RawSecurityDescriptor(directory)), (@"C:\ProgramData\StarPlayerTrust\trust-anchor.json", new RawSecurityDescriptor(file)),
    ]);

    [Fact]
    public void Nsc4_the_candidate_location_passes_on_this_machine_s_real_ancestors_and_every_attack_variant_fails()
    {
        Assert.Null(Candidate(ProtectedDirectory, AnchorInherited));
        // attacks on the directory
        Assert.Contains("C:\\ProgramData\\StarPlayerTrust:", Candidate(ProtectedDirectory.Replace("D:PAI", "D:AI"), AnchorInherited));   // not protected: inherits BU add-file from ProgramData
        Assert.Contains("소유자", Candidate(ProtectedDirectory.Replace("O:BA", "O:S-1-5-21-1-2-3-1001"), AnchorInherited));             // created by a standard user
        Assert.Contains("S-1-5-21-1-2-3-1003", Candidate(ProtectedDirectory + "(A;;0x116;;;S-1-5-21-1-2-3-1003)", AnchorInherited));      // the verifier may add files
        Assert.Contains("S-1-5-21-1-2-3-1002", Candidate(ProtectedDirectory + "(A;;DT;;;S-1-5-21-1-2-3-1002)", AnchorInherited));         // an OWNER registrant may delete the anchor
        Assert.Contains("S-1-1-0", Candidate(ProtectedDirectory + "(A;OIIO;GW;;;WD)", AnchorInherited));                                // an inherit-only write that the anchor file would inherit
        // attacks on the file
        Assert.Contains("소유자", Candidate(ProtectedDirectory, AnchorInherited.Replace("O:BA", "O:S-1-5-21-1-2-3-1001")));
        Assert.Contains("S-1-5-21-1-2-3-1003", Candidate(ProtectedDirectory, AnchorInherited + "(A;;FW;;;S-1-5-21-1-2-3-1003)"));
        Assert.Contains("S-1-5-21-1-2-3-1003", Candidate(ProtectedDirectory, AnchorInherited + "(A;;WD;;;S-1-5-21-1-2-3-1003)"));
    }

    [Fact]
    public void Nsc4_on_real_paths_a_tcb_only_system_file_passes_and_user_files_unreadable_paths_and_junctions_fail()
    {
        // a real TCB-only chain on this machine (owners TrustedInstaller / SYSTEM, inherit-only CREATOR OWNER templates, add-subdirectory on C:\)
        Assert.Null(AnchorAcl.CheckPath(@"C:\Windows\System32\drivers\etc\hosts"));
        Assert.Null(AnchorAcl.CheckPath(@"C:\Windows\System32\kernel32.dll"));
        // a file under this user's profile: refused at the first component this user can change (top-down: the profile directory, where this user holds full control)
        Assert.Contains(System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value, AnchorAcl.CheckPath(Put(Anchor())));
        // a descriptor that cannot be read is not trusted (fail-closed)
        Assert.Contains("읽을 수 없어", AnchorAcl.CheckPath(Path.Combine(_dir, "missing", "anchor.json")));
        // a junction on the anchor path is refused before any ACL decision, even with an ACL check that would pass
        var real = Path.Combine(_dir, "real");
        Directory.CreateDirectory(real);
        File.Copy(Put(Anchor()), Path.Combine(real, "anchor.json"));
        var junction = Path.Combine(_dir, "link");
        using (var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{real}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!)
            p.WaitForExit();
        Assert.True(File.GetAttributes(junction).HasFlag(FileAttributes.ReparsePoint), "junction was not created");
        Assert.Contains("Reparse", Assert.Throws<EvidenceException>(() => ReadAnchor(Path.Combine(junction, "anchor.json"))).Error.Message);
        Directory.Delete(junction);                                                                 // the link only (non-recursive), before the fixture deletes the tree
    }

    // ---------------------------------------------------------------- NSC-5

    [Theory]
    [InlineData(@"C:\StarPlayerRegistry\registry.db", true)]
    [InlineData(@"\\server\share\registry.db", false)]
    [InlineData(@"\\?\C:\registry.db", false)]
    [InlineData(@"\\.\C:\registry.db", false)]
    [InlineData(@"C:registry.db", false)]
    [InlineData(@"\registry.db", false)]
    [InlineData(@"registry.db", false)]
    [InlineData(@"C:/StarPlayerRegistry/registry.db", false)]
    [InlineData(@"C:\a\..\registry.db", false)]
    [InlineData(@"C:\a\.\registry.db", false)]
    [InlineData(@"C:\a\\registry.db", false)]
    [InlineData(@"C:\CON", false)]
    [InlineData(@"C:\a\nul.txt", false)]
    [InlineData(@"C:\a\registry.db:stream", false)]
    [InlineData(@"C:\a\reg<istry.db", false)]
    [InlineData(@"C:\a.\registry.db", false)]
    [InlineData("C:\\a\\reg\u0001.db", false)]
    public void Nsc5_windows_local_abs_path_syntax(string path, bool ok) => Assert.Equal(ok, WindowsLocalAbsPath.Syntax(path) is null);

    [Fact]
    public void Nsc5_the_real_checks_refuse_a_path_inside_the_repository_or_evidence_root_and_a_non_canonical_one()
    {
        Assert.Equal(@"C:\StarPlayerRegistry\registry.db", WindowsLocalAbsPath.Resolve(@"C:\StarPlayerRegistry\registry.db", H.Root));
        var inRepo = Path.Combine(H.Root, "build", "registry.db");
        Assert.Throws<EvidenceException>(() => WindowsLocalAbsPath.Resolve(inRepo, H.Root));

        JObject Config(bool readers) => (JObject)JEdit.Add(JEdit.Set(Anchor(readers), "/format", J.S(ApprovalFormats.ServiceConfigFormat)), "", "registry_database_path",
            J.S(@"C:\StarPlayerRegistry\registry.db"));
        ApprovalFormats.ReadServiceConfig(Jcs.Bytes(Config(true)));
        Assert.Equal(EvidenceCodes.SchemaInvalid, Assert.Throws<EvidenceException>(() => ApprovalFormats.ReadServiceConfig(Jcs.Bytes(Config(false)))).Error.Code);   // AT-14n (service side)
        Assert.Equal(EvidenceCodes.SchemaInvalid, Assert.Throws<EvidenceException>(() => ApprovalFormats.ReadServiceConfig(Jcs.Bytes(JEdit.Set(Config(true), "/registry_database_path", J.S(@"\\srv\s\r.db"))))).Error.Code);
        Assert.Equal(EvidenceCodes.SchemaInvalid, Assert.Throws<EvidenceException>(() => ApprovalFormats.ReadServiceConfig(Jcs.Bytes(JEdit.Remove(Config(true), "/registry_database_path")))).Error.Code);
    }
}
