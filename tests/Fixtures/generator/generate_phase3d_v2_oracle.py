"""Independent oracle for Phase 3-D v2: the park, fielding, runner-speed and fielder-arm modifiers of the `defense_result` and `advancement` stages.

Shares NO code with src/StarPlayer.Simulation or Domain. The arithmetic is written from ADR-006 item 8 and the slopes / caps of content/tuning/virtual_tuning_v2.json
(prob-model-v2, tuning-virtual-proto-v2), whose baseline rows it reads directly:

  defense_result slots in model order: defense.fielding(out), park.home_runs(home_run), park.hits(single), park.extra_base_hits(double, triple)
  advancement    slots in model order: runner.speed(advance_extra), defense.arm(out_on_bases)
  factor        = clamp(1_000_000 + (input - neutral) * slope, slot min, slot max)           (park input range 800..1250 permille, neutral 1000, slope 1000)
  per outcome   : running = 1_000_000; each slot (model order) that targets it and whose factor != 1_000_000: running = (running * f + 500_000) // 1_000_000;
                  then clamp to [combined_min, combined_max]; weights = baseline * combined; ONE normalisation to 1_000_000 by largest remainder, lowest index first.
  "sequential" (advancement only, NOT the approved contract): speed alone is normalised, then the arm factor is applied to that result and it is normalised a second time;
                  written out to show that the approved single joint computation (owner decision R4) gives a different distribution.

Run:  python tests/Fixtures/generator/generate_phase3d_v2_oracle.py
Writes tests/Fixtures/phase3d_v2_oracle.json (deterministic).
"""
import json
import os

ROLL = 1_000_000
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))

tuning = json.load(open(os.path.join(ROOT, "content", "tuning", "virtual_tuning_v2.json"), encoding="utf-8"))
assert tuning["model_version"] == "prob-model-v2" and tuning["tuning_version"] == "tuning-virtual-proto-v2"
BASE = {(b["stage"], b["condition"]): (b["outcomes"], b["ppm"]) for b in tuning["baselines"]}
MODS = {m["slot"]: m for m in tuning["modifiers"]}
CMIN, CMAX = tuning["combined_min_ppm"], tuning["combined_max_ppm"]

BATTED = ["ground_weak", "ground_medium", "ground_hard", "line_weak", "line_medium", "line_hard", "fly_weak", "fly_medium", "fly_hard", "popup"]
CONTEXTS = ["single_runner_on_second", "single_runner_on_first", "double_runner_on_first", "fly_out_runner_on_third"]
DEFENSE_SLOTS = [("defense.fielding", ["out"]), ("park.home_runs", ["home_run"]), ("park.hits", ["single"]), ("park.extra_base_hits", ["double", "triple"])]
ADVANCEMENT_SLOTS = [("runner.speed", ["advance_extra"]), ("defense.arm", ["out_on_bases"])]


def factor(slot, value):
    m = MODS[slot]
    raw = 1_000_000 + (value - m["neutral"]) * m["slope_ppm_per_unit"]
    return max(m["min_ppm"], min(m["max_ppm"], raw))


def normalize(weights, total=ROLL):
    s = sum(weights)
    q = [w * total // s for w in weights]
    r = [w * total % s for w in weights]
    left = total - sum(q)
    for i in sorted((i for i in range(len(weights)) if r[i] > 0), key=lambda i: (-r[i], i))[:left]:
        q[i] += 1
    assert sum(q) == total
    return q


def distribution(stage, condition, slots, inputs, start=None):
    """inputs: {slot: value}. A slot that is not in inputs is not supplied (neutral). `start` replaces the baseline (sequential variant only)."""
    outcomes, base = BASE[(stage, condition)]
    base = start if start is not None else base
    combined = []
    for o in outcomes:
        running = 1_000_000
        for slot, targets in slots:
            if slot not in inputs or o not in targets:
                continue
            f = factor(slot, inputs[slot])
            if f == 1_000_000:
                continue
            running = (running * f + 500_000) // 1_000_000
        combined.append(max(CMIN, min(CMAX, running)))
    final = normalize([b * c for b, c in zip(base, combined)])
    for o, b_, f_ in zip(outcomes, base, final):
        assert (b_ > 0) == (f_ > 0), "a positive outcome vanished or a zero appeared"
    return outcomes, final, combined


PARKS = [None, (800, 800, 800), (1000, 1000, 1000), (1250, 1250, 1250), (1100, 1000, 1000), (1000, 1100, 1000), (1000, 1000, 1100), (1250, 800, 1000), (800, 1250, 1000),
         (900, 1150, 1050), (900, 1150, 1000), (1080, 1020, 1050), (1100, 1020, 1050), (1100, 1030, 1060), (940, 980, 960)]
FIELDING = [None, 0, 25, 50, 75, 100]
ABILITY = [None, 0, 10, 15, 20, 25, 40, 50, 66, 75, 80, 85, 90, 100]

defense = []
for b in BATTED:
    for park in PARKS:
        for fld in FIELDING:
            ins = {}
            if fld is not None:
                ins["defense.fielding"] = fld
            if park is not None:
                ins["park.home_runs"], ins["park.hits"], ins["park.extra_base_hits"] = park
            outcomes, final, combined = distribution("defense_result", b, DEFENSE_SLOTS, ins)
            defense.append({"batted": b, "park": list(park) if park else None, "fielding": fld, "outcomes": outcomes, "ppm": final, "combined": combined})

advancement = []
for c in CONTEXTS:
    for speed in ABILITY:
        for arm in ABILITY:
            ins = {}
            if speed is not None:
                ins["runner.speed"] = speed
            if arm is not None:
                ins["defense.arm"] = arm
            outcomes, final, combined = distribution("advancement", c, ADVANCEMENT_SLOTS, ins)
            entry = {"context": c, "speed": speed, "arm": arm, "outcomes": outcomes, "ppm": final, "combined": combined}
            if speed is not None and arm is not None:
                _, first, _ = distribution("advancement", c, ADVANCEMENT_SLOTS, {"runner.speed": speed})
                _, second, _ = distribution("advancement", c, ADVANCEMENT_SLOTS, {"defense.arm": arm}, start=first)
                entry["sequential_ppm"] = second
            advancement.append(entry)

doc = {
    "generator": "tests/Fixtures/generator/generate_phase3d_v2_oracle.py (independent; reads content/tuning/virtual_tuning_v2.json only)",
    "model_version": tuning["model_version"],
    "tuning_version": tuning["tuning_version"],
    "defense": defense,
    "advancement": advancement,
}
out = os.path.join(ROOT, "tests", "Fixtures", "phase3d_v2_oracle.json")
with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write("{\n")
    f.write('  "generator": %s,\n  "model_version": %s,\n  "tuning_version": %s,\n' % (json.dumps(doc["generator"]), json.dumps(doc["model_version"]), json.dumps(doc["tuning_version"])))
    for key in ("defense", "advancement"):
        f.write('  "%s": [\n' % key)
        f.write(",\n".join("    " + json.dumps(e, separators=(", ", ": ")) for e in doc[key]))
        f.write("\n  ]%s\n" % ("," if key == "defense" else ""))
    f.write("}\n")
print("defense cases:", len(defense), "advancement cases:", len(advancement))
