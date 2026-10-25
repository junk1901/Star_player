using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.EvidenceVerifier;

/// <summary>
/// T06b Phase root (§9.2, §11.1-§11.2, §7): the 13 checks made by the verifier itself from the three VERIFIED families and its own Test Profile evidence, and the Phase payload. #1 and #5/#10/#12a are the
/// observation results of the verification; #2 / #3 are recomputed from the verified documents; #4, #7, #8, #9, #11 are the Test Profile; #6 is the verified #6 document plus the Test Profile; #12b is
/// always DEFERRED. Coverage follows §7 (NSC-6); a zero coverage is never a reason by itself (N36-b).
/// </summary>
public static class PhaseVerifier
{
    private static readonly string[] Ids = EvidenceHash.PhaseCheckIds;

    private static JObject Cov(long[] v) => J.O(("completed_games", J.N(v[0])), ("events", J.N(v[1])), ("pitches", J.N(v[2])), ("called_pitch_steps", J.N(v[3])));

    private static long[] Sum(IEnumerable<JObject> units) =>
        EvidenceShapes.CoverageFields.Select(f => units.Sum(u => Long(Obj(Obj(Items(u["checks"])[0])["coverage"])[f]))).ToArray();

    private static string Status(JObject unit, string check) => Text(Obj(Items(unit["checks"]).Select(Obj).Single(c => Text(c["id"]) == check))["status"]);

    public static IReadOnlyList<JObject> Checks(VerifiedFamily cal, VerifiedFamily integration, VerifiedFamily dom, IReadOnlyDictionary<string, JObject> testEvidence)
    {
        var families = new[] { cal, integration, dom };
        var units = families.SelectMany(f => f.PzlUnits).ToList();
        var docs = families.Select(f => f.Document).ToList();
        var all = Sum(units);
        var automated = units.Where(u => EvidenceRules.ModeOf(Text(Obj(u["execution"])["family"]), Text(Obj(u["plan"])["unit_id"])) == "Automated").ToList();
        var six = EvidenceShapes.CoverageFields.Select(f => Long(Obj(Obj(integration.Pzl6!["check"])["coverage"])[f])).ToArray();
        long[] zero = [0, 0, 0, 0];
        var technical = docs.SelectMany(d => Items(d["units"]).Select(u => Obj(Obj(u)["technical"]))).ToList();

        (string, string) Observed(bool ok, string pass, string fail) => ok ? ("PASS", pass) : ("FAIL", fail);
        JObject Te(string id) => testEvidence.TryGetValue(id, out var te) ? te : throw EvidenceCodes.Fail(EvidenceCodes.TestProfileMismatch, "TestProfile.check_missing", "", "phase", $"{id}의 Test Profile 실행 결과가 없습니다(§12.3).");

        var located = technical.All(t => Long(Obj(t["counters"])["unlocated_pitches"]) == 0 && Long(Obj(t["counters"])["located_pitches"]) == Long(Obj(t["counters"])["pitches"]));
        var pitchesCross = technical.SelectMany(t => Items(t["games"]).Select(Obj)).Where(g => g["box_score_cross_check"] is JObject)
            .All(g => Items(Obj(g["box_score_cross_check"])["items"]).Select(Obj).Where(i => Text(i["name"]) == "Pitches").All(i => Long(i["from_events"]) == Long(i["from_box_score"])));
        var rows = new List<JObject>();
        foreach (var id in Ids)
        {
            (string Status, string Reason) s;
            long[] cov;
            JNode te = JNode.Null, deferral = JNode.Null;
            switch (id)
            {
                case "pzl-1":
                    (s, cov) = (("PASS", "세 Family의 모든 경기를 승인 입력으로 재실행해 사건·위치·H0/H1/H2·최종 RNG·기록과 events_digest가 일치했습니다."), all);
                    break;
                case "pzl-2":
                    (s, cov) = (Observed(located, "모든 완료 경기의 투구가 위치를 가지며 located_pitches = pitches, unlocated_pitches = 0입니다.", "위치 없는 투구가 있습니다."), all);
                    break;
                case "pzl-3":
                    (s, cov) = (Observed(pitchesCross, "CrossCheck Pitches 항목이 모든 경기에서 사건 집계와 BoxScore가 같습니다(in_play 1건 = 투구 1개).", "CrossCheck Pitches가 다른 경기가 있습니다."), all);
                    break;
                case "pzl-5":
                    (s, cov) = (Observed(units.All(u => Status(u, "pzl-5") == "PASS"), "모든 Unit PZL의 pzl-5가 PASS입니다.", "pzl-5가 PASS가 아닌 Unit이 있습니다."), all);
                    break;
                case "pzl-10":
                    te = Te(id);
                    (s, cov) = (Observed(automated.All(u => Status(u, "pzl-10") == "PASS") && TestProfiles.AllPass((JObject)te), "Automated Unit의 pzl-10 PASS와 NonAutomated 양성 대조 시험이 통과했습니다.",
                        "Automated Unit pzl-10 또는 시험 Case가 통과하지 않았습니다."), Sum(automated));
                    break;
                case "pzl-12a":
                    te = Te(id);
                    (s, cov) = (Observed(units.All(u => Status(u, "pzl-12a") == "PASS") && TestProfiles.AllPass((JObject)te), "모든 Unit의 pzl-12a PASS와 등록 집합 = 고정 집합 시험이 통과했습니다.",
                        "pzl-12a 또는 시험 Case가 통과하지 않았습니다."), all);
                    break;
                case "pzl-6":
                    te = Te(id);
                    (s, cov) = (Observed(Text(Obj(integration.Pzl6!["check"])["status"]) == "PASS" && TestProfiles.AllPass((JObject)te), "공식 고정 20경기 #6 PASS와 Legacy 코퍼스 시험이 통과했습니다.",
                        "#6 또는 Legacy 시험 Case가 통과하지 않았습니다."), six);
                    break;
                case "pzl-12b":
                    (s, cov, deferral) = (("DEFERRED", "BCL/LMV 기능 미구현: DEFERRED_TO_PHASE_5_6_28 (PASS로 기록하지 않음)."), zero, UnitPzlEvidence.Deferral());
                    break;
                default:                                                                        // #4 #7 #8 #9 #11: the Test Profile alone
                    te = Te(id);
                    (s, cov) = (Observed(TestProfiles.AllPass((JObject)te), "필수 시험 Case가 모두 통과했습니다(검증기 직접 실행).", "실패한 필수 시험 Case가 있습니다."), zero);
                    break;
            }

            var failed = te is JObject e ? Items(e["cases"]).Select(Obj).Where(c => Text(c["status"]) == "FAIL").Select(c => ((string?)null, (string?)null, (string?)Text(c["id"]), "case_failed")).ToList() : [];
            rows.Add(J.O(("id", J.S(id)), ("status", J.S(s.Status)), ("reason", J.S(s.Reason)), ("coverage", Cov(cov)), ("violations", J.A(UnitPzlEvidence.Sorted(failed))),
                ("test_evidence", te), ("deferral", deferral)));
        }

        return rows;
    }

    public static EvidencePayload Payload(VerifiedFamily cal, VerifiedFamily integration, VerifiedFamily dom, IReadOnlyDictionary<string, JObject> testEvidence) =>
        EvidenceHash.Phase(cal.Payload, integration.Payload, dom.Payload, Checks(cal, integration, dom, testEvidence));
}
