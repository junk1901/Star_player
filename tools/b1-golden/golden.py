"""B1-T10 independent Golden (B1-T00 3부 3.1, S00 §8.2): a second implementation, written without the C# code, of
JCS (RFC 8785, keys sorted by UTF-16 code units), M(D) (§9.1), the Family / Dominance / Phase payloads (§9.2) and the
prefixed A+ (§9.3). It reads the FROZEN TEST_ONLY input documents in tests/Fixtures/b1-golden and writes expected.json.

    python -I tools/b1-golden/golden.py tests/Fixtures/b1-golden

No value here is an official Seed, Pin, SID or approval. Only integers and strings occur in the inputs (every F64 is
already a string), so the number rule needed is the integer one; anything else is refused.
"""
import hashlib
import json
import pathlib
import sys

HASH_CONTRACT = "simharness-evidence-hash-v1"


def jcs(value):
    if value is None:
        return "null"
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, int):
        if abs(value) > 2**53 - 1:
            raise ValueError("integer beyond 2^53-1")
        return str(value)
    if isinstance(value, float):
        raise ValueError("no binary64 number is expected in these inputs")
    if isinstance(value, str):
        return json.dumps(value, ensure_ascii=False)
    if isinstance(value, list):
        return "[" + ",".join(jcs(v) for v in value) + "]"
    if isinstance(value, dict):
        keys = sorted(value.keys(), key=lambda k: k.encode("utf-16-be"))
        return "{" + ",".join(json.dumps(k, ensure_ascii=False) + ":" + jcs(value[k]) for k in keys) + "}"
    raise TypeError(type(value))


def canonical(value):
    return jcs(value).encode("utf-8")


def aplus(prefix, data):
    return "sha256:" + hashlib.sha256(prefix.encode("ascii") + b"\n" + data).hexdigest()


def m(doc):
    out = dict(doc)
    extra = dict(out["additional_evidence"])
    del extra["artifacts"]
    out["additional_evidence"] = extra
    return out


def load(path):
    raw = path.read_bytes()
    return raw, json.loads(raw.decode("utf-8"))


def family_set(folder, prefix, with_pzl6):
    raw_doc, doc = load(folder / "document.json")
    units = []
    files = {"document.json": hashlib.sha256(raw_doc).hexdigest()}
    for p in sorted(folder.glob("unit-*.json")):
        raw, u = load(p)
        files[p.name] = hashlib.sha256(raw).hexdigest()
        units.append(m(u))
    payload = {"hash_contract": HASH_CONTRACT, "result": m(doc), "pzl_units": units}
    if with_pzl6:
        raw, pzl6 = load(folder / "pzl6.json")
        files["pzl6.json"] = hashlib.sha256(raw).hexdigest()
        payload["pzl6"] = m(pzl6)
    elif prefix.endswith("FAMILY-V1"):
        payload["pzl6"] = None
    c = canonical(payload)
    return payload, {"raw_sha256": files, "payload_bytes": len(c), "payload_sha256": hashlib.sha256(c).hexdigest(), "a_plus": aplus(prefix, c)}


FIELDS = ["completed_games", "events", "pitches", "called_pitch_steps"]


def automated(family, unit_id):
    if family == "fc3-cal":
        return unit_id in ("L1", "L3")
    if family == "fc3-int":
        return unit_id == "automated"
    return unit_id[5] == "A"   # DOM-{hand}{mode}-{batter}{pitcher}


def phase_set(children):
    """The Phase root over the three real child payloads; coverage per §7 recomputed here from the children's Unit PZL documents."""
    cal, integ, dom = (p for _, p, _ in children)
    units = [u for p in (cal, integ, dom) for u in p["pzl_units"]]

    def total(us):
        return [sum(u["checks"][0]["coverage"][f] for u in us) for f in FIELDS]

    allc = total(units)
    autoc = total([u for u in units if automated(u["execution"]["family"], u["plan"]["unit_id"])])
    six = [integ["pzl6"]["check"]["coverage"][f] for f in FIELDS]
    test_evidence = {"profile_id": "pzl-profile-test", "profile_sha256": "d" * 64, "source_git_sha": cal["result"]["execution"]["git_commit_sha"], "cases": []}
    checks = []
    for cid in ["pzl-1", "pzl-2", "pzl-3", "pzl-4", "pzl-5", "pzl-6", "pzl-7", "pzl-8", "pzl-9", "pzl-10", "pzl-11", "pzl-12a", "pzl-12b"]:
        cov = allc if cid in ("pzl-1", "pzl-2", "pzl-3", "pzl-5", "pzl-12a") else autoc if cid == "pzl-10" else six if cid == "pzl-6" else [0, 0, 0, 0]
        needs = cid in ("pzl-4", "pzl-6", "pzl-7", "pzl-8", "pzl-9", "pzl-10", "pzl-11", "pzl-12a")
        checks.append({"id": cid, "status": "DEFERRED" if cid == "pzl-12b" else "PASS", "reason": "golden fixture check", "coverage": dict(zip(FIELDS, cov)), "violations": [],
                       "test_evidence": test_evidence if needs else None,
                       "deferral": {"code": "DEFERRED_TO_PHASE_5_6_28", "bcl_phases": [5, 6, 28], "lmv_a_phases": [5, 28]} if cid == "pzl-12b" else None})
    first = cal["result"]
    payload = {"hash_contract": HASH_CONTRACT, "contracts": first["schema"]["contracts"], "validation_set_id": first["execution"]["validation_set_id"], "approvals": first["approvals"],
               "children": [{"family": fam, "run_id": p["result"]["execution"]["run_id"], "evidence_id": p["result"]["execution"]["evidence_id"], "evidence_hash": h["a_plus"]}
                            for fam, p, h in children],
               "checks": checks}
    c = canonical(payload)
    return {"checks": checks}, {"payload_bytes": len(c), "payload_sha256": hashlib.sha256(c).hexdigest(), "a_plus": aplus("STARPLAYER-APLUS-PHASE-V1", c)}

def main(root):
    root = pathlib.Path(root)
    cal = family_set(root / "cal", "STARPLAYER-APLUS-FAMILY-V1", False)
    integ = family_set(root / "int", "STARPLAYER-APLUS-FAMILY-V1", True)
    dom = family_set(root / "dom", "STARPLAYER-APLUS-DOMINANCE-V1", False)
    phase_inputs, phase = phase_set([("fc3-cal", cal[0], cal[1]), ("fc3-int", integ[0], integ[1]), ("fc3-dom", dom[0], dom[1])])
    (root / "phase-input.json").write_bytes(canonical(phase_inputs))
    gv_c02 = canonical({"max": "1.7976931348623157e+308", "min": "5e-324", "negative_zero": "0"})
    expected = {
        "generator": "tools/b1-golden/golden.py (independent Python implementation)",
        "status": "TEST_ONLY Golden; schema-valid inputs; not official evidence",
        "calibration": cal[1], "integration": integ[1], "dominance": dom[1], "phase": phase,
        "gv_c02": {"dominance": aplus("STARPLAYER-APLUS-DOMINANCE-V1", gv_c02), "phase": aplus("STARPLAYER-APLUS-PHASE-V1", gv_c02)},
    }
    (root / "expected.json").write_bytes(canonical(expected))
    print(json.dumps({k: v["a_plus"] for k, v in expected.items() if isinstance(v, dict) and "a_plus" in v}, indent=1))

if __name__ == "__main__":
    main(sys.argv[1])
