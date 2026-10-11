"""Independent oracle for Phase 3-C2A: the new stream game/target@1 and the single-pitch probability branch.

Shares NO code with src/StarPlayer.Simulation (PCG32 + kd1 are re-implemented here from the published reference and ADR-004; SHA-256 comes from hashlib)
and does NOT call the C# probability model: with neutral inputs and no tendency the stage distribution equals the baseline row of
content/tuning/virtual_tuning_v1.json, so the oracle reads those rows directly and selects by the cumulative table.

Branch contract (docs/game-engine.md section 21, ADR-007 3-C2A):
  target_zone      game/target@1    1 call per actual pitch, condition = pitcher intent
  actual_location  game/location@1  1 call, condition = target zone
  batter_decision  game/pitch@1     1 call, condition = actual zone
  called_pitch     game/pitch@1     1 call if the batter took (decision == take, or an explicit Take that was not a hit_by_pitch)
  swing_result     game/contact@1   1 call if the batter swung (never in an explicit Take)
  batted_ball      game/contact@1   1 call if in_play
  defense_result   game/field@1     1 call if there is a batted ball
Every call is NextBounded(1_000_000). Nothing is drawn for a branch that is not reached. advancement is NOT drawn here (3-C2B).

Run:  python tests/Fixtures/generator/generate_pitch_branch_golden.py
Writes tests/Fixtures/phase3c2a_pitch_branch_golden.json (deterministic: fixed seeds, fixed search order).
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

T, L, P, C, F, R = "game/target@1", "game/location@1", "game/pitch@1", "game/contact@1", "game/field@1", "game/runner@1"


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


def h32(v):
    return "0x%08x" % v


def h64(v):
    return "%016x" % v


class Random:
    def __init__(self, seed, context):
        self.seed, self.context, self.streams = seed, context, {}

    def stream(self, sid):
        if sid not in self.streams:
            st, sq = kd1(self.seed, self.context, sid)
            self.streams[sid] = Pcg32(st, sq)
        return self.streams[sid]

    def canonical(self):
        lines = ["rngset1|pcg32-xsh-rr-v1|kd1|%s|%s" % (h64(self.seed), self.context)]
        for sid in sorted(k for k, g in self.streams.items() if g.draws > 0):
            g = self.streams[sid]
            lines.append("%s|%s|%s|%d" % (sid, h64(g.state), h64(g.inc), g.draws))
        return "\n".join(lines)


tuning = json.load(open(os.path.join(ROOT, "content", "tuning", "virtual_tuning_v1.json"), encoding="utf-8"))
BASE = {(b["stage"], b["condition"]): (b["outcomes"], b["ppm"]) for b in tuning["baselines"]}
CLEAR = {"in.r2c2", "out.high_in", "out.low_in", "out.low_out", "out.high_out"}


def select(stage, condition, roll):
    outcomes, ppm = BASE[(stage, condition)]
    assert sum(ppm) == ROLL
    running = 0
    for o, p in zip(outcomes, ppm):
        running += p
        if roll < running:
            return o
    raise AssertionError


def group(zone):
    if zone.startswith("out."):
        return "outside"
    return "heart" if zone == "in.r2c2" else "edge"


def applied_side(batter, pitcher):
    if batter == "right":
        return "right"
    if batter == "left":
        return "left"
    return "left" if pitcher == "right" else "right"


def pitch(rnd, spec):
    steps, calls, draws = [], {}, {}

    def draw(sid, stage, condition):
        g = rnd.stream(sid)
        before = g.draws
        roll = g.bounded(ROLL)
        outcome = select(stage, condition, roll)
        calls[sid] = calls.get(sid, 0) + 1
        draws[sid] = draws.get(sid, 0) + (g.draws - before)
        steps.append({"stage": stage, "condition": condition, "outcome": outcome, "stream": sid, "roll": roll, "raw_draws": g.draws - before})
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
    return {
        "target_zone": target, "location_zone": zone, "applied_side": applied_side(spec["batter_side"], spec["pitcher_hand"]),
        "boundary": "clear" if zone in CLEAR else "borderline", "decision": decision, "called_pitch": called, "swing_result": swing,
        "batted_ball": batted, "defense_result": defense, "kind": kind, "steps": steps,
        "total_calls": sum(calls.values()), "calls_by_stream": calls, "draws_by_stream": draws,
    }


out = {"generator": "tests/Fixtures/generator/generate_pitch_branch_golden.py", "tuning_version": tuning["tuning_version"], "roll_bound": ROLL}

# ---- game/target@1: key derivation, raw u32 and NextBounded(1_000_000) vectors ----
tv = []
for seed, ctx in [(0, "game:KBO-T:fx-0001"), (1, "game:KBO-T:fx-0001"), (0x0123456789ABCDEF, "g"), (M64, "game:3c2a:max_seed"), (2026101201, "game:3c2a:target")]:
    st, sq = kd1(seed, ctx, T)
    a, b = Pcg32(st, sq), Pcg32(st, sq)
    u = [h32(a.u32()) for _ in range(6)]
    bounded, cumulative = [], []
    for _ in range(8):
        bounded.append(b.bounded(ROLL))
        cumulative.append(b.draws)
    tv.append({"seed": h64(seed), "context": ctx, "stream": T, "initstate": h64(st), "initseq": h64(sq), "u32": u,
               "bounded_1000000": bounded, "draws_after_each": cumulative})
out["target_vectors"] = tv

# the same (seed, context) on the other streams must give different sequences than game/target@1 (independence)
ind = []
for sid in (P, C, F, R, L, "game/ai@1"):
    st, sq = kd1(0, "game:KBO-T:fx-0001", sid)
    g = Pcg32(st, sq)
    ind.append({"stream": sid, "u32": [h32(g.u32()) for _ in range(4)]})
out["independence_vectors"] = ind

# a seed whose first 40 NextBounded(1_000_000) calls on game/target@1 include a rejected raw output (calls != raw draws)
seed = 7000
while True:
    st, sq = kd1(seed, "game:3c2a:rejection", T)
    g = Pcg32(st, sq)
    values, per_call = [], []
    for _ in range(40):
        before = g.draws
        values.append(g.bounded(ROLL))
        per_call.append(g.draws - before)
    if g.draws > 40:
        out["rejection_vector"] = {"seed": h64(seed), "context": "game:3c2a:rejection", "stream": T, "calls": 40, "raw_draws": g.draws, "bounded_1000000": values,
                                   "raw_draws_per_call": per_call, "rejected_call_index": per_call.index(2)}
        REJ_SEED, REJ_INDEX = seed, per_call.index(2)
        break
    seed += 1

# ---- single-pitch branch scenarios (chains of 1..3 pitches that share one RNG) ----
INTENTS = ["balanced", "attack", "careful"]
BATTERS = ["right", "left", "switch"]
HANDS = ["right", "left"]
MODES = ["automated", "non_automated"]

scenarios = []
wanted = {("hit_by_pitch", False), ("hit_by_pitch", True), ("called_ball", False), ("called_ball", True), ("called_strike", False), ("called_strike", True),
          ("swinging_strike", False), ("foul", False), ("in_play", False)}
seen = set()
seed = 2026101200
n = 0
while wanted - seen and n < 400000:
    n += 1
    seed += 1
    spec = {
        "intent": INTENTS[n % 3], "explicit_take": (n % 4) == 0, "batter_side": BATTERS[(n // 3) % 3],
        "pitcher_hand": HANDS[(n // 2) % 2], "called_pitch_mode": MODES[(n // 5) % 2],
    }
    rnd = Random(seed, "game:3c2a:probe")
    res = pitch(rnd, spec)
    key = (res["kind"], spec["explicit_take"])
    if key in wanted - seen:
        seen.add(key)
        # a probe (seed, spec) that reaches a new category becomes a scenario; it is re-run below as the first pitch of a chain
        scenarios.append({"seed": seed, "spec": spec, "probe": True, "context": "game:3c2a:probe"})
assert not (wanted - seen), wanted - seen

# extra rich coverage: every in_play defense result, each called-pitch mode and each switch-hitter/hand combination
need_defense = {"out", "single", "double", "triple", "home_run", "reached_on_error"}
seed = 2026109000
n = 0
while need_defense and n < 400000:
    n += 1
    seed += 1
    spec = {"intent": INTENTS[n % 3], "explicit_take": False, "batter_side": BATTERS[(n // 3) % 3], "pitcher_hand": HANDS[n % 2], "called_pitch_mode": MODES[(n // 7) % 2]}
    res = pitch(Random(seed, "game:3c2a:probe"), spec)
    if res["defense_result"] in need_defense:
        need_defense.discard(res["defense_result"])
        scenarios.append({"seed": seed, "spec": spec, "probe": True, "context": "game:3c2a:probe"})
assert not need_defense

combos = [(b, h, m) for b in BATTERS for h in HANDS for m in MODES]
for i, (b, h, m) in enumerate(combos):
    scenarios.append({"seed": 2026109500 + i, "spec": {"intent": INTENTS[i % 3], "explicit_take": i % 5 == 0, "batter_side": b, "pitcher_hand": h, "called_pitch_mode": m}})

final = []
for idx, sc in enumerate(scenarios):
    context = sc.get("context", "game:3c2a:s%02d" % idx)
    chain_len = 1 + idx % 3
    # a non-fresh start: some streams already advanced by whole NextBounded(1_000_000) calls (the caller's earlier pitches / other use)
    prior = {}
    if idx % 2 == 1 and not sc.get("probe"):
        prior = {T: 1 + idx % 4, P: idx % 3, C: 2, L: 1}
    rnd = Random(sc["seed"], context)
    for sid, count in prior.items():
        for _ in range(count):
            rnd.stream(sid).bounded(ROLL)
    pitches = []
    for k in range(chain_len):
        spec = dict(sc["spec"])
        spec["intent"] = INTENTS[(INTENTS.index(spec["intent"]) + k) % 3]
        spec["explicit_take"] = sc["spec"]["explicit_take"] if k == 0 else (k % 2 == 1)
        res = pitch(rnd, spec)
        pitches.append({"spec": spec, "expected": res})
    final.append({"id": "p%02d" % idx, "seed": h64(sc["seed"]), "context": context, "prior_calls": prior, "pitches": pitches,
                  "final_canonical": rnd.canonical(), "final_state_hash": hashlib.sha256(rnd.canonical().encode("utf-8")).hexdigest()})

# a pitch whose game/target@1 call is the one that rejects a raw output: 1 decision call, 2 raw draws
rnd = Random(REJ_SEED, "game:3c2a:rejection")
for _ in range(REJ_INDEX):
    rnd.stream(T).bounded(ROLL)
rej_spec = {"intent": "balanced", "explicit_take": False, "batter_side": "right", "pitcher_hand": "right", "called_pitch_mode": "non_automated"}
rej_res = pitch(rnd, rej_spec)
assert rej_res["steps"][0]["raw_draws"] == 2 and rej_res["calls_by_stream"][T] == 1 and rej_res["draws_by_stream"][T] == 2
final.append({"id": "rejection", "seed": h64(REJ_SEED), "context": "game:3c2a:rejection", "prior_calls": {T: REJ_INDEX},
              "pitches": [{"spec": rej_spec, "expected": rej_res}], "final_canonical": rnd.canonical(),
              "final_state_hash": hashlib.sha256(rnd.canonical().encode("utf-8")).hexdigest()})

out["scenarios"] = final
covered = {(p["expected"]["kind"], p["spec"]["explicit_take"]) for s in final for p in s["pitches"]}
assert wanted <= covered

# an explicit Take never draws contact or field; a chain must show it
assert all(not (p["spec"]["explicit_take"] and (C in p["expected"]["calls_by_stream"] or F in p["expected"]["calls_by_stream"])) for s in final for p in s["pitches"])

target = os.path.join(HERE, "..", "phase3c2a_pitch_branch_golden.json")
with open(target, "w", encoding="utf-8", newline="\n") as f:
    json.dump(out, f, indent=1, ensure_ascii=False)
    f.write("\n")
print("wrote", os.path.normpath(target), "scenarios:", len(final), "pitches:", sum(len(s["pitches"]) for s in final))
