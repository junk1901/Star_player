using System.Security.Cryptography;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>One artifact as read ONCE from the archive: its reference and the exact bytes every later hash / parse / replay uses (§21 "Hash한 뒤 같은 경로를 다시 열어 다른 파일을 파싱하지 않습니다").</summary>
public sealed record ArchivedFile(JObject Reference, byte[] Bytes)
{
    public string ArtifactId => Text(Reference["artifact_id"]);

    public string Role => Text(Reference["role"]);
}

/// <summary>
/// T08: the evidence archive (B1-T00 §2.5 ArtifactRef, §21). Raw SHA-256 and byte length belong to the archive and the receipt, never to A+; a file is read once, compared with its reference and kept as a
/// snapshot; a missing, changed or linked file is <c>Evidence.archive_invalid</c>. New files are created without overwrite, flushed, re-read and re-hashed. The artifact DAG points only downwards
/// (Family / Dominance → Unit PZL / PZL6 → bundles / manifests, R-13). B1-T11-04: every create, snapshot read and retry read goes through a <see cref="PinnedDirectory"/> chain (handles from the volume root,
/// links refused, no FILE_SHARE_DELETE), so no parent can be swapped between the check and the use.
/// </summary>
public static class Archive
{
    public const string InventoryFormat = "simharness-raw-inventory-v1";

    private static EvidenceException Invalid(string cause, string pointer, string message) => EvidenceCodes.Fail(EvidenceCodes.ArchiveInvalid, cause, pointer, "archive", message);

    public static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    /// <summary>Tests only (B1-T11-04): called with the file's relative path after the path has been checked and before the file is opened or created — where a parent could be swapped.</summary>
    internal static Action<string>? BetweenCheckAndUse { get; set; }

    public static JObject Reference(string artifactId, string role, string? unitId, string? gameId, string relativePath, byte[] bytes)
    {
        var r = J.O(("artifact_id", J.S(artifactId)), ("role", J.S(role)), ("unit_id", J.Sn(unitId)), ("game_id", J.Sn(gameId)), ("relative_path", J.S(relativePath)),
            ("raw_sha256", J.S(Sha(bytes))), ("byte_length", J.N(bytes.Length)));
        EvidenceShapes.ArtifactRef(r, "");
        return r;
    }

    /// <summary>Reads every referenced file once (no write / delete sharing while it is read) and requires its exact SHA and length (A20 missing, A18/A19 changed bytes).</summary>
    public static IReadOnlyDictionary<string, ArchivedFile> Snapshot(string archiveRoot, IEnumerable<JObject> references)
    {
        var files = new Dictionary<string, ArchivedFile>(StringComparer.Ordinal);
        foreach (var r in references)
        {
            EvidenceShapes.ArtifactRef(r, "");
            var id = Text(r["artifact_id"]);
            var rel = Text(r["relative_path"]);
            var ptr = $"/artifacts/{StrictJson.Token(id)}";
            if (files.ContainsKey(id))
                throw Invalid("Archive.duplicate_artifact", ptr, $"artifact_id '{id}'가 두 번 나옵니다.");
            var bytes = ReadPinned(archiveRoot, rel, ptr) ?? throw Invalid("Archive.missing", ptr, $"'{rel}'가 없습니다.");

            if (bytes.LongLength != Long(r["byte_length"]) || Sha(bytes) != Text(r["raw_sha256"]))
                throw Invalid("Archive.raw_mismatch", ptr, $"'{rel}'의 바이트 수·Raw SHA-256이 참조와 다릅니다.");
            files.Add(id, new ArchivedFile(r, bytes));
        }

        return files;
    }

