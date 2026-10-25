using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>Codex B1-T11 FIX_REQUIRED regression tests (Red before the fix, Green after). One region per finding.</summary>
public sealed class T11EnvironmentProfileTests
{
    private static readonly JObject Profile = TestOnlyTrust.EnvironmentProfile();

    private static EvidenceError? Read(JNode profile)
    {
        try
        {
            ApprovalFormats.Read(ApprovalFormats.EnvironmentProfile, Jcs.Bytes(profile));
            return null;
        }
        catch (EvidenceException e)
        {
            return e.Error;
        }
    }

    private static JObject WithRows(string pointer, IEnumerable<JNode> rows) => (JObject)JEditT.Set(Profile, pointer, J.A(rows.ToArray()));

    // ---------------------------------------------------------------- B1-T11-05

    [Fact]
    public void T11_05_runtime_files_must_be_exactly_the_three_pinned_files_in_ordinal_order_in_the_reader_and_in_the_comparison()
    {
        Assert.Null(Read(Profile));
        Assert.Null(ExecutionEnvironment.Mismatch(ExecutionEnvironment.Capture(), Profile));
        var rows = ((JArray)Profile["runtime_files"]).Items;
        foreach (var bad in new[]
                 {
                     WithRows("/runtime_files", [rows[0], rows[0], rows[0]]),                       // the Codex path: three CoreLib rows hide clrjit / coreclr
                     WithRows("/runtime_files", [rows[0], rows[1], rows[1]]),                       // a duplicate replaces one file
                     WithRows("/runtime_files", [rows[1], rows[0], rows[2]]),                       // reordered
                 })
        {
            Assert.Equal(EvidenceCodes.SchemaInvalid, Read(bad)?.Code);
            Assert.NotNull(ExecutionEnvironment.Mismatch(ExecutionEnvironment.Capture(), bad));   // the comparison does not trust the reader alone
        }
    }

    [Fact]
    public void T11_05_upper_quantile_must_be_exactly_divisors_1_to_36_in_order_in_the_reader_and_in_the_golden_comparison()
    {
        Assert.Null(ExecutionEnvironment.GoldenMismatch(Profile));
        var rows = ((JArray)JEditT.Get(Profile, "/math_golden/upper_quantile")).Items;
        foreach (var bad in new[]
                 {
                     WithRows("/math_golden/upper_quantile", Enumerable.Repeat(rows[0], 36)),        // the Codex path: 36 rows of divisor 1 skip 2..36
                     WithRows("/math_golden/upper_quantile", [rows[1], rows[0], .. rows.Skip(2)]),   // reordered
                     WithRows("/math_golden/upper_quantile", [.. rows.Take(35), rows[34]]),         // 36 missing, 35 twice
                 })
        {
            Assert.Equal(EvidenceCodes.SchemaInvalid, Read(bad)?.Code);
            Assert.NotNull(ExecutionEnvironment.GoldenMismatch(bad));
        }
    }
}

/// <summary>B1-T11-07: R03-4 5-7 — RevertToSelf exactly once whenever ImpersonateSelf was CALLED (whatever it returned), after the token is closed; a failed revert is fatal.</summary>
public sealed class T11SelfAccessTests
{
    private static (AccessCheckResult Result, List<string> Calls) Run(bool impersonate = true, bool open = true, bool check = true, bool status = true, uint granted = 0x00120089, bool revert = true)
    {
        var calls = new List<string>();
        var seam = new AccessCalls(
            () => { calls.Add("ImpersonateSelf"); return impersonate; },
            () => { calls.Add("OpenThreadToken"); return open ? new IntPtr(42) : null; },
            (_, _) => { calls.Add("AccessCheck"); return (check, status, granted); },
            _ => calls.Add("CloseHandle"),
            () => { calls.Add("RevertToSelf"); return revert; });
        return (Native.SelfAccess([1, 2, 3], seam), calls);
    }

