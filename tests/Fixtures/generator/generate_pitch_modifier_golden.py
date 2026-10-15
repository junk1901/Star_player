"""Independent oracle for Phase 3-D: the ability / strategy / count / handedness modifiers on the single-pitch branch.

Shares NO code with src/StarPlayer.Simulation or Domain (PCG32 + kd1 are re-implemented here exactly as in generate_pitch_branch_golden.py; the modifier arithmetic is
written from ADR-006 item 8 and the slopes/caps/tables of content/tuning/virtual_tuning_v1.json, not from the C# probability model).

Modifier contract (ADR-006 item 8, docs/game-engine.md section 29), per stage in the model's slot order:
  actual_location  pitcher.control(@condition)
  batter_decision  batter.eye(swing; outer zones only), strategy.approach(swing), situation.count_pressure(swing)
  swing_result     pitcher.stuff(miss), batter.contact(miss), matchup.handedness(miss)
  batted_ball      pitcher.velocity(hard), batter.power(hard)            hard = ground_hard, line_hard, fly_hard
  factor: linear = clamp(1_000_000 + (input - neutral) * slope, min, max); table = the listed factor.
  per outcome: running = 1_000_000; for each slot (model order) that targets it and whose factor != 1_000_000: running = (running * f + 500_000) // 1_000_000;
  then clamp to [combined_min, combined_max]; weights = baseline * combined; normalize to 1_000_000 by largest remainder, lowest index first.
Inputs (D-ABILITY-1/2): the base ratings as given; approach normal/none -> neutral; count 3 balls & <2 strikes -> three_balls, 2 strikes & <3 balls -> two_strikes,
else (including 3-2) neutral; handedness: batter's applied side == pitcher's throwing hand -> same_hand, else opposite_hand.
The neutral tendency (all zero) changes nothing as long as the favourable mass stays inside its guard window; the script asserts that it does.

Run:  python tests/Fixtures/generator/generate_pitch_modifier_golden.py
Writes tests/Fixtures/phase3d_pitch_modifier_golden.json (deterministic). Reads the scenario definitions (seeds, specs) of phase3c2a_pitch_branch_golden.json.
"""
import hashlib
import json
import os
import struct

M64 = 0xFFFFFFFFFFFFFFFF
MULT = 6364136223846793005
ROLL = 1_000_000
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))

T, L, P, C, F = "game/target@1", "game/location@1", "game/pitch@1", "game/contact@1", "game/field@1"


class Pcg32:
    def __init__(self, initstate, initseq):
        self.state = 0
        self.inc = ((initseq << 1) | 1) & M64
        self._step()
        self.state = (self.state + initstate) & M64
        self._step()
        self.draws = 0

    def _step(self):
        old = self.state
        self.state = (old * MULT + self.inc) & M64
        return old

    def u32(self):
        old = self._step()
        self.draws += 1
        xorshifted = (((old >> 18) ^ old) >> 27) & 0xFFFFFFFF
        rot = old >> 59
        return ((xorshifted >> rot) | (xorshifted << ((-rot) & 31))) & 0xFFFFFFFF

    def bounded(self, bound):
        threshold = ((1 << 32) - bound) % bound
        while True:
            r = self.u32()
            if r >= threshold:
                return r % bound


def kd1(seed, context, stream):
    ctx = context.encode("utf-8")
    sid = stream.encode("utf-8")
    data = b"SPRNG-kd1" + struct.pack(">Q", seed) + struct.pack(">I", len(ctx)) + ctx + struct.pack(">I", len(sid)) + sid
    digest = hashlib.sha256(data).digest()
    return struct.unpack(">Q", digest[0:8])[0], struct.unpack(">Q", digest[8:16])[0]


class Random:
    def __init__(self, seed, context):
        self.seed, self.context, self.streams = seed, context, {}

    def stream(self, sid):
        if sid not in self.streams:
            st, sq = kd1(self.seed, self.context, sid)
            self.streams[sid] = Pcg32(st, sq)
        return self.streams[sid]

    def canonical(self):
        lines = ["rngset1|pcg32-xsh-rr-v1|kd1|%016x|%s" % (self.seed, self.context)]
        for sid in sorted(k for k, g in self.streams.items() if g.draws > 0):
            g = self.streams[sid]
            lines.append("%s|%016x|%016x|%d" % (sid, g.state, g.inc, g.draws))
        return chr(10).join(lines)


