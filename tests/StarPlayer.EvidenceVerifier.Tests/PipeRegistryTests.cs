using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// B1-T09 over a REAL named pipe on this machine (one Windows account): the service runs as this account (registry_account_sid = this TokenUser), so the client and role SIDs come from the test seams and
/// every such connection is <c>Trusted = false</c>. The step-5 AccessCheck runs for real and refuses this account (it holds the account ACE's full rights); the transport tests replace only that result.
/// None of this is the AT-1..AT-14 measurement with separate service / verifier / OWNER / reader accounts.
/// </summary>
public sealed class PipeRegistryTests(TestOnlyWorld w) : IClassFixture<TestOnlyWorld>
{
    internal static readonly string Me = WindowsIdentity.GetCurrent().User!.Value;

    private static string Cause(Action a) => Assert.Throws<EvidenceException>(a).Error.Cause;

    internal static AccessCheckResult Clean(AccessCheckResult _) => new(true, true, true, true, 0x00120089, true);

    internal sealed record Setup(string Pipe, byte[] Config, TrustAnchorV1 Anchor, string Database);

    internal static Setup Make(string instance = TestOnlyTrust.InstanceId, string? anchorInstance = null)
    {
        var pipe = $"starplayer-test-{Guid.NewGuid():N}";
        var dir = Path.Combine(Path.GetTempPath(), "starplayer-registry-tests");
        Directory.CreateDirectory(dir);
        var db = Path.GetFullPath(Path.Combine(dir, $"{pipe}.db"));
        (string, JNode)[] Roles(string id) =>
        [
            ("registry_instance_id", J.S(id)), ("registry_pipe_name", J.S(pipe)), ("registry_account_sid", J.S(Me)), ("registry_service_sid", JNode.Null),
            ("owner_principal_sids", J.A(J.S(TestOnlyTrust.Owner))), ("verifier_principal_sid", J.S(TestOnlyTrust.Verifier)), ("evidence_reader_sids", J.A(J.S("S-1-5-21-1-2-3-1004"))),
        ];
        var config = Jcs.Bytes(J.O([("format", J.S(ApprovalFormats.ServiceConfigFormat)), .. Roles(instance), ("registry_database_path", J.S(db))]));
        var anchorBytes = Jcs.Bytes(J.O([("format", J.S(ApprovalFormats.AnchorFormat)), .. Roles(anchorInstance ?? instance)]));
        return new Setup(pipe, config, new TrustAnchorV1(ApprovalFormats.ReadAnchor(anchorBytes), anchorBytes, Archive.Sha(anchorBytes)), db);
    }

    /// <summary>The service sees every client as this account; the seam maps it to the principal a test plays (null = the service's own TokenUser, unchanged).</summary>
    internal static Func<string?, string> As(Func<string> client) => sid => sid is null ? Me : client();

