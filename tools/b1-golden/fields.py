"""B1-T00 field-level traceability: every JSON path that occurs in the committed b1-golden documents, mapped to its contract clause, its check site and its tests.

Run (stdlib only, isolated):  python -I tools/b1-golden/fields.py  > docs/contracts/b1/B1_FIELD_TRACEABILITY.md

Paths come from real documents (TEST_ONLY world), so a field that is always null there is still listed with the types actually seen. The mapping is a longest-prefix table below; a path no rule
covers makes the script fail, so a new field cannot slip into a document without a clause.
"""
import json
import os
import sys

BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tests", "Fixtures", "b1-golden")

COMMON = [
    ("/schema", "§2.1", "`EvidenceRules.Header`, `EvidenceShapes.Contracts`", "OD (N06·N23·A11)"),
    ("/execution/evidence_id", "§2.2 R-12", "`EvidenceIds`, `EvidenceRules.Header`", "OD (N08)"),
    ("/execution", "§2.2·§2.4", "`EvidenceRules.Header`, `Verifier.BindApproval`", "OD (R-14), VT (A03)"),
    ("/approvals", "§2.3", "`Verifier.BindApproval`", "VT (A03), EH (A01)"),
    ("/additional_evidence/artifacts", "§2.5·§21", "`Archive.CheckReferences`", "AR (A18~A20), EH (A12)"),
    ("/additional_evidence", "§2.5", "`EvidenceRules.Header`", "OD, EH (A10)"),
]

