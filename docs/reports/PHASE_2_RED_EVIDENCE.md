# Phase 2 RED 검증 증거 (추적 가능한 요약)

저장소는 `.gitignore`의 `*.log` 규칙으로 모든 `.log` 파일을 Git에서 제외한다(이 정책은 유지한다). 그래서 수정 전 실패(RED) 원본 로그 5개는 로컬 `docs/reports/`에 **그대로 보존**되지만 Git에는 올라가지 않는다. 이 문서는 각 원본 로그에서 **실제 출력 그대로** 뽑은 실패 요약(테스트 이름, 요약 줄, SHA-256)을 추적 가능한 Markdown으로 옮긴 것이다. 원본과 이 문서가 다르면 원본이 우선한다. 새로 지어낸 결과는 없다.

작성: 2026-10-11 (Claude Code). 각 항목의 "수정 후" 결과는 해당 Part의 `PHASE_2_REPORT.md`와 `PHASE_2_VERIFY_LOG_*.md`(추적됨)에 있다.

## C33 승계 주자 책임(슬롯 장부) 수정 전

- 원본(로컬, Git 제외): `docs/reports/PHASE_2_C33_RED.log` — 165줄, SHA-256 `aba137c921921893bf877565e3f06ce7fabe55fab47e94918cc421b82db48c42`
- 테스트 클래스: `ResponsibilityLedgerTests`
- 요약 줄(원본 그대로): `(요약 줄 없음)`
- 수정 전 실패한 테스트 8개(원본의 `[FAIL]` 줄):

  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.When_the_relief_pitcher_walks_the_first_batter_on_a_decided_count_the_walked_runner_is_still_Peters_charge`
  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.Example_5_three_pitchers_two_fielders_choice_force_outs_and_a_home_run_charge_one_run_to_each`
  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.Example_6_a_runner_forced_out_at_home_leaves_the_runs_of_Baker_and_Charlie_split_between_Peter_and_Roger`
  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.Runs_charged_by_the_ledger_get_their_earned_verdict_from_each_pitchers_own_reconstruction`
  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.A_rejected_event_leaves_the_state_and_every_charge_untouched`
  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.Example_2_a_runner_forced_out_by_a_fielders_choice_leaves_Peters_charge_with_the_batter_who_took_his_place`
  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.The_loss_goes_to_Peter_when_the_go_ahead_run_is_the_one_Peters_charge_was_carried_to_by_a_fielders_choice`
  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.Example_3_a_runner_of_Peter_out_at_home_on_a_fielders_choice_keeps_Peters_charge_for_the_next_run_to_score`

## C33 모호한 복합 야수선택 거부 수정 전

- 원본(로컬, Git 제외): `docs/reports/PHASE_2_C33_AMBIGUOUS_RED.log` — 94줄, SHA-256 `7b288cbaed8be2ad503e2a821ddc2cfef1cb8d8be4e3a05b5bfb4b2e6348d36d`
- 테스트 클래스: `ResponsibilityLedgerTests`
- 요약 줄(원본 그대로): `(요약 줄 없음)`
- 수정 전 실패한 테스트 3개(원본의 `[FAIL]` 줄):

  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.The_order_of_the_two_outs_does_not_make_the_play_decidable`
  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.A_refused_play_can_be_replaced_by_a_decidable_one_and_the_game_goes_on_with_the_old_charges`
  - `StarPlayer.Simulation.Tests.ResponsibilityLedgerTests.A_fielders_choice_that_retires_runners_of_two_different_pitchers_is_refused_because_the_slot_to_pass_on_is_not_decided_by_the_input`

## C41 안타와 포스아웃 모순 수정 전

