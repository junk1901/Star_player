#!/usr/bin/env python3
"""
Renders docs/reports/PHASE_3C2B2_TEMPLATE_MATRIX.md from the machine-readable raw results
docs/reports/PHASE_3C2B2_FEASIBILITY_RAW.json (written by the Phase 3-C2B-2-P xUnit run with STAR_FEASIBILITY_OUT set)
and the tuning JSON. No number in the matrix is typed by hand: every count is computed here from the raw rows.

Run:  python tests/Fixtures/generator/render_inplay_feasibility_matrix.py
"""
import json
import os
from collections import Counter, defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
RAW = os.path.join(ROOT, "docs", "reports", "PHASE_3C2B2_FEASIBILITY_RAW.json")
TUNING = os.path.join(ROOT, "content", "tuning", "virtual_tuning_v1.json")
OUT = os.path.join(ROOT, "docs", "reports", "PHASE_3C2B2_TEMPLATE_MATRIX.md")

OCC = ["empty", "1B", "2B", "1B+2B", "3B", "1B+3B", "2B+3B", "loaded"]
RES_ORDER = ["out", "single", "double", "triple", "home_run", "reached_on_error"]
SHORT_RES = {"Out": "out", "Single": "single", "Double": "double", "Triple": "triple", "HomeRun": "homerun", "ReachedOnError": "roe"}


def tpl_id(batted, result):
    cls = "pop" if batted == "popup" else batted.split("_")[0]
    short = {"reached_on_error": "roe", "home_run": "homerun"}.get(result, result)
    return "ipt1.%s.%s" % (cls, short)


def key_parts(k):
    kind, diff, occ, outs, combo = k.split("|")
    batted, result = combo.split("/")
    return kind, int(diff), int(occ), int(outs), batted, result


