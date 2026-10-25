using StarPlayer.SimHarness.Evidence;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.EvidenceVerifier;

public enum ReceiptState { Certified, TestOnlyRegistered }

/// <summary>A persisted and registered receipt: its bytes, raw SHA, registry sequence and state. Only <see cref="ReceiptState.Certified"/> counts as certification (a trusted registry and test_only=false).</summary>
public sealed record IssuedReceipt(string ReceiptId, byte[] Bytes, string RawSha256, long Sequence, ReceiptState State);

/// <summary>
/// T09: <c>simharness-verification-receipt-v1</c> (§20.1, R-11, P-15), the certification state table (§19) and issuance (§20.2 steps 5-7): E2-7 re-check, a NEW file flushed and re-read, an atomic
/// registration (idempotent for the same bytes, conflict otherwise), then a re-query that must return the same id, SHA and bytes. A failed registration leaves the receipt pending (never certified).
/// </summary>
public static class Receipts
{
    public const string Format = "simharness-verification-receipt-v1";

    private static readonly Shape Uuid = Pattern("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", "UUID-D");

    private static Shape IdRef => Obj(("id", Id), ("raw_sha256", H));

    public static readonly Shape Target = Obj(("kind", OneOf("CALIBRATION", "INTEGRATION", "DOMINANCE", "PHASE")), ("run_id", Opt(Run)), ("evidence_id", Id), ("evidence_hash", AH),
        ("technical_status", Const("COMPLETED")), ("statistical_status", Opt(OneOf("PASS", "FAIL", "INSUFFICIENT_SAMPLE"))));

    public static readonly Shape SourceFile = Obj(("artifact_id", Id), ("role", OneOf(EvidenceShapes.ArtifactRoles)), ("unit_id", Opt(Exact)), ("game_id", Opt(Exact)), ("relative_path", RelPath),
        ("raw_sha256", H), ("byte_length", N), ("origin", OneOf("ORIGINAL", "VERIFIER_DERIVED")));

    public static readonly Shape Shape = Obj(
        ("format", Const(Format)), ("receipt_id", Uuid), ("validation_set_id", Id), ("approvals", EvidenceShapes.Approvals), ("contracts", EvidenceShapes.Contracts),
        ("verifier", Obj(("id", Id), ("version", Text1), ("source_git_sha", Git), ("binary_sha256", H))), ("build_provenance", IdRef), ("environment_profile", IdRef), ("pzl_test_profile", IdRef),
        ("execution_authorization_id", Id), ("test_only", Bool), ("verification_decision", Const("ELIGIBLE")), ("pzl_status", Const("REQUIRED_CHECKS_SATISFIED")),
        ("targets", Arr(Target, 1, 4)), ("checks", Arr(EvidenceShapes.Check)), ("source_files", Arr(SourceFile)), ("child_receipt_ids", Arr(Uuid, 0, 3)));

    private static EvidenceException Bad(string cause, string pointer, string message) => Schema.Bad(pointer, cause, message);

    /// <summary>Shape plus R-11: allowed statistical states per target kind, the target count and order, the Phase child ids, sorted unique source files without a receipt or registry record (§21).</summary>
    public static JObject Validate(ReadOnlySpan<byte> raw)
    {
        var r = ApprovalFormats.Read(Shape, raw);
        var targets = Items(r["targets"]).Select(Obj).ToList();
        var kinds = targets.Select(t => Text(t["kind"])).ToList();
        var children = Items(r["child_receipt_ids"]).Select(Text).ToList();
        var phase = kinds.Contains("PHASE");
        if (phase ? !kinds.SequenceEqual(["CALIBRATION", "INTEGRATION", "DOMINANCE", "PHASE"]) || children.Count != 3 || children.Distinct(StringComparer.Ordinal).Count() != 3
                  : targets.Count != 1 || children.Count != 0)
            throw Bad("Receipt.targets", "/targets", "Family 영수증은 target 1건·child 0건, Phase 영수증은 CAL·INT·DOM·PHASE 4건·서로 다른 child 3건입니다.");
        for (var i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            var stat = t["statistical_status"] is JString s ? s.Value : null;
            var ok = Text(t["kind"]) switch
            {
                "PHASE" => stat is null && IsNull(t["run_id"]),
                "DOMINANCE" => stat is "PASS" or "FAIL" && !IsNull(t["run_id"]),
                _ => stat is "PASS" or "FAIL" or "INSUFFICIENT_SAMPLE" && !IsNull(t["run_id"]),
            };
            if (!ok)
                throw Bad("Receipt.statistical_status", $"/targets/{i}", "target 종류별 허용 statistical_status·run_id가 아닙니다(R-11, N11).");
        }

        var files = Items(r["source_files"]).Select(Obj).ToList();
        for (var i = 0; i < files.Count; i++)
        {
            if (Text(files[i]["role"]) is "VERIFICATION_RECEIPT" or "REGISTRY_RECORD")
                throw Bad("Receipt.self_reference", $"/source_files/{i}", "영수증 source_files에 영수증 자신·기록부 항목을 넣지 않습니다.");
            if (i > 0 && string.CompareOrdinal(Text(files[i - 1]["artifact_id"]), Text(files[i]["artifact_id"])) >= 0)
                throw Bad("Schema.order", $"/source_files/{i}", "source_files는 artifact_id ordinal 순서·중복 금지입니다.");
        }

        return r;
    }