    [Fact]
    public void T11_07_revert_is_called_once_on_every_path_where_impersonate_self_was_called()
    {
        var failed = Run(impersonate: false);
        Assert.Equal(["ImpersonateSelf", "RevertToSelf"], failed.Calls);                                   // the Codex path: ImpersonateSelf=false still reverts
        Assert.Equal("Trust.registry_identity_mismatch", PipeSecurity.CheckAccess(failed.Result));
        Assert.Equal(["ImpersonateSelf", "OpenThreadToken", "RevertToSelf"], Run(open: false).Calls);
        Assert.Equal(["ImpersonateSelf", "OpenThreadToken", "AccessCheck", "CloseHandle", "RevertToSelf"], Run(check: false).Calls);
        var ok = Run();
        Assert.Equal(["ImpersonateSelf", "OpenThreadToken", "AccessCheck", "CloseHandle", "RevertToSelf"], ok.Calls);
        Assert.Null(PipeSecurity.CheckAccess(ok.Result));
        Assert.Equal(0, Run(check: false, granted: 0x7FFFFFFF).Result.GrantedAccess);                    // FALSE return: GrantedAccess not used

        var fatal = new List<string>();
        var original = PipeSecurity.FailFast;
        PipeSecurity.FailFast = fatal.Add;
        try
        {
            PipeSecurity.CheckAccess(Run(impersonate: false, revert: false).Result);                     // a failed revert is fatal even when impersonation failed
            PipeSecurity.CheckAccess(Run(revert: false).Result);
        }
        finally
        {
            PipeSecurity.FailFast = original;
        }

        Assert.Equal(2, fatal.Count);
    }
}

/// <summary>B1-T11-08: S3/S4 — saturation at n = 4 is recorded and never an error; three failed creations while n &lt; 4 record the error and stop the service; stopping never hangs on a client that does not read.</summary>
public sealed class T11PipeInstanceTests
{
    private static System.IO.Pipes.NamedPipeClientStream Raw(PipeRegistryTests.Setup s)
    {
        var c = new System.IO.Pipes.NamedPipeClientStream(".", s.Pipe, System.IO.Pipes.PipeDirection.InOut);
        c.Connect(5_000);
        return c;
    }

    private static bool Until(Func<bool> condition, int ms)
    {
        var deadline = Environment.TickCount64 + ms;
        while (!condition() && Environment.TickCount64 < deadline)
            Thread.Sleep(50);
        return condition();
    }

    [Fact]
    public void T11_08_three_failed_extra_instances_record_the_error_and_stop_the_service()
    {
        var s = PipeRegistryTests.Make();
        using var service = new RegistryPipeService(s.Config, PipeRegistryTests.As(() => TestOnlyTrust.Verifier), TestOnlyWorld.RepositoryRoot);
        var attempts = 0;
        service.CreateExtra = () => { attempts++; return new Microsoft.Win32.SafeHandles.SafePipeHandle(new IntPtr(-1), ownsHandle: true); };
        using var c = Raw(s);                                                                            // takes the only waiting instance → S3 tries to create one more
        Assert.True(Until(() => service.Fault is not null, 10_000), "no error was recorded after three failed creations");
        Assert.Equal(3, attempts);                                                                       // exactly three attempts, no endless retry
        Assert.False(service.Saturated);                                                                 // a creation failure is not saturation
        Assert.True(Until(() => service.Running == 0, 15_000), "the service did not stop");
        Assert.ThrowsAny<Exception>(() => Raw(s).Dispose());                                             // nothing listens any more
    }

    [Fact]
    public void T11_08_four_connected_clients_saturate_the_service_without_an_error()
    {
        var s = PipeRegistryTests.Make();
        using var service = new RegistryPipeService(s.Config, PipeRegistryTests.As(() => TestOnlyTrust.Verifier), TestOnlyWorld.RepositoryRoot);
        var clients = new List<System.IO.Pipes.NamedPipeClientStream>();
        try
        {
            for (var i = 0; i < RegistryPipeService.MaxInstances; i++)
            {
                Assert.True(Until(() => service.Instances > i, 5_000));
                clients.Add(Raw(s));
            }

            Assert.True(Until(() => service.Saturated, 5_000));
            Assert.Equal(RegistryPipeService.MaxInstances, service.Instances);
            Assert.Null(service.Fault);
        }
        finally
        {
            clients.ForEach(c => c.Dispose());
        }
    }

