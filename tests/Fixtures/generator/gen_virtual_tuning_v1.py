"""Generates content/tuning/virtual_tuning_v1.json (Phase 3-B2, tuning-virtual-proto-v1) and computes its profile hash INDEPENDENTLY of the C# code.

  python tests/Fixtures/generator/gen_virtual_tuning_v1.py            -> writes the JSON, prints the content hash
  python tests/Fixtures/generator/gen_virtual_tuning_v1.py --hash     -> reads the JSON, recomputes the canonical text and hash (the C# test pins this value)

The numbers are a VIRTUAL PROTOTYPE design (fictional league, no real statistics). The JSON is the source of truth once written; this script documents how the
baseline rows were derived (weights -> parts per million by the largest-remainder rule, lowest index first) and is the oracle for the canonical text format.
"""
import hashlib
import json
import pathlib
import sys

D = 1_000_000
ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / "content" / "tuning" / "virtual_tuning_v1.json"

INNER = [f"in.r{r}c{c}" for r in (1, 2, 3) for c in (1, 2, 3)]
OUTER = ["out.high", "out.high_in", "out.in", "out.low_in", "out.low", "out.low_out", "out.out", "out.high_out"]
ZONES = INNER + OUTER

# Positions on a 5x5 grid (row, column); column 0 is the batter's inside. Used only to derive the actual-location spread (Chebyshev distance).
POS = {f"in.r{r}c{c}": (r, c) for r in (1, 2, 3) for c in (1, 2, 3)}
POS.update({"out.high": (0, 2), "out.high_in": (0, 0), "out.in": (2, 0), "out.low_in": (4, 0), "out.low": (4, 2),
            "out.low_out": (4, 4), "out.out": (2, 4), "out.high_out": (0, 4)})
BOUNDARY_CLEAR = {"in.r2c2", "out.high_in", "out.low_in", "out.low_out", "out.high_out"}

BATTED = ["ground_weak", "ground_medium", "ground_hard", "line_weak", "line_medium", "line_hard", "fly_weak", "fly_medium", "fly_hard", "popup"]
DEFENCE = ["out", "single", "double", "triple", "home_run", "reached_on_error"]
ADVANCE = ["advance_extra", "hold", "out_on_bases"]