    [Fact]
    public void The_service_and_client_exchange_closed_requests_over_a_real_pipe_with_roles_from_the_snapshot_and_seamed_connections_are_never_trusted()
    {
        var s = Make();
        var playing = TestOnlyTrust.Owner;
        using var service = new RegistryPipeService(s.Config, As(() => playing), TestOnlyWorld.RepositoryRoot);
        var t = new TestOnlyTrust(w, anchor: s.Anchor);                                                       // the provenance's TRUST_CONFIG is this service's anchor
        using (var owner = RegistryPipeClient.Connect(s.Anchor, () => TestOnlyTrust.Owner, Clean))
        {
            Assert.False(owner.Trusted);
            Assert.Equal(RegisterOutcome.Inserted, owner.Register("APPROVAL_RECORD", t.Approval.Id, t.Approval.Raw).Outcome);
            Assert.Equal(RegisterOutcome.AlreadyRegistered, owner.Register("APPROVAL_RECORD", t.Approval.Id, t.Approval.Raw).Outcome);   // insert-or-compare
            foreach (var (kind, id, bytes) in new[] { ("EXECUTION_AUTHORIZATION", "auth-test-1", t.Authorization), ("BUILD_PROVENANCE", "bp-test-1", t.Build),
                         ("ENVIRONMENT_PROFILE", "env-test-1", t.Environment), ("PZL_TEST_PROFILE", "pzl-profile-test-1", t.TestProfile) })
                owner.Register(kind, id, bytes);
        }

        playing = TestOnlyTrust.Verifier;
        using (var verifier = RegistryPipeClient.Connect(s.Anchor, () => TestOnlyTrust.Verifier, Clean))
        {
            var got = verifier.Get("APPROVAL_RECORD", t.Approval.Id)!;
            Assert.Equal(t.Approval.Raw, got.RawBytes);
            Assert.Equal(TestOnlyTrust.Owner, got.RegistrantPrincipalId);                                       // the registrant is the SID the service mapped, never a request field
            Assert.Single(verifier.GetByScope("APPROVAL_RECORD", Schema.Text(t.Approval.Record["validation_set_id"])));
            Assert.Null(verifier.Get("APPROVAL_RECORD", "apr-none"));
            Assert.Equal(EvidenceCodes.RegistrationUnauthorized, Assert.Throws<EvidenceException>(() => verifier.Register("APPROVAL_RECORD", "x", [1])).Error.Code);   // VERIFIER_SERVICE: receipts only
            // the trust bootstrap (L2-L8, T6) runs unchanged over the pipe client; the records are TEST_ONLY
            Assert.Equal(ApprovalResolution.ResolvedTestOnly, t.Resolve(verifier).Resolution);
            // T6: an anchor that is not the provenance's TRUST_CONFIG
            Assert.Equal("Trust.anchor_mismatch", Cause(() => TrustBootstrap.Resolve(new TestOnlyTrust(w).Anchor, verifier, t.Approval.Id, t.Approval.RawSha256, "auth-test-1", w.Root)));
        }

        playing = "S-1-5-21-1-2-3-1004";                                                                      // an EVIDENCE_READER reaching the service (not via the verifier client)
        using (var reader = RegistryPipeClient.Connect(s.Anchor, () => TestOnlyTrust.Owner, Clean))
            Assert.Equal(EvidenceCodes.RegistrationUnauthorized, Assert.Throws<EvidenceException>(() => reader.Register("APPROVAL_RECORD", t.Approval.Id, t.Approval.Raw)).Error.Code);
        playing = "S-1-5-21-1-2-3-1999";                                                                      // nobody in the snapshot: refused already at the identity request
        Assert.Equal("Registry.unauthorized", Cause(() => RegistryPipeClient.Connect(s.Anchor, () => TestOnlyTrust.Owner, Clean)));
    }

    [Fact]
    public void Step_5_runs_the_real_access_check_and_refuses_a_token_holding_forbidden_rights()
    {
        var s = Make();
        using var service = new RegistryPipeService(s.Config, As(() => TestOnlyTrust.Verifier), TestOnlyWorld.RepositoryRoot);
        AccessCheckResult? real = null;
        // no access seam: the real ImpersonateSelf / OpenThreadToken / AccessCheck of this account against the pipe's real descriptor
        Assert.Equal("Trust.registry_identity_mismatch", Cause(() => RegistryPipeClient.Connect(s.Anchor, () => TestOnlyTrust.Verifier, a => real = a)));
        Assert.NotNull(real);
        Assert.True(real!.ImpersonateSelf && real.OpenThreadToken && real.AccessCheckReturned && real.AccessStatus && real.RevertToSelf);
        Assert.NotEqual(0, real.GrantedAccess & PipeSecurity.Forbidden);                                    // this account is registry_account_sid: it holds 0x1F01FF (write/ACL rights)
    }

    [Fact]
    public void Step_1_and_7_the_client_principal_then_the_registry_identity_on_the_same_connection_are_checked_before_any_other_operation()
    {
        var s = Make();
        Assert.Equal("Trust.client_principal_not_allowed", Cause(() => RegistryPipeClient.Connect(s.Anchor, () => "S-1-5-21-1-2-3-1004", Clean)));  // EVIDENCE_READER (AT-14i)
        Assert.Equal("Trust.client_principal_not_allowed", Cause(() => RegistryPipeClient.Connect(s.Anchor, () => "S-1-5-21-1-2-3-1999", Clean)));
        // N27 (OWNER decision 3): the service answers another instance id than the anchor → refused at Connect, no other request is ever sent
        var other = Make("registry-other", anchorInstance: TestOnlyTrust.InstanceId);
        using (new RegistryPipeService(other.Config, As(() => TestOnlyTrust.Verifier), TestOnlyWorld.RepositoryRoot))
            Assert.Equal("Trust.registry_mismatch", Cause(() => RegistryPipeClient.Connect(other.Anchor, () => TestOnlyTrust.Verifier, Clean)));
        // the matching identity lets the session run; once its connection breaks it is discarded for good (no reconnection, step 9)
        using var service = new RegistryPipeService(s.Config, As(() => TestOnlyTrust.Verifier), TestOnlyWorld.RepositoryRoot);
        var client = RegistryPipeClient.Connect(s.Anchor, () => TestOnlyTrust.Verifier, Clean);
        Assert.Null(client.Get("APPROVAL_RECORD", "apr-x"));
        client.Dispose();
        Assert.Equal("Trust.registry_unavailable", Cause(() => client.Get("APPROVAL_RECORD", "apr-x")));
        Assert.Equal("Trust.registry_unavailable", Cause(() => client.Get("APPROVAL_RECORD", "apr-x")));
    }