tuning = json.load(open(os.path.join(ROOT, "content", "tuning", "virtual_tuning_v1.json"), encoding="utf-8"))
BASE = {(b["stage"], b["condition"]): (b["outcomes"], b["ppm"]) for b in tuning["baselines"]}
MODS = {m["slot"]: m for m in tuning["modifiers"]}
CMIN, CMAX = tuning["combined_min_ppm"], tuning["combined_max_ppm"]
HARD = ["ground_hard", "line_hard", "fly_hard"]
CLEAR = {"in.r2c2", "out.high_in", "out.low_in", "out.low_out", "out.high_out"}

# (slot, stage, targets, zone scope) in the model's slot order within each stage
SLOTS = [
    ("pitcher.control", "actual_location", ["@condition"], "all"),
    ("batter.eye", "batter_decision", ["swing"], "outer"),
    ("strategy.approach", "batter_decision", ["swing"], "all"),
    ("situation.count_pressure", "batter_decision", ["swing"], "all"),
    ("pitcher.stuff", "swing_result", ["miss"], "all"),
    ("batter.contact", "swing_result", ["miss"], "all"),
    ("matchup.handedness", "swing_result", ["miss"], "all"),
    ("pitcher.velocity", "batted_ball", HARD, "all"),
    ("batter.power", "batted_ball", HARD, "all"),
]


def applied_side(batter, pitcher):
    if batter in ("right", "left"):
        return batter
    return "left" if pitcher == "right" else "right"


def count_key(balls, strikes):
    if balls == 3 and strikes < 2:
        return "three_balls"
    if balls < 3 and strikes == 2:
        return "two_strikes"
    return "neutral"


def inputs_for(stage, spec):
    """The modifier inputs of one stage as an ordered list of (slot, value-or-key). Written from the contract, not from the C# code."""
    b, p = spec["batter"], spec["pitcher"]
    if stage == "actual_location":
        return [("pitcher.control", p["control"])]
    if stage == "batter_decision":
        approach = {"normal": "neutral", None: "neutral", "patient": "patient", "aggressive": "aggressive"}[spec["approach"]]
        return [("batter.eye", b["eye"]), ("strategy.approach", approach), ("situation.count_pressure", count_key(spec["balls"], spec["strikes"]))]
    if stage == "swing_result":
        side = applied_side(spec["batter_side"], spec["pitcher_hand"])
        return [("pitcher.stuff", p["stuff"]), ("batter.contact", b["contact"]), ("matchup.handedness", "same_hand" if side == spec["pitcher_hand"] else "opposite_hand")]
    if stage == "batted_ball":
        return [("pitcher.velocity", p["velocity"]), ("batter.power", b["power"])]
    return []


def factor(slot, value):
    m = MODS[slot]
    if m["table"] is not None:
        return next(e["factor_ppm"] for e in m["table"] if e["key"] == value)
    raw = 1_000_000 + (value - m["neutral"]) * m["slope_ppm_per_unit"]
    return max(m["min_ppm"], min(m["max_ppm"], raw))


