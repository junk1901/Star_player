# Phase 3-C2B-2-P — Template Matrix

Policy lock and template feasibility. **No production code exists for any of this**: the matrix is the result of a test-only planner whose events were judged by the real `GameReducer.Apply` and compared with an independent Python oracle. It is generated, not hand-typed: `python tests/Fixtures/generator/render_inplay_feasibility_matrix.py` reads [PHASE_3C2B2_FEASIBILITY_RAW.json](PHASE_3C2B2_FEASIBILITY_RAW.json) (written by the xUnit run) and `content/tuning/virtual_tuning_v1.json`.

- Versions: inplay generation policy `inplay-mapping-v1`, walk-off official-record policy `walkoff-credit-v1`, template catalogue `inplay-template-v1`.
- Tuning: `tuning-virtual-proto-v1` (virtual prototype, not KBO/MLB data). Rule basis: the confirmed 2026 OBR as a **temporary prototype basis**; the corresponding 2026 KBO clauses were **not** certified (owner decision D4).
- Status vocabulary: `LEGAL_BASE`, `PENDING_ADVANCEMENT`, `NEEDS_WALKOFF_POLICY`, `UNSUPPORTED_TEMPLATE`, `UNREACHABLE_ZERO_PPM`, `DEFECT_OR_UNRESOLVED` (TASK section 7). `NEEDS_WALKOFF_POLICY` here means: legal **only** through the approved `walkoff-credit-v1` re-templating (the official result or the run count differs from the naive template of the drawn result) and verified legal under it.

## 1. The 60 nominal combinations: 45 positive, 15 zero

Counted from the tuning JSON: 10 batted balls x 6 defence results = 60 nominal combinations; **45** have a positive probability and **15** are exactly 0 ppm (45 + 15 = 60).

Positive combinations per batted ball: ground_weak 3, ground_medium 4, ground_hard 4, line_weak 4, line_medium 5, line_hard 6, fly_weak 4, fly_medium 6, fly_hard 6, popup 3.

### 1.1 The 45 positive combinations

`G` = the 24 ordinary situations (8 runner situations x 3 outs) of this combination, `W` = its 96 walk-off situations (the same 24 x 4 score differences, bottom of the 9th). Counts are `LEGAL_BASE / PENDING_ADVANCEMENT / NEEDS_WALKOFF_POLICY` from the raw rows.

