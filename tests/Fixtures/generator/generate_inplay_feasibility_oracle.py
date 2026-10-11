#!/usr/bin/env python3
"""
Phase 3-C2B-2-P independent oracle (policy lock and template feasibility).

Writes tests/Fixtures/phase3c2b2p_inplay_oracle.json: for every positive-probability (batted ball, defence result) combination of
content/tuning/virtual_tuning_v1.json and every runner situation, the EXPECTED virtual play under the owner-approved prototype policy
(ADR-007 "Decisions (3-C2B-2 prototype policy)"): template, official result, runs, outs, runners left, walk-off effect, pending-advancement flag, status.

This script shares NO code with the C# tests or production code. It reads only the tuning JSON. The runner rules are written here a second time, from the
policy text, so a disagreement with the C# harness is a finding, not a coincidence.

Run:  python tests/Fixtures/generator/generate_inplay_feasibility_oracle.py
"""
import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
TUNING = os.path.join(ROOT, "content", "tuning", "virtual_tuning_v1.json")
OUT = os.path.join(ROOT, "tests", "Fixtures", "phase3c2b2p_inplay_oracle.json")

POLICY = {
    "inplay_mapping": "inplay-mapping-v1",
    "walkoff_credit": "walkoff-credit-v1",
    "template_catalog": "inplay-template-v1",
}

# ---- templates (19 = every (class, result) pair that has a positive-probability combination) --------------------------------------------------------------
# credits: position:kind (PO = putout, A = assist); err = the single error position of a reached-on-error template.
RESULT_NAMES = {"out": "Out", "single": "Single", "double": "Double", "triple": "Triple", "home_run": "HomeRun", "reached_on_error": "ReachedOnError"}
RES_SHORT = {"out": "out", "single": "single", "double": "double", "triple": "triple", "home_run": "homerun", "reached_on_error": "roe"}
BATTER_DEST = {"out": 0, "single": 1, "double": 2, "triple": 3, "home_run": 4, "reached_on_error": 1}
BASES = {"single": 1, "double": 2, "triple": 3}

OUT_CREDITS = {
    "ground": ["SS:A", "1B:PO"],
    "line": ["SS:PO"],
    "fly": ["CF:PO"],
    "pop": ["2B:PO"],
}
ROE_ERROR = {"ground": "SS", "line": "SS", "fly": "CF", "pop": "CF"}


def cls_of(batted):
    return "pop" if batted == "popup" else batted.split("_")[0]


def tpl_id(cls, result):
    return "ipt1.%s.%s" % (cls, RES_SHORT[result])


def occupied(occ):
    return [b for b in (1, 2, 3) if occ & (1 << (b - 1))]


def forced(b, occ):
    """A runner on base b is forced when every base behind him (1..b-1) is occupied; the batter-runner makes base 1 'behind' every runner."""
    return all(occ & (1 << (x - 1)) for x in range(1, b))


def moves_for(result, cls, occ, outs):
    """Runner destinations (base -> destination 1..3 stay/advance, 4 scored) for the base template; the batter's destination is BATTER_DEST."""
    mv = {}
    if result == "out":
        # ground ball, fewer than two outs: the runners the batter's run to first forces along move one base; everyone else holds.
        # otherwise (line / fly / pop, or the batter's out is the third out): every runner holds.
        if cls == "ground" and outs < 2:
            for b in occupied(occ):
                mv[b] = b + 1 if forced(b, occ) else b
        else:
            for b in occupied(occ):
                mv[b] = b
    elif result in BASES:
        k = BASES[result]
        for b in occupied(occ):
            mv[b] = min(4, b + k)
    elif result == "home_run":
        for b in occupied(occ):
            mv[b] = 4
    elif result == "reached_on_error":
        for b in occupied(occ):
            mv[b] = b + 1 if forced(b, occ) else b
    return mv