    /// <summary>The roles a document of each kind may reference (only downwards, R-13 / N10): a lower document never names an upper one or a later derived file.</summary>
    public static IReadOnlySet<string> AllowedRoles(EvidenceKind kind) => kind switch
    {
        EvidenceKind.UnitPzl => new HashSet<string>(StringComparer.Ordinal) { "GAME_EVENTS", "SEED_MANIFEST", "UNIT_MANIFEST", "GAME_RESULTS", "DIAGNOSTICS", "INPUT_BUNDLE" },
        // OWNER decision 3 §7: the 20 fixed-sample JSON files are PZL6 artifacts of the #6 document (never GAME_RESULTS)
        EvidenceKind.Pzl6 => new HashSet<string>(StringComparer.Ordinal) { "UNIT_MANIFEST", "PZL6", "DIAGNOSTICS", "REGRESSION_PROOF" },
        // OWNER decision 3 §5: GAME_RESULTS only through the Unit PZL (one per unit, never counted twice)
        _ => new HashSet<string>(StringComparer.Ordinal)
        {
            "APPROVAL_RECORD", "BUILD_PROVENANCE", "INPUT_BUNDLE", "SEED_MANIFEST", "PREFLIGHT", "UNIT_MANIFEST", "ATTESTATION", "GAME_EVENTS", "UNIT_PZL", "PZL6",
            "REGRESSION_PROOF", "DIAGNOSTICS",
        },
    };

    /// <summary>
    /// The references of one validated document against the archive rules: roles allowed for the kind (DAG direction), a Unit PZL with exactly one GAME_EVENTS of its own unit, a Family / Dominance
    /// with exactly one SEED_MANIFEST per unit whose raw SHA is the attestation's pin (§21 R-13).
    /// </summary>
    public static void CheckReferences(EvidenceKind kind, JObject doc)
    {
        var refs = Items(Obj(doc["additional_evidence"])["artifacts"]).Select(Obj).ToList();
        var allowed = AllowedRoles(kind);
        foreach (var r in refs)
            if (!allowed.Contains(Text(r["role"])))
                throw EvidenceCodes.Fail(EvidenceCodes.ReferenceInvalid, "Archive.reverse_reference", $"/additional_evidence/artifacts/{StrictJson.Token(Text(r["artifact_id"]))}", "archive",
                    $"{kind} 문서는 역할 {Text(r["role"])}를 참조할 수 없습니다(상위·후속 문서 역방향 참조, N10).");
        if (kind == EvidenceKind.UnitPzl)
        {
            var unit = Text(Obj(doc["plan"])["unit_id"]);
            foreach (var (role, cause) in new[] { ("GAME_EVENTS", "Archive.game_events_count"), ("GAME_RESULTS", "Archive.game_results_count") })
            {
                var bundles = refs.Where(r => Text(r["role"]) == role).ToList();
                if (bundles.Count != 1 || bundles[0]["unit_id"] is not JString { Value: var u } || u != unit || bundles[0]["game_id"] is not JNull)
                    throw Invalid(cause, "/additional_evidence/artifacts", $"Unit PZL '{unit}'에는 그 Unit의 {role}(game_id null)가 정확히 1개 있어야 합니다.");
            }
        }
        else if (kind == EvidenceKind.Pzl6)
        {
            // OWNER decision 3 §7: exactly the 20 fixed-sample JSON files, role PZL6, game_id = the game, id pzl6-json-<mode>-<index:D2> with unit_id = mode
            var json = refs.Where(r => Text(r["role"]) == "PZL6").ToList();
            var ids = new[] { "automated", "non-automated" }.SelectMany(m => Enumerable.Range(1, 10).Select(i => $"pzl6-json-{m}-{i:D2}")).Order(StringComparer.Ordinal).ToList();
            if (!json.Select(r => Text(r["artifact_id"])).SequenceEqual(ids, StringComparer.Ordinal)
                || json.Any(r => r["game_id"] is not JString || r["unit_id"] is not JString { Value: var m } || !Text(r["artifact_id"]).StartsWith($"pzl6-json-{m}-", StringComparison.Ordinal)
                    || Text(r["artifact_id"]).Length != $"pzl6-json-{m}-00".Length))
                throw Invalid("Archive.pzl6_json_count", "/additional_evidence/artifacts", "#6 문서는 고정 표본 20경기의 JSON(role PZL6, game_id 지정, ID pzl6-json-<mode>-<index:D2>)을 정확히 20개 참조합니다.");
        }
        else if (kind is EvidenceKind.Family or EvidenceKind.Dominance)
        {
            foreach (var u in Items(doc["units"]).Select(Obj))
            {
                var unit = kind == EvidenceKind.Dominance ? Text(Obj(u["cell"])["id"]) : Text(u["unit_id"]);
                var pin = Text(Obj(Obj(u["inputs"])["attestation"])["seed_manifest_sha256"]);
                var seeds = refs.Where(r => Text(r["role"]) == "SEED_MANIFEST" && r["unit_id"] is JString { Value: var x } && x == unit).ToList();
                if (seeds.Count != 1 || Text(seeds[0]["raw_sha256"]) != pin)
                    throw Invalid("Archive.seed_manifest", "/additional_evidence/artifacts", $"Unit '{unit}'의 SEED_MANIFEST는 정확히 1개이고 raw_sha256이 attestation.seed_manifest_sha256과 같아야 합니다.");
            }
        }
    }