def normalize(weights, total=ROLL):
    s = sum(weights)
    q = [w * total // s for w in weights]
    r = [w * total % s for w in weights]
    left = total - sum(q)
    for i in sorted(range(len(weights)), key=lambda i: (-r[i], i))[:left]:
        q[i] += 1
    return q


def distribution(stage, condition, ins):
    outcomes, base = BASE[(stage, condition)]
    combined = []
    for o in outcomes:
        running = 1_000_000
        for slot, st, targets, scope in SLOTS:
            if st != stage or slot not in dict(ins):
                continue
            applies = scope == "all" or (scope == "outer" and condition.startswith("out."))
            f = factor(slot, dict(ins)[slot]) if applies else 1_000_000
            if not any((t == "@condition" and o == condition) or t == o for t in targets) or f == 1_000_000:
                continue
            running = (running * f + 500_000) // 1_000_000
        combined.append(max(CMIN, min(CMAX, running)))
    final = normalize([b * c for b, c in zip(base, combined)])
    # the neutral-tendency guard window must not clip (then "tendency supplied, all zero" is exactly the unguarded distribution)
    if stage in ("swing_result", "batted_ball") and condition.startswith(("in.", "heart", "edge")):
        cap, fav = (60_000, ("foul", "in_play")) if stage == "swing_result" else (40_000, tuple(HARD))
        mass = sum(v for o, v in zip(outcomes, final) if o in fav)
        assert cap + 1000 <= mass <= ROLL - cap - 1000, (stage, condition, mass)
    for o, b_, f_ in zip(outcomes, base, final):
        assert (b_ > 0) == (f_ > 0), "a positive outcome vanished or a zero appeared"
    scope_of = {slot: scope for slot, _, _, scope in SLOTS}
    listed = []
    for slot, v in ins:
        applies = scope_of[slot] == "all" or condition.startswith("out.")
        listed.append({"slot": slot, "input": str(v), "factor_ppm": factor(slot, v) if applies else 1_000_000, "applied": applies})
    return outcomes, final, combined, listed


def group(zone):
    if zone.startswith("out."):
        return "outside"
    return "heart" if zone == "in.r2c2" else "edge"


def pitch(rnd, spec):
    steps, calls = [], 0

    def draw(sid, stage, condition):
        nonlocal calls
        g = rnd.stream(sid)
        before = g.draws
        roll = g.bounded(ROLL)
        outcomes, final, combined, listed = distribution(stage, condition, inputs_for(stage, spec))
        running, outcome = 0, None
        for o, p in zip(outcomes, final):
            running += p
            if roll < running:
                outcome = o
                break
        calls += 1
        steps.append({"stage": stage, "condition": condition, "outcome": outcome, "stream": sid, "roll": roll, "raw_draws": g.draws - before,
                      "final": final, "combined": combined, "modifiers": listed})
        return outcome

    target = draw(T, "target_zone", spec["intent"])
    zone = draw(L, "actual_location", target)
    decision = draw(P, "batter_decision", zone)
    called = swing = batted = defense = None
    if decision == "hit_by_pitch":
        kind = "hit_by_pitch"
    elif decision == "take" or spec["explicit_take"]:
        mode = "automated" if spec["called_pitch_mode"] == "automated" else "non_automated"
        called = draw(P, "called_pitch", "%s:%s" % (mode, zone))
        kind = "called_ball" if called == "ball" else "called_strike"
    else:
        swing = draw(C, "swing_result", zone)
        if swing == "miss":
            kind = "swinging_strike"
        elif swing == "foul":
            kind = "foul"
        else:
            kind = "in_play"
            batted = draw(C, "batted_ball", group(zone))
            defense = draw(F, "defense_result", batted)
    return {"target_zone": target, "location_zone": zone, "applied_side": applied_side(spec["batter_side"], spec["pitcher_hand"]),
            "boundary": "clear" if zone in CLEAR else "borderline", "decision": decision, "called_pitch": called, "swing_result": swing, "batted_ball": batted,
            "defense_result": defense, "kind": kind, "steps": steps, "total_calls": calls}


DEFAULT_SHEET = {"batter": {"contact": 50, "power": 50, "eye": 50}, "pitcher": {"control": 50, "stuff": 50, "velocity": 50}}

out = {"generator": "tests/Fixtures/generator/generate_pitch_modifier_golden.py", "tuning_version": tuning["tuning_version"], "roll_bound": ROLL}

# ---- 1. the 3-C2A golden scenarios re-run under the default modifier context of PitchPreparation.Prepare (all ratings 50, approach normal/none, count 0-0) ----
old = json.load(open(os.path.join(HERE, "..", "phase3c2a_pitch_branch_golden.json"), encoding="utf-8"))
prepared = []
for sc in old["scenarios"]:
    rnd = Random(int(sc["seed"], 16), sc["context"])
    for sid, count in sc["prior_calls"].items():
        for _ in range(count):
            rnd.stream(sid).bounded(ROLL)
    pitches = []
    for pt in sc["pitches"]:
        spec = dict(pt["spec"], **DEFAULT_SHEET, approach=None if pt["spec"]["explicit_take"] else "normal", balls=0, strikes=0)
        pitches.append({"spec": pt["spec"], "expected": pitch(rnd, spec)})
    prepared.append({"id": sc["id"], "seed": sc["seed"], "context": sc["context"], "prior_calls": sc["prior_calls"], "pitches": pitches,
                     "final_canonical": rnd.canonical(), "final_state_hash": hashlib.sha256(rnd.canonical().encode("utf-8")).hexdigest()})
    # the modifiers add no draw: the chain must end exactly where the ability-free oracle ended
    assert prepared[-1]["final_canonical"] == sc["final_canonical"] and prepared[-1]["final_state_hash"] == sc["final_state_hash"]
out["prepare_default_scenarios"] = prepared

# ---- 2. a deterministic matrix: ratings, approach, every count, every hand combination, both call modes ----
RATINGS = [0, 25, 50, 75, 100]
BATTERS = ["right", "left", "switch"]
HANDS = ["right", "left"]
APPROACHES = ["normal", "patient", "aggressive"]
COUNTS = [(b, s) for b in range(4) for s in range(3)]
INTENTS = ["balanced", "attack", "careful"]
MODES = ["automated", "non_automated"]
matrix = []
for i in range(360):
    take = i % 4 == 0
    balls, strikes = COUNTS[i % len(COUNTS)]
    spec = {
        "intent": INTENTS[i % 3], "explicit_take": take, "batter_side": BATTERS[(i // 3) % 3], "pitcher_hand": HANDS[(i // 2) % 2],
        "called_pitch_mode": MODES[(i // 5) % 2], "approach": None if take else APPROACHES[(i // 7) % 3], "balls": balls, "strikes": strikes,
        "batter": {"contact": RATINGS[(i * 3) % 5], "power": RATINGS[(i * 7 + 1) % 5], "eye": RATINGS[(i * 2 + 3) % 5]},
        "pitcher": {"control": RATINGS[(i * 5 + 2) % 5], "stuff": RATINGS[(i + 1) % 5], "velocity": RATINGS[(i * 4 + 2) % 5]},
    }
    seed = 2026110000 + i
    context = "game:3d:m%03d" % i
    matrix.append({"id": "m%03d" % i, "seed": "%016x" % seed, "context": context, "spec": spec, "expected": pitch(Random(seed, context), spec)})
out["matrix"] = matrix

# coverage the C# tests rely on
swing_steps = [s for m in matrix for s in m["expected"]["steps"] if s["stage"] == "swing_result"]
decision_steps = [s for m in matrix for s in m["expected"]["steps"] if s["stage"] == "batter_decision"]
assert len(swing_steps) > 80 and any(s["stage"] == "batted_ball" for m in matrix for s in m["expected"]["steps"])
keys = {dict((x["slot"], x["input"]) for x in s["modifiers"])["situation.count_pressure"] for s in decision_steps}
assert keys == {"three_balls", "two_strikes", "neutral"}
assert {dict((x["slot"], x["input"]) for x in s["modifiers"])["matchup.handedness"] for s in swing_steps} == {"same_hand", "opposite_hand"}
assert {dict((x["slot"], x["input"]) for x in s["modifiers"])["strategy.approach"] for s in decision_steps} == {"neutral", "patient", "aggressive"}
assert {(m["spec"]["balls"], m["spec"]["strikes"]) for m in matrix} == set(COUNTS)

target = os.path.join(HERE, "..", "phase3d_pitch_modifier_golden.json")
with open(target, "w", encoding="utf-8", newline="\n") as f:
    json.dump(out, f, separators=(",", ":"), ensure_ascii=False)
    f.write("\n")
print("wrote", os.path.normpath(target), "prepare scenarios:", len(prepared), "matrix:", len(matrix), "swing steps:", len(swing_steps))