    [Fact]
    public void T11_08_stopping_does_not_hang_on_a_connected_client_that_never_reads_and_every_instance_thread_ends()
    {
        var s = PipeRegistryTests.Make();
        var service = new RegistryPipeService(s.Config, PipeRegistryTests.As(() => TestOnlyTrust.Verifier), TestOnlyWorld.RepositoryRoot);
        using var c = Raw(s);
        c.Write(Framing.Frame(Jcs.Bytes(J.O(("op", J.S(RegistryRequests.IdentityOp))))));               // a request whose reply is never read, the connection kept open
        Thread.Sleep(300);
        var started = Environment.TickCount64;
        service.Dispose();
        Assert.InRange(Environment.TickCount64 - started, 0, 5_000);
        Assert.Equal(0, service.Running);                                                               // no thread left blocked on the silent client
    }
}

/// <summary>B1-T11-06: the EVIDENCE_READER's own client — the same server authentication, Get / GetByScope only.</summary>
public sealed class T11ReaderClientTests(TestOnlyWorld w) : IClassFixture<TestOnlyWorld>
{
    private const string Reader = "S-1-5-21-1-2-3-1004";

    private static string Cause(Action a) => Assert.Throws<EvidenceException>(a).Error.Cause;

    [Fact]
    public void T11_06_the_reader_client_authenticates_like_the_verifier_reads_only_and_cannot_register_or_certify()
    {
        // the API surface: read operations only, not a registry the bootstrap or issuance could use
        var type = typeof(RegistryReaderClient);
        Assert.False(typeof(IEvidenceRegistry).IsAssignableFrom(type));
        Assert.Equal(["Connect", "Dispose", "Get", "GetByScope", "get_Trusted"],
            type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(m => m.Name).Order(StringComparer.Ordinal));

        var s = PipeRegistryTests.Make();
        var t = new TestOnlyTrust(w, anchor: s.Anchor);
        var playing = TestOnlyTrust.Owner;
        using var service = new RegistryPipeService(s.Config, PipeRegistryTests.As(() => playing), TestOnlyWorld.RepositoryRoot);
        using (var owner = RegistryPipeClient.Connect(s.Anchor, () => TestOnlyTrust.Owner, PipeRegistryTests.Clean))
            owner.Register("APPROVAL_RECORD", t.Approval.Id, t.Approval.Raw);

        playing = Reader;
        using (var reader = RegistryReaderClient.Connect(s.Anchor, () => Reader, PipeRegistryTests.Clean))
        {
            Assert.False(reader.Trusted);                                                                 // seamed: never trusted
            Assert.Equal(t.Approval.Raw, reader.Get("APPROVAL_RECORD", t.Approval.Id)!.RawBytes);
            Assert.Single(reader.GetByScope("APPROVAL_RECORD", t.Approval.ValidationSetId));
        }

        // step 1 admits only a reader account; the verifier client still refuses a reader (AT-14i)
        Assert.Equal("Trust.client_principal_not_allowed", Cause(() => RegistryReaderClient.Connect(s.Anchor, () => TestOnlyTrust.Verifier, PipeRegistryTests.Clean)));
        Assert.Equal("Trust.client_principal_not_allowed", Cause(() => RegistryReaderClient.Connect(s.Anchor, () => TestOnlyTrust.Owner, PipeRegistryTests.Clean)));
        Assert.Equal("Trust.client_principal_not_allowed", Cause(() => RegistryPipeClient.Connect(s.Anchor, () => Reader, PipeRegistryTests.Clean)));
        // the same server checks: the real AccessCheck refuses this account's rights, another registry identity is refused before any read
        Assert.Equal("Trust.registry_identity_mismatch", Cause(() => RegistryReaderClient.Connect(s.Anchor, () => Reader)));
        var other = PipeRegistryTests.Make("registry-other", anchorInstance: TestOnlyTrust.InstanceId);
        using (new RegistryPipeService(other.Config, PipeRegistryTests.As(() => Reader), TestOnlyWorld.RepositoryRoot))
            Assert.Equal("Trust.registry_mismatch", Cause(() => RegistryReaderClient.Connect(other.Anchor, () => Reader, PipeRegistryTests.Clean)));
    }
}