| BattedBall | DefenseResult | ProbabilityPpm | TemplateId | ExpectedResult | FieldingAction | BatterDestination | RunnerPolicy | ErrorPolicy | AdvancementRequired | OfficialCreditPolicy | SupportStatus |
|---|---|---:|---|---|---|---|---|---|---|---|---|
| ground_weak | out | 880000 | `ipt1.ground.out` | Out | SS fields the grounder and throws to 1B; 1B makes the only putout. | out (no base) | `RP-OUT-GROUND-FORCED` | none; credits `SS:A,1B:PO` | no | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| ground_weak | single | 85000 | `ipt1.ground.single` | Single | Grounder through the infield into left field; no out is made, no throw is credited. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| ground_weak | reached_on_error | 35000 | `ipt1.ground.roe` | ReachedOnError | SS misplays the grounder (E6); no out is made. | 1B | `RP-ROE-FORCED-PUSH` | one error: SS; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| ground_medium | out | 780000 | `ipt1.ground.out` | Out | SS fields the grounder and throws to 1B; 1B makes the only putout. | out (no base) | `RP-OUT-GROUND-FORCED` | none; credits `SS:A,1B:PO` | no | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| ground_medium | single | 190000 | `ipt1.ground.single` | Single | Grounder through the infield into left field; no out is made, no throw is credited. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| ground_medium | double | 10000 | `ipt1.ground.double` | Double | Grounder down the line into the left-field corner; the batter-runner stops on second. | 2B | `RP-HIT-PLUS-2` | none; no putout, no assist, no error | conditional: a runner on 1B (`double_runner_on_first`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 12 / 12 / 0. W: 48 / 36 / 12 |
| ground_medium | reached_on_error | 20000 | `ipt1.ground.roe` | ReachedOnError | SS misplays the grounder (E6); no out is made. | 1B | `RP-ROE-FORCED-PUSH` | one error: SS; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| ground_hard | out | 600000 | `ipt1.ground.out` | Out | SS fields the grounder and throws to 1B; 1B makes the only putout. | out (no base) | `RP-OUT-GROUND-FORCED` | none; credits `SS:A,1B:PO` | no | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| ground_hard | single | 340000 | `ipt1.ground.single` | Single | Grounder through the infield into left field; no out is made, no throw is credited. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| ground_hard | double | 40000 | `ipt1.ground.double` | Double | Grounder down the line into the left-field corner; the batter-runner stops on second. | 2B | `RP-HIT-PLUS-2` | none; no putout, no assist, no error | conditional: a runner on 1B (`double_runner_on_first`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 12 / 12 / 0. W: 48 / 36 / 12 |
| ground_hard | reached_on_error | 20000 | `ipt1.ground.roe` | ReachedOnError | SS misplays the grounder (E6); no out is made. | 1B | `RP-ROE-FORCED-PUSH` | one error: SS; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| line_weak | out | 470000 | `ipt1.line.out` | Out | SS catches the line drive. | out (no base) | `RP-OUT-HOLD` | none; credits `SS:PO` | no | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| line_weak | single | 480000 | `ipt1.line.single` | Single | Line drive falls in front of the center fielder. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| line_weak | double | 40000 | `ipt1.line.double` | Double | Line drive into the right-center gap; the batter-runner stops on second. | 2B | `RP-HIT-PLUS-2` | none; no putout, no assist, no error | conditional: a runner on 1B (`double_runner_on_first`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 12 / 12 / 0. W: 48 / 36 / 12 |
| line_weak | reached_on_error | 10000 | `ipt1.line.roe` | ReachedOnError | SS misplays the line drive (E6); no out is made. | 1B | `RP-ROE-FORCED-PUSH` | one error: SS; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| line_medium | out | 300000 | `ipt1.line.out` | Out | SS catches the line drive. | out (no base) | `RP-OUT-HOLD` | none; credits `SS:PO` | no | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| line_medium | single | 560000 | `ipt1.line.single` | Single | Line drive falls in front of the center fielder. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| line_medium | double | 120000 | `ipt1.line.double` | Double | Line drive into the right-center gap; the batter-runner stops on second. | 2B | `RP-HIT-PLUS-2` | none; no putout, no assist, no error | conditional: a runner on 1B (`double_runner_on_first`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 12 / 12 / 0. W: 48 / 36 / 12 |
| line_medium | triple | 8000 | `ipt1.line.triple` | Triple | Line drive into the right-field corner; the batter-runner stops on third. | 3B | `RP-HIT-PLUS-3` | none; no putout, no assist, no error | no | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 24 / 0 / 0. W: 72 / 0 / 24 |
| line_medium | reached_on_error | 12000 | `ipt1.line.roe` | ReachedOnError | SS misplays the line drive (E6); no out is made. | 1B | `RP-ROE-FORCED-PUSH` | one error: SS; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| line_hard | out | 180000 | `ipt1.line.out` | Out | SS catches the line drive. | out (no base) | `RP-OUT-HOLD` | none; credits `SS:PO` | no | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| line_hard | single | 480000 | `ipt1.line.single` | Single | Line drive falls in front of the center fielder. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| line_hard | double | 250000 | `ipt1.line.double` | Double | Line drive into the right-center gap; the batter-runner stops on second. | 2B | `RP-HIT-PLUS-2` | none; no putout, no assist, no error | conditional: a runner on 1B (`double_runner_on_first`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 12 / 12 / 0. W: 48 / 36 / 12 |
| line_hard | triple | 15000 | `ipt1.line.triple` | Triple | Line drive into the right-field corner; the batter-runner stops on third. | 3B | `RP-HIT-PLUS-3` | none; no putout, no assist, no error | no | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 24 / 0 / 0. W: 72 / 0 / 24 |
| line_hard | home_run | 60000 | `ipt1.line.homerun` | HomeRun | Line drive over the outfield fence (out of the playing field). | home (scored) | `RP-HR-ALL-SCORE` | none; no putout, no assist, no error | no | out-of-the-park (D2): batter and every runner score, also in a walk-off (OBR 9.06(g), 7.01(e)(3) exception) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| line_hard | reached_on_error | 15000 | `ipt1.line.roe` | ReachedOnError | SS misplays the line drive (E6); no out is made. | 1B | `RP-ROE-FORCED-PUSH` | one error: SS; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| fly_weak | out | 940000 | `ipt1.fly.out` | Out | CF catches the fly ball. | out (no base) | `RP-OUT-HOLD` | none; credits `CF:PO` | conditional: a runner on 3B, fewer than 2 outs (`fly_out_runner_on_third`), game not ended | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 16 / 8 / 0. W: 64 / 32 / 0 |
| fly_weak | single | 40000 | `ipt1.fly.single` | Single | Fly ball drops in front of the center fielder. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| fly_weak | double | 10000 | `ipt1.fly.double` | Double | Fly ball drops in the gap; the batter-runner stops on second. | 2B | `RP-HIT-PLUS-2` | none; no putout, no assist, no error | conditional: a runner on 1B (`double_runner_on_first`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 12 / 12 / 0. W: 48 / 36 / 12 |
| fly_weak | reached_on_error | 10000 | `ipt1.fly.roe` | ReachedOnError | CF drops the fly ball (E8); no out is made. | 1B | `RP-ROE-FORCED-PUSH` | one error: CF; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| fly_medium | out | 760000 | `ipt1.fly.out` | Out | CF catches the fly ball. | out (no base) | `RP-OUT-HOLD` | none; credits `CF:PO` | conditional: a runner on 3B, fewer than 2 outs (`fly_out_runner_on_third`), game not ended | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 16 / 8 / 0. W: 64 / 32 / 0 |
| fly_medium | single | 100000 | `ipt1.fly.single` | Single | Fly ball drops in front of the center fielder. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| fly_medium | double | 90000 | `ipt1.fly.double` | Double | Fly ball drops in the gap; the batter-runner stops on second. | 2B | `RP-HIT-PLUS-2` | none; no putout, no assist, no error | conditional: a runner on 1B (`double_runner_on_first`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 12 / 12 / 0. W: 48 / 36 / 12 |
| fly_medium | triple | 10000 | `ipt1.fly.triple` | Triple | Fly ball over the right fielder; the batter-runner stops on third. | 3B | `RP-HIT-PLUS-3` | none; no putout, no assist, no error | no | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 24 / 0 / 0. W: 72 / 0 / 24 |
| fly_medium | home_run | 30000 | `ipt1.fly.homerun` | HomeRun | Fly ball over the outfield fence (out of the playing field). | home (scored) | `RP-HR-ALL-SCORE` | none; no putout, no assist, no error | no | out-of-the-park (D2): batter and every runner score, also in a walk-off (OBR 9.06(g), 7.01(e)(3) exception) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| fly_medium | reached_on_error | 10000 | `ipt1.fly.roe` | ReachedOnError | CF drops the fly ball (E8); no out is made. | 1B | `RP-ROE-FORCED-PUSH` | one error: CF; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| fly_hard | out | 330000 | `ipt1.fly.out` | Out | CF catches the fly ball. | out (no base) | `RP-OUT-HOLD` | none; credits `CF:PO` | conditional: a runner on 3B, fewer than 2 outs (`fly_out_runner_on_third`), game not ended | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 16 / 8 / 0. W: 64 / 32 / 0 |
| fly_hard | single | 80000 | `ipt1.fly.single` | Single | Fly ball drops in front of the center fielder. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| fly_hard | double | 200000 | `ipt1.fly.double` | Double | Fly ball drops in the gap; the batter-runner stops on second. | 2B | `RP-HIT-PLUS-2` | none; no putout, no assist, no error | conditional: a runner on 1B (`double_runner_on_first`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 12 / 12 / 0. W: 48 / 36 / 12 |
| fly_hard | triple | 20000 | `ipt1.fly.triple` | Triple | Fly ball over the right fielder; the batter-runner stops on third. | 3B | `RP-HIT-PLUS-3` | none; no putout, no assist, no error | no | ordinary: official = drawn. Walk-off: `walkoff-credit-v1` (OBR 9.06(f)): bases = min(drawn bases, bases advanced by the winning-run scorer); facts = that template | SUPPORTED. G: 24 / 0 / 0. W: 72 / 0 / 24 |
| fly_hard | home_run | 360000 | `ipt1.fly.homerun` | HomeRun | Fly ball over the outfield fence (out of the playing field). | home (scored) | `RP-HR-ALL-SCORE` | none; no putout, no assist, no error | no | out-of-the-park (D2): batter and every runner score, also in a walk-off (OBR 9.06(g), 7.01(e)(3) exception) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| fly_hard | reached_on_error | 10000 | `ipt1.fly.roe` | ReachedOnError | CF drops the fly ball (E8); no out is made. | 1B | `RP-ROE-FORCED-PUSH` | one error: CF; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| popup | out | 975000 | `ipt1.pop.out` | Out | 2B catches the pop-up. | out (no base) | `RP-OUT-HOLD` | none; credits `2B:PO` | no | no hit credit; a bases-loaded ground out may force the winning run (unchanged result `Out`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |
| popup | single | 5000 | `ipt1.pop.single` | Single | Pop-up drops in shallow center field, beyond any infielder's ordinary effort; CF retrieves it. | 1B | `RP-HIT-PLUS-1` | none; no putout, no assist, no error | conditional: a runner on 1B (`single_runner_on_first`) or 2B (`single_runner_on_second`) and the base plan does not end the game | ordinary: official = drawn. Walk-off: credit is always 1 (the winning run comes from 3B); unchanged | SUPPORTED. G: 6 / 18 / 0. W: 33 / 63 / 0 |
| popup | reached_on_error | 20000 | `ipt1.pop.roe` | ReachedOnError | Pop-up in shallow center field, beyond any infielder's ordinary effort; CF could ordinarily catch it and drops it (E8). | 1B | `RP-ROE-FORCED-PUSH` | one error: CF; batter reaches 1B on it (no separate batter move); forced runners `Forced`, others `Play`-hold | no | no hit credit; a bases-loaded push may score the winning run (unchanged result `ReachedOnError`) | SUPPORTED. G: 24 / 0 / 0. W: 96 / 0 / 0 |

Intensity (weak / medium / hard) selects only the probability row; the template is the same, and the intensity stays in the internal audit record.

### 1.2 The 15 zero-probability combinations

These cannot be drawn under `tuning-virtual-proto-v1`; they are listed so that none is silently dropped, **not executed**, and not counted as passed. A tuning change that makes one positive needs a template, a re-run of this matrix and a new tuning version.

| BattedBall | DefenseResult | ProbabilityPpm | SupportStatus |
|---|---|---:|---|
| ground_weak | double | 0 | `UNREACHABLE_ZERO_PPM` |
| ground_weak | triple | 0 | `UNREACHABLE_ZERO_PPM` |
| ground_weak | home_run | 0 | `UNREACHABLE_ZERO_PPM` |
| ground_medium | triple | 0 | `UNREACHABLE_ZERO_PPM` |
| ground_medium | home_run | 0 | `UNREACHABLE_ZERO_PPM` |
| ground_hard | triple | 0 | `UNREACHABLE_ZERO_PPM` |
| ground_hard | home_run | 0 | `UNREACHABLE_ZERO_PPM` |
| line_weak | triple | 0 | `UNREACHABLE_ZERO_PPM` |
| line_weak | home_run | 0 | `UNREACHABLE_ZERO_PPM` |
| line_medium | home_run | 0 | `UNREACHABLE_ZERO_PPM` |
| fly_weak | triple | 0 | `UNREACHABLE_ZERO_PPM` |
| fly_weak | home_run | 0 | `UNREACHABLE_ZERO_PPM` |
| popup | double | 0 | `UNREACHABLE_ZERO_PPM` |
| popup | triple | 0 | `UNREACHABLE_ZERO_PPM` |
| popup | home_run | 0 | `UNREACHABLE_ZERO_PPM` |

## 2. Template catalogue `inplay-template-v1` (19 templates)

| TemplateId | Class | Result | Batter destination | Credits (PO = putout, A = assist) | Error | Retrieving fielder (hits, audit only) | Premise |
|---|---|---|---:|---|---|---|---|
| `ipt1.ground.out` | ground | Out | 0 | SS:A,1B:PO | - | - | No other play is attempted (no double play, no play at another base). |
| `ipt1.ground.single` | ground | Single | 1 | - | - | LF | Safe hit: no putout, no error. |
| `ipt1.ground.double` | ground | Double | 2 | - | - | LF | Safe hit: no putout, no error; the batter-runner's final base is second. |
| `ipt1.ground.roe` | ground | ReachedOnError | 1 | - | SS | - | One error by one fielder; never reclassified as a fielder's choice or a compound error. |
| `ipt1.line.out` | line | Out | 0 | SS:PO | - | - | Caught in the air: the only putout is the batter's; nobody is doubled off. |
| `ipt1.line.single` | line | Single | 1 | - | - | CF | Safe hit: no putout, no error. |
| `ipt1.line.double` | line | Double | 2 | - | - | RF | Safe hit: no putout, no error. |
| `ipt1.line.triple` | line | Triple | 3 | - | - | RF | Safe hit: no putout, no error. |
| `ipt1.line.homerun` | line | HomeRun | 4 | - | - | - | Out-of-the-park home run (owner D2); inside-the-park home runs are not generated. |
| `ipt1.line.roe` | line | ReachedOnError | 1 | - | SS | - | One error by one fielder. |
| `ipt1.fly.out` | fly | Out | 0 | CF:PO | - | - | Deep fly ball in the center fielder's range, beyond the infielders' ordinary effort: the Infield Fly rule does not apply. |
| `ipt1.fly.single` | fly | Single | 1 | - | - | CF | Safe hit: no putout, no error; beyond the infielders' ordinary effort. |
| `ipt1.fly.double` | fly | Double | 2 | - | - | CF | Safe hit: no putout, no error. |
| `ipt1.fly.triple` | fly | Triple | 3 | - | - | RF | Safe hit: no putout, no error. |
| `ipt1.fly.homerun` | fly | HomeRun | 4 | - | - | - | Out-of-the-park home run (owner D2). |
| `ipt1.fly.roe` | fly | ReachedOnError | 1 | - | CF | - | A ball the center fielder could ordinarily catch, beyond the infielders' ordinary effort. |
| `ipt1.pop.out` | pop | Out | 0 | 2B:PO | - | - | A pop-up an infielder catches with ordinary effort: with runners on first and second (or loaded) and fewer than two outs this is an Infield Fly; the caught ball gives the same record. |
| `ipt1.pop.single` | pop | Single | 1 | - | - | CF | Not catchable by an infielder with ordinary effort: the Infield Fly rule does NOT apply (by premise), whatever the runners. |
| `ipt1.pop.roe` | pop | ReachedOnError | 1 | - | CF | - | Not catchable by an infielder with ordinary effort (no Infield Fly, by premise); catchable by the outfielder in his own range. |

The fielder of a template is fixed (SS/1B for ground outs, SS for line outs and for E6, CF for fly outs and E8, 2B for pop outs). That is a deliberate prototype simplification with a statistical consequence recorded in section 7.

## 3. Base runner movement (all 8 runner situations)

Moves are listed from the lead runner back as `from>to` (4 = scored; `n>n` = holds, stated explicitly: every existing runner has exactly one `RunnerMove`). Read from the raw rows of the real run.

| Situation | Ground out, 0-1 out (`RP-OUT-GROUND-FORCED`) | Out with 2 outs, line / fly / pop out (`RP-OUT-HOLD`) | Single (+1) | Double (+2) | Triple (+3) / home run | Reached on error (`RP-ROE-FORCED-PUSH`) |
|---|---|---|---|---|---|---|
| empty | (no runner) | (no runner) | (no runner) | (no runner) | (no runner) | (no runner) |
| 1B | 1>2 | 1>1 | 1>2 | 1>3 | 1>4 | 1>2 |
| 2B | 2>2 | 2>2 | 2>3 | 2>4 | 2>4 | 2>2 |
| 1B+2B | 2>3,1>2 | 2>2,1>1 | 2>3,1>2 | 2>4,1>3 | 2>4,1>4 | 2>3,1>2 |
| 3B | 3>3 | 3>3 | 3>4 | 3>4 | 3>4 | 3>3 |
| 1B+3B | 3>3,1>2 | 3>3,1>1 | 3>4,1>2 | 3>4,1>3 | 3>4,1>4 | 3>3,1>2 |
| 2B+3B | 3>3,2>2 | 3>3,2>2 | 3>4,2>3 | 3>4,2>4 | 3>4,2>4 | 3>3,2>2 |
| loaded | 3>4,2>3,1>2 | 3>3,2>2,1>1 | 3>4,2>3,1>2 | 3>4,2>4,1>3 | 3>4,2>4,1>4 | 3>4,2>3,1>2 |

Causes: hits, outs and holds use `MoveCause.Play`; a runner pushed by a reaching batter on a reached-on-error uses `MoveCause.Forced`. The batter-runner of a hit or a reached-on-error has **no** `RunnerMove` of its own (his destination is the result); a batter out is the event's own out with its credits. No move carries an `OutKind`, a putout or an assist (no runner is ever put out in a base plan). No `Sequence` is ever given: no base plan contains a run together with a third out (checked on every row).

## 4. The 1080 ordinary situations (45 combinations x 8 runner situations x 3 outs)

Top of the 3rd inning, away team batting, 0-0, count 0-0, approach chosen: no walk-off is possible. Mirror runs (bottom of the 3rd, no designated hitter, a 3-2 count) are summarised in the report.

| Status | Cases |
|---|---:|
| `LEGAL_BASE` | 780 |
| `PENDING_ADVANCEMENT` | 300 |
| `NEEDS_WALKOFF_POLICY` | 0 |
| `UNSUPPORTED_TEMPLATE` | 0 |
| `DEFECT_OR_UNRESOLVED` | 0 |
| reducer accepted the base event | 1080 of 1080 |
| independent oracle and policy checks agree | 1080 of 1080 |

| Official result | LEGAL_BASE | PENDING_ADVANCEMENT | Total |
|---|---:|---:|---:|
| Out | 216 | 24 | 240 |
| Single | 60 | 180 | 240 |
| Double | 96 | 96 | 192 |
| Triple | 96 | 0 | 96 |
| HomeRun | 72 | 0 | 72 |
| ReachedOnError | 240 | 0 | 240 |

**Pending advancement.** A base plan is `PENDING_ADVANCEMENT` exactly when the situation is one of the four declared `advancement` contexts of `prob-model-v1` (`single_runner_on_first`, `single_runner_on_second`, `double_runner_on_first`, `fly_out_runner_on_third`) and the base plan did not already end the game. Of the 300 pending ordinary cases, **90 are a single named runner** with nothing else on base, and **210 are composite** (another runner is on base too): `prob-model-v1` declares no context, order or interaction for those, so the 3-C2B-3 design must define it (finding F-1 in the report). Composite cases by official result and situation:

| Official result | Situation | Cases (x combinations x outs) |
|---|---|---:|
| Double | 1B+2B | 24 |
| Double | 1B+3B | 24 |
| Double | loaded | 24 |
| Out | 1B+3B | 6 |
| Out | 2B+3B | 6 |
| Out | loaded | 6 |
| Single | 1B+2B | 30 |
| Single | 1B+3B | 30 |
| Single | 2B+3B | 30 |
| Single | loaded | 30 |

## 5. The 4320 walk-off situations (45 combinations x 8 runner situations x 3 outs x 4 score differences)

Bottom of the 9th inning, away team ahead by 0 (tied), 1, 2 or 3 runs. All 4320 nominal combinations are reachable game states and were executed; none had to be excluded as impossible. The game-end reading is the reducer's own: the home team scores more runs than the visitors.

| Status | Cases |
|---|---:|
| `LEGAL_BASE` | 3114 |
| `PENDING_ADVANCEMENT` | 1014 |
| `NEEDS_WALKOFF_POLICY` | 192 |
| `UNSUPPORTED_TEMPLATE` | 0 |
| `DEFECT_OR_UNRESOLVED` | 0 |
| reducer accepted the event | 4320 of 4320 |
| independent oracle and policy checks agree | 4320 of 4320 |

| Score behind | LEGAL_BASE | PENDING_ADVANCEMENT | NEEDS_WALKOFF_POLICY | game ends on the play | credit reduced |
|---|---:|---:|---:|---:|---:|
| tied | 774 | 138 | 168 | 456 | 168 |
| 1 | 780 | 276 | 24 | 159 | 24 |
| 2 | 780 | 300 | 0 | 48 | 0 |
| 3 | 780 | 300 | 0 | 9 | 0 |

**Official credit changed by `walkoff-credit-v1`** (the drawn result stays recorded; the facts of the play are those of the official template):

| Drawn | Official | Score behind | Cases |
|---|---|---:|---:|
| Double | Single | 0 | 96 |
| Triple | Double | 0 | 24 |
| Triple | Double | 1 | 24 |
| Triple | Single | 0 | 48 |

A walk-off by a non-home-run play scores **exactly** the runs needed, never more (the reducer would refuse more with `Game.run_after_winning_run`); a home run scores everyone. Checked on every row.

### 5.1 Outcome grids

Cell = `official result, runs scored` followed by a marker: `E` the game ends on the play, `C` the credit was reduced and the game ends, `P` pending advancement (nothing invented), `H` half-inning over. When the 0, 1 and 2 out variants differ the cell shows them separately (`0/1: ... ; 2: ...`). Rows are the runner situations, columns the score difference.

**Drawn Single**

| Situation | tied | behind 1 | behind 2 | behind 3 |
|---|---|---|---|---|
| empty | Single, 0r  | Single, 0r  | Single, 0r  | Single, 0r  |
| 1B | Single, 0r P | Single, 0r P | Single, 0r P | Single, 0r P |
| 2B | Single, 0r P | Single, 0r P | Single, 0r P | Single, 0r P |
| 1B+2B | Single, 0r P | Single, 0r P | Single, 0r P | Single, 0r P |
| 3B | Single, 1r E | Single, 1r  | Single, 1r  | Single, 1r  |
| 1B+3B | Single, 1r E | Single, 1r P | Single, 1r P | Single, 1r P |
| 2B+3B | Single, 1r E | Single, 1r P | Single, 1r P | Single, 1r P |
| loaded | Single, 1r E | Single, 1r P | Single, 1r P | Single, 1r P |

**Drawn Double**

| Situation | tied | behind 1 | behind 2 | behind 3 |
|---|---|---|---|---|
| empty | Double, 0r  | Double, 0r  | Double, 0r  | Double, 0r  |
| 1B | Double, 0r P | Double, 0r P | Double, 0r P | Double, 0r P |
| 2B | Double, 1r E | Double, 1r  | Double, 1r  | Double, 1r  |
| 1B+2B | Double, 1r E | Double, 1r P | Double, 1r P | Double, 1r P |
| 3B | Double->Single, 1r C | Double, 1r  | Double, 1r  | Double, 1r  |
| 1B+3B | Double->Single, 1r C | Double, 1r P | Double, 1r P | Double, 1r P |
| 2B+3B | Double->Single, 1r C | Double, 2r E | Double, 2r  | Double, 2r  |
| loaded | Double->Single, 1r C | Double, 2r E | Double, 2r P | Double, 2r P |

**Drawn Triple**

| Situation | tied | behind 1 | behind 2 | behind 3 |
|---|---|---|---|---|
| empty | Triple, 0r  | Triple, 0r  | Triple, 0r  | Triple, 0r  |
| 1B | Triple, 1r E | Triple, 1r  | Triple, 1r  | Triple, 1r  |
| 2B | Triple->Double, 1r C | Triple, 1r  | Triple, 1r  | Triple, 1r  |
| 1B+2B | Triple->Double, 1r C | Triple, 2r E | Triple, 2r  | Triple, 2r  |
| 3B | Triple->Single, 1r C | Triple, 1r  | Triple, 1r  | Triple, 1r  |
| 1B+3B | Triple->Single, 1r C | Triple, 2r E | Triple, 2r  | Triple, 2r  |
| 2B+3B | Triple->Single, 1r C | Triple->Double, 2r C | Triple, 2r  | Triple, 2r  |
| loaded | Triple->Single, 1r C | Triple->Double, 2r C | Triple, 3r E | Triple, 3r  |

**Drawn home run**

| Situation | tied | behind 1 | behind 2 | behind 3 |
|---|---|---|---|---|
| empty | HomeRun, 1r E | HomeRun, 1r  | HomeRun, 1r  | HomeRun, 1r  |
| 1B | HomeRun, 2r E | HomeRun, 2r E | HomeRun, 2r  | HomeRun, 2r  |
| 2B | HomeRun, 2r E | HomeRun, 2r E | HomeRun, 2r  | HomeRun, 2r  |
| 1B+2B | HomeRun, 3r E | HomeRun, 3r E | HomeRun, 3r E | HomeRun, 3r  |
| 3B | HomeRun, 2r E | HomeRun, 2r E | HomeRun, 2r  | HomeRun, 2r  |
| 1B+3B | HomeRun, 3r E | HomeRun, 3r E | HomeRun, 3r E | HomeRun, 3r  |
| 2B+3B | HomeRun, 3r E | HomeRun, 3r E | HomeRun, 3r E | HomeRun, 3r  |
| loaded | HomeRun, 4r E | HomeRun, 4r E | HomeRun, 4r E | HomeRun, 4r E |

**Drawn reached on error**

| Situation | tied | behind 1 | behind 2 | behind 3 |
|---|---|---|---|---|
| empty | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  |
| 1B | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  |
| 2B | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  |
| 1B+2B | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  |
| 3B | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  |
| 1B+3B | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  |
| 2B+3B | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  | ReachedOnError, 0r  |
| loaded | ReachedOnError, 1r E | ReachedOnError, 1r  | ReachedOnError, 1r  | ReachedOnError, 1r  |

**Drawn ground out**

| Situation | tied | behind 1 | behind 2 | behind 3 |
|---|---|---|---|---|
| empty | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 1B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 2B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 1B+2B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 3B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 1B+3B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 2B+3B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| loaded | 0/1: Out, 1r E ; 2: Out, 0r H | 0/1: Out, 1r  ; 2: Out, 0r H | 0/1: Out, 1r  ; 2: Out, 0r H | 0/1: Out, 1r  ; 2: Out, 0r H |

**Drawn fly out**

| Situation | tied | behind 1 | behind 2 | behind 3 |
|---|---|---|---|---|
| empty | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 1B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 2B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 1B+2B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 3B | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H |
| 1B+3B | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H |
| 2B+3B | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H |
| loaded | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H | 0/1: Out, 0r P ; 2: Out, 0r H |

**Drawn line / pop out**

| Situation | tied | behind 1 | behind 2 | behind 3 |
|---|---|---|---|---|
| empty | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 1B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 2B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 1B+2B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 3B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 1B+3B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| 2B+3B | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |
| loaded | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H | 0/1: Out, 0r  ; 2: Out, 0r H |

## 6. The ten representative walk-off / continuation cases

The expected values were derived by hand before the run and are asserted as literals in `Ten_representative_cases_match_their_hand_derived_expectations`; the columns below are the actual raw results.

| # | Situation | Drawn | Official | Moves | Runs | Game over | Walk-off effect | Status |
|---|---|---|---|---|---:|---|---|---|
| 1 | tied, runner on 3B, 0 outs, double drawn | Double | Single | 3>4 | 1 | yes | credit_reduced | `NEEDS_WALKOFF_POLICY` |
| 2 | tied, bases loaded, 1 out, double drawn | Double | Single | 3>4,2>3,1>2 | 1 | yes | credit_reduced | `NEEDS_WALKOFF_POLICY` |
| 3 | tied, runner on 2B, 0 outs, triple drawn | Triple | Double | 2>4 | 1 | yes | credit_reduced | `NEEDS_WALKOFF_POLICY` |
| 4 | tied, runner on 1B, 2 outs, triple drawn | Triple | Triple | 1>4 | 1 | yes | game_ends | `LEGAL_BASE` |
| 5 | tied, bases loaded, 0 outs, out-of-the-park home run | HomeRun | HomeRun | 3>4,2>4,1>4 | 4 | yes | game_ends | `LEGAL_BASE` |
| 6 | behind 1, runners on 2B and 3B, 1 out, double | Double | Double | 3>4,2>4 | 2 | yes | game_ends | `LEGAL_BASE` |
| 7 | behind 2, bases loaded, 2 outs, triple | Triple | Triple | 3>4,2>4,1>4 | 3 | yes | game_ends | `LEGAL_BASE` |
| 8 | tied, runner on 2B, 0 outs, single | Single | Single | 2>3 | 0 | no | none | `PENDING_ADVANCEMENT` |
| 9 | tied, bases loaded, 2 outs, single reached on error | ReachedOnError | ReachedOnError | 3>4,2>3,1>2 | 1 | yes | game_ends | `LEGAL_BASE` |
| 10 | tied, no runner, 0 outs, double | Double | Double | - | 0 | no | none | `LEGAL_BASE` |

## 7. Statistical bias of the fixed fielders (illustration, not calibration)

Templates fix the fielder of every out and error. Given a batted-ball location group (`heart`, `edge`, `outside` rows of `batted_ball`), the expected share of all **outs** each position would receive, from the tuning rows alone (exact arithmetic on the ppm values, `P(batted ball) x P(out | batted ball)`):

| Location group | 1B putouts (ground outs) | SS assists (ground outs) | SS putouts (line outs) | CF putouts (fly outs) | 2B putouts (pop outs) | E6 share of errors | E8 share of errors | other fielders' share of putouts / assists / errors |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| heart | 33.5 % | 33.5 % | 12.1 % | 38.5 % | 15.8 % | 63.5 % | 36.5 % | 0 % |
| edge | 34.6 % | 34.6 % | 10.2 % | 33.5 % | 21.6 % | 62.7 % | 37.3 % | 0 % |
| outside | 35.3 % | 35.3 % | 8.6 % | 31.2 % | 24.9 % | 61.9 % | 38.1 % | 0 % |

So in the prototype 1B receives every ground-out putout, SS every ground-out assist, and 2B only pop-out putouts; 3B, LF, RF and C never receive a putout or an assist on a ball in play; no double play, fielder's choice or sacrifice is ever produced; and every error is an E6 or an E8. These distributions are a property of the fixed templates, not of the tuning; they must not be read as league-like defensive statistics and need the 3-F calibration (or fielder selection by hit location) before any statistic that depends on them is shown.
