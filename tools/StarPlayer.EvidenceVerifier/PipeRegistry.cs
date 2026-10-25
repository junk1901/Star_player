using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using StarPlayer.SimHarness.Evidence;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.EvidenceVerifier;

/// <summary>The Win32 calls of R03-3 / R03-4 / R03-5 (only these; nothing else touches the pipe).</summary>
[SupportedOSPlatform("windows")]
internal static partial class Native
{
    public const uint PipeAccessDuplex = 0x3, FirstPipeInstance = 0x00080000, PipeRejectRemote = 0x8, OpenExisting = 3, SqosPresent = 0x00100000, SecurityIdentification = 0x00010000;
    public const uint ClientAccess = 0x00120003, TokenQuery = 0x0008, MaximumAllowed = 0x02000000;
    public const uint OwnerInfo = 0x1, GroupInfo = 0x2, DaclInfo = 0x4, KernelObject = 6;
    public const int ErrorPipeBusy = 231, ErrorPipeConnected = 535, ErrorNoData = 232, ErrorFileNotFound = 2, ErrorSemTimeout = 121;

    [StructLayout(LayoutKind.Sequential)]
    public struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        public int InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GenericMapping
    {
        public uint Read, Write, Execute, All;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateNamedPipeW(string name, uint openMode, uint pipeMode, uint maxInstances, uint outBuffer, uint inBuffer, uint timeout, ref SecurityAttributes security);

    [LibraryImport("kernel32.dll", EntryPoint = "WaitNamedPipeW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WaitNamedPipeW(string name, uint timeout);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ConnectNamedPipe(SafePipeHandle pipe, IntPtr overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DisconnectNamedPipe(SafePipeHandle pipe);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FlushFileBuffers(SafePipeHandle pipe);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ImpersonateNamedPipeClient(SafePipeHandle pipe);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ImpersonateSelf(int level);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RevertToSelf();

    [LibraryImport("kernel32.dll")]
    public static partial IntPtr GetCurrentThread();

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    public const uint ThreadTerminate = 0x0001;                                                       // the right CancelSynchronousIo needs

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr OpenThread(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint threadId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CancelSynchronousIo(IntPtr thread);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenThreadToken(IntPtr thread, uint access, [MarshalAs(UnmanagedType.Bool)] bool openAsSelf, out IntPtr token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AccessCheck(IntPtr sd, IntPtr token, uint desired, ref GenericMapping mapping, IntPtr privileges, ref uint privilegesLength, out uint granted,
        [MarshalAs(UnmanagedType.Bool)] out bool status);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    public static partial uint GetSecurityInfo(IntPtr handle, uint objectType, uint info, out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr sd);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    public static partial uint GetSecurityDescriptorLength(IntPtr sd);

    [LibraryImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ConvertStringSecurityDescriptor(string sddl, uint revision, out IntPtr sd, out uint size);

    [LibraryImport("kernel32.dll")]
    public static partial IntPtr LocalFree(IntPtr memory);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(IntPtr handle);

    public static readonly IntPtr Invalid = new(-1);

    /// <summary>The self-relative security descriptor of a kernel handle (owner, group, DACL) as bytes (step 3).</summary>
    public static byte[] Descriptor(IntPtr handle)
    {
        var rc = GetSecurityInfo(handle, KernelObject, OwnerInfo | GroupInfo | DaclInfo, out _, out _, out _, out _, out var sd);
        if (rc != 0)
            throw new Win32Exception((int)rc);
        try
        {
            var bytes = new byte[GetSecurityDescriptorLength(sd)];
            Marshal.Copy(sd, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            LocalFree(sd);
        }
    }

    /// <summary>The Win32 calls of R03-4 step 5 as one seam (tests record their order and count, B1-T11-07); <see cref="Real"/> is the production binding.</summary>
    /// <remarks>OpenThreadToken returns the token or null; AccessCheck takes (security descriptor, token) and returns (returned, AccessStatus, GrantedAccess).</remarks>
    public static AccessCalls Real { get; } = new(
        () => ImpersonateSelf(1),                                                                          // SecurityIdentification
        () => OpenThreadToken(GetCurrentThread(), TokenQuery, true, out var t) ? t : null,
        (sd, token) =>
        {
            var privileges = Marshal.AllocHGlobal(1024);
            try
            {
                var mapping = new GenericMapping { Read = 0x00120089, Write = 0x00120116, Execute = 0x001200A0, All = 0x001F01FF };
                uint length = 1024;
                var returned = AccessCheck(sd, token, MaximumAllowed, ref mapping, privileges, ref length, out var granted, out var status);
                return (returned, status, granted);
            }
            finally
            {
                Marshal.FreeHGlobal(privileges);
            }
        },
        t => CloseHandle(t),
        RevertToSelf);

    /// <summary>
    /// R03-4 step 5 with the caller's own token (MINOR-01): 5-1 ImpersonateSelf → 5-2 OpenThreadToken → 5-3 AccessCheck; GrantedAccess is kept only when AccessCheck returned TRUE. 5-7 (finally, every
    /// path): the token is closed if it was opened, and RevertToSelf is called exactly once whenever 5-1 was CALLED, whatever it returned (its result goes to <see cref="PipeSecurity.CheckAccess"/>, which
    /// fails fast on FALSE).
    /// </summary>
    public static AccessCheckResult SelfAccess(byte[] descriptor, AccessCalls? calls = null)
    {
        calls ??= Real;
        var sd = Marshal.AllocHGlobal(descriptor.Length);
        IntPtr? token = null;
        bool called = false, impersonated = false, returned = false, status = false, reverted = true;
        uint granted = 0;
        try
        {
            Marshal.Copy(descriptor, 0, sd, descriptor.Length);
            called = true;
            impersonated = calls.ImpersonateSelf();
            if (impersonated)
            {
                token = calls.OpenThreadToken();
                if (token is { } t)
                {
                    (returned, status, granted) = calls.AccessCheck(sd, t);
                    if (!returned)
                        (status, granted) = (false, 0);
                }
            }
        }
        finally
        {
            if (token is { } t)
                calls.CloseToken(t);
            if (called)
                reverted = calls.RevertToSelf();
            Marshal.FreeHGlobal(sd);
        }

        return new AccessCheckResult(impersonated, token is not null, returned, status, unchecked((int)granted), reverted);
    }
}

/// <summary>The four Win32 calls of R03-4 step 5 plus CloseHandle (see <see cref="Native.SelfAccess"/>).</summary>
[SupportedOSPlatform("windows")]
public sealed record AccessCalls(Func<bool> ImpersonateSelf, Func<IntPtr?> OpenThreadToken, Func<IntPtr, IntPtr, (bool Returned, bool Status, uint Granted)> AccessCheck, Action<IntPtr> CloseToken,
    Func<bool> RevertToSelf);

/// <summary>
/// The registry service (R03-3, R03-5): ONE immutable snapshot of <c>simharness-registry-service-config-v1</c> read at start (R03-5-1, NSC-5 <c>registry_database_path</c>), its start checks
/// (TokenUser == <c>registry_account_sid</c>, the descriptor of its own first instance == the snapshot's configuration; else no handle stays open), up to 4 byte-mode instances that refuse remote
/// clients, created with the snapshot's protected DACL, the first with <c>FILE_FLAG_FIRST_PIPE_INSTANCE</c> (a pre-claimed name refuses the start), and per request: whole frame → impersonate → token
/// level and TokenUser → RevertToSelf (FailFast on failure) → role from the snapshot → closed request → permission → store. Instances are reused (Flush → Disconnect → Connect).
/// <paramref name="identitySeam"/> exists only for single-account transport tests (it replaces the service's own TokenUser and the client SID); production passes null.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryPipeService : IDisposable
{
    public const int MaxInstances = 4;

    private readonly JObject _config;
    private readonly string _pipe;
    private readonly string _database;
    private readonly IntPtr _descriptor;
    private readonly Func<string?, string>? _identitySeam;
    /// <summary>One server instance: its thread, its pipe handle (used and closed by that thread only) and an OS handle to the thread so a stop can cancel its synchronous I/O.</summary>
    private sealed class Instance
    {
        public Thread Thread = null!;

        public IntPtr Os;
    }

    private readonly List<Instance> _instances = [];
    private readonly CancellationTokenSource _stop = new();
    private readonly ManualResetEventSlim _stopped = new();
    private readonly object _gate = new();
    private int _waiting, _stopping;

    public int Instances { get { lock (_gate) return _instances.Count; } }

    /// <summary>Instance threads still alive (0 once the service has stopped).</summary>
    public int Running { get { lock (_gate) return _instances.Count(i => i.Thread.IsAlive); } }

    /// <summary>S3: n reached 4 — recorded, never an error.</summary>
    public bool Saturated { get; private set; }

    /// <summary>S4: the recorded error that stopped the service (an extra instance could not be created three times while n &lt; 4), or null.</summary>
    public string? Fault { get; private set; }

    /// <summary>Tests only: replaces CreateNamedPipeW for the instances after the first (to make creation fail).</summary>
    internal Func<SafePipeHandle>? CreateExtra { get; set; }

    public static string Sddl(JObject config) =>
        $"O:{Text(config["registry_account_sid"])}G:{Text(config["registry_account_sid"])}D:P"
        + string.Concat(PipeSecurity.Expected(config).Select(e => $"(A;;0x{e.Mask:x};;;{e.Sid})"));

    /// <param name="configBytes">The protected service configuration, read once by the caller.</param>
    public RegistryPipeService(byte[] configBytes, Func<string?, string>? identitySeam = null, params string[] forbiddenRoots)
    {
        _config = ApprovalFormats.ReadServiceConfig(configBytes);                                       // R03-5-2: syntax, order, duplicates, role exclusivity
        _pipe = $@"\\.\pipe\{Text(_config["registry_pipe_name"])}";
        _database = WindowsLocalAbsPath.Resolve(Text(_config["registry_database_path"]), forbiddenRoots);
        _identitySeam = identitySeam;
        var self = identitySeam?.Invoke(null) ?? WindowsIdentity.GetCurrent().User!.Value;
        if (self != Text(_config["registry_account_sid"]))
            throw EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, "Trust.registry_config_mismatch", "/registry_account_sid", "service_start", "서비스 TokenUser가 registry_account_sid가 아닙니다(R03-2 ⑤).");
        if (_config["registry_service_sid"] is JString serviceSid
            && !(WindowsIdentity.GetCurrent().Groups?.Any(g => g.Value == serviceSid.Value) ?? false))
            throw EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, "Trust.registry_config_mismatch", "/registry_service_sid", "service_start", "registry_service_sid가 TokenGroups에 없습니다.");
        if (!Native.ConvertStringSecurityDescriptor(Sddl(_config), 1, out _descriptor, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        using (new SqliteRegistry(_database, RegistryRole.Owner, self)) { }                              // the store exists with the §15 DDL

        var first = Create(first: true);
        if (first.IsInvalid)
        {
            var e = Marshal.GetLastWin32Error();
            Native.LocalFree(_descriptor);
            throw EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, "Trust.registry_unavailable", "", "service_start", $"첫 Pipe 인스턴스를 만들 수 없습니다(이름 선점·권한, Win32 {e}, S2).");
        }

        var own = new RawSecurityDescriptor(Native.Descriptor(first.DangerousGetHandle()), 0);
        if (PipeSecurity.CheckDescriptor(own, _config) is { } bad)
        {
            first.Dispose();
            Native.LocalFree(_descriptor);
            throw EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, bad, "", "service_start", "자기 서버 핸들의 보안 설명자가 스냅샷 구성과 다릅니다(R03-5-2).");
        }

        lock (_gate)
            Start(first);
    }

    private SafePipeHandle Create(bool first)
    {
        var sa = new Native.SecurityAttributes { Length = Marshal.SizeOf<Native.SecurityAttributes>(), SecurityDescriptor = _descriptor };
        var h = Native.CreateNamedPipeW(_pipe, Native.PipeAccessDuplex | (first ? Native.FirstPipeInstance : 0), Native.PipeRejectRemote, MaxInstances, 65536, 65536, 0, ref sa);
        return new SafePipeHandle(h, ownsHandle: true);
    }

    /// <summary>Each instance belongs to its own thread, which alone uses and finally closes the pipe handle (S6: the handles close only when the service stops). Called under <see cref="_gate"/>.</summary>
    private void Start(SafePipeHandle h)
    {
        var instance = new Instance();
        var t = new Thread(() =>
        {
            Volatile.Write(ref instance.Os, Native.OpenThread(Native.ThreadTerminate, false, Native.GetCurrentThreadId()));
            try
            {
                Serve(h);
            }
            finally
            {
                h.Dispose();
            }
        }) { IsBackground = true, Name = "registry-pipe" };
        instance.Thread = t;
        _instances.Add(instance);
        t.Start();
    }

    /// <summary>
    /// S3: when nobody is waiting and fewer than 4 instances exist, one more is created; at 4 the service is saturated (recorded by n only) and creates nothing. S4: a creation that fails three times
    /// (1 s apart) while n &lt; 4 records the error and stops the service normally — no endless retry, never confused with saturation.
    /// </summary>
    private void Grow()
    {
        lock (_gate)
        {
            if (_waiting > 0 || _stop.IsCancellationRequested)
                return;
            if (_instances.Count >= MaxInstances)
            {
                Saturated = true;
                return;
            }

            for (var attempt = 0; attempt < 3; attempt++)
            {
                var h = CreateExtra?.Invoke() ?? Create(first: false);
                if (!h.IsInvalid)
                {
                    Start(h);
                    return;
                }

                h.Dispose();
                if (attempt < 2)
                    Thread.Sleep(1000);
            }

            Fault = $"S4: n = {_instances.Count} < {MaxInstances}인데 추가 Pipe 인스턴스 생성이 3회 실패해 서비스를 중지합니다(Win32 {Marshal.GetLastWin32Error()}).";
            System.Diagnostics.Trace.TraceError(Fault);
            new Thread(Dispose) { IsBackground = true, Name = "registry-pipe-stop" }.Start();       // the stop joins this thread too, so it runs on its own
        }
    }

    private void Serve(SafePipeHandle h)
    {
        while (!_stop.IsCancellationRequested)
        {
            Interlocked.Increment(ref _waiting);
            var connected = Native.ConnectNamedPipe(h, IntPtr.Zero) || Marshal.GetLastWin32Error() == Native.ErrorPipeConnected;
            Interlocked.Decrement(ref _waiting);
            if (_stop.IsCancellationRequested)
                return;
            if (connected)
            {
                Grow();
                try
                {
                    using var stream = new NamedPipeServerStream(PipeDirection.InOut, false, true, new SafePipeHandle(h.DangerousGetHandle(), ownsHandle: false));
                    while (true)
                    {
                        byte[] body;
                        try
                        {
                            body = Framing.Read(stream);
                        }
                        catch (Exception e) when (e is EndOfStreamException or IOException)
                        {
                            break;
                        }

                        var response = Handle(h, body);
                        var frame = Framing.Frame(Jcs.Bytes(response));
                        stream.Write(frame);
                        stream.Flush();
                    }
                }
                catch (Exception)
                {
                    // fail-closed: any failure ends this session without a reply (the client sees a broken pipe → registry_unavailable); the instance is reused
                }
            }

            if (_stop.IsCancellationRequested)
                return;
            Native.FlushFileBuffers(h);                                                                     // S5: reuse the instance
            Native.DisconnectNamedPipe(h);
        }
    }

    /// <summary>R03-5-3 steps 2-6 for one whole frame.</summary>
    private JObject Handle(SafePipeHandle h, byte[] body)
    {
        JObject Refuse(string code, string cause) => J.O(("status", J.S("ERROR")), ("code", J.S(code)), ("cause", J.S(cause)));
        if (!Native.ImpersonateNamedPipeClient(h))
            return Refuse(EvidenceCodes.RegistrationUnauthorized, "Registry.impersonation_failed");
        string sid;
        try
        {
            using var who = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
            if (who.ImpersonationLevel == TokenImpersonationLevel.Anonymous)
                return Refuse(EvidenceCodes.RegistrationUnauthorized, "Registry.anonymous");
            sid = who.User!.Value;
        }
        finally
        {
            if (!Native.RevertToSelf())
                PipeSecurity.FailFast("RevertToSelf 실패: 서비스가 클라이언트 문맥에 남을 수 없어 즉시 종료합니다(R03-5-3 3).");
        }

        sid = _identitySeam?.Invoke(sid) ?? sid;
        var role = Role(_config, sid);
        if (role == RegistryRole.Unauthorized)
            return Refuse(EvidenceCodes.RegistrationUnauthorized, "Registry.unauthorized");
        using var store = new SqliteRegistry(_database, role, sid, Text(_config["registry_instance_id"]));
        return RegistryRequests.Handle(store, role, body);
    }

    /// <summary>R03-5-3 step 4 role table (after RevertToSelf, from the snapshot only).</summary>
    public static RegistryRole Role(JObject config, string sid) =>
        Items(config["owner_principal_sids"]).Select(Text).Contains(sid, StringComparer.Ordinal) ? RegistryRole.Owner
        : Text(config["verifier_principal_sid"]) == sid ? RegistryRole.VerifierService
        : Items(config["evidence_reader_sids"]).Select(Text).Contains(sid, StringComparer.Ordinal) ? RegistryRole.EvidenceReader
        : RegistryRole.Unauthorized;

    /// <summary>
    /// Stops the service (also after an S4 fault; a second call waits for the first). The stop flag is set, then each instance thread's pending synchronous I/O — ConnectNamedPipe, a session's Read or
    /// Write, FlushFileBuffers — is cancelled with CancelSynchronousIo until the thread has returned and closed its own handle (S6). A client that never reads or never disconnects cannot keep the
    /// service alive. (DisconnectNamedPipe from another thread is not used: on a synchronous handle it would queue behind the pending call.) The descriptor is freed once every thread has ended.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 1)
        {
            _stopped.Wait(15_000);
            return;
        }

        Instance[] instances;
        lock (_gate)
        {
            _stop.Cancel();
            instances = [.. _instances];
        }

        var deadline = Environment.TickCount64 + 10_000;
        while (instances.Any(i => i.Thread.IsAlive) && Environment.TickCount64 < deadline)
            foreach (var i in instances.Where(i => i.Thread.IsAlive))
            {
                if (Volatile.Read(ref i.Os) is var os && os != IntPtr.Zero && os != Native.Invalid)
                    Native.CancelSynchronousIo(os);                                                      // no-op between calls; repeated until the thread sees the stop flag
                i.Thread.Join(50);
            }

        foreach (var i in instances.Where(i => !i.Thread.IsAlive && i.Os != IntPtr.Zero && i.Os != Native.Invalid))
            Native.CloseHandle(i.Os);
        if (instances.All(i => !i.Thread.IsAlive))
            Native.LocalFree(_descriptor);
        _stopped.Set();
    }
}

/// <summary>
/// The Official EvidenceVerifier's registry client (R03-4 steps 1-9). <see cref="Trusted"/> is true only when every step ran for real; a connection that used a test seam is never trusted.
/// Step 7 (OWNER decision 3): after the pipe owner / DACL and the client's own access check, the FIRST request on the same authenticated connection is <c>get_registry_identity</c>; only an identity equal
/// to the anchor's <c>registry_instance_id</c> lets any other operation run. A mismatch, an error or a broken connection ends the session at once (no reconnection, step 9).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryPipeClient : IEvidenceRegistry, IDisposable
{
    public const int ConnectDeadlineMs = PipeSession.ConnectDeadlineMs;

    private readonly PipeSession _session;

    private RegistryPipeClient(PipeSession session) => _session = session;

    public bool Trusted => _session.Trusted;

    public string InstanceId => _session.InstanceId;

    /// <param name="identitySeam">Tests only: replaces this process's TokenUser for step 1.</param>
    /// <param name="accessSeam">Tests only: replaces the real step-5 AccessCheck result (MINOR-01 injection).</param>
    public static RegistryPipeClient Connect(TrustAnchorV1 trustAnchor, Func<string>? identitySeam = null, Func<AccessCheckResult, AccessCheckResult>? accessSeam = null) =>
        new(PipeSession.Open(trustAnchor, (anchor, me) =>
            Items(anchor["evidence_reader_sids"]).Select(Text).Contains(me, StringComparer.Ordinal)
                ? "EVIDENCE_READER 계정으로는 EvidenceVerifier를 시작하지 않습니다(R03-4 1단계, AT-14i)."
                : me != Text(anchor["verifier_principal_sid"]) && !Items(anchor["owner_principal_sids"]).Select(Text).Contains(me, StringComparer.Ordinal)
                    ? "이 계정은 verifier_principal_sid도 OWNER도 아닙니다(R03-4 1단계)."
                    : null, identitySeam, accessSeam));

    public RegistryEntry? Get(string kind, string id) => _session.Get(kind, id);

    public IReadOnlyList<RegistryEntry> GetByScope(string kind, string scope) => _session.GetByScope(kind, scope);

    public (RegisterOutcome Outcome, long Sequence) Register(string kind, string id, byte[] raw)
    {
        var r = _session.Call(J.O(("op", J.S("register")), ("entry_kind", J.S(kind)), ("entry_id", J.S(id)), ("raw_bytes_base64", J.S(Convert.ToBase64String(raw)))));
        return (Text(r["status"]) == "INSERTED" ? RegisterOutcome.Inserted : RegisterOutcome.AlreadyRegistered, Long(r["registry_sequence"]));
    }

    public void Dispose() => _session.Dispose();
}

/// <summary>
/// B1-T11-06: the EVIDENCE_READER's read-only registry client (R03-4). It runs the SAME connection and server authentication as the verifier's client (<see cref="PipeSession"/>: principal, pipe owner /
/// DACL, the real AccessCheck, the registry identity before any other request) but step 1 admits only an <c>evidence_reader_sids</c> account, and it offers Get and GetByScope only — no Register, it is not
/// an <see cref="IEvidenceRegistry"/>, so it can feed neither the trust bootstrap nor receipt issuance.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryReaderClient : IDisposable
{
    private readonly PipeSession _session;

    private RegistryReaderClient(PipeSession session) => _session = session;

    public bool Trusted => _session.Trusted;

    /// <param name="identitySeam">Tests only: replaces this process's TokenUser for step 1.</param>
    /// <param name="accessSeam">Tests only: replaces the real step-5 AccessCheck result.</param>
    public static RegistryReaderClient Connect(TrustAnchorV1 trustAnchor, Func<string>? identitySeam = null, Func<AccessCheckResult, AccessCheckResult>? accessSeam = null) =>
        new(PipeSession.Open(trustAnchor, (anchor, me) => Items(anchor["evidence_reader_sids"]).Select(Text).Contains(me, StringComparer.Ordinal)
            ? null : "이 계정은 evidence_reader_sids가 아닙니다(Reader 전용 클라이언트, R03-4 1단계).", identitySeam, accessSeam));

    public RegistryEntry? Get(string kind, string id) => _session.Get(kind, id);

    public IReadOnlyList<RegistryEntry> GetByScope(string kind, string scope) => _session.GetByScope(kind, scope);

    public void Dispose() => _session.Dispose();
}

/// <summary>
/// One authenticated registry connection, shared by both clients (R03-4 steps 1-9): step 1 the caller's principal against the client's rule, steps 2-5 the pipe, its owner / DACL and the caller's own real
/// AccessCheck, step 6 the stream, step 7 <c>get_registry_identity</c> on the same connection before anything else (OWNER decision 3), step 9 a broken connection is never reused. A seamed connection is
/// never <see cref="Trusted"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class PipeSession : IDisposable
{
    public const int ConnectDeadlineMs = 30_000;

    private readonly NamedPipeClientStream _stream;
    private bool _broken;

    public bool Trusted { get; }

    public string InstanceId { get; }

    private PipeSession(JObject anchor, NamedPipeClientStream stream, bool trusted)
    {
        (_stream, Trusted) = (stream, trusted);
        InstanceId = Text(anchor["registry_instance_id"]);
    }

    private static EvidenceException Fail(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, cause, "", "registry_connect", message);

    /// <param name="principalRule">Step 1: the reason this caller may not use this client, or null.</param>
    public static PipeSession Open(TrustAnchorV1 trustAnchor, Func<JObject, string, string?> principalRule, Func<string>? identitySeam, Func<AccessCheckResult, AccessCheckResult>? accessSeam)
    {
        var anchor = trustAnchor.Anchor;
        var me = identitySeam?.Invoke() ?? WindowsIdentity.GetCurrent().User!.Value;
        if (principalRule(anchor, me) is { } refused)
            throw Fail("Trust.client_principal_not_allowed", refused);

        var name = $@"\\.\pipe\{Text(anchor["registry_pipe_name"])}";
        var deadline = Environment.TickCount64 + ConnectDeadlineMs;
        IntPtr h;
        while (true)
        {
            h = Native.CreateFileW(name, Native.ClientAccess, 0, IntPtr.Zero, Native.OpenExisting, Native.SqosPresent | Native.SecurityIdentification, IntPtr.Zero);
            if (h != Native.Invalid)
                break;
            var error = Marshal.GetLastWin32Error();
            var left = deadline - Environment.TickCount64;
            if (left <= 0 || error is not (Native.ErrorPipeBusy or Native.ErrorFileNotFound))
                throw Fail("Trust.registry_unavailable", $"Registry Pipe에 연결할 수 없습니다(Win32 {error}, C4).");
            if (error == Native.ErrorPipeBusy && Native.WaitNamedPipeW(name, (uint)left))
                continue;
            Thread.Sleep(100);
        }

        var pipe = new SafePipeHandle(h, ownsHandle: true);
        try
        {
            var descriptor = Native.Descriptor(h);
            if (PipeSecurity.CheckDescriptor(new RawSecurityDescriptor(descriptor, 0), anchor) is { } bad)
                throw Fail(bad, "Pipe 보안 설명자가 앵커 구성과 다릅니다(R03-4 4단계).");
            var access = Native.SelfAccess(descriptor);
            if (accessSeam is not null)
                access = accessSeam(access);
            if (PipeSecurity.CheckAccess(access) is { } denied)
                throw Fail(denied, "자기 토큰의 실제 접근 판정이 통과하지 않았습니다(R03-4 5단계).");
            var stream = new NamedPipeClientStream(PipeDirection.InOut, isAsync: false, isConnected: true, pipe);
            var session = new PipeSession(anchor, stream, trusted: identitySeam is null && accessSeam is null);
            try
            {
                // step 7: the registry identity on this very connection, before any other operation
                var id = session.Call(J.O(("op", J.S(RegistryRequests.IdentityOp))));
                if (id.Get("registry_instance_id") is not JString { Value: var instance } || Text(id["status"]) != "FOUND" || instance != session.InstanceId)
                    throw Fail("Trust.registry_mismatch", "Registry가 보고한 registry_instance_id가 앵커 값과 다릅니다(R03-4 7단계, OWNER 결정 3).");
            }
            catch
            {
                // mismatch, refusal or broken connection: the session is discarded with its own cause (fail-closed)
                session.Dispose();
                throw;
            }

            return session;
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    public JObject Call(JObject request)
    {
        if (_broken)
            throw Fail("Trust.registry_unavailable", "이 Registry 세션은 이미 폐기되었습니다(재연결 없음, R03-4 9단계).");
        JObject reply;
        try
        {
            _stream.Write(Framing.Frame(Jcs.Bytes(request)));
            _stream.Flush();
            reply = (JObject)StrictJson.Parse(Framing.Read(_stream), "registry_reply");
        }
        catch (EvidenceException)
        {
            _broken = true;                                                                            // an unreadable frame or reply: the session is not reused
            throw;
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
        {
            _broken = true;
            throw Fail("Trust.registry_unavailable", $"Registry 연결이 끊겼습니다(재연결 없이 세션 폐기, 9단계): {e.Message}");
        }

        if (Text(reply["status"]) == "ERROR")
            throw EvidenceCodes.Fail(Text(reply["code"]), Text(reply["cause"]), "", "registry", "Registry가 요청을 거부했습니다.");
        return reply;
    }

    private static RegistryEntry Entry(JObject e) => new(Long(e["registry_sequence"]), Text(e["entry_kind"]), Text(e["entry_id"]), null, Text(e["raw_sha256"]),
        Convert.FromBase64String(Text(e["raw_bytes_base64"])), Text(e["registrant_principal_id"]));

    public RegistryEntry? Get(string kind, string id)
    {
        var r = Call(J.O(("op", J.S("get")), ("entry_kind", J.S(kind)), ("entry_id", J.S(id))));
        return Text(r["status"]) == "FOUND" ? Entry((JObject)r["entry"]) : null;
    }

    public IReadOnlyList<RegistryEntry> GetByScope(string kind, string scope) =>
        Items(Call(J.O(("op", J.S("get_by_scope")), ("entry_kind", J.S(kind)), ("scope_key", J.S(scope))))["entries"]).Select(Obj).Select(Entry).ToList();

    public void Dispose()
    {
        _broken = true;
        _stream.Dispose();
    }
}
