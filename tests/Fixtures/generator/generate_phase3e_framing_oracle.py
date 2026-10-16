"""Independent oracle for Phase 3-E Package A: the `catcher.framing` modifier of the `called_pitch` stage under prob-model-v3 / tuning-virtual-proto-v3.

Shares NO code with src/StarPlayer.Domain, Simulation or Infrastructure. The arithmetic is written from ADR-006 item 8 and the numbers of content/tuning/virtual_tuning_v3.json,
whose `called_pitch` baseline rows and `catcher.framing` modifier it reads directly. The boundary class of a zone is written HERE from the rule of docs/game-engine.md section 18
(NOT read from the C# zone model): a region that shares an edge with the strike-zone outline is Borderline (the eight non-centre inner cells and the four outer zones that border
an edge: high, in, low, out); the centre cell and the four diagonal outer zones (high_in, low_in, low_out, high_out) are Clear.

  applies   = mode == "non_automated" and zone is Borderline            (Automated: never; non_automated Clear: never)
  factor    = clamp(1_000_000 + (framing - 50) * 1000, 950_000, 1_050_000) if applies else 1_000_000
  combined  : running = 1_000_000; if factor != 1_000_000: running = (running * factor + 500_000) // 1_000_000; clamp to [combined_min, combined_max]; ONLY the `strike` outcome is targeted
  weights   = baseline * combined; ONE normalisation to 1_000_000 by largest remainder, lowest index first.

Run:  python tests/Fixtures/generator/generate_phase3e_framing_oracle.py
Writes tests/Fixtures/phase3e_framing_oracle.json (deterministic).
"""
import json
import os

ROLL = 1_000_000
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))

tuning = json.load(open(os.path.join(ROOT, "content", "tuning", "virtual_tuning_v3.json"), encoding="utf-8"))
assert tuning["model_version"] == "prob-model-v3" and tuning["tuning_version"] == "tuning-virtual-proto-v3"
BASE = {(b["stage"], b["condition"]): (b["outcomes"], b["ppm"]) for b in tuning["baselines"]}
FRAMING = next(m for m in tuning["modifiers"] if m["slot"] == "catcher.framing")
CMIN, CMAX = tuning["combined_min_ppm"], tuning["combined_max_ppm"]
assert (FRAMING["neutral"], FRAMING["slope_ppm_per_unit"], FRAMING["min_ppm"], FRAMING["max_ppm"]) == (50, 1000, 950_000, 1_050_000)

INNER = [f"in.r{r}c{c}" for r in (1, 2, 3) for c in (1, 2, 3)]
OUTER = ["out.high", "out.high_in", "out.in", "out.low_in", "out.low", "out.low_out", "out.out", "out.high_out"]
ZONES = INNER + OUTER
CLEAR = {"in.r2c2", "out.high_in", "out.low_in", "out.low_out", "out.high_out"}
assert len(ZONES) == 17 and len(CLEAR) == 5


def boundary(zone):
    return "Clear" if zone in CLEAR else "Borderline"


def normalize(weights, total=ROLL):
    s = sum(weights)
    q = [w * total // s for w in weights]
    r = [w * total % s for w in weights]
    left = total - sum(q)
    for i in sorted((i for i in range(len(weights)) if r[i] > 0), key=lambda i: (-r[i], i))[:left]:
        q[i] += 1
    assert sum(q) == total
    return q


def called(mode, zone, framing):
    outcomes, base = BASE[("called_pitch", f"{mode}:{zone}")]
    assert outcomes == ["ball", "strike"]
    applies = mode == "non_automated" and boundary(zone) == "Borderline"
    f = max(FRAMING["min_ppm"], min(FRAMING["max_ppm"], 1_000_000 + (framing - FRAMING["neutral"]) * FRAMING["slope_ppm_per_unit"])) if applies else 1_000_000
    running = 1_000_000
    if f != 1_000_000:
        running = (running * f + 500_000) // 1_000_000
    combined = [1_000_000, max(CMIN, min(CMAX, running))]
    final = normalize([b * c for b, c in zip(base, combined)])
    for b_, f_ in zip(base, final):
        assert (b_ > 0) == (f_ > 0), "a positive outcome vanished or a zero appeared"
    return {"mode": mode, "zone": zone, "boundary": boundary(zone), "framing": framing, "applied": applies, "factor": f if applies else 1_000_000, "baseline": base, "ppm": final}


cases = []
for mode in ("automated", "non_automated"):
    for zone in ZONES:
        for framing in (0, 50, 100):
            cases.append(called(mode, zone, framing))
# extra ratings on every Borderline non_automated zone (slope and rounding, not only the three corners)
for zone in ZONES:
    for framing in (1, 10, 25, 49, 51, 75, 90, 99):
        cases.append(called("non_automated", zone, framing))

doc = {
    "model_version": "prob-model-v3", "tuning_version": "tuning-virtual-proto-v3",
    "classification": {z: boundary(z) for z in ZONES},
    "cases": cases,
}
out = os.path.join(ROOT, "tests", "Fixtures", "phase3e_framing_oracle.json")
with open(out, "w", encoding="utf-8", newline="\n") as f:
    json.dump(doc, f, indent=1, sort_keys=True)
    f.write("\n")
print(f"{len(cases)} cases -> {out}")
