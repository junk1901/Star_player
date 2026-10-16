"""Independent oracle for Phase 3-E Game Calling v4 (GC-A): the `catcher.game_calling` modifier next to `pitcher.control` in the `actual_location` stage under
prob-model-v4 / tuning-virtual-proto-v4.

Shares NO code with src/StarPlayer.Domain, Simulation or Infrastructure. The arithmetic is written from ADR-006 item 8 and the numbers of content/tuning/virtual_tuning_v4.json, whose
`actual_location` baseline rows and the two modifier entries it reads directly. The boundary class of a zone is DERIVED HERE from the rule of docs/game-engine.md section 18 (NOT read from
the C# zone model and NOT a copy of its table): an inner cell borders the strike-zone outline unless it is the centre cell (r2c2); an outer zone borders an edge only when its name is a single
direction (high, in, low, out) - the four diagonal outer zones (high_in, low_in, low_out, high_out) touch only a corner. The expected 12 Borderline / 5 Clear split is asserted below.

  applies(gc)   = boundary(TARGET zone) == "Borderline"                 (the zone the pitcher AIMED at, the condition key; never the zone the pitch lands on; independent of the called-pitch mode)
  f_control     = clamp(1_000_000 + (control - 50) * 1500, 900_000, 1_100_000)         (always applies; only the outcome == target is targeted)
  f_gc          = clamp(1_000_000 + (gc - 50) * 1000, 950_000, 1_050_000) if applies else 1_000_000
  combined      : running = 1_000_000; for f in (f_control, f_gc) in model slot order: if f != 1_000_000: running = (running * f + 500_000) // 1_000_000; clamp to [combined_min, combined_max]
  weights       = baseline * combined (only the target outcome has combined != 1_000_000); ONE normalisation to 1_000_000 by largest remainder, lowest index first; zeros stay zero.

Run:  python tests/Fixtures/generator/generate_phase3e_game_calling_oracle.py
Writes tests/Fixtures/phase3e_game_calling_oracle.json (deterministic).
"""
import json
import os

ROLL = 1_000_000
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))

tuning = json.load(open(os.path.join(ROOT, "content", "tuning", "virtual_tuning_v4.json"), encoding="utf-8"))
assert tuning["model_version"] == "prob-model-v4" and tuning["tuning_version"] == "tuning-virtual-proto-v4"
BASE = {(b["stage"], b["condition"]): (b["outcomes"], b["ppm"]) for b in tuning["baselines"]}
MOD = {m["slot"]: m for m in tuning["modifiers"]}
CONTROL, GC = MOD["pitcher.control"], MOD["catcher.game_calling"]
CMIN, CMAX = tuning["combined_min_ppm"], tuning["combined_max_ppm"]
assert (CONTROL["neutral"], CONTROL["slope_ppm_per_unit"], CONTROL["min_ppm"], CONTROL["max_ppm"]) == (50, 1500, 900_000, 1_100_000)
assert (GC["neutral"], GC["slope_ppm_per_unit"], GC["min_ppm"], GC["max_ppm"]) == (50, 1000, 950_000, 1_050_000)
assert (CMIN, CMAX) == (800_000, 1_250_000)
# battery.chemistry stays reserved: no numeric authority
BC = MOD["battery.chemistry"]
assert (BC["slope_ppm_per_unit"], BC["min_ppm"], BC["max_ppm"]) == (0, 1_000_000, 1_000_000)

INNER = [f"in.r{r}c{c}" for r in (1, 2, 3) for c in (1, 2, 3)]
OUTER = ["out.high", "out.high_in", "out.in", "out.low_in", "out.low", "out.low_out", "out.out", "out.high_out"]
ZONES = INNER + OUTER


def boundary(zone):
    """Derived from the geometric rule, not from a table."""
    if zone.startswith("in."):
        return "Clear" if zone == "in.r2c2" else "Borderline"
    name = zone[len("out."):]
    return "Borderline" if "_" not in name else "Clear"


assert len(ZONES) == 17
assert sum(1 for z in ZONES if boundary(z) == "Borderline") == 12 and sum(1 for z in ZONES if boundary(z) == "Clear") == 5


def normalize(weights, total=ROLL):
    s = sum(weights)
    q = [w * total // s for w in weights]
    r = [w * total % s for w in weights]
    left = total - sum(q)
    for i in sorted((i for i in range(len(weights)) if r[i] > 0), key=lambda i: (-r[i], i))[:left]:
        q[i] += 1
    assert sum(q) == total
    return q


def clamp(v, lo, hi):
    return max(lo, min(hi, v))


def actual_location(mode, zone, control, gc):
    outcomes, base = BASE[("actual_location", zone)]
    assert outcomes == ZONES, "outcome order of the actual_location row"
    applies = boundary(zone) == "Borderline"
    f_control = clamp(1_000_000 + (control - CONTROL["neutral"]) * CONTROL["slope_ppm_per_unit"], CONTROL["min_ppm"], CONTROL["max_ppm"])
    f_gc = clamp(1_000_000 + (gc - GC["neutral"]) * GC["slope_ppm_per_unit"], GC["min_ppm"], GC["max_ppm"]) if applies else 1_000_000
    running = 1_000_000
    for f in (f_control, f_gc):  # model slot order: pitcher.control, then catcher.game_calling
        if f != 1_000_000:
            running = (running * f + 500_000) // 1_000_000
    combined_target = clamp(running, CMIN, CMAX)
    combined = [combined_target if o == zone else 1_000_000 for o in outcomes]
    final = normalize([b * c for b, c in zip(base, combined)])
    for b_, f_ in zip(base, final):
        assert (b_ > 0) == (f_ > 0), "a positive outcome vanished or a zero appeared"
    return {"mode": mode, "zone": zone, "boundary": boundary(zone), "control": control, "game_calling": gc, "applied": applies,
            "control_factor": f_control, "gc_factor": f_gc if applies else 1_000_000, "combined_target": combined_target, "baseline": base, "ppm": final}


cases = []
for mode in ("automated", "non_automated"):  # actual_location is not keyed by the called-pitch mode: both modes must give identical rows
    for zone in ZONES:
        for control in (0, 50, 100):
            for gc in (0, 50, 100):
                cases.append(actual_location(mode, zone, control, gc))
assert len(cases) == 17 * 2 * 3 * 3 == 306
# extra game-calling ratings on every zone (slope and rounding, not only the three corners)
for zone in ZONES:
    for control in (0, 50, 100):
        for gc in (1, 10, 25, 49, 51, 75, 90, 99):
            cases.append(actual_location("non_automated", zone, control, gc))

# the approved combined factors of a Borderline target (TASK section 10)
expected = {(0, 0): 878_750, (0, 100): 971_250, (50, 50): 1_000_000, (100, 0): 1_021_250, (100, 100): 1_128_750}
for (c, g), want in expected.items():
    z = actual_location("non_automated", "in.r1c1", c, g)
    assert z["combined_target"] == want, (c, g, z["combined_target"], want)

doc = {
    "model_version": "prob-model-v4", "tuning_version": "tuning-virtual-proto-v4",
    "classification": {z: boundary(z) for z in ZONES},
    "combined_factors": [{"control": c, "game_calling": g, "factor": f} for (c, g), f in sorted(expected.items())],
    "cases": cases,
}
out = os.path.join(ROOT, "tests", "Fixtures", "phase3e_game_calling_oracle.json")
with open(out, "w", encoding="utf-8", newline="\n") as f:
    json.dump(doc, f, indent=1, sort_keys=True)
    f.write("\n")
print(f"{len(cases)} cases -> {out}")