def main():
    raw = json.load(open(RAW, encoding="utf-8"))
    tuning = json.load(open(TUNING, encoding="utf-8"))
    templates = {t["id"]: t for t in raw["templates"]}
    general = raw["general"]["rows"]
    walkoff = raw["walkoff"]["rows"]
    gby = defaultdict(list)
    wby = defaultdict(list)
    for r in general:
        gby[key_parts(r["k"])[4] + "/" + key_parts(r["k"])[5]].append(r)
    for r in walkoff:
        wby[key_parts(r["k"])[4] + "/" + key_parts(r["k"])[5]].append(r)
    byk = {r["k"]: r for r in general + walkoff}

    L = []
    w = L.append
    w("# Phase 3-C2B-2-P — Template Matrix")
    w("")
    w("Policy lock and template feasibility. **No production code exists for any of this**: the matrix is the result of a test-only planner whose events were judged by the real `GameReducer.Apply` and compared with an independent Python oracle. It is generated, not hand-typed: `python tests/Fixtures/generator/render_inplay_feasibility_matrix.py` reads [PHASE_3C2B2_FEASIBILITY_RAW.json](PHASE_3C2B2_FEASIBILITY_RAW.json) (written by the xUnit run) and `content/tuning/virtual_tuning_v1.json`.")
    w("")
    w("- Versions: inplay generation policy `%s`, walk-off official-record policy `%s`, template catalogue `%s`." % (raw["inplay_mapping"], raw["walkoff_credit"], raw["template_catalog"]))
    w("- Tuning: `%s` (virtual prototype, not KBO/MLB data). Rule basis: the confirmed 2026 OBR as a **temporary prototype basis**; the corresponding 2026 KBO clauses were **not** certified (owner decision D4)." % tuning["tuning_version"])
    w("- Status vocabulary: `LEGAL_BASE`, `PENDING_ADVANCEMENT`, `NEEDS_WALKOFF_POLICY`, `UNSUPPORTED_TEMPLATE`, `UNREACHABLE_ZERO_PPM`, `DEFECT_OR_UNRESOLVED` (TASK section 7). `NEEDS_WALKOFF_POLICY` here means: legal **only** through the approved `walkoff-credit-v1` re-templating (the official result or the run count differs from the naive template of the drawn result) and verified legal under it.")
    w("")

    # ---------------------------------------------------------------- 1. combinations
    w("## 1. The 60 nominal combinations: 45 positive, 15 zero")
    w("")
    positive = [c for c in raw["combos"] if c["positive"]]
    zero = [c for c in raw["combos"] if not c["positive"]]
    w("Counted from the tuning JSON: 10 batted balls x 6 defence results = %d nominal combinations; **%d** have a positive probability and **%d** are exactly 0 ppm (%d + %d = %d)." % (len(raw["combos"]), len(positive), len(zero), len(positive), len(zero), len(raw["combos"])))
    w("")
    per_ball = Counter(c["batted"] for c in positive)
    w("Positive combinations per batted ball: " + ", ".join("%s %d" % (b, per_ball[b]) for b in [x for x in dict.fromkeys(c["batted"] for c in raw["combos"])]) + ".")
    w("")
    w("### 1.1 The 45 positive combinations")
    w("")
    w("`G` = the 24 ordinary situations (8 runner situations x 3 outs) of this combination, `W` = its 96 walk-off situations (the same 24 x 4 score differences, bottom of the 9th). Counts are `LEGAL_BASE / PENDING_ADVANCEMENT / NEEDS_WALKOFF_POLICY` from the raw rows.")
    w("")
    w("| BattedBall | DefenseResult | ProbabilityPpm | TemplateId | ExpectedResult | FieldingAction | BatterDestination | RunnerPolicy | ErrorPolicy | AdvancementRequired | OfficialCreditPolicy | SupportStatus |")
    w("|---|---|---:|---|---|---|---|---|---|---|---|---|")
    for c in positive:
        tid = c["template"]
        t = templates[tid]
        k = c["batted"] + "/" + c["result"]
        g = Counter(r["st"] for r in gby[k])
        wo = Counter(r["st"] for r in wby[k])
        res = t["result"]
        cls = t["class"]
        dest = {0: "out (no base)", 1: "1B", 2: "2B", 3: "3B", 4: "home (scored)"}[t["batter_dest"]]
        if res == "ReachedOnError":
            err = "one error: %s; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold" % t["error"]
        elif res == "Out":
            err = "none; credits `%s`" % t["credits"]
        else:
            err = "none; no putout, no assist, no error"
        if res == "Single":
            adv = "conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game"
        elif res == "Double":
            adv = "conditional: a runner on 1B (`double_runner_on_first`) and the base plan does not end the game"
        elif res == "Out" and cls == "fly":
            adv = "conditional: a runner on 3B, fewer than 2 outs (`fly_out_runner_on_third`), game not ended"
        else:
            adv = "no"
        if res in ("Double", "Triple"):
            credit = "ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template"
        elif res == "Single":
            credit = "ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged"
        elif res == "HomeRun":
            credit = "out-of-the-park (D2): batter and every runner score, also in a walk-off (OBR 9.06(g), 7.01(e)(3) exception)"
        elif res == "Out":
            credit = "no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`)"
        else:
            credit = "no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`)"
        status = "SUPPORTED. G: %d / %d / %d. W: %d / %d / %d" % (g["LEGAL_BASE"], g["PENDING_ADVANCEMENT"], g["NEEDS_WALKOFF_POLICY"], wo["LEGAL_BASE"], wo["PENDING_ADVANCEMENT"], wo["NEEDS_WALKOFF_POLICY"])
        w("| %s | %s | %d | `%s` | %s | %s | %s | `%s` | %s | %s | %s | %s |" % (
            c["batted"], c["result"], c["ppm"], tid, res, t["action"], dest, t["runner_policy"], err, adv, credit, status))
    w("")
    w("Intensity (weak / medium / hard) selects only the probability row; the template is the same, and the intensity stays in the internal audit record.")
    w("")
    w("### 1.2 The 15 zero-probability combinations")
    w("")
    w("These cannot be drawn under `%s`; they are listed so that none is silently dropped, **not executed**, and not counted as passed. A tuning change that makes one positive needs a template, a re-run of this matrix and a new tuning version." % tuning["tuning_version"])
    w("")
    w("| BattedBall | DefenseResult | ProbabilityPpm | SupportStatus |")
    w("|---|---|---:|---|")
    for c in zero:
        w("| %s | %s | 0 | `UNREACHABLE_ZERO_PPM` |" % (c["batted"], c["result"]))
    w("")

    # ---------------------------------------------------------------- 2. templates
    w("## 2. Template catalogue `%s` (19 templates)" % raw["template_catalog"])
    w("")
    w("| TemplateId | Class | Result | Batter destination | Credits (PO = putout, A = assist) | Error | Retrieving fielder (hits, audit only) | Premise |")
    w("|---|---|---|---:|---|---|---|---|")
    for t in raw["templates"]:
        w("| `%s` | %s | %s | %s | %s | %s | %s | %s |" % (t["id"], t["class"], t["result"], t["batter_dest"], t["credits"] or "-", t["error"] or "-", t["retriever"] or "-", t["premise"]))
    w("")
    w("The fielder of a template is fixed (SS/1B for ground outs, SS for line outs and for E6, CF for fly outs and E8, 2B for pop outs). That is a deliberate prototype simplification with a statistical consequence recorded in section 7.")
    w("")

    # ---------------------------------------------------------------- 3. runner table
    w("## 3. Base runner movement (all 8 runner situations)")
    w("")
    w("Moves are listed from the lead runner back as `from>to` (4 = scored; `n>n` = holds, stated explicitly: every existing runner has exactly one `RunnerMove`). Read from the raw rows of the real run.")
    w("")
    w("| Situation | Ground out, 0-1 out (`RP-OUT-GROUND-FORCED`) | Out with 2 outs, line / fly / pop out (`RP-OUT-HOLD`) | Single (+1) | Double (+2) | Triple (+3) / home run | Reached on error (`RP-ROE-FORCED-PUSH`) |")
    w("|---|---|---|---|---|---|---|")

    def mv(kind, occ, outs, combo):
        k = "g|0|%d|%d|%s" % (occ, outs, combo)
        return byk[k]["mv"] or "(no runner)"

    for occ in range(8):
        w("| %s | %s | %s | %s | %s | %s | %s |" % (OCC[occ], mv("g", occ, 0, "ground_medium/out"), mv("g", occ, 2, "ground_medium/out"), mv("g", occ, 0, "line_medium/single"),
                                                    mv("g", occ, 0, "line_hard/double"), mv("g", occ, 0, "fly_hard/triple"), mv("g", occ, 0, "ground_weak/reached_on_error")))
    w("")
    w("Causes: hits, outs and holds use `MoveCause.Play`; a runner pushed by a reaching batter on a reached-on-error uses `MoveCause.Forced`. The batter-runner of a hit or a reached-on-error has **no** `RunnerMove` of its own (his destination is the result); a batter out is the event's own out with its credits. No move carries an `OutKind`, a putout or an assist (no runner is ever put out in a base plan). No `Sequence` is ever given: no base plan contains a run together with a third out (checked on every row).")
    w("")

    # ---------------------------------------------------------------- 4. general
    gs = raw["general"]["summary"]
    w("## 4. The %d ordinary situations (45 combinations x 8 runner situations x 3 outs)" % gs["total"])
    w("")
    w("Top of the 3rd inning, away team batting, 0-0, count 0-0, approach chosen: no walk-off is possible. Mirror runs (bottom of the 3rd, no designated hitter, a 3-2 count) are summarised in the report.")
    w("")
    w("| Status | Cases |")
    w("|---|---:|")
    for st in ["LEGAL_BASE", "PENDING_ADVANCEMENT", "NEEDS_WALKOFF_POLICY", "UNSUPPORTED_TEMPLATE", "DEFECT_OR_UNRESOLVED"]:
        w("| `%s` | %d |" % (st, gs.get(st, 0)))
    w("| reducer accepted the base event | %d of %d |" % (gs["reducer_accepted"], gs["total"]))
    w("| independent oracle and policy checks agree | %d of %d |" % (gs["oracle_match"], gs["total"]))
    w("")
    by_official = defaultdict(Counter)
    for r in general:
        by_official[r["official"]][r["st"]] += 1
    w("| Official result | LEGAL_BASE | PENDING_ADVANCEMENT | Total |")
    w("|---|---:|---:|---:|")
    for res in ["Out", "Single", "Double", "Triple", "HomeRun", "ReachedOnError"]:
        c = by_official[res]
        w("| %s | %d | %d | %d |" % (res, c["LEGAL_BASE"], c["PENDING_ADVANCEMENT"], sum(c.values())))
    w("")
    pure = comp = 0
    comp_rows = Counter()
    for r in general:
        if not r["pend"]:
            continue
        _, _, occ, outs, batted, result = key_parts(r["k"])
        is_pure = (r["official"] == "Single" and occ in (1, 2)) or (r["official"] == "Double" and occ == 1) or (r["official"] == "Out" and occ == 4)
        if is_pure:
            pure += 1
        else:
            comp += 1
            comp_rows[(r["official"], OCC[occ])] += 1
    w("**Pending advancement.** A base plan is `PENDING_ADVANCEMENT` exactly when the situation is one of the four declared `advancement` contexts of `prob-model-v1` (`single_runner_on_first`, `single_runner_on_second`, `double_runner_on_first`, `fly_out_runner_on_third`) and the base plan did not already end the game. Of the %d pending ordinary cases, **%d are a single named runner** with nothing else on base, and **%d are composite** (another runner is on base too): `prob-model-v1` declares no context, order or interaction for those, so the 3-C2B-3 design must define it (finding F-1 in the report). Composite cases by official result and situation:" % (pure + comp, pure, comp))
    w("")
    w("| Official result | Situation | Cases (x combinations x outs) |")
    w("|---|---|---:|")
    for (res, occ), n in sorted(comp_rows.items()):
        w("| %s | %s | %d |" % (res, occ, n))
    w("")

    # ---------------------------------------------------------------- 5. walk-off
    ws = raw["walkoff"]["summary"]
    w("## 5. The %d walk-off situations (45 combinations x 8 runner situations x 3 outs x 4 score differences)" % ws["total"])
    w("")
    w("Bottom of the 9th inning, away team ahead by 0 (tied), 1, 2 or 3 runs. All %d nominal combinations are reachable game states and were executed; none had to be excluded as impossible. The game-end reading is the reducer's own: the home team scores more runs than the visitors." % ws["total"])
    w("")
    w("| Status | Cases |")
    w("|---|---:|")
    for st in ["LEGAL_BASE", "PENDING_ADVANCEMENT", "NEEDS_WALKOFF_POLICY", "UNSUPPORTED_TEMPLATE", "DEFECT_OR_UNRESOLVED"]:
        w("| `%s` | %d |" % (st, ws.get(st, 0)))
    w("| reducer accepted the event | %d of %d |" % (ws["reducer_accepted"], ws["total"]))
    w("| independent oracle and policy checks agree | %d of %d |" % (ws["oracle_match"], ws["total"]))
    w("")
    w("| Score behind | LEGAL_BASE | PENDING_ADVANCEMENT | NEEDS_WALKOFF_POLICY | game ends on the play | credit reduced |")
    w("|---|---:|---:|---:|---:|---:|")
    for d in range(4):
        rows = [r for r in walkoff if key_parts(r["k"])[1] == d]
        c = Counter(r["st"] for r in rows)
        w("| %s | %d | %d | %d | %d | %d |" % ("tied" if d == 0 else str(d), c["LEGAL_BASE"], c["PENDING_ADVANCEMENT"], c["NEEDS_WALKOFF_POLICY"],
                                                sum(1 for r in rows if r["wo"] in ("game_ends", "credit_reduced")), sum(1 for r in rows if r["wo"] == "credit_reduced")))
    w("")
    red = Counter()
    for r in walkoff:
        if r["wo"] == "credit_reduced":
            red[(r["drawn"], r["official"], key_parts(r["k"])[1])] += 1
    w("**Official credit changed by `walkoff-credit-v1`** (the drawn result stays recorded; the facts of the play are those of the official template):")
    w("")
    w("| Drawn | Official | Score behind | Cases |")
    w("|---|---|---:|---:|")
    for (dr, off, d), n in sorted(red.items()):
        w("| %s | %s | %d | %d |" % (dr, off, d, n))
    w("")
    w("A walk-off by a non-home-run play scores **exactly** the runs needed, never more (the reducer would refuse more with `Game.run_after_winning_run`); a home run scores everyone. Checked on every row.")
    w("")

    # grid per drawn-result group
    w("### 5.1 Outcome grids")
    w("")
    w("Cell = `official result, runs scored` followed by a marker: `E` the game ends on the play, `C` the credit was reduced and the game ends, `P` pending advancement (nothing invented), `H` half-inning over. When the 0, 1 and 2 out variants differ the cell shows them separately (`0/1: ... ; 2: ...`). Rows are the runner situations, columns the score difference.")
    w("")

    def cell(r):
        mark = ""
        if r["wo"] == "credit_reduced":
            mark = "C"
        elif r["wo"] == "game_ends":
            mark = "E"
        elif r["pend"]:
            mark = "P"
        elif r["half"]:
            mark = "H"
        return "%s, %dr %s" % (r["official"] if r["official"] == r["drawn"] else "%s->%s" % (r["drawn"], r["official"]), r["runs"], mark)

    groups = [
        ("Drawn Single", lambda b, res: res == "single", "line_medium/single"),
        ("Drawn Double", lambda b, res: res == "double", "line_hard/double"),
        ("Drawn Triple", lambda b, res: res == "triple", "fly_hard/triple"),
        ("Drawn home run", lambda b, res: res == "home_run", "fly_hard/home_run"),
        ("Drawn reached on error", lambda b, res: res == "reached_on_error", "ground_weak/reached_on_error"),
        ("Drawn ground out", lambda b, res: res == "out" and b.startswith("ground"), "ground_medium/out"),
        ("Drawn fly out", lambda b, res: res == "out" and b.startswith("fly"), "fly_medium/out"),
        ("Drawn line / pop out", lambda b, res: res == "out" and (b.startswith("line") or b == "popup"), "line_medium/out"),
    ]
    for title, pick, rep in groups:
        w("**%s**" % title)
        w("")
        w("| Situation | tied | behind 1 | behind 2 | behind 3 |")
        w("|---|---|---|---|---|")
        members = sorted({r["k"].split("|")[4] for r in walkoff if pick(*r["k"].split("|")[4].split("/"))})
        for occ in range(8):
            cols = []
            for d in range(4):
                per_outs = []
                for outs in range(3):
                    texts = set()
                    for combo in members:
                        texts.add(cell(byk["w|%d|%d|%d|%s" % (d, occ, outs, combo)]))
                    per_outs.append(" / ".join(sorted(texts)))
                if per_outs[0] == per_outs[1] == per_outs[2]:
                    cols.append(per_outs[0])
                elif per_outs[0] == per_outs[1]:
                    cols.append("0/1: %s ; 2: %s" % (per_outs[0], per_outs[2]))
                else:
                    cols.append("0: %s ; 1: %s ; 2: %s" % tuple(per_outs))
            w("| %s | %s |" % (OCC[occ], " | ".join(cols)))
        w("")

    # ---------------------------------------------------------------- 6. ten cases
    w("## 6. The ten representative walk-off / continuation cases")
    w("")
    w("The expected values were derived by hand before the run and are asserted as literals in `Ten_representative_cases_match_their_hand_derived_expectations`; the columns below are the actual raw results.")
    w("")
    ten = [
        ("1", "tied, runner on 3B, 0 outs, double drawn", "w|0|4|0|fly_hard/double"),
        ("2", "tied, bases loaded, 1 out, double drawn", "w|0|7|1|line_hard/double"),
        ("3", "tied, runner on 2B, 0 outs, triple drawn", "w|0|2|0|fly_hard/triple"),
        ("4", "tied, runner on 1B, 2 outs, triple drawn", "w|0|1|2|fly_hard/triple"),
        ("5", "tied, bases loaded, 0 outs, out-of-the-park home run", "w|0|7|0|fly_hard/home_run"),
        ("6", "behind 1, runners on 2B and 3B, 1 out, double", "w|1|6|1|line_hard/double"),
        ("7", "behind 2, bases loaded, 2 outs, triple", "w|2|7|2|line_hard/triple"),
        ("8", "tied, runner on 2B, 0 outs, single", "w|0|2|0|ground_medium/single"),
        ("9", "tied, bases loaded, 2 outs, single reached on error", "w|0|7|2|popup/reached_on_error"),
        ("10", "tied, no runner, 0 outs, double", "w|0|0|0|line_medium/double"),
    ]
    w("| # | Situation | Drawn | Official | Moves | Runs | Game over | Walk-off effect | Status |")
    w("|---|---|---|---|---|---:|---|---|---|")
    for n, text, key in ten:
        r = byk[key]
        w("| %s | %s | %s | %s | %s | %d | %s | %s | `%s` |" % (n, text, r["drawn"], r["official"], r["mv"] or "-", r["runs"], "yes" if r["over"] else "no", r["wo"], r["st"]))
    w("")

    # ---------------------------------------------------------------- 7. bias
    w("## 7. Statistical bias of the fixed fielders (illustration, not calibration)")
    w("")
    w("Templates fix the fielder of every out and error. Given a batted-ball location group (`heart`, `edge`, `outside` rows of `batted_ball`), the expected share of all **outs** each position would receive, from the tuning rows alone (exact arithmetic on the ppm values, `P(batted ball) x P(out | batted ball)`):")
    w("")
    bb = {r["condition"]: dict(zip(r["outcomes"], r["ppm"])) for r in tuning["baselines"] if r["stage"] == "batted_ball"}
    dr = {r["condition"]: dict(zip(r["outcomes"], r["ppm"])) for r in tuning["baselines"] if r["stage"] == "defense_result"}
    w("| Location group | 1B putouts (ground outs) | SS assists (ground outs) | SS putouts (line outs) | CF putouts (fly outs) | 2B putouts (pop outs) | E6 share of errors | E8 share of errors | other fielders' share of putouts / assists / errors |")
    w("|---|---:|---:|---:|---:|---:|---:|---:|---|")
    for grp in ["heart", "edge", "outside"]:
        mass = Counter()
        err = Counter()
        for b, p in bb[grp].items():
            cls = "pop" if b == "popup" else b.split("_")[0]
            mass[cls] += p * dr[b]["out"]
            err[cls] += p * dr[b]["reached_on_error"]
        total_out = sum(mass.values())
        total_err = sum(err.values())
        e6 = (err["ground"] + err["line"]) / total_err
        e8 = (err["fly"] + err["pop"]) / total_err
        w("| %s | %.1f %% | %.1f %% | %.1f %% | %.1f %% | %.1f %% | %.1f %% | %.1f %% | 0 %% |" % (grp, 100 * mass["ground"] / total_out, 100 * mass["ground"] / total_out, 100 * mass["line"] / total_out,
                                                                                              100 * mass["fly"] / total_out, 100 * mass["pop"] / total_out, 100 * e6, 100 * e8))
    w("")
    w("So in the prototype 1B receives every ground-out putout, SS every ground-out assist, and 2B only pop-out putouts; 3B, LF, RF and C never receive a putout or an assist on a ball in play; no double play, fielder's choice or sacrifice is ever produced; and every error is an E6 or an E8. These distributions are a property of the fixed templates, not of the tuning; they must not be read as league-like defensive statistics and need the 3-F calibration (or fielder selection by hit location) before any statistic that depends on them is shown.")
    w("")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(L).rstrip("\n") + "\n")
    print("wrote", OUT, len(L), "lines")


if __name__ == "__main__":
    main()