    /// <summary>
    /// §19: the target's (technical, statistical) status, or the reason v1 issues no receipt. Technical: every unit COMPLETED (an engine / metrics failure is never certified, A27). Statistical: Family
    /// PASS / FAIL, INSUFFICIENT_SAMPLE only with the approval's allowance; Dominance PASS / FAIL (INCOMPLETE / NOT_EVALUATED refused). A FAIL is issued as evidence of a failure (A26), never a Gate PASS.
    /// </summary>
    public static (string Technical, string Statistical) Eligibility(JObject document, bool insufficientSampleAllowed)
    {
        static EvidenceException No(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.NotCertifiable, cause, "", "certification", message);
        var units = Items(document["units"]).Select(Obj).ToList();
        if (units.Any(u => Text(Obj(u["technical"])["run_status"]) != "COMPLETED"))
            throw No("Certification.technical", "engine·metrics 실패가 있는 실행은 v1 정상 인증 대상이 아닙니다(§19).");
        var dominance = Text(Obj(document["schema"])["id"]) == EvidenceShapes.SchemaId(EvidenceKind.Dominance);
        var gate = Text(Obj(dominance ? Obj(document["aggregate_evaluation"])["gate"] : document["aggregate_evaluation"])["status"]);
        return gate switch
        {
            "PASS" or "FAIL" => ("COMPLETED", gate),
            "INSUFFICIENT_SAMPLE" when !dominance && insufficientSampleAllowed => ("COMPLETED", gate),
            _ => throw No("Certification.statistical", $"통계 상태 {gate}는 v1 인증 허용 범위가 아닙니다(§19, 허용 사유 목록 비어 있음)."),
        };
    }

    /// <summary>
    /// OWNER decision 3 §2: a receipt's RAW_INVENTORY — exactly one VERIFIER_DERIVED source file of that role — read through the archive snapshot (its SHA and length are the source file's), parsed by
    /// the closed reader, and equal to the receipt's ORIGINAL source files (origin removed): none missing, none added. Else <c>archive_invalid</c>.
    /// </summary>
    public static void CheckInventory(JObject receipt, string archiveRoot)
    {
        static EvidenceException Bad(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.ArchiveInvalid, cause, "/source_files", "receipt", message);
        static JObject Plain(JObject f) => new(f.Members.Where(m => m.Key != "origin").ToList());
        var files = Items(receipt["source_files"]).Select(Obj).ToList();
        var inventories = files.Where(f => Text(f["role"]) == "RAW_INVENTORY").ToList();
        if (inventories.Count != 1 || Text(inventories[0]["origin"]) != "VERIFIER_DERIVED")
            throw Bad("Receipt.inventory_missing", "영수증에는 VERIFIER_DERIVED RAW_INVENTORY가 정확히 1개 있어야 합니다.");
        var bytes = Archive.Snapshot(archiveRoot, [Plain(inventories[0])]).Values.Single().Bytes;              // SHA / length / pinned read
        var listed = Archive.ReadInventory(bytes).Select(x => Jcs.Text(x)).ToList();
        var originals = files.Where(f => Text(f["origin"]) == "ORIGINAL").Select(f => Jcs.Text(Plain(f))).Order(StringComparer.Ordinal).ToList();
        if (!listed.Order(StringComparer.Ordinal).SequenceEqual(originals, StringComparer.Ordinal))
            throw Bad("Receipt.inventory_mismatch", "RAW_INVENTORY가 영수증의 ORIGINAL source_files와 다릅니다(누락·추가).");
    }