def normalize(weights, total=D):
    """Largest remainder, ties to the lowest index; zero weights stay zero (same rule as ProbabilityScale.Normalize)."""
    s = sum(weights)
    floors = [w * total // s for w in weights]
    rems = [w * total % s for w in weights]
    left = total - sum(floors)
    order = sorted((i for i in range(len(weights)) if rems[i] > 0), key=lambda i: (-rems[i], i))
    for k in range(left):
        floors[order[k]] += 1
    assert sum(floors) == total
    return floors


rows = []


def row(stage, condition, outcomes, ppm):
    assert sum(ppm) == D, (stage, condition, sum(ppm))
    rows.append({"stage": stage, "condition": condition, "outcomes": outcomes, "ppm": ppm})


# s1 target_zone: weights per intent
inner_w = {
    "balanced": [7, 8, 7, 8, 10, 8, 7, 8, 7],
    "attack": [9, 10, 9, 10, 14, 10, 9, 10, 9],
    "careful": [9, 8, 9, 8, 3, 8, 9, 8, 9],
}
outer_w = {  # out.high, high_in, in, low_in, low, low_out, out, high_out
    "balanced": [8, 4, 10, 6, 16, 9, 13, 4],
    "attack": [5, 3, 6, 4, 10, 5, 8, 3],
    "careful": [12, 7, 14, 9, 22, 12, 18, 7],
}
for intent in ("balanced", "attack", "careful"):
    row("target_zone", intent, ZONES, normalize(inner_w[intent] + outer_w[intent]))

# s2 actual_location: on-target mass plus a spread by Chebyshev distance on the 5x5 grid
ON_TARGET = {True: 500_000, False: 400_000}  # keyed by "target is an inner cell"
SPREAD = {1: 8, 2: 3, 3: 1}
for target in ZONES:
    inner = target.startswith("in.")
    rest = D - ON_TARGET[inner]
    weights = []
    for z in ZONES:
        d = max(abs(POS[z][0] - POS[target][0]), abs(POS[z][1] - POS[target][1]))
        weights.append(0 if d == 0 else SPREAD.get(d, 0))
    spread = normalize(weights, rest)
    ppm = [ON_TARGET[inner] if z == target else spread[i] for i, z in enumerate(ZONES)]
    row("actual_location", target, ZONES, ppm)

# s3 batter_decision: hit_by_pitch, take, swing
SWING = {"in.r2c2": 800_000}
for z in ("in.r2c1", "in.r2c3", "in.r1c2", "in.r3c2"):
    SWING[z] = 700_000
for z in ("in.r1c1", "in.r1c3", "in.r3c1", "in.r3c3"):
    SWING[z] = 600_000
SWING.update({"out.high": 240_000, "out.in": 250_000, "out.out": 280_000, "out.low": 300_000,
              "out.high_in": 120_000, "out.low_in": 120_000, "out.low_out": 120_000, "out.high_out": 120_000})
HBP = {"out.in": 15_000, "out.high_in": 12_000, "out.low_in": 8_000}
for z in ZONES:
    hbp, swing = HBP.get(z, 0), SWING[z]
    row("batter_decision", z, ["hit_by_pitch", "take", "swing"], [hbp, D - hbp - swing, swing])

# s4 called_pitch: [ball, strike] per mode and zone
for z in ZONES:
    row("called_pitch", f"automated:{z}", ["ball", "strike"], [0, D] if z.startswith("in.") else [D, 0])
NON_AUTO_STRIKE = {"in.r2c2": 990_000}
for z in ("in.r1c2", "in.r2c1", "in.r2c3", "in.r3c2"):
    NON_AUTO_STRIKE[z] = 880_000
for z in ("in.r1c1", "in.r1c3", "in.r3c1", "in.r3c3"):
    NON_AUTO_STRIKE[z] = 760_000
NON_AUTO_STRIKE.update({"out.high": 220_000, "out.in": 220_000, "out.low": 220_000, "out.out": 220_000,
                        "out.high_in": 20_000, "out.low_in": 20_000, "out.low_out": 20_000, "out.high_out": 20_000})
for z in ZONES:
    s = NON_AUTO_STRIKE[z]
    row("called_pitch", f"non_automated:{z}", ["ball", "strike"], [D - s, s])

# s5 swing_result: miss, foul, in_play
SWING_RESULT = {"in.r2c2": (150_000, 250_000)}
for z in ("in.r2c1", "in.r2c3", "in.r1c2", "in.r3c2"):
    SWING_RESULT[z] = (190_000, 270_000)
for z in ("in.r1c1", "in.r1c3", "in.r3c1", "in.r3c3"):
    SWING_RESULT[z] = (230_000, 280_000)
for z in ("out.high", "out.in", "out.low", "out.out"):
    SWING_RESULT[z] = (380_000, 290_000)
for z in ("out.high_in", "out.low_in", "out.low_out", "out.high_out"):
    SWING_RESULT[z] = (480_000, 260_000)
for z in ZONES:
    miss, foul = SWING_RESULT[z]
    row("swing_result", z, ["miss", "foul", "in_play"], [miss, foul, D - miss - foul])

# s6 batted_ball by location group
BATTED_W = {
    "heart": [6, 9, 7, 3, 8, 12, 6, 11, 15, 8],
    "edge": [9, 10, 5, 4, 8, 7, 8, 10, 9, 12],
    "outside": [12, 9, 3, 5, 6, 3, 10, 8, 5, 14],
}
for g in ("heart", "edge", "outside"):
    row("batted_ball", g, BATTED, normalize(BATTED_W[g]))

# s7 defense_result by batted ball (out, single, double, triple, home_run, reached_on_error)
DEFENCE_PPM = {
    "ground_weak": [880_000, 85_000, 0, 0, 0, 35_000],
    "ground_medium": [780_000, 190_000, 10_000, 0, 0, 20_000],
    "ground_hard": [600_000, 340_000, 40_000, 0, 0, 20_000],
    "line_weak": [470_000, 480_000, 40_000, 0, 0, 10_000],
    "line_medium": [300_000, 560_000, 120_000, 8_000, 0, 12_000],
    "line_hard": [180_000, 480_000, 250_000, 15_000, 60_000, 15_000],
    "fly_weak": [940_000, 40_000, 10_000, 0, 0, 10_000],
    "fly_medium": [760_000, 100_000, 90_000, 10_000, 30_000, 10_000],
    "fly_hard": [330_000, 80_000, 200_000, 20_000, 360_000, 10_000],
    "popup": [975_000, 5_000, 0, 0, 0, 20_000],
}
for b in BATTED:
    row("defense_result", b, DEFENCE, DEFENCE_PPM[b])

# s8 advancement (advance_extra, hold, out_on_bases)
for cond, ppm in (("single_runner_on_second", [590_000, 380_000, 30_000]), ("single_runner_on_first", [270_000, 700_000, 30_000]),
                  ("double_runner_on_first", [560_000, 400_000, 40_000]), ("fly_out_runner_on_third", [700_000, 280_000, 20_000])):
    row("advancement", cond, ADVANCE, ppm)

LINEAR = lambda slot, neutral, slope, lo, hi: {"slot": slot, "neutral": neutral, "slope_ppm_per_unit": slope, "min_ppm": lo, "max_ppm": hi, "table": None}
TABLE = lambda slot, lo, hi, entries: {"slot": slot, "neutral": 0, "slope_ppm_per_unit": 0, "min_ppm": lo, "max_ppm": hi, "table": [{"key": k, "factor_ppm": v} for k, v in entries]}
RESERVED = lambda slot, neutral: {"slot": slot, "neutral": neutral, "slope_ppm_per_unit": 0, "min_ppm": D, "max_ppm": D, "table": None}

modifiers = [
    LINEAR("pitcher.control", 50, 1500, 900_000, 1_100_000),
    RESERVED("catcher.game_calling", 50),
    RESERVED("battery.chemistry", 50),
    LINEAR("batter.eye", 50, -1500, 900_000, 1_100_000),
    TABLE("strategy.approach", 850_000, 1_150_000, [("patient", 880_000), ("neutral", D), ("aggressive", 1_100_000)]),
    TABLE("situation.count_pressure", 850_000, 1_150_000, [("three_balls", 880_000), ("neutral", D), ("two_strikes", 1_100_000)]),
    RESERVED("catcher.framing", 50),
    LINEAR("pitcher.stuff", 50, 1200, 900_000, 1_100_000),
    LINEAR("batter.contact", 50, -1500, 900_000, 1_100_000),
    TABLE("matchup.handedness", 950_000, 1_050_000, [("opposite_hand", 970_000), ("neutral", D), ("same_hand", 1_040_000)]),
    LINEAR("pitcher.velocity", 50, -800, 900_000, 1_100_000),
    LINEAR("batter.power", 50, 1500, 900_000, 1_100_000),
    LINEAR("defense.fielding", 50, 800, 950_000, 1_050_000),
    RESERVED("park.home_runs", 1000),
    RESERVED("park.hits", 1000),
    RESERVED("park.extra_base_hits", 1000),
    LINEAR("runner.speed", 50, 2000, 900_000, 1_100_000),
    LINEAR("defense.arm", 50, 2000, 900_000, 1_100_000),
]

doc = {
    "format_version": 1,
    "model_version": "prob-model-v1",
    "tuning_version": "tuning-virtual-proto-v1",
    "status": "virtual_prototype",
    "provenance": "VIRTUAL PROTOTYPE values chosen by the Phase 3-B2 author for a fictional league; not derived from KBO, MLB or any real statistics; to be reviewed and recalibrated against pre-declared targets in Phase 3-F",
    "zone_model": "pzl-zones-v1",
    "reference_distribution_id": "pzl-ref-uniform-v1",
    "normalization_policy": "largest-remainder-lowest-index-v1",
    "rounding_policy": "half-up-v1",
    "denominator": D,
    "min_positive_baseline_ppm": 1000,
    "combined_min_ppm": 800_000,
    "combined_max_ppm": 1_250_000,
    "tendency": {"contact_cap_ppm": 60_000, "quality_cap_ppm": 40_000, "min_mass_ppm": 1000},
    "baselines": rows,
    "modifiers": modifiers,
    "scenarios": [
        {"id": "s2_location_on_target_center", "description": "actual_location given target in.r2c2: observed frequencies vs the baseline row", "stage": "actual_location", "condition": "in.r2c2",
         "seed": 2026101101, "context_key": "tuning-check:s2:in.r2c2", "samples": 200000, "tolerance_rule": "binomial-4.5-sigma-ceiling-v1"},
        {"id": "s3_decision_inner_corner", "description": "batter_decision given an actual in.r1c1 pitch", "stage": "batter_decision", "condition": "in.r1c1",
         "seed": 2026101102, "context_key": "tuning-check:s3:in.r1c1", "samples": 200000, "tolerance_rule": "binomial-4.5-sigma-ceiling-v1"},
        {"id": "s4_called_pitch_nonauto_border", "description": "called_pitch given a take on in.r1c2 under non_automated", "stage": "called_pitch", "condition": "non_automated:in.r1c2",
         "seed": 2026101103, "context_key": "tuning-check:s4:in.r1c2", "samples": 200000, "tolerance_rule": "binomial-4.5-sigma-ceiling-v1"},
        {"id": "s7_defense_fly_hard", "description": "defense_result given a hard fly ball", "stage": "defense_result", "condition": "fly_hard",
         "seed": 2026101104, "context_key": "tuning-check:s7:fly_hard", "samples": 200000, "tolerance_rule": "binomial-4.5-sigma-ceiling-v1"},
    ],
    "targets": [
        {"id": "pa_strikeout_rate", "description": "strikeouts per plate appearance, league average, neutral inputs (needs the 3-C plate-appearance loop)", "low_ppm": 160000, "high_ppm": 260000, "status": "declared_for_3f_not_evaluated"},
        {"id": "pa_walk_rate", "description": "walks per plate appearance, league average", "low_ppm": 60000, "high_ppm": 110000, "status": "declared_for_3f_not_evaluated"},
        {"id": "pa_hit_by_pitch_rate", "description": "hit-by-pitch per plate appearance", "low_ppm": 4000, "high_ppm": 15000, "status": "declared_for_3f_not_evaluated"},
        {"id": "pa_home_run_rate", "description": "home runs per plate appearance", "low_ppm": 15000, "high_ppm": 35000, "status": "declared_for_3f_not_evaluated"},
        {"id": "babip", "description": "batting average on balls in play", "low_ppm": 270000, "high_ppm": 320000, "status": "declared_for_3f_not_evaluated"},
        {"id": "pitch_zone_rate", "description": "share of actual pitches located in the nine inner cells, balanced intent, neutral inputs", "low_ppm": 450000, "high_ppm": 650000, "status": "declared_for_3f_not_evaluated"},
        {"id": "pitch_swing_rate", "description": "share of actual pitches swung at (excluding hit-by-pitch), neutral inputs", "low_ppm": 400000, "high_ppm": 560000, "status": "declared_for_3f_not_evaluated"},
        {"id": "swing_miss_rate", "description": "share of swings that miss, neutral inputs", "low_ppm": 180000, "high_ppm": 300000, "status": "declared_for_3f_not_evaluated"},
    ],
}


def canonical(d):
    n = lambda v: str(int(v))
    lines = [
        "tuning-profile-canonical-v1",
        f"model={d['model_version']}",
        f"tuning={d['tuning_version']}",
        "status=virtual_prototype",
        f"zone_model={d['zone_model']}",
        "reference=pzl-ref-uniform-v1|1,1,1,1,1,1,1,1,1",
        f"normalization={d['normalization_policy']}",
        f"rounding={d['rounding_policy']}",
        f"denominator={n(d['denominator'])}",
        f"min_positive_baseline_ppm={n(d['min_positive_baseline_ppm'])}",
        f"combined_ppm={n(d['combined_min_ppm'])},{n(d['combined_max_ppm'])}",
        f"tendency_caps_ppm=contact:{n(d['tendency']['contact_cap_ppm'])}|quality:{n(d['tendency']['quality_cap_ppm'])}|min_mass:{n(d['tendency']['min_mass_ppm'])}",
        f"provenance={d['provenance']}",
    ]
    # Baselines are written in the model's stage order, then the model's condition order; the generator writes them in exactly that order.
    for r in d["baselines"]:
        lines.append("baseline|%s|%s|%s" % (r["stage"], r["condition"], ",".join(f"{o}={n(p)}" for o, p in zip(r["outcomes"], r["ppm"]))))
    table_slots = {"strategy.approach", "situation.count_pressure", "matchup.handedness"}
    for m in d["modifiers"]:
        if m["slot"] in table_slots:
            lines.append("modifier|%s|table|min=%s|max=%s|%s" % (m["slot"], n(m["min_ppm"]), n(m["max_ppm"]), ",".join(f"{t['key']}={n(t['factor_ppm'])}" for t in m["table"])))
        else:
            lines.append("modifier|%s|linear|neutral=%s|slope=%s|min=%s|max=%s" % (m["slot"], n(m["neutral"]), n(m["slope_ppm_per_unit"]), n(m["min_ppm"]), n(m["max_ppm"])))
    for s in sorted(d["scenarios"], key=lambda s: s["id"]):
        lines.append(f"scenario|{s['id']}|{s['stage']}|{s['condition']}|seed={s['seed']}|context={s['context_key']}|samples={s['samples']}|rule={s['tolerance_rule']}")
    for t in sorted(d["targets"], key=lambda t: t["id"]):
        lines.append(f"target|{t['id']}|{t['low_ppm']}|{t['high_ppm']}|{t['status']}")
    return "\n".join(lines)


def write_json(d):
    OUT.parent.mkdir(parents=True, exist_ok=True)
    out = ["{"]
    for key, value in d.items():
        if key in ("baselines", "modifiers", "scenarios", "targets"):
            out.append(f'  "{key}": [')
            out.append(",\n".join("    " + json.dumps(item, ensure_ascii=False, separators=(", ", ": ")) for item in value))
            out.append("  ],")
        else:
            out.append(f'  "{key}": {json.dumps(value, ensure_ascii=False)},')
    out[-1] = out[-1].rstrip(",")
    out.append("}")
    OUT.write_text("\n".join(out) + "\n", encoding="utf-8")


if __name__ == "__main__":
    if "--hash" in sys.argv:
        loaded = json.loads(OUT.read_text(encoding="utf-8"))
        print(hashlib.sha256(canonical(loaded).encode("utf-8")).hexdigest())
    else:
        write_json(doc)
        print(hashlib.sha256(canonical(doc).encode("utf-8")).hexdigest())