KINDS = {
    "FAMILY": COMMON + [
        ("/plan", "§3.1", "`EvidenceRules.Validate` (Family Root), `Verifier.BindUnits`", "OD, VX (A04)"),
        ("/units", "§3.1 (Unit 순서 = plan.unit_ids)", "`EvidenceRules.Validate`, `Verifier.BindUnits`", "OD"),
        ("/units/*/inputs/attestation", "§3.3", "`EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare`", "OD, VT, VX (A04)"),
        ("/units/*/inputs", "§3.2", "`EvidenceShapes.UnitInputs`, `Verifier.BindUnits`", "OD, VX (A04)"),
        ("/units/*/technical/counters", "§3.5", "`EvidenceShapes.CounterFields`", "OD (A22)"),
        ("/units/*/technical/games/*/counters", "§3.5", "`EvidenceShapes.CounterFields`", "OD, CX (N01)"),
        ("/units/*/technical/games/*/box_score_cross_check", "§3.6", "`EvidenceRules.GameRow` (CrossCheck 15항목)", "OD (N03)"),
        ("/units/*/technical/games/*/regulation_half_innings", "§3.6 A행", "`EvidenceRules.GameRow`", "OD, CX (N02)"),
        ("/units/*/technical/games/*/regulation_error_code", "§3.6 A행", "`EvidenceRules.GameRow`", "OD, CX (N02)"),
        ("/units/*/technical/games/*/rng_hash", "§3.6 D행·§10.7", "`EvidenceRules.GameRow`, `OfficialDocuments.Game`", "OD, CX (N01·N33·N34)"),
        ("/units/*/technical/games/*/failure", "§3.6 B~D행", "`EvidenceRules.GameRow`", "OD (N03), CX (N33·N34)"),
        ("/units/*/technical/games", "§3.6", "`EvidenceRules.GameRow`, 재실행 비교", "OD (A06), VT (A06), CX (N01)"),
        ("/units/*/technical/stop", "§3.4", "`EvidenceRules.UnitResult`", "OD, VT (Calibration SEEDS_EXHAUSTED)"),
        ("/units/*/technical", "§3.4", "`EvidenceRules.UnitResult`, `OfficialDocuments.Technical`", "OD (A06), VT (A06)"),
        ("/units/*/evaluation/metrics", "§3.8", "`EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation`", "OD, GD (Calibration)"),
        ("/units/*/evaluation/gate", "§3.10", "`EvidenceRules.Evaluation`", "OD, VT (A05)"),
        ("/units/*/evaluation", "§3.7 R-5", "`EvidenceRules.Evaluation`", "OD"),
        ("/units/*/unit_id", "§3.1", "`EvidenceRules.Validate`", "OD"),
        ("/aggregate_evaluation", "§3.7·§3.10", "`EvidenceRules.Evaluation`, 재평가 `Verifier.Compare`", "OD, VT (A05)"),
    ],
    "DOMINANCE": COMMON + [
        ("/plan", "§4", "`EvidenceRules.DominanceRules`, `Verifier.BindUnits`", "OD, VX (A04)"),
        ("/units", "§4 (Cell 순서 = plan.cell_ids)", "`EvidenceRules.DominanceRules`, `Verifier.BindUnits`", "OD"),
        ("/units/*/cell", "§4 (카탈로그)", "`EvidenceRules.DominanceRules`", "OD (카탈로그)"),
        ("/units/*/inputs/attestation", "§3.3", "`EvidenceShapes.Attestation`", "OD, VT, VX (A04)"),
        ("/units/*/inputs", "§3.2", "`EvidenceShapes.UnitInputs`, `Verifier.BindUnits`", "OD, VX (A04)"),
        ("/units/*/technical/counters", "§3.5", "`EvidenceShapes.CounterFields`", "OD"),
        ("/units/*/technical/games/*/counters", "§3.5", "`EvidenceShapes.CounterFields`", "OD, CX (N01)"),
        ("/units/*/technical/games/*/box_score_cross_check", "§3.6", "`EvidenceRules.GameRow`", "OD (N03)"),
        ("/units/*/technical/games/*/regulation_half_innings", "§3.6 A행·§4", "`EvidenceRules.GameRow` (dominance)", "CX (N02)"),
        ("/units/*/technical/games/*/regulation_error_code", "§3.6 A행·§4", "`EvidenceRules.GameRow` (dominance)", "CX (N02)"),
        ("/units/*/technical/games/*/rng_hash", "§3.6 D행·§10.7", "`EvidenceRules.GameRow`", "CX (N01·N33·N34)"),
        ("/units/*/technical/games/*/failure", "§3.6 B~D행", "`EvidenceRules.GameRow`", "OD (N03), CX"),
        ("/units/*/technical/games", "§3.6", "`EvidenceRules.GameRow`, 재실행 비교", "VT (A06)"),
        ("/units/*/technical", "§3.4", "`EvidenceRules.UnitResult`", "VT (A06)"),
        ("/aggregate_evaluation/cells/*/sample", "§4·§8.4 (F64)", "`EvidenceRules.Side`, `EvidenceScalars.F64`", "OD (A24)"),
        ("/aggregate_evaluation/cells", "§4", "`EvidenceRules.DominanceRules`", "OD"),
        ("/aggregate_evaluation/batter", "§4 SideResult·R-7", "`EvidenceRules.Side`", "OD, VT"),
        ("/aggregate_evaluation/pitcher", "§4 SideResult·R-7", "`EvidenceRules.Side`", "OD, VT"),
        ("/aggregate_evaluation/frozen_games_per_cell", "§4 R-7", "`EvidenceRules.DominanceRules`", "OD (Schema.r7)"),
        ("/aggregate_evaluation", "§4", "`OfficialDocuments.DominanceAggregate`, 재평가 `Verifier.Compare`", "VT (A05)"),
    ],
    "UNIT_PZL": COMMON + [
        ("/plan", "§5", "`EvidenceRules.UnitPzlRules`, `Verifier.BindBundleHeader`", "UP"),
        ("/registered_streams", "§5", "`EvidenceRules.UnitPzlRules`", "UP"),
        ("/games/*/summary", "§5.2", "`UnitPzlEvidence.PzlGame`", "UP"),
        ("/games/*/final_rng_canonical", "§10.4", "`EvidenceRules.UnitPzlRules` (rng_boundary)", "UP, CX (N35)"),
        ("/games/*/rng_hash", "§10.4", "`EvidenceRules.UnitPzlRules` (rng_boundary)", "UP, CX (N33·N34)"),
        ("/games/*/events_digest", "§10.5", "`UnitPzlEvidence.CompareGame`", "UP (N14)"),
        ("/games", "§5.1", "`UnitPzlEvidence.CompareGame`", "UP (A07·A08·A09·N17)"),
        ("/checks/*/coverage", "§7 coverage (NSC-6)", "`UnitPzlEvidence.Coverage`, 재투영 `Verifier` (unit_pzl_differs)", "UP (N05-a·b·d), CX (N05-c·N05-e)"),
        ("/checks/*/deferral", "§7 Deferral", "`UnitPzlEvidence.Deferral`", "UP"),
        ("/checks/*/test_evidence", "§7 TestEvidence", "`EvidenceRules.UnitPzlRules`", "EH (A25-a·b)"),
        ("/checks", "§7·§11.1", "`UnitPzlEvidence.Checks` (`PzlChecks.Evaluate`)", "UP, EH"),
    ],
    "PZL6": COMMON + [
        ("/plan", "§6", "`EvidenceRules.Pzl6Rules`", "P6, EH (A10)"),
        ("/samples", "§6 (고정 20경기)", "`Pzl6Verifier.Check`", "P6"),
        ("/check/coverage", "§7 coverage", "`Pzl6Verifier.Check`", "EH (A10-a)"),
        ("/check", "§6·§7", "`Pzl6Verifier.Check`, `EvidenceRules.Pzl6Rules`", "P6, EH"),
    ],
}

