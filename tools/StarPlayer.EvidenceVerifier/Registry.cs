using System.Buffers.Binary;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using Microsoft.Data.Sqlite;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier;

public enum RegistryRole { Owner, VerifierService, EvidenceReader, Unauthorized }

public sealed record RegistryEntry(long Sequence, string Kind, string Id, string? Scope, string RawSha256, byte[] RawBytes, string RegistrantPrincipalId);

public enum RegisterOutcome { Inserted, AlreadyRegistered }

/// <summary>
/// The trust registry operations (§15). Two implementations exist: <see cref="SqliteRegistry"/> (the store the registry service owns; in tests a TEST_ONLY local instance, which is never a trusted registry:
/// A17 — its receipts are TEST_ONLY) and the operational client behind the authenticated pipe (R03-4), whose transport is not implemented in B-1 (AT-1~AT-14r not run).
/// </summary>
public interface IEvidenceRegistry
{
    /// <summary>True only for a registry reached through the authenticated pipe of a trusted anchor (§16 ②③). A local store is never trusted.</summary>
    bool Trusted { get; }

    /// <summary>The registry instance id the service reports (R03-4 step 7 compares it with the anchor's <c>registry_instance_id</c>).</summary>
    string InstanceId { get; }

    RegistryEntry? Get(string kind, string id);

    IReadOnlyList<RegistryEntry> GetByScope(string kind, string scope);

    (RegisterOutcome Outcome, long Sequence) Register(string kind, string id, byte[] raw);
}

/// <summary>The fixed kind table (§15) and the role → registrable kinds table (R03-5-3 step 6).</summary>
public static class RegistryKinds
{
    public static readonly string[] All = ["APPROVAL_RECORD", "EXECUTION_AUTHORIZATION", "BUILD_PROVENANCE", "ENVIRONMENT_PROFILE", "PZL_TEST_PROFILE", "VERIFICATION_RECEIPT"];

    public static IReadOnlySet<string> Registrable(RegistryRole role) => role switch
    {
        RegistryRole.Owner => new HashSet<string>(StringComparer.Ordinal) { "APPROVAL_RECORD", "EXECUTION_AUTHORIZATION", "BUILD_PROVENANCE", "ENVIRONMENT_PROFILE", "PZL_TEST_PROFILE" },
        RegistryRole.VerifierService => new HashSet<string>(StringComparer.Ordinal) { "VERIFICATION_RECEIPT" },
        _ => new HashSet<string>(StringComparer.Ordinal),
    };

    /// <summary>The id member inside each kind's content and its closed shape.</summary>
    public static (string IdField, Shape Shape) Format(string kind) => kind switch
    {
        "APPROVAL_RECORD" => ("approval_record_id", ApprovalFormats.Approval),
        "EXECUTION_AUTHORIZATION" => ("authorization_id", ApprovalFormats.Authorization),
        "BUILD_PROVENANCE" => ("build_provenance_id", ApprovalFormats.BuildProvenance),
        "ENVIRONMENT_PROFILE" => ("environment_profile_id", ApprovalFormats.EnvironmentProfile),
        "PZL_TEST_PROFILE" => ("profile_id", ApprovalFormats.TestProfile),
        "VERIFICATION_RECEIPT" => ("receipt_id", Receipts.Shape),
        _ => throw EvidenceCodes.Fail(EvidenceCodes.SchemaInvalid, "Registry.kind_unknown", "/entry_kind", "registry", $"알 수 없는 entry_kind '{kind}'입니다."),
    };

    /// <summary>R03-5-3 step 6: the permission decision BEFORE any transaction. A reader is refused for every kind and body without a BEGIN (AT-14c/d/q).</summary>
    public static EvidenceError? Permission(RegistryRole role, string op, string kind)
    {
        if (role == RegistryRole.Unauthorized)
            return new EvidenceError(EvidenceCodes.RegistrationUnauthorized, "Registry.unauthorized", "", "registry", "승인되지 않은 주체입니다.");
        if (op != "register")
            return null;
        return Registrable(role).Contains(kind) ? null
            : new EvidenceError(EvidenceCodes.RegistrationUnauthorized, "Registry.kind_not_allowed", "/entry_kind", "registry", $"역할 {role}은(는) {kind}를 등록할 수 없습니다.");
    }
}

