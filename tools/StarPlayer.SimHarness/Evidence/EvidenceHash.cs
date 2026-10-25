using System.Security.Cryptography;
using System.Text;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>A canonical payload and its A+ (<c>sha256:…</c>, §9.3).</summary>
public sealed record EvidencePayload(string Kind, JObject Payload, byte[] Canonical, string Hash);

/// <summary>
/// T07: the semantic projection <c>M(D)</c> (§9.1), the four payloads (§9.2), their parent / child binding and the prefixed hash (§9.3). Inputs are documents that already passed
/// <see cref="EvidenceRules"/>; nothing unverified is hashed. A pure function: no clock, no file, no self reference.
/// </summary>
public static class EvidenceHash
{
    public const string FamilyPrefix = "STARPLAYER-APLUS-FAMILY-V1", DominancePrefix = "STARPLAYER-APLUS-DOMINANCE-V1", PhasePrefix = "STARPLAYER-APLUS-PHASE-V1";

    public static string APlus(string prefix, byte[] canonical)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        h.AppendData(Encoding.ASCII.GetBytes(prefix + "\n"));
        h.AppendData(canonical);
        return "sha256:" + Convert.ToHexString(h.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>§9.1: the document without <c>additional_evidence.artifacts</c> — and nothing else removed or added.</summary>
    public static JObject M(JObject doc) => new(doc.Members.Select(m => m.Key == "additional_evidence"
        ? new KeyValuePair<string, JNode>(m.Key, new JObject(((JObject)m.Value).Members.Where(x => x.Key != "artifacts").ToList()))
        : m).ToList());

    private static EvidenceException Bind(string code, string cause, string pointer, string message) => EvidenceCodes.Fail(code, cause, pointer, "payload", message);

    private static string T(JObject o, string pointer) => Text(Walk(o, pointer));

    private static JNode Walk(JNode n, string pointer)
    {
        foreach (var t in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
            n = n is JObject o ? o[t] : Items(n)[int.Parse(t, System.Globalization.CultureInfo.InvariantCulture)];
        return n;
    }

    /// <summary>The shared header binding of a child to its parent (§2.4 S3-S5 seen from both, R-1 identical contracts bytes).</summary>
    private static void SameHeader(JObject parent, JObject child, string where)
    {
        if (!Jcs.Bytes(Walk(parent, "schema/contracts")).AsSpan().SequenceEqual(Jcs.Bytes(Walk(child, "schema/contracts"))))
            throw Bind(EvidenceCodes.ContractNotApproved, "Evidence.contracts_differ", $"{where}/schema/contracts", "하나의 Payload 안 모든 문서의 contracts는 바이트까지 같습니다(R-1).");
        foreach (var f in new[] { "validation_set_id", "run_id", "family", "git_commit_sha", "build_provenance_id" })
            if (T(parent, $"execution/{f}") != T(child, $"execution/{f}"))
                throw Bind(EvidenceCodes.ApprovalMismatch, "Evidence.child_execution_mismatch", $"{where}/execution/{f}", $"자식 문서의 execution.{f}가 부모와 다릅니다(다른 검증 집합·실행 혼합, A01·A02).");
        if (!Jcs.Bytes(parent["approvals"]).AsSpan().SequenceEqual(Jcs.Bytes(child["approvals"])))
            throw Bind(EvidenceCodes.ApprovalMismatch, "Evidence.child_approvals_mismatch", $"{where}/approvals", "자식 문서의 승인 참조가 부모와 다릅니다.");
    }

    /// <summary>The Unit PZL documents bound to the parent's units: id, unit, ConfigHash, RunResultHash (§9.2 결속 조건).</summary>
    private static List<JNode> Units(JObject parent, IReadOnlyList<JObject> pzl, bool dominance)
    {
        var ids = Items(Walk(parent, "additional_evidence/pzl_unit_ids")).Select(Text).ToList();
        var units = Items(parent["units"]).Select(Obj).ToList();
        if (pzl.Count != ids.Count)
            throw Bind(EvidenceCodes.ReferenceInvalid, "Evidence.pzl_units_count", "/pzl_units", $"Unit PZL 문서 {pzl.Count}개가 부모 Unit {ids.Count}개와 다릅니다(누락·중복).");
        var list = new List<JNode>();
        for (var i = 0; i < pzl.Count; i++)
        {
            var where = $"/pzl_units/{i}";
            SameHeader(parent, pzl[i], where);
            var unitId = dominance ? T(units[i], "cell/id") : T(units[i], "unit_id");
            if (T(pzl[i], "execution/evidence_id") != ids[i] || T(pzl[i], "plan/unit_id") != unitId)
                throw Bind(EvidenceCodes.ReferenceInvalid, "Evidence.pzl_unit_identity", where, $"{i + 1}번째 Unit PZL 문서가 부모 Unit {unitId}의 문서가 아닙니다(순서·교체).");
            if (T(pzl[i], "plan/config_hash") != T(units[i], "inputs/attestation/config_hash")
                || Walk(pzl[i], "plan/run_result_version") is not JString { Value: var v } || v != T(units[i], "technical/run_result_version")
                || Walk(pzl[i], "plan/run_result_hash") is not JString { Value: var h } || h != T(units[i], "technical/run_result_hash"))
                throw Bind(EvidenceCodes.HashMismatch, "Evidence.pzl_unit_hash_binding", where, $"{unitId}: Unit PZL의 ConfigHash·RunResultHash가 부모와 다릅니다(MISMATCH-01 정상 인증 조건).");
            var games = Items(Walk(units[i], "technical/games")).Select(Obj).ToList();
            var rows = Items(pzl[i]["games"]).Select(Obj).ToList();
            if (rows.Count != games.Count)
                throw Bind(EvidenceCodes.PzlIncomplete, "Evidence.pzl_games_count", $"{where}/games", $"{unitId}: Unit PZL games가 부모의 실행 경기 {games.Count}개와 다릅니다(N05).");
            for (var k = 0; k < rows.Count; k++)
                if (T(rows[k], "game_id") != T(games[k], "game_id") || T(rows[k], "seed") != T(games[k], "seed") || T(rows[k], "status") != T(games[k], "status")
                    || T(rows[k], "game_result_hash") != T(games[k], "result_hash") || T(rows[k], "event_hash") != T(games[k], "event_hash")
                    || Jcs.Text(rows[k]["rng_hash"]) != Jcs.Text(games[k]["rng_hash"]) || Long(rows[k]["committed_events"]) != Long(games[k]["committed_events"]))
                    throw Bind(EvidenceCodes.PzlMismatch, "Evidence.pzl_game_binding", $"{where}/games/{k}", $"{unitId}: {k + 1}번째 경기의 식별·Hash가 부모 기술 결과와 다릅니다(A07).");
            list.Add(M(pzl[i]));
        }

        return list;
    }

    private static void Pzl6Binding(JObject parent, JObject pzl6)
    {
        SameHeader(parent, pzl6, "/pzl6");
        if (T(pzl6, "execution/evidence_id") != Text(Walk(parent, "additional_evidence/pzl6_id")) || T(pzl6, "plan/integration_evidence_id") != T(parent, "execution/evidence_id"))
            throw Bind(EvidenceCodes.ReferenceInvalid, "Evidence.pzl6_identity", "/pzl6", "#6 문서가 이 Integration 증거의 #6이 아닙니다.");
        var units = Items(parent["units"]).Select(Obj).ToList();
        var samples = Items(pzl6["samples"]).Select(Obj).ToList();
        for (var i = 0; i < samples.Count; i++)
        {
            var unit = units[i < 10 ? 0 : 1];
            var k = (int)Long(samples[i]["index"]) - 1;
            var spec = Obj(Items(Walk(unit, "inputs/games"))[k]);
            var game = Obj(Items(Walk(unit, "technical/games"))[k]);
            if (T(samples[i], "game_id") != T(spec, "game_id") || T(samples[i], "seed") != T(spec, "seed") || T(samples[i], "game_result_hash") != T(game, "result_hash")
                || T(samples[i], "event_hash") != T(game, "event_hash") || Jcs.Text(samples[i]["rng_hash"]) != Jcs.Text(game["rng_hash"]))
                throw Bind(EvidenceCodes.PzlMismatch, "Evidence.pzl6_sample_binding", $"/pzl6/samples/{i}", $"#6 {i + 1}번째 표본이 승인 Integration 계획의 경기와 다릅니다(A10).");
        }
    }

    public static EvidencePayload Family(JObject family, IReadOnlyList<JObject> pzlUnits, JObject? pzl6)
    {
        var integration = T(family, "execution/family") == "fc3-int";
        if (integration != pzl6 is not null)
            throw Bind(EvidenceCodes.ReferenceInvalid, "Evidence.pzl6_presence", "/pzl6", "#6 문서는 Integration에만 있고 반드시 있습니다.");
        var units = Units(family, pzlUnits, dominance: false);
        if (pzl6 is not null)
            Pzl6Binding(family, pzl6);
        var payload = J.O(("hash_contract", J.S(EvidenceShapes.HashContract)), ("result", M(family)), ("pzl_units", J.A(units)), ("pzl6", pzl6 is null ? JNode.Null : M(pzl6)));
        var c = Jcs.Bytes(payload);
        return new EvidencePayload(integration ? "INTEGRATION" : "CALIBRATION", payload, c, APlus(FamilyPrefix, c));
    }

    public static EvidencePayload Dominance(JObject dominance, IReadOnlyList<JObject> pzlUnits)
    {
        var payload = J.O(("hash_contract", J.S(EvidenceShapes.HashContract)), ("result", M(dominance)), ("pzl_units", J.A(Units(dominance, pzlUnits, dominance: true))));
        var c = Jcs.Bytes(payload);
        return new EvidencePayload("DOMINANCE", payload, c, APlus(DominancePrefix, c));
    }

    public static readonly string[] PhaseCheckIds = ["pzl-1", "pzl-2", "pzl-3", "pzl-4", "pzl-5", "pzl-6", "pzl-7", "pzl-8", "pzl-9", "pzl-10", "pzl-11", "pzl-12a", "pzl-12b"];

    /// <summary>
    /// §9.2 Phase Root: the three children in the order Calibration → Integration → Dominance, all of one validation set and one approval, with distinct run ids (§13.3; the same Git SHA alone is never
    /// "the same set", A02), and the 13 Phase checks (R-9 statuses and test_evidence, §7 coverage scope). Children are the A+ of the given child payloads.
    /// </summary>
    public static EvidencePayload Phase(EvidencePayload cal, EvidencePayload integration, EvidencePayload dom, IReadOnlyList<JObject> checks)
    {
        var children = new[] { cal, integration, dom };
        string[] kinds = ["CALIBRATION", "INTEGRATION", "DOMINANCE"];
        string[] families = ["fc3-cal", "fc3-int", "fc3-dom"];
        var results = children.Select(c => (JObject)c.Payload["result"]).ToList();
        for (var i = 0; i < 3; i++)
            if (children[i].Kind != kinds[i] || T(results[i], "execution/family") != families[i])
                throw Bind(EvidenceCodes.ReferenceInvalid, "Evidence.phase_child_role", $"/children/{i}", "자식은 Calibration → Integration → Dominance 순서의 세 Family입니다(누락·중복·역할 교체 거부).");
        for (var i = 1; i < 3; i++)
        {
            if (T(results[i], "execution/validation_set_id") != T(results[0], "execution/validation_set_id")
                || !Jcs.Bytes(results[i]["approvals"]).AsSpan().SequenceEqual(Jcs.Bytes(results[0]["approvals"]))
                || !Jcs.Bytes(Walk(results[i], "schema/contracts")).AsSpan().SequenceEqual(Jcs.Bytes(Walk(results[0], "schema/contracts"))))
                throw Bind(EvidenceCodes.ApprovalMismatch, "Evidence.phase_mixed_set", $"/children/{i}", "세 자식이 같은 검증 집합·승인·계약 조합이 아닙니다(A01·A02).");
        }

        if (results.Select(r => T(r, "execution/run_id")).Distinct(StringComparer.Ordinal).Count() != 3)
            throw Bind(EvidenceCodes.ApprovalMismatch, "Evidence.phase_run_id_reused", "/children", "세 자식의 run_id가 서로 달라야 합니다(§13.3).");
        PhaseChecks(checks, results, children);
        var first = results[0];
        var payload = J.O(
            ("hash_contract", J.S(EvidenceShapes.HashContract)), ("contracts", Walk(first, "schema/contracts")), ("validation_set_id", Walk(first, "execution/validation_set_id")),
            ("approvals", first["approvals"]),
            ("children", J.A(children.Select((c, i) => (JNode)J.O(("family", J.S(families[i])), ("run_id", Walk(results[i], "execution/run_id")),
                ("evidence_id", Walk(results[i], "execution/evidence_id")), ("evidence_hash", J.S(c.Hash)))))),
            ("checks", J.A(checks)));
        var bytes = Jcs.Bytes(payload);
        return new EvidencePayload("PHASE", payload, bytes, APlus(PhasePrefix, bytes));
    }

    /// <summary>R-9 statuses / test_evidence of the 13 Phase checks and the §7 coverage scope (NSC-6) computed from the children's Unit PZL coverage; no "coverage 0 forbidden" rule (N36-b).</summary>
    internal static void PhaseChecks(IReadOnlyList<JObject> checks, IReadOnlyList<JObject> results, IReadOnlyList<EvidencePayload> children)
    {
        if (checks.Count != 13)
            throw Bad("/checks", "Schema.array_length", "Phase checks는 13개(#1~#11, #12a, #12b)입니다.");
        var units = children.SelectMany(c => Items(c.Payload["pzl_units"]).Select(Obj)).ToList();
        if (units.Count != 54)
            throw Bind(EvidenceCodes.ReferenceInvalid, "Evidence.phase_units", "/children", $"전체 Unit PZL은 54개입니다(받은 수 {units.Count}).");
        var pzl6 = Obj(children[1].Payload["pzl6"]);
        long[] Sum(IEnumerable<JObject> us) => EvidenceShapes.CoverageFields.Select(f => us.Sum(u => Long(Walk(Obj(Items(u["checks"])[0]), $"coverage/{f}")))).ToArray();
        var all = Sum(units);
        var automated = Sum(units.Where(u => EvidenceRules.ModeOf(T(u, "execution/family"), T(u, "plan/unit_id")) == "Automated"));
        var six = EvidenceShapes.CoverageFields.Select(f => Long(Walk(Obj(pzl6["check"]), $"coverage/{f}"))).ToArray();
        long[] zero = [0, 0, 0, 0];
        for (var i = 0; i < 13; i++)
        {
            var c = checks[i];
            var ptr = $"/checks/{i}";
            EvidenceShapes.Check(c, ptr);
            var id = Text(c["id"]);
            if (id != PhaseCheckIds[i])
                throw Bad($"{ptr}/id", "Schema.order", $"Phase checks 순서는 {string.Join(", ", PhaseCheckIds)}입니다.");
            var status = Text(c["status"]);
            var needsTest = id is "pzl-4" or "pzl-6" or "pzl-7" or "pzl-8" or "pzl-9" or "pzl-10" or "pzl-11" or "pzl-12a";
            if (id == "pzl-12b" ? status != "DEFERRED" || IsNull(c["deferral"]) : status is not ("PASS" or "FAIL") || !IsNull(c["deferral"]))
                throw Bad($"{ptr}/status", "Schema.r9", $"Phase {id}에 status '{status}'는 허용되지 않습니다(R-9).");
            if (needsTest == IsNull(c["test_evidence"]))
                throw Bad($"{ptr}/test_evidence", "Schema.r9", $"Phase {id}의 test_evidence는 {(needsTest ? "필수" : "null")}입니다(R-9).");
            EvidenceRules.Violations(c, ptr);
            var expected = id switch
            {
                "pzl-1" or "pzl-2" or "pzl-3" or "pzl-5" or "pzl-12a" => all,
                "pzl-10" => automated,
                "pzl-6" => six,
                _ => zero,
            };
            var got = EvidenceShapes.CoverageFields.Select(f => Long(Walk(c, $"coverage/{f}"))).ToArray();
            if (!got.SequenceEqual(expected))
                throw Bind(EvidenceCodes.PzlMismatch, "Evidence.coverage_scope", $"{ptr}/coverage", $"Phase {id}의 coverage가 §7 범위의 중복 없는 집계가 아닙니다(A10-a·A25-a·A25-b).");
        }
    }
}