FILES = [("cal/document.json", "FAMILY"), ("int/document.json", "FAMILY"), ("dom/document.json", "DOMINANCE"), ("int/pzl6.json", "PZL6")]


def walk(node, path, out):
    kind = "null" if node is None else "bool" if isinstance(node, bool) else "number" if isinstance(node, (int, float)) else "string" if isinstance(node, str) else "array" if isinstance(node, list) else "object"
    out.setdefault(path or "/", set()).add(kind)
    if isinstance(node, dict):
        for k, v in node.items():
            walk(v, f"{path}/{k}", out)
    elif isinstance(node, list):
        for v in node:
            walk(v, f"{path}/*", out)


def rule(rules, path):
    best = None
    for prefix, *rest in rules:
        if path == prefix or path.startswith(prefix + "/"):
            if best is None or len(prefix) > len(best[0]):
                best = (prefix, *rest)
    return best


def main():
    seen = {}
    for name, kind in FILES:
        with open(os.path.join(BASE, name), encoding="utf-8") as f:
            walk(json.load(f), "", seen.setdefault(kind, {}))
    for folder in ("cal", "int", "dom"):
        for name in sorted(os.listdir(os.path.join(BASE, folder))):
            if name.startswith("unit-"):
                with open(os.path.join(BASE, folder, name), encoding="utf-8") as f:
                    walk(json.load(f), "", seen.setdefault("UNIT_PZL", {}))
    lines = ["# B-1 필드 단위 Traceability (생성 문서)", "",
             "- 생성: `python -I tools/b1-golden/fields.py > docs/contracts/b1/B1_FIELD_TRACEABILITY.md` (입력: `tests/Fixtures/b1-golden/`의 TEST_ONLY 실제 문서). 손으로 고치지 않습니다.",
             "- 경로의 `*`는 배열 원소입니다. '관찰 타입'은 Golden 문서에 실제 나타난 JSON 타입이며, 계약상 허용 타입·Null 조건의 정본은 계약 본문과 `EvidenceShapes`입니다(관찰되지 않은 타입은 표에 없을 수 있음).",
             "- 시험 약어는 [B1_TRACEABILITY.md](B1_TRACEABILITY.md)와 같고, `CX` = `CounterexampleTests`(하네스), `VX` = `VerifierCounterexampleTests`(검증기), `BP`·`PR` = `BootstrapAndProfileTests`·`PipeRegistryTests`입니다.", ""]
    missing = []
    for kind in ("FAMILY", "DOMINANCE", "UNIT_PZL", "PZL6"):
        lines += [f"## {kind}", "", "| JSON 경로 | 관찰 타입 | 계약 조항 | 검사 위치 | 시험 |", "|---|---|---|---|---|"]
        for path in sorted(seen[kind]):
            if path == "/":
                continue
            r = rule(KINDS[kind], path)
            if r is None:
                missing.append(f"{kind} {path}")
                continue
            lines.append(f"| `{path}` | {', '.join(sorted(seen[kind][path]))} | {r[1]} | {r[2]} | {r[3]} |")
        lines.append("")
    if missing:
        sys.stderr.write("규칙 없는 경로:\n" + "\n".join(missing) + "\n")
        sys.exit(1)
    sys.stdout.buffer.write(("\n".join(lines)).encode("utf-8"))


main()
