# StarPlayer.SimHarness

Headless, deterministic runner of the production baseball engine (Phase 3-F **F-A** execution harness + **F-B** metrics collection). Smoke-sized only: the 100,000-PA and 100-game validations (F-C) are **not** implemented or approved.

## What it is

A consumer of `StarPlayer.Domain` and `StarPlayer.Simulation` and nothing else (no Infrastructure, no Application, no `InternalsVisibleTo` into production). It chooses an action through an `IGamePolicy`, then confirms what the engine builds: `GameStarter.Begin` → `PitchPreparation` / `InPlayPitchPlanning` / `AdvancementPlanning` → `PitchCommitGate` / `CommitGate` → `GameCursor.Publish`. It computes no probability, outcome, base running, scoring, inning change or ending.

Pinned inputs: `prob-model-v4` / `tuning-virtual-proto-v4` (embedded `content/tuning/virtual_tuning_v4.json`), a `gamerules-v2` profile (embedded `content/rules/game_rules_profiles_v2.json`; fictional test profiles, **not** the 2026 KBO rules), `advancement-policy-v2`, a neutral park snapshot and a `player-input-set-v3` of the chosen cohort.

## Use

```
dotnet run -c Release --project tools/StarPlayer.SimHarness -- run [--seeds 3,11,29] [--rules non-automated|automated] [--advancement v1|v2] [--event-budget N] [--tuning FILE] [--run-id ID] [--out DIR]
dotnet run -c Release --project tools/StarPlayer.SimHarness -- replay --manifest FILE
```

`run` plays one game per seed on the **neutral** cohort (every rating 50, all right-handed) and writes `manifest.json`, `summary.json`, `summary.txt` (default `build/simharness/<run id>/`, git-ignored). `replay` plays a neutral-cohort manifest again and compares the result hash. Exit code: 0 every game completed and the metrics were computed (a `NOT_EVALUATED` / `INSUFFICIENT_SAMPLE` / `FAIL` verdict is still 0); 1 a game failed / was cancelled or a replay differed; 2 bad input; 3 every game completed but the metrics could not be computed (for example `Metrics.target_missing`; see `run_status` and `metrics.error_code` in `summary.json`). `--tuning FILE` reads a test tuning file through the same strict reader (its hash is recorded in the manifest and such a manifest cannot be replayed). More than 10 games is refused.

## Reading the output

- **Game ids** are `game:sim:<cohort>-<index>`; the seed is explicit. Seed 3, 11 and 29 are the small regression seeds of earlier phases, **not** an official seed list.
- **Hashes** (`config_hash`, per game `event_hash` / `rng_hash` / `result_hash`, `run_result_hash`) contain no time or machine facts; `environment_not_hashed` does.
- **Metrics F1-F8** are measurements against the targets declared in the tuning file. Every official verdict stays `NOT_EVALUATED` until a minimum sample and the evaluation cohort are approved; a zero denominator is `INSUFFICIENT_SAMPLE`. The neutral cohort is **not** evidence for the league-average targets F1-F5.
- A failed game keeps its GameId, Seed, the engine's error code and its last receipts, and is excluded from the aggregate (never dropped from the report).