/// <summary>
/// §15 store: the exact DDL (immutable rows by trigger, UNIQUE (kind, id), one approval record per validation set) and <c>Register</c> = permission → strict parse with the id inside the content → in
/// <c>BEGIN IMMEDIATE</c> insert, or the same bytes again (idempotent), or a conflict. The registrant SID is the one the service authenticated, never a request field.
/// </summary>
public sealed class SqliteRegistry : IEvidenceRegistry, IDisposable
{
    public const string Ddl = """
        CREATE TABLE IF NOT EXISTS registry_entry(
          registry_sequence       INTEGER PRIMARY KEY AUTOINCREMENT,
          entry_kind              TEXT NOT NULL CHECK(entry_kind IN ('APPROVAL_RECORD','EXECUTION_AUTHORIZATION','BUILD_PROVENANCE',
                                                                     'ENVIRONMENT_PROFILE','PZL_TEST_PROFILE','VERIFICATION_RECEIPT')),
          entry_id                TEXT NOT NULL,
          scope_key               TEXT NULL,
          raw_sha256              TEXT NOT NULL,
          raw_bytes               BLOB NOT NULL,
          registrant_principal_id TEXT NOT NULL,
          UNIQUE(entry_kind, entry_id));
        CREATE UNIQUE INDEX IF NOT EXISTS registry_scope ON registry_entry(entry_kind, scope_key) WHERE scope_key IS NOT NULL;
        CREATE TRIGGER IF NOT EXISTS registry_no_update BEFORE UPDATE ON registry_entry BEGIN SELECT RAISE(ABORT,'immutable'); END;
        CREATE TRIGGER IF NOT EXISTS registry_no_delete BEFORE DELETE ON registry_entry BEGIN SELECT RAISE(ABORT,'immutable'); END;
        """;

    private readonly SqliteConnection _db;
    private readonly RegistryRole _role;
    private readonly string _principal;

    /// <param name="role">The role the service mapped the authenticated caller to (R03-5-3 step 4).</param>
    /// <param name="principal">The caller's SID as the service obtained it (step 3), recorded as registrant.</param>
    public SqliteRegistry(string path, RegistryRole role, string principal, string instanceId = "registry-local-test")
    {
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        _db.Open();
        using var c = _db.CreateCommand();
        c.CommandText = Ddl;
        c.ExecuteNonQuery();
        (_role, _principal, InstanceId) = (role, principal, instanceId);
    }

    public bool Trusted => false;

    public string InstanceId { get; }

    public void Dispose() => _db.Dispose();

