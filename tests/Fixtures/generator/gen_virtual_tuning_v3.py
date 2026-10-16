"""Generates content/tuning/virtual_tuning_v3.json (Phase 3-E, prob-model-v3 / tuning-virtual-proto-v3) and computes its profile hash INDEPENDENTLY of the C# code.

  python tests/Fixtures/generator/gen_virtual_tuning_v3.py            -> writes the JSON, prints the content hash
  python tests/Fixtures/generator/gen_virtual_tuning_v3.py --hash     -> reads the JSON, recomputes the canonical text and hash (the C# test pins this value)

v3 is v2 (content/tuning/virtual_tuning_v2.json, never edited) with exactly four changes (owner decision E1-E7, Codex PASS_WITH_NOTES): the model and tuning version texts, the provenance
text, and the `catcher.framing` modifier, which is no longer reserved:  factor = 1_000_000 + (framing - 50) * 1000, input range 0..100, factor range 950_000..1_050_000.
Every baseline row, scenario, target, tendency cap, combined cap and every other modifier is copied from v2 unchanged. The numbers are VIRTUAL PROTOTYPE values (not KBO statistics);
their balance is judged in Phase 3-F. WHERE the framing factor may act (non_automated mode, Borderline zones) is the model's ModifierConditionScope, not tuning data.
"""
import hashlib
import importlib.util
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
V2 = ROOT / "content" / "tuning" / "virtual_tuning_v2.json"
OUT = ROOT / "content" / "tuning" / "virtual_tuning_v3.json"

# reuse the independent canonical-text/hash/write functions of the v1 generator without running its main block
spec = importlib.util.spec_from_file_location("gen_v1", HERE / "gen_virtual_tuning_v1.py")
gen_v1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gen_v1)
gen_v1.OUT = OUT  # write_json() of the v1 script writes to OUT


def build():
    doc = json.loads(V2.read_text(encoding="utf-8"))
    assert doc["model_version"] == "prob-model-v2" and doc["tuning_version"] == "tuning-virtual-proto-v2"
    doc["model_version"] = "prob-model-v3"
    doc["tuning_version"] = "tuning-virtual-proto-v3"
    doc["provenance"] = ("VIRTUAL PROTOTYPE values for a fictional league: tuning-virtual-proto-v2 plus the catcher.framing slot (50 neutral, 1000 ppm per rating point, "
                         "factor 950000..1050000, non_automated Borderline called pitches only); not derived from KBO, MLB or any real statistics; balance is judged in Phase 3-F")
    mods = []
    for m in doc["modifiers"]:
        if m["slot"] == "catcher.framing":
            m = {"slot": "catcher.framing", "neutral": 50, "slope_ppm_per_unit": 1000, "min_ppm": 950_000, "max_ppm": 1_050_000, "table": None}
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