    private static readonly string[] NotInInventory = ["RAW_INVENTORY", "VERIFICATION_RECEIPT", "REGISTRY_RECORD", "CANONICAL_PAYLOAD"];

    private static readonly Shape InventoryShape = Obj(("artifacts", Arr(EvidenceShapes.ArtifactRef)), ("format", Const(InventoryFormat)));

    /// <summary>
    /// OWNER decision 3 §2: the closed RAW_INVENTORY reader — exactly <c>{artifacts, format}</c>, format <c>simharness-raw-inventory-v1</c>, the bytes their own JCS, ArtifactRef 7 fields each,
    /// artifact_id ordinal ascending without duplicates, none of the excluded roles. Violations are <c>archive_invalid</c>.
    /// </summary>
    public static IReadOnlyList<JObject> ReadInventory(byte[] raw)
    {
        JObject doc;
        try
        {
            doc = ApprovalFormats.Read(InventoryShape, raw);
        }
        catch (EvidenceException e)
        {
            throw Invalid("Inventory.schema", "/", $"RAW_INVENTORY 형식이 아닙니다: {e.Error.Cause} {e.Error.Message}");
        }

        if (!Jcs.Bytes(doc).AsSpan().SequenceEqual(raw))
            throw Invalid("Inventory.not_canonical", "/", "RAW_INVENTORY가 JCS 바이트가 아닙니다.");
        var list = Items(doc["artifacts"]).Select(Obj).ToList();
        for (var i = 0; i < list.Count; i++)
        {
            if (NotInInventory.Contains(Text(list[i]["role"])))
                throw Invalid("Archive.self_reference", $"/artifacts/{i}", $"RAW_INVENTORY에 {Text(list[i]["role"])}를 넣지 않습니다.");
            if (i > 0 && string.CompareOrdinal(Text(list[i - 1]["artifact_id"]), Text(list[i]["artifact_id"])) >= 0)
                throw Invalid("Inventory.order", $"/artifacts/{i}", "RAW_INVENTORY artifacts는 artifact_id ordinal 오름차순·중복 금지입니다.");
        }

        return list;
    }

    /// <summary>RAW_INVENTORY of one snapshot: every original artifact, never itself, a receipt, a registry record or a later canonical payload (§21 자기 참조 제외).</summary>
    public static byte[] Inventory(IEnumerable<JObject> references)
    {
        var list = references.ToList();
        if (list.FirstOrDefault(r => NotInInventory.Contains(Text(r["role"]))) is { } self)
            throw Invalid("Archive.self_reference", "/artifacts", $"RAW_INVENTORY에 {Text(self["role"])}를 넣지 않습니다.");
        var sorted = list.OrderBy(r => Text(r["artifact_id"]), StringComparer.Ordinal).ToList();
        if (sorted.Select(r => Text(r["artifact_id"])).Distinct(StringComparer.Ordinal).Count() != sorted.Count)
            throw Invalid("Archive.duplicate_artifact", "/artifacts", "artifact_id가 중복됩니다.");
        return Jcs.Bytes(J.O(("format", J.S(InventoryFormat)), ("artifacts", J.A(sorted))));
    }