    /// <summary>
    /// §19 PZL axis (B1-T11-01): every Unit PZL's required checks are satisfied — pzl-5 and pzl-12a PASS, pzl-10 PASS (or NOT_APPLICABLE, which the schema allows only for a NonAutomated unit),
    /// pzl-12b DEFERRED — and an Integration's #6 is PASS. A PZL FAIL or a NOT_EVALUATED (missing observation) is not certifiable in v1.
    /// </summary>
    public static void PzlEligibility(IReadOnlyList<JObject> unitDocs, JObject? pzl6)
    {
        static EvidenceException No(string where, string status) =>
            EvidenceCodes.Fail(EvidenceCodes.NotCertifiable, "Certification.pzl", where, "certification", $"{where}의 PZL 상태 {status}는 v1 인증 대상이 아닙니다(§19, 허용 목록 비어 있음).");
        foreach (var u in unitDocs)
        {
            var unit = Text(Obj(u["plan"])["unit_id"]);
            foreach (var c in Items(u["checks"]).Select(Obj))
            {
                var (id, status) = (Text(c["id"]), Text(c["status"]));
                var ok = id switch
                {
                    "pzl-12b" => status == "DEFERRED",
                    "pzl-10" => status is "PASS" or "NOT_APPLICABLE",
                    _ => status == "PASS",
                };
                if (!ok)
                    throw No($"{unit}/{id}", status);
            }
        }

        if (pzl6 is not null && Text(Obj(pzl6["check"])["status"]) is var s6 && s6 != "PASS")
            throw No("pzl-6", s6);
    }

    /// <summary>
    /// §20.2 steps 5-7. <paramref name="recheck"/> is E2-7 (the environment against C0 and the profile; null = conformant): on failure nothing is written or registered. A retry after a lost response reuses
    /// the receipt id and bytes (the existing file must be byte-identical, read through the pinned archive path). B1-T11-01: internal — the only production caller is <see cref="Certification"/>, which
    /// builds <paramref name="receipt"/> from a verified result and passes its own E2-7 re-capture; no caller can hand in a receipt or skip the re-check.
    /// </summary>
    internal static IssuedReceipt Issue(JObject receipt, string archiveRoot, IEvidenceRegistry registry, Func<string?> recheck)
    {
        var bytes = Jcs.Bytes(receipt);
        var r = Validate(bytes);
        if (recheck() is { } env)
            throw EvidenceCodes.Fail(EvidenceCodes.EnvironmentMismatch, "Environment.recheck", "", "issuance", $"발급 직전 환경이 C0·프로필과 다릅니다(E2-7): {env}");
        var id = Text(r["receipt_id"]);
        var rel = $"receipts/{id}.json";
        if (Archive.TryRead(archiveRoot, rel) is { } existing)
        {
            if (!existing.AsSpan().SequenceEqual(bytes))
                throw EvidenceCodes.Fail(EvidenceCodes.RegistrationConflict, "Receipt.file_conflict", "", "issuance", "같은 영수증 ID의 다른 파일이 있습니다(덮어쓰지 않음).");
        }
        else
            Archive.WriteNew(archiveRoot, rel, bytes, "receipt-" + id, "VERIFICATION_RECEIPT");

        long seq;
        try
        {
            seq = registry.Register("VERIFICATION_RECEIPT", id, bytes).Sequence;
        }
        catch (Exception e) when (e is not EvidenceException)                              // conflict / unauthorized stay what they are
        {
            throw EvidenceCodes.Fail(EvidenceCodes.RegistrationPending, "Registry.unavailable", "", "issuance", $"등록 성공 여부를 확인하지 못했습니다(인증 완료 아님): {e.Message}");
        }

        var back = registry.Get("VERIFICATION_RECEIPT", id);
        if (back is null || back.RawSha256 != Archive.Sha(bytes) || !back.RawBytes.AsSpan().SequenceEqual(bytes) || back.Sequence != seq)
            throw EvidenceCodes.Fail(EvidenceCodes.RegistrationConflict, "Registry.requery_mismatch", "", "issuance", "기록부 재조회 결과가 등록한 영수증과 다릅니다.");
        var certified = registry.Trusted && r["test_only"] is JBool { Value: false };
        return new IssuedReceipt(id, bytes, back.RawSha256, seq, certified ? ReceiptState.Certified : ReceiptState.TestOnlyRegistered);
    }
}