    private static RegistryEntry Row(SqliteDataReader r) =>
        new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4), (byte[])r[5], r.GetString(6));

    private const string Columns = "registry_sequence, entry_kind, entry_id, scope_key, raw_sha256, raw_bytes, registrant_principal_id";

    public RegistryEntry? Get(string kind, string id)
    {
        if (RegistryKinds.Permission(_role, "get", kind) is { } refused)
            throw new EvidenceException(refused);
        return Query("entry_id", kind, id, null).FirstOrDefault();
    }

    public IReadOnlyList<RegistryEntry> GetByScope(string kind, string scope)
    {
        if (RegistryKinds.Permission(_role, "get_by_scope", kind) is { } refused)
            throw new EvidenceException(refused);
        return Query("scope_key", kind, scope, null);
    }

    private List<RegistryEntry> Query(string column, string kind, string value, SqliteTransaction? tx)
    {
        using var c = _db.CreateCommand();
        c.Transaction = tx;
        c.CommandText = $"SELECT {Columns} FROM registry_entry WHERE entry_kind = $k AND {column} = $v ORDER BY registry_sequence";
        c.Parameters.AddWithValue("$k", kind);
        c.Parameters.AddWithValue("$v", value);
        using var r = c.ExecuteReader();
        var list = new List<RegistryEntry>();
        while (r.Read())
            list.Add(Row(r));
        return list;
    }

    public (RegisterOutcome Outcome, long Sequence) Register(string kind, string id, byte[] raw)
    {
        if (RegistryKinds.Permission(_role, "register", kind) is { } refused)
            throw new EvidenceException(refused);                                        // decided before any transaction
        var (idField, shape) = RegistryKinds.Format(kind);
        var content = ApprovalFormats.Read(shape, raw);
        if (Schema.Text(content[idField]) != id)
            throw EvidenceCodes.Fail(EvidenceCodes.SchemaInvalid, "Registry.id_mismatch", $"/{idField}", "registry", "내용 안의 ID가 등록 ID와 다릅니다.");
        var scope = kind == "APPROVAL_RECORD" ? Schema.Text(content["validation_set_id"]) : null;
        var sha = Archive.Sha(raw);
        using var tx = _db.BeginTransaction(deferred: false);                            // BEGIN IMMEDIATE
        var existing = Query("entry_id", kind, id, tx).FirstOrDefault();
        if (existing is not null)
        {
            if (existing.RawSha256 == sha && existing.RawBytes.AsSpan().SequenceEqual(raw))
                return (RegisterOutcome.AlreadyRegistered, existing.Sequence);
            throw EvidenceCodes.Fail(EvidenceCodes.RegistrationConflict, "Registry.conflict", "", "registry", $"{kind} '{id}'가 다른 내용으로 이미 등록되어 있습니다(교체 금지).");
        }

        if (scope is not null && Query("scope_key", kind, scope, tx).Count > 0)
            throw EvidenceCodes.Fail(EvidenceCodes.RegistrationConflict, "Registry.scope_conflict", "", "registry", $"검증 집합 '{scope}'에 다른 승인 기록이 이미 있습니다.");
        using var c = _db.CreateCommand();
        c.Transaction = tx;
        c.CommandText = "INSERT INTO registry_entry(entry_kind, entry_id, scope_key, raw_sha256, raw_bytes, registrant_principal_id) VALUES ($k, $i, $s, $h, $b, $p); SELECT last_insert_rowid();";
        c.Parameters.AddWithValue("$k", kind);
        c.Parameters.AddWithValue("$i", id);
        c.Parameters.AddWithValue("$s", (object?)scope ?? DBNull.Value);
        c.Parameters.AddWithValue("$h", sha);
        c.Parameters.AddWithValue("$b", raw);
        c.Parameters.AddWithValue("$p", _principal);
        var seq = (long)c.ExecuteScalar()!;
        tx.Commit();
        return (RegisterOutcome.Inserted, seq);
    }

    /// <summary>A raw SQL statement against the store (tests only: shows that the immutability triggers refuse UPDATE / DELETE).</summary>
    internal int Execute(string sql)
    {
        using var c = _db.CreateCommand();
        c.CommandText = sql;
        return c.ExecuteNonQuery();
    }
}

/// <summary>
/// R03-5-3 step 5: the closed request format; fields such as <c>role</c>, <c>registrant_principal_id</c> or <c>principal</c> are <c>Evidence.schema_invalid</c> (AT-14e, N27-c). OWNER decision 3
/// (2026-10-24) adds ONE read-only operation, <c>{"op":"get_registry_identity"}</c> → <c>{"status":"FOUND","registry_instance_id":ID}</c>, open to every authenticated role; the other three are unchanged.
/// </summary>
public static class RegistryRequests
{
    public const string IdentityOp = "get_registry_identity";

    private static readonly Shape Kind = Schema.OneOf(RegistryKinds.All);

    public static readonly Shape Identity = Schema.Obj(("op", Schema.Const(IdentityOp)));