    [Fact]
    public void The_service_refuses_to_start_on_a_claimed_name_another_account_or_a_bad_database_path_and_the_client_waits_then_gives_up()
    {
        var s = Make();
        using (new RegistryPipeService(s.Config, As(() => TestOnlyTrust.Verifier), TestOnlyWorld.RepositoryRoot))
            Assert.Equal("Trust.registry_unavailable", Cause(() => new RegistryPipeService(s.Config, As(() => TestOnlyTrust.Verifier), TestOnlyWorld.RepositoryRoot)));   // S2 FIRST_PIPE_INSTANCE
        Assert.Equal("Trust.registry_config_mismatch", Cause(() => new RegistryPipeService(s.Config, _ => "S-1-5-21-1-2-3-1001", TestOnlyWorld.RepositoryRoot)));   // TokenUser ≠ registry_account_sid
        var inRepo = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(s.Config).Replace(Path.GetDirectoryName(s.Database)!.Replace("\\", "\\\\"),
            Path.Combine(TestOnlyWorld.RepositoryRoot, "build").Replace("\\", "\\\\")));
        Assert.Equal("Schema.windows_local_abs_path", Cause(() => new RegistryPipeService(inRepo, As(() => TestOnlyTrust.Verifier), TestOnlyWorld.RepositoryRoot)));   // NSC-5: inside the repository
        var started = Environment.TickCount64;
        var missing = Make();
        var e = Assert.Throws<EvidenceException>(() => RegistryPipeClient.Connect(missing.Anchor, () => TestOnlyTrust.Verifier, Clean));   // no server: C4 after the 30 s deadline
        Assert.Equal("Trust.registry_unavailable", e.Error.Cause);
        Assert.InRange(Environment.TickCount64 - started, RegistryPipeClient.ConnectDeadlineMs - 1000, RegistryPipeClient.ConnectDeadlineMs + 15_000);
    }

    /// <summary>AT-4 pre-measurement (not AT-4): can THIS account create a pipe whose owner is another SID? The actual result is recorded, not asserted as a PASS of AT-4.</summary>
    [Fact]
    public void AT4_pre_measurement_a_pipe_owned_by_another_sid_cannot_be_created_by_this_account()
    {
        var sddl = $"O:{TestOnlyTrust.Owner}G:{TestOnlyTrust.Owner}D:P(A;;0x1f01ff;;;{Me})";
        Assert.True(Native.ConvertStringSecurityDescriptor(sddl, 1, out var sd, out _), new Win32Exception(Marshal.GetLastWin32Error()).Message);
        try
        {
            var sa = new Native.SecurityAttributes { Length = Marshal.SizeOf<Native.SecurityAttributes>(), SecurityDescriptor = sd };
            var h = Native.CreateNamedPipeW($@"\\.\pipe\starplayer-at4-{Guid.NewGuid():N}", Native.PipeAccessDuplex | Native.FirstPipeInstance, Native.PipeRejectRemote, 1, 4096, 4096, 0, ref sa);
            var error = Marshal.GetLastWin32Error();
            if (h != Native.Invalid)
                Native.CloseHandle(h);
            Assert.Equal(Native.Invalid, h);
            Assert.Equal(1307, error);                                                                         // ERROR_INVALID_OWNER, measured on this machine (B1 report §5)
        }
        finally
        {
            Native.LocalFree(sd);
        }
    }
}
