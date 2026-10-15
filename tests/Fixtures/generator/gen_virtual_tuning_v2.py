"""Generates content/tuning/virtual_tuning_v2.json (Phase 3-D, prob-model-v2 / tuning-virtual-proto-v2) and computes its profile hash INDEPENDENTLY of the C# code.

  python tests/Fixtures/generator/gen_virtual_tuning_v2.py            -> writes the JSON, prints the content hash
  python tests/Fixtures/generator/gen_virtual_tuning_v2.py --hash     -> reads the JSON, recomputes the canonical text and hash (the C# test pins this value)

v2 is v1 (content/tuning/virtual_tuning_v1.json, never edited) with exactly four changes (owner decision P4): the model and tuning version texts, the provenance text, and the three
park slots, which are no longer reserved:  factor = 1_000_000 + (park_permille - 1000) * 1000, input range 800..1250 permille, factor range 800_000..1_250_000.
Every baseline row, scenario, target, tendency cap, combined cap and every other modifier is copied from v1 unchanged. The numbers are VIRTUAL PROTOTYPE values (not KBO statistics);
their balance is judged in Phase 3-F.
"""
import hashlib
import importlib.util
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
V1 = ROOT / "content" / "tuning" / "virtual_tuning_v1.json"
OUT = ROOT / "content" / "tuning" / "virtual_tuning_v2.json"

# reuse the independent canonical-text/hash/write functions of the v1 generator without running its main block
spec = importlib.util.spec_from_file_location("gen_v1", HERE / "gen_virtual_tuning_v1.py")
gen_v1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gen_v1)  # importing runs nothing but the table construction: the v1 main block is guarded
gen_v1.OUT = OUT  # write_json() of the v1 script writes to OUT

PARK = {"park.home_runs", "park.hits", "park.extra_base_hits"}


def build():
    doc = json.loads(V1.read_text(encoding="utf-8"))
    doc["model_version"] = "prob-model-v2"
    doc["tuning_version"] = "tuning-virtual-proto-v2"
    doc["provenance"] = ("VIRTUAL PROTOTYPE values for a fictional league: tuning-virtual-proto-v1 plus the three park slots (1000 permille neutral, 1000 ppm per permille, "
                         "800..1250 permille); not derived from KBO, MLB or any real statistics; balance is judged in Phase 3-F")
    mods = []
    for m in doc["modifiers"]:
        if m["slot"] in PARK:
            m = {"slot": m["slot"], "neutral": 1000, "slope_ppm_per_unit": 1000, "min_ppm": 800_000, "max_ppm": 1_250_000, "table": None}
        mods.append(m)
    doc["modifiers"] = mods
    return doc


if __name__ == "__main__":
    if "--hash" in sys.argv:
        loaded = json.loads(OUT.read_text(encoding="utf-8"))
        print(hashlib.sha256(gen_v1.canonical(loaded).encode("utf-8")).hexdigest())
    else:
        doc = build()
        gen_v1.write_json(doc)
        print(hashlib.sha256(gen_v1.canonical(doc).encode("utf-8")).hexdigest())