    public static readonly Shape Get = Schema.Obj(("op", Schema.Const("get")), ("entry_kind", Kind), ("entry_id", Schema.Id));
    public static readonly Shape GetByScope = Schema.Obj(("op", Schema.Const("get_by_scope")), ("entry_kind", Schema.Const("APPROVAL_RECORD")), ("scope_key", Schema.Id));
    public static readonly Shape Register = Schema.Obj(("op", Schema.Const("register")), ("entry_kind", Kind), ("entry_id", Schema.Id),
        ("raw_bytes_base64", Schema.Str(v => { try { Convert.FromBase64String(v); return true; } catch (FormatException) { return false; } }, "base64")));

    public static JObject Parse(ReadOnlySpan<byte> body)
    {
        var n = StrictJson.Parse(body, "registry_request");
        var op = n is JObject o && o.Get("op") is JString s ? s.Value : null;
        (op switch { "get" => Get, "get_by_scope" => GetByScope, "register" => Register, IdentityOp => Identity, _ => throw Schema.Bad("/op", "Schema.format", "op는 get·get_by_scope·register·get_registry_identity입니다.") })(n, "");
        return (JObject)n;
    }

    private static JObject Registered((RegisterOutcome Outcome, long Sequence) r) =>
        J.O(("status", J.S(r.Outcome == RegisterOutcome.Inserted ? "INSERTED" : "ALREADY_REGISTERED")), ("registry_sequence", J.N(r.Sequence)));

    /// <summary>The service side of one request after authentication: permission first (a reader never reaches a transaction), then the store.</summary>
    public static JObject Handle(SqliteRegistry store, RegistryRole role, ReadOnlySpan<byte> body)
    {
        JObject Entry(RegistryEntry e) => J.O(("entry_kind", J.S(e.Kind)), ("entry_id", J.S(e.Id)), ("registry_sequence", J.N(e.Sequence)), ("raw_sha256", J.S(e.RawSha256)),
            ("raw_bytes_base64", J.S(Convert.ToBase64String(e.RawBytes))), ("registrant_principal_id", J.S(e.RegistrantPrincipalId)));
        try
        {
            var r = Parse(body);
            var op = Schema.Text(r["op"]);
            if (op == IdentityOp)
                return RegistryKinds.Permission(role, op, "") is { } unauthorized ? throw new EvidenceException(unauthorized)
                    : J.O(("status", J.S("FOUND")), ("registry_instance_id", J.S(store.InstanceId)));
            var kind = Schema.Text(r["entry_kind"]);
            if (RegistryKinds.Permission(role, op, kind) is { } refused)
                throw new EvidenceException(refused);
            return op switch
            {
                "get" => store.Get(kind, Schema.Text(r["entry_id"])) is { } e ? J.O(("status", J.S("FOUND")), ("entry", Entry(e))) : J.O(("status", J.S("NOT_FOUND"))),
                "get_by_scope" => J.O(("status", J.S("FOUND")), ("entries", J.A(store.GetByScope(kind, Schema.Text(r["scope_key"])).Select(e => (JNode)Entry(e))))),
                _ => Registered(store.Register(kind, Schema.Text(r["entry_id"]), Convert.FromBase64String(Schema.Text(r["raw_bytes_base64"])))),
            };
        }
        catch (EvidenceException e)
        {
            return J.O(("status", J.S("ERROR")), ("code", J.S(e.Error.Code)), ("cause", J.S(e.Error.Cause)));
        }
    }
}

/// <summary>R03-3 framing: a 4-byte big-endian length, then the JCS UTF-8 body, at most 16 MiB.</summary>
public static class Framing
{
    public const int MaxBody = 16 << 20;

    public static byte[] Frame(byte[] body)
    {
        if (body.Length > MaxBody)
            throw new ArgumentOutOfRangeException(nameof(body), "프레임 본문이 16 MiB를 넘습니다.");
        var b = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(b, body.Length);
        body.CopyTo(b, 4);
        return b;
    }