- 원본(로컬, Git 제외): `docs/reports/PHASE_2_HIT_FORCE_RED.log` — 127줄, SHA-256 `398a35cc1ac5bd94d832bd898c8d383baf918ce90dcfdb99f90da4797132a675`
- 테스트 클래스: `HitWithForceOutTests`
- 요약 줄(원본 그대로): `(요약 줄 없음)`
- 수정 전 실패한 테스트 3개(원본의 `[FAIL]` 줄):

  - `StarPlayer.Simulation.Tests.HitWithForceOutTests.A_forced_runner_put_out_by_a_tag_is_still_a_force_out_and_the_hit_is_refused`
  - `StarPlayer.Simulation.Tests.HitWithForceOutTests.A_single_on_a_batted_ball_that_forces_out_a_preceding_runner_is_refused_and_nothing_is_recorded`
  - `StarPlayer.Simulation.Tests.HitWithForceOutTests.A_force_removed_by_an_earlier_out_leaves_the_later_out_a_tag_out_and_the_hit_stands_in_that_order_only`

## C43 실책으로 면한 포스아웃 수정 전

- 원본(로컬, Git 제외): `docs/reports/PHASE_2_C43_RED.log` — 140줄, SHA-256 `002b4c219ec0cce3103b51568178565d799a641cab11a64bcba2dc1255ba17a6`
- 테스트 클래스: `HitErrorForceTests`
- 요약 줄(원본 그대로): `(요약 줄 없음)`
- 수정 전 실패한 테스트 3개(원본의 `[FAIL]` 줄):

  - `StarPlayer.Simulation.Tests.HitErrorForceTests.A_hit_with_a_runner_on_first_and_an_error_that_no_move_explains_is_refused_as_unsupported`
  - `StarPlayer.Simulation.Tests.HitErrorForceTests.The_same_two_moves_in_the_other_order_keep_the_force_on_the_saved_runner_and_the_hit_is_refused`
  - `StarPlayer.Simulation.Tests.HitErrorForceTests.An_accepted_single_with_an_explained_error_is_a_harmless_duplicate_and_the_refused_one_never_is`

## C44 다중 실책 수정 전

- 원본(로컬, Git 제외): `docs/reports/PHASE_2_C44_MULTI_ERROR_RED.log` — 56줄, SHA-256 `cf6e4cc35204a3d23a01808146c2c8ea39670d52994a146a9d3b277a7cacfe55`
- 테스트 클래스: `HitMultiErrorTests`
- 요약 줄(원본 그대로): `실패!  - 실패:     5, 통과:     3, 건너뜀:     0, 전체:     8, 기간: 18 ms - StarPlayer.Simulation.Tests.dll (net8.0)`
- 수정 전 실패한 테스트 3개(원본의 `[FAIL]` 줄):

  - `StarPlayer.Simulation.Tests.HitMultiErrorTests.A_re_delivered_accepted_event_is_a_duplicate_and_the_refused_one_stays_refused`
  - `StarPlayer.Simulation.Tests.HitMultiErrorTests.A_single_with_a_runner_on_first_two_errors_and_one_error_move_is_refused`
  - `StarPlayer.Simulation.Tests.HitMultiErrorTests.Two_errors_and_two_error_moves_without_a_link_between_them_are_not_taken_as_explained`

- 실패 메시지 종류(원본 그대로, 중복 제거):

  - `the multi-error hit was accepted`

## C44 다중 실책 — 수정 후(GREEN) 실행 증거

- 명령: `dotnet test tests/StarPlayer.Simulation.Tests --filter "FullyQualifiedName~HitMultiError|FullyQualifiedName~HitErrorForce"` (종료 코드 0)
- 출력 요약 줄: `(없음)` — `HitMultiErrorTests` 8건과 기존 `HitErrorForceTests` 11건
- `HitErrorReplayTests`(Infrastructure): `통과!  - 실패:     0, 통과:     1`
- 전체 게이트: `docs/reports/PHASE_2_VERIFY_LOG_c44multi_run1.md`, `docs/reports/PHASE_2_VERIFY_LOG_c44multi.md` (둘 다 `GATE: PASS`, 종료 코드 0)
- 수정 전 실패한 5건(A·B·C·재전달)은 위 "C44 다중 실책 수정 전" 항목이며, 수정 후 같은 5건이 통과했다.
