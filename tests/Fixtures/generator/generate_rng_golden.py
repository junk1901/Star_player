"""Independent PCG32 / key-derivation reference for Phase 3-A golden vectors.

Written in Python straight from the published PCG reference (pcg_basic.c: pcg32_srandom_r, pcg32_random_r, pcg32_boundedrand_r, pcg_advance)
and from the kd1 / rngset1 text in docs/decisions/ADR-004.md. It shares NO code with src/StarPlayer.Simulation/Randomness (SHA-256 comes from
Python's hashlib). Before writing anything it checks itself against the values PUBLISHED by the PCG author (pcg-random.org, "Using PCG
(C Basic)", demo output round 1, seed 42 / sequence 54): the six 32-bit values and the 33 dice rolls (bounded(6)+1), which follow 6 + 65
earlier draws. If those assertions fail the script stops and writes nothing.

Run:  python tests/Fixtures/generator/generate_rng_golden.py
"""
import hashlib
import json
import os
import struct

M64 = 0xFFFFFFFFFFFFFFFF
MULT = 6364136223846793005


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
        assert bound >= 1
        threshold = ((1 << 32) - bound) % bound
        while True:
            r = self.u32()
            if r >= threshold:
                return r % bound

    def advance(self, delta):
        # naive stepping on purpose (the C# side uses the O(log n) jump; this checks it against the plain definition)
        for _ in range(delta):
            self._step()
        self.draws += delta


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


# ---- self-check against the PCG author's published demo output ----
PUBLISHED_U32 = [0xA15C02B7, 0x7B47F409, 0xBA1D3330, 0x83D2F293, 0xBFA4784B, 0xCBED606E]
PUBLISHED_ROLLS = [int(x) for x in "3 4 1 1 2 2 3 2 4 3 2 4 3 3 5 2 3 1 3 1 5 1 4 1 5 6 4 6 6 2 6 3 3".split()]
p = Pcg32(42, 54)
assert [p.u32() for _ in range(6)] == PUBLISHED_U32, "does not reproduce the published 32-bit output"
p = Pcg32(42, 54)
for _ in range(71):
    p.u32()
assert [p.bounded(6) + 1 for _ in range(33)] == PUBLISHED_ROLLS, "does not reproduce the published dice rolls"

out = {
    "generator": "tests/Fixtures/generator/generate_rng_golden.py",
    "published_pcg_demo": {
        "source": "pcg-random.org Using PCG (C Basic), pcg32-demo round 1; pcg_basic.c at github.com/imneme/pcg-c-basic",
        "initstate": 42,
        "initseq": 54,
        "u32": [h32(v) for v in PUBLISHED_U32],
        "skip_draws_before_rolls": 71,
        "rolls_bounded6_plus1": PUBLISHED_ROLLS,
    },
}

seed_vectors = []
for st, sq in [(42, 54), (0, 0), (1, 1), (M64, M64), (0x123456789ABCDEF0, 0x0FEDCBA987654321), (0, M64), (M64, 0)]:
    p = Pcg32(st, sq)
    seed_vectors.append({"initstate": h64(st), "initseq": h64(sq), "increment": h64(p.inc), "state_after_seeding": h64(p.state),
                         "u32": [h32(p.u32()) for _ in range(8)]})
out["seed_vectors"] = seed_vectors

bounded_vectors = []
for bound in [1, 2, 3, 6, 7, 100, 1000000, 0x80000000, 0x80000001, 0xC0000001, 0xFFFFFFFF]:
    p = Pcg32(0x0123456789ABCDEF, 0x1111)
    values, cumulative = [], []
    for _ in range(40):
        values.append(p.bounded(bound))
        cumulative.append(p.draws)
    bounded_vectors.append({"initstate": h64(0x0123456789ABCDEF), "initseq": h64(0x1111), "bound": bound, "values": values,
                            "cumulative_draws": cumulative,
                            "threshold": ((1 << 32) - bound) % bound})
out["bounded_vectors"] = bounded_vectors

advance_vectors = []
for delta in [0, 1, 2, 3, 63, 64, 1000, 12345]:
    p = Pcg32(42, 54)
    p.advance(delta)
    advance_vectors.append({"initstate": h64(42), "initseq": h64(54), "delta": delta, "state": h64(p.state), "next_u32": h32(p.u32())})
out["advance_vectors"] = advance_vectors

kd_vectors = []
for seed, ctx, stream in [
    (0, "game:KBO-T:fx-0001", "game/pitch@1"),
    (0, "game:KBO-T:fx-0001", "game/location@1"),
    (0, "game:KBO-T:fx-0001", "game/location@2"),
    (0, "game:KBO-T:fx-0002", "game/location@1"),
    (1, "game:KBO-T:fx-0001", "game/location@1"),
    (0x0123456789ABCDEF, "g", "game/ai@1"),
    (M64, "game:ünï", "game/field@1"),
]:
    st, sq = kd1(seed, ctx, stream)
    p = Pcg32(st, sq)
    kd_vectors.append({"seed": h64(seed), "context": ctx, "stream": stream, "initstate": h64(st), "initseq": h64(sq),
                       "u32": [h32(p.u32()) for _ in range(6)]})
out["kd_vectors"] = kd_vectors

# A set scenario: operations on several streams, then the rngset1 canonical text and its SHA-256.
SEED, CTX = 0x0123456789ABCDEF, "game:KBO-T:fx-0001"
ops = [("game/pitch@1", "u32", 5), ("game/contact@1", "bounded", 6), ("game/contact@1", "bounded", 6), ("game/contact@1", "bounded", 100),
       ("game/location@1", "u32", 2), ("game/pitch@1", "bounded", 0x80000001)]
streams = {}
produced = []
for sid, kind, arg in ops:
    if sid not in streams:
        streams[sid] = Pcg32(*kd1(SEED, CTX, sid))
    g = streams[sid]
    if kind == "u32":
        produced.append([h32(g.u32()) for _ in range(arg)])
    else:
        produced.append([g.bounded(arg)])
lines = ["rngset1|pcg32-xsh-rr-v1|kd1|%s|%s" % (h64(SEED), CTX)]
for sid in sorted(streams):
    g = streams[sid]
    lines.append("%s|%s|%s|%d" % (sid, h64(g.state), h64(g.inc), g.draws))
canonical = "\n".join(lines)
out["set_vector"] = {
    "seed": h64(SEED), "context": CTX,
    "ops": [{"stream": s, "kind": k, "arg": a} for s, k, a in ops],
    "produced": produced,
    "canonical": canonical,
    "state_hash": hashlib.sha256(canonical.encode("utf-8")).hexdigest(),
    "next_u32_per_stream": {sid: h32(streams[sid].u32()) for sid in sorted(streams)},
}

target = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "phase3a_rng_golden.json")
with open(target, "w", encoding="utf-8", newline="\n") as f:
    json.dump(out, f, indent=1, ensure_ascii=False)
    f.write("\n")
print("wrote", os.path.normpath(target))