    /// <summary>
    /// Writes a NEW file below <paramref name="root"/> (never overwritten: an existing name fails), flushes it to disk, reads it back through a fresh handle and returns its reference only when the read-back
    /// bytes are the written ones (§20.2 step 5, §21 보존 규칙).
    /// </summary>
    public static JObject WriteNew(string root, string relativePath, byte[] bytes, string artifactId, string role, string? unitId = null, string? gameId = null)
    {
        if (!IsRelPath(relativePath))
            throw Invalid("Archive.path_invalid", "/relative_path", $"'{relativePath}'는 RELPATH가 아닙니다.");
        var segments = relativePath.Split('/');
        using var dir = Pinned(root, segments[..^1], create: true, "/relative_path", createRoot: true);   // B1 T11 R4: the root too, only through the pinned chain
        BetweenCheckAndUse?.Invoke(relativePath);
        var written = dir.WriteNewFile(segments[^1], bytes);                                              // FILE_CREATE through the pinned parent: atomic, never an overwrite
        if (!written.IsSuccess)
            throw written.Error!.Value.Code == "Official.output_exists"
                ? Invalid("Archive.exists", "/relative_path", $"'{relativePath}'를 새로 만들 수 없습니다(덮어쓰지 않음): {written.Error.Value.Message}")
                : Invalid("Archive.unpinnable", "/relative_path", $"'{relativePath}': {written.Error.Value.Message}");
        var back = dir.ReadFile(segments[^1]) is { IsSuccess: true } r ? r.Value : throw Invalid("Archive.readback_mismatch", "/relative_path", $"'{relativePath}'를 다시 읽을 수 없습니다.");
        if (!back.AsSpan().SequenceEqual(bytes))
            throw Invalid("Archive.readback_mismatch", "/relative_path", $"'{relativePath}'를 다시 읽은 바이트가 쓴 바이트와 다릅니다.");
        return Reference(artifactId, role, unitId, gameId, relativePath, back);
    }

    /// <summary>The bytes of an existing archive file read through the pinned chain (the receipt retry, §20.2), or null when it does not exist; a link or an unreadable path is <c>archive_invalid</c>.</summary>
    public static byte[]? TryRead(string root, string relativePath)
    {
        if (!IsRelPath(relativePath))
            throw Invalid("Archive.path_invalid", "/relative_path", $"'{relativePath}'는 RELPATH가 아닙니다.");
        return ReadPinned(root, relativePath, "/relative_path");
    }

    private static PinnedDirectory Pinned(string root, string[] directories, bool create, string ptr, bool createRoot = false)
    {
        var pinned = PinnedDirectory.Open(root, directories, create, createRoot);
        if (pinned.IsSuccess)
            return pinned.Value;
        throw pinned.Error!.Value.Code switch
        {
            "Official.output_reparse_point" => Invalid("Archive.reparse_point", ptr, $"경로에 링크/Junction이 있습니다: {pinned.Error.Value.Message}"),
            "Official.output_missing" => new MissingDirectory(),
            _ => Invalid("Archive.unpinnable", ptr, $"Archive 경로를 핸들로 고정할 수 없습니다: {pinned.Error.Value.Message}"),
        };
    }

    private sealed class MissingDirectory : Exception;

    /// <summary>One file through a pinned parent chain: its bytes, or null when the file or a directory on the way does not exist.</summary>
    private static byte[]? ReadPinned(string root, string relativePath, string ptr)
    {
        var segments = relativePath.Split('/');
        PinnedDirectory dir;
        try
        {
            dir = Pinned(root, segments[..^1], create: false, ptr);
        }
        catch (MissingDirectory)
        {
            return null;
        }

        using (dir)
        {
            BetweenCheckAndUse?.Invoke(relativePath);
            var read = dir.ReadFile(segments[^1]);
            if (read.IsSuccess)
                return read.Value;
            return read.Error!.Value.Code switch
            {
                "Official.output_missing" => null,
                "Official.output_reparse_point" => throw Invalid("Archive.reparse_point", ptr, $"'{relativePath}'는 링크입니다."),
                _ => throw Invalid("Archive.missing", ptr, $"'{relativePath}'를 읽을 수 없습니다: {read.Error.Value.Message}"),
            };
        }
    }
}