    public static byte[] Read(Stream s)
    {
        Span<byte> head = stackalloc byte[4];
        s.ReadExactly(head);
        var length = BinaryPrimitives.ReadInt32BigEndian(head);
        if (length is < 0 or > MaxBody)
            throw EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, "Trust.registry_unavailable", "", "framing", $"프레임 길이 {length}가 허용 범위 밖입니다.");
        var body = new byte[length];
        s.ReadExactly(body);
        return body;
    }
}

/// <summary>What the AccessCheck sequence of R03-4 step 5 returned (a seam: the real values come from the Win32 calls, tests inject them, MINOR-01).</summary>
public sealed record AccessCheckResult(bool ImpersonateSelf, bool OpenThreadToken, bool AccessCheckReturned, bool AccessStatus, int GrantedAccess, bool RevertToSelf);

/// <summary>
/// R03-4 steps 4-5 as closed rules over a pipe's security descriptor and the client's own AccessCheck. (a)-(c) and step 5 → <c>Trust.registry_identity_mismatch</c>, (d) → <c>Trust.registry_config_mismatch</c>.
/// A failed RevertToSelf is fatal (<see cref="FailFast"/>, default <see cref="Environment.FailFast(string)"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public static class PipeSecurity
{
    public const int ClientAccess = 0x00120003, AccountAccess = 0x001F01FF, Forbidden = 0x000D0114;

    public static Action<string> FailFast { get; set; } = Environment.FailFast;

    /// <summary>The expected (SID, mask) multiset of the anchor (R03-4 4(d)).</summary>
    public static IReadOnlyList<(string Sid, int Mask)> Expected(JObject anchor) =>
    [
        (Schema.Text(anchor["registry_account_sid"]), AccountAccess), (Schema.Text(anchor["verifier_principal_sid"]), ClientAccess),
        .. Schema.Items(anchor["owner_principal_sids"]).Select(s => (Schema.Text(s), ClientAccess)),
        .. Schema.Items(anchor["evidence_reader_sids"]).Select(s => (Schema.Text(s), ClientAccess)),
    ];

    public static string? CheckDescriptor(RawSecurityDescriptor sd, JObject anchor)
    {
        if (sd.Owner?.Value != Schema.Text(anchor["registry_account_sid"]))
            return "Trust.registry_identity_mismatch";
        if (sd.DiscretionaryAcl is null || (sd.ControlFlags & ControlFlags.DiscretionaryAclPresent) == 0)
            return "Trust.registry_identity_mismatch";
        var got = new List<(string, int)>();
        foreach (GenericAce ace in sd.DiscretionaryAcl)
        {
            if (ace is not CommonAce c || c.AceType != AceType.AccessAllowed || ace.AceFlags != AceFlags.None)
                return "Trust.registry_identity_mismatch";
            got.Add((c.SecurityIdentifier.Value, AnchorAcl.Map(c.AccessMask)));
        }

        var expected = Expected(anchor).OrderBy(x => x.Sid, StringComparer.Ordinal).ThenBy(x => x.Mask).ToList();
        return got.OrderBy(x => x.Item1, StringComparer.Ordinal).ThenBy(x => x.Item2).SequenceEqual(expected) ? null : "Trust.registry_config_mismatch";
    }

    /// <summary>Step 5 decision: every call must succeed, the AccessCheck status must be TRUE, and the granted rights must hold none of the forbidden ones. GrantedAccess is never read after a FALSE return.</summary>
    public static string? CheckAccess(AccessCheckResult r)
    {
        if (!r.RevertToSelf)                                                                         // RevertToSelf is called whenever ImpersonateSelf was called (5-7)
            FailFast("RevertToSelf 실패: 보안 문맥을 되돌릴 수 없어 즉시 종료합니다(R03-4 5-7).");
        if (!r.ImpersonateSelf || !r.OpenThreadToken || !r.AccessCheckReturned || !r.AccessStatus)
            return "Trust.registry_identity_mismatch";
        return (r.GrantedAccess & Forbidden) != 0 ? "Trust.registry_identity_mismatch" : null;
    }
}