def simulate(result, cls, occ, outs, walkoff, diff):
    """diff = away runs - home runs (>= 0) when walkoff is True (bottom of the 9th, home batting); ignored otherwise."""
    drawn = result
    need = diff + 1
    official = result
    reduced = False
    if walkoff and result in ("double", "triple"):
        k = BASES[result]
        scorers = sorted([b for b in occupied(occ) if b + k >= 4], reverse=True)  # lead runner first
        if len(scorers) >= need:
            win_base = scorers[need - 1]
            k2 = min(k, 4 - win_base)
            if k2 != k:
                reduced = True
                official = {1: "single", 2: "double", 3: "triple"}[k2]
    mv = moves_for(official, cls, occ, outs)
    runs = sum(1 for d in mv.values() if d == 4) + (1 if official == "home_run" else 0)
    outs_after = outs + (1 if official == "out" else 0)
    half_over = outs_after == 3
    ends = walkoff and runs >= need
    if walkoff and official in ("single", "double", "triple", "reached_on_error", "out") and ends:
        assert runs == need, ("non-home-run walk-off must score exactly the needed runs", official, occ, outs, diff, runs)
    # runners standing after the play (a scored runner is gone; the batter stands on his base unless he is out or scored)
    standing = [d for d in mv.values() if d < 4]
    if BATTER_DEST[official] in (1, 2, 3):
        standing.append(BATTER_DEST[official])
    assert len(set(standing)) == len(standing), ("two runners on one base", official, occ, outs)
    mask = 0
    for d in standing:
        mask |= 1 << (d - 1)
    lob = len(standing) if (half_over or ends) else 0
    # pending advancement: the base plan does not end the game, but the situation is one of the four declared 'advancement' contexts of prob-model-v1
    ctx = False
    if official == "single" and (occ & 0b011):
        ctx = True            # single_runner_on_first / single_runner_on_second
    if official == "double" and (occ & 0b001):
        ctx = True            # double_runner_on_first
    if official == "out" and cls == "fly" and outs < 2 and (occ & 0b100):
        ctx = True            # fly_out_runner_on_third
    pending = ctx and not ends and not half_over
    game_over = ends or (half_over and walkoff and diff >= 1)
    if reduced and ends:
        status = "NEEDS_WALKOFF_POLICY"
    elif pending:
        status = "PENDING_ADVANCEMENT"
    else:
        status = "LEGAL_BASE"
    wo = "none"
    if walkoff and ends:
        wo = "credit_reduced" if reduced else "game_ends"
    rbi = 0 if official == "reached_on_error" else runs
    return {
        "drawn": RESULT_NAMES[drawn], "official": RESULT_NAMES[official], "runs": runs, "outs_after": outs_after, "half_over": half_over,
        "game_over": game_over, "bases_after": mask, "lob": lob, "rbi": rbi,
        "hit": 1 if official in ("single", "double", "triple", "home_run") else 0, "ab": 1,
        "pending": pending, "context_present": ctx, "walkoff": wo, "status": status,
        "moves": {str(b): d for b, d in sorted(mv.items())},
    }


def main():
    tuning = json.load(open(TUNING, encoding="utf-8"))
    rows = {r["condition"]: dict(zip(r["outcomes"], r["ppm"])) for r in tuning["baselines"] if r["stage"] == "defense_result"}
    batted_order = [r["condition"] for r in tuning["baselines"] if r["stage"] == "defense_result"]
    defense_order = ["out", "single", "double", "triple", "home_run", "reached_on_error"]
    combos = []
    for batted in batted_order:
        for res in defense_order:
            ppm = rows[batted][res]
            combos.append({"batted": batted, "result": res, "ppm": ppm, "positive": ppm > 0,
                           "template": tpl_id(cls_of(batted), res) if ppm > 0 else None})
    positive = [c for c in combos if c["positive"]]
    assert len(batted_order) == 10 and len(combos) == 60
    if len(positive) != 45:
        sys.exit("expected 45 positive combinations, found %d" % len(positive))

    templates = {}
    for c in positive:
        cls = cls_of(c["batted"])
        res = c["result"]
        tid = tpl_id(cls, res)
        credits = OUT_CREDITS[cls] if res == "out" else []
        templates[tid] = {
            "class": cls, "result": RESULT_NAMES[res], "batter_dest": BATTER_DEST[res], "credits": credits,
            "error": ROE_ERROR[cls] if res == "reached_on_error" else None,
        }

    cases = {}
    for walkoff, diffs in ((False, [0]), (True, [0, 1, 2, 3])):
        for diff in diffs:
            for occ in range(8):
                for outs in range(3):
                    for c in positive:
                        key = "%s|%d|%d|%d|%s/%s" % ("w" if walkoff else "g", diff, occ, outs, c["batted"], c["result"])
                        r = simulate(c["result"], cls_of(c["batted"]), occ, outs, walkoff, diff)
                        r["template"] = c["template"]
                        cases[key] = r

    doc = {
        "format": "phase3c2b2p-inplay-oracle-v1",
        "policy": POLICY,
        "source": "independent Python oracle; reads only content/tuning/virtual_tuning_v1.json; shares no code with the C# tests",
        "tuning_version": tuning["tuning_version"],
        "combos": combos,
        "positive_count": len(positive),
        "zero_count": len(combos) - len(positive),
        "templates": templates,
        "cases": cases,
    }
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, ensure_ascii=False, separators=(",", ":"), sort_keys=True)
        f.write("\n")
    by_status = {}
    for k, v in cases.items():
        by_status.setdefault((k[0], v["status"]), 0)
        by_status[(k[0], v["status"])] += 1
    print("positive=%d zero=%d templates=%d cases=%d" % (len(positive), len(combos) - len(positive), len(templates), len(cases)))
    for k in sorted(by_status):
        print(k, by_status[k])


if __name__ == "__main__":
    main()
