"""Independent RNG reference for Phase 3-E Game Calling v4 (GC-S2): the first `target_zone` and `actual_location` draws of a pitch.

Written in Python from the published PCG reference (pcg_basic.c: pcg32_srandom_r, pcg32_random_r, pcg32_boundedrand_r) and the `kd1` key derivation of docs/decisions/ADR-004.md. It shares NO code with
src/StarPlayer.Simulation/Randomness and none with the production pitch resolver or the commit-evidence replay. It first checks itself against the values PUBLISHED by the PCG author
(pcg-random.org "Using PCG (C Basic)", seed 42 / sequence 54) and stops if they do not reproduce.

For a game context, a master seed and a number of earlier whole `NextBounded(1,000,000)` calls on the `game/target@1` and `game/location@1` streams (the position H0 a pitch starts from) it records the
NEXT bounded roll and the raw 32-bit outputs that call consumed (rejection sampling can use more than one) on each of the two streams, and the two calls after it. The C# tests compare the rolls the
real resolver drew, and the rolls the commit evidence replays from H0, with these numbers.

Run:  python tests/Fixtures/generator/generate_phase3e_game_calling_rng_reference.py
Writes tests/Fixtures/phase3e_game_calling_rng_reference.json (deterministic).
"""
import hashlib
import json
import os
import struct

M64 = 0xFFFFFFFFFFFFFFFF
MULT = 6364136223846793005
BOUND = 1_000_000


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


# ---- self-check against the PCG author's published demo output ----
PUBLISHED_U32 = [0xA15C02B7, 0x7B47F409, 0xBA1D3330, 0x83D2F293, 0xBFA4784B, 0xCBED606E]
p = Pcg32(42, 54)
assert [p.u32() for _ in range(6)] == PUBLISHED_U32, "does not reproduce the published 32-bit output"
PUBLISHED_ROLLS = [int(x) for x in "3 4 1 1 2 2 3 2 4 3 2 4 3 3 5 2 3 1 3 1 5 1 4 1 5 6 4 6 6 2 6 3 3".split()]
p = Pcg32(42, 54)
for _ in range(71):
    p.u32()
assert [p.bounded(6) + 1 for _ in range(33)] == PUBLISHED_ROLLS, "does not reproduce the published dice rolls"

CONTEXT = "game:3c3b:probe"          # Pc.Gid of the C# commit fixtures
STREAMS = {"target": "game/target@1", "location": "game/location@1"}


def next_calls(seed, stream, prior, count):
    st, sq = kd1(seed, CONTEXT, stream)
    g = Pcg32(st, sq)
    for _ in range(prior):
        g.bounded(BOUND)
    out = []
    for _ in range(count):
        before = g.draws
        roll = g.bounded(BOUND)
        out.append({"roll": roll, "raw": g.draws - before})
    return out


records = []
SEEDS = list(range(1, 41)) + [9999, 123456789, 0xFFFFFFFF, 0x0123456789ABCDEF]
for seed in SEEDS:
    for prior_target in (0, 1, 3):
        for prior_location in (0, 2, 5):
            records.append({
                "seed": f"{seed:016x}", "prior_target_calls": prior_target, "prior_location_calls": prior_location,
                "target": next_calls(seed, STREAMS["target"], prior_target, 3),
                "location": next_calls(seed, STREAMS["location"], prior_location, 3),
            })

# Rejection sampling at bound 1,000,000 rejects about 0.02% of the raw outputs, so the sample above never shows a second raw draw. Scan for seeds whose FIRST target or location call really rejected
# (raw draws > 1) and add them (no earlier calls), so the replay's raw-draw comparison is exercised by genuine cases and not only by the one-draw-per-call rule.
rejected = []
for seed in range(1, 400_000):
    t = next_calls(seed, STREAMS["target"], 0, 3)
    l = next_calls(seed, STREAMS["location"], 0, 3)
    if any(c["raw"] > 1 for c in t[:1] + l[:1]):
        rejected.append({"seed": f"{seed:016x}", "prior_target_calls": 0, "prior_location_calls": 0, "target": t, "location": l})
    if len(rejected) == 6:
        break
assert len(rejected) >= 2, "no rejected first draw found in the scanned range"
records.extend(rejected)
multi = sum(1 for r in records for s in ("target", "location") for c in r[s] if c["raw"] > 1)
# The pitch stream (`game/pitch@1`) is shared by the batter decision (call 1) and the called pitch (call 2) of an explicit Take. Find seeds where the two calls consumed DIFFERENT numbers of raw outputs (one of
# them rejected): swapping those two raw-draw numbers keeps the stream's total, so the per-stream growth check of the commit gate cannot see it - only a per-step replay can.
pitch_swaps = []
for seed in range(1, 600_000):
    calls = next_calls(seed, "game/pitch@1", 0, 2)
    if calls[0]["raw"] != calls[1]["raw"]:
        pitch_swaps.append({"seed": f"{seed:016x}", "pitch": calls})
    if len(pitch_swaps) == 6:
        break
assert len(pitch_swaps) >= 2, "no seed with different raw draws on the first two pitch-stream calls in the scanned range"

doc = {"context": CONTEXT, "bound": BOUND, "streams": STREAMS, "records": records, "pitch_stream_swaps": pitch_swaps}
out = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "phase3e_game_calling_rng_reference.json"))
with open(out, "w", encoding="utf-8", newline="\n") as f:
    json.dump(doc, f, indent=1, sort_keys=True)
    f.write("\n")
print(f"{len(records)} records, {multi} calls with raw draws > 1 -> {out}")
