# Phase 1 Report — Player identity, visibility, and baseball vocabulary

- Date: 2026-10-10
- Implementer: Claude Code (Claude Sonnet 5.5), on the owner's separate Phase 1 instruction (Phase 1-A/1-B/1-C/1-D)
- Review status: **`승인 완료` (PASS WITH NOTES, 2026-10-10)** — superseded text follows: (Obsidian `Star/01_Phase_Reviews/Phase_01_Review.md`). No GPT Chat review has happened (`미실시`); the Codex review's `CHANGES REQUIRED` is preserved in §0 and its fixes are re-verified here but have **not been re-reviewed by Codex** (`미실시`). Not `승인 완료`.
- Gate history:
  - Initial self-verified run (pre-Codex): PASS, 254 tests — preserved in §1–§8 below as the pre-remediation record.
  - Independent Codex review: **`CHANGES REQUIRED`** (2 High, 2 Medium) — §0.
  - After remediation (2026-10-10): **PASS (self-verified)**, two consecutive full `tools/verify.ps1` runs, 289 tests — §0.4. Evidence: [PHASE_1_VERIFY_LOG.md](PHASE_1_VERIFY_LOG.md) (run 2, current) and [PHASE_1_VERIFY_LOG_run1.md](PHASE_1_VERIFY_LOG_run1.md) (run 1). Still `미검증`: a human click-through of the screen **after** the fixes (the owner reported the pre-fix manual UI check as done, without per-item details) and FHD.
- Git: branch `main`, HEAD `e28cb8d` (Phase 0). All Phase 1 and remediation work is **uncommitted** (15 modified, 51 new files); nothing was committed or pushed.
- Next: stop. Phase 2 is not started; wait for GPT Chat / owner review.

## Final approval (2026-10-10)

**`승인 완료` — PASS WITH NOTES**, relayed by the owner from the GPT Chat final review. Basis (as relayed): 289/289 xUnit, two consecutive `verify.ps1` PASS, Windows export and SQLite 8/8, post-fix manual UI check completed by the owner (no per-item details or screenshots were supplied and none are recorded here), Codex final re-review `PASS WITH NOTES`, no High/Critical blocking Phase 2. Verdict history, all preserved: self-verification PASS → Codex `CHANGES REQUIRED` (2 High, 2 Medium) → fixes → Codex re-review `PASS WITH NOTES` → GPT Chat `승인 완료 (PASS WITH NOTES)`.

**Open Notes (tracked technical debt):** Domain 컬렉션 가변성(Codex Medium, Notes): `Ratings.cs`(`PitchRepertoire.Pitches`), `Proficiency.cs`, `PlayerStatus.cs`, `DevelopmentProfile.cs`, `Player.cs`가 `List` 기반 `IReadOnlyList`를 그대로 노출. **Phase 2에서 이 컬렉션을 경기 상태·결정론적 처리에 쓰기 전에** 방어 복사, 읽기 전용(`AsReadOnly`) 노출, 캐스팅 변이 테스트(`IList` 캐스트 후 변경 시 예외)를 적용한다. 현재 승인된 Phase 1 코드는 수정하지 않는다.
FHD remains a Phase 5+ item. Nothing is committed; Phase 2 is not started.

## 0. Codex review remediation (CHANGES REQUIRED → fixed and re-verified)

The 254 passing tests of the first run did not cover these defects; each fix below comes with counter-example tests that fail without it.

### 0.1 Findings and fixes

| # | Severity | Finding | Fix | Counter-example tests |
|---|---|---|---|---|
| 1 | High | The public API returned the raw `PlayerWorld` (exact NPC ratings, hidden `DevelopmentProfile`) and any caller could construct `PlayerInfoService` with an arbitrary `ControlledPlayerId`, impersonating an NPC. | `PlayerWorld`, `IPlayerWorldSource`, `ViewerContext` and the service constructor are `internal`; Infrastructure's `FixturePlayerWorldSource`/`WorldJson`/DTOs are `internal`; `InternalsVisibleTo` limited to Infrastructure + test assemblies (client not listed). Client entry point is `PlayerInfoComposition.CreateFictionalPhase1(PlayerSessionOptions)`. `PlayerSessionOptions` lost `ControlledPlayerId` (only `CheatMode`); "my player" comes from the pack's own binding (`controlled_player_id`, Phase 1 stand-in for the Phase 4 career binding). No new layer. | `TrustedBoundaryTests` (14) + 1 new and 3 rewritten `PlayerVisibilityTests`, see §0.2 |
| 2 | High | Jersey ledger allowed one player to hold numbers at two clubs over overlapping dates; `ActiveOn` picked an arbitrary first row on duplicate data. | A player holds at most one number at a time across all clubs (`player_at_other_club`); same-club and same-club-number rules kept; retired restriction kept; half-open `[from, to)` documented; same-day handover legal; `ActiveOn` returns `Result` with `conflicting_active_rows`; ledger collections are read-only copies; query service refuses to start on a conflict; rejected requests leave the ledger unchanged. | 9 new Domain tests + 2 pack tests, see §0.3 |
| 3 | Medium | `RuleProfile`/catalog accepted `default(RuleVersionId)`, shared mutable lists, and unstructured simplifications. | `RuleAreaDefinition`/`RuleSimplification` are immutable classes built by validating factories (area, version, window, provenance, simplification `{id, description}` unique ids); catalog/profile copy inputs, expose read-only copies, reject default versions, duplicate and **overlapping** versions of one area. Pack schema v1 simplifications became objects (pre-commit change). | 7 new Domain tests + 2 pack tests |
| 4 | Medium | `verify.ps1` could hang when MSBuild child nodes kept redirected handles open (`Start-Process -Wait`). | Script-scoped `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false`; `--disable-build-servers -nr:false` on the solution build and `-nr:false` on `dotnet msbuild` probes; `Invoke-Logged` waits with a per-command limit (build/export 900 s, tests 600 s, Godot runs 300 s) and on expiry kills only the process tree it started (exit 124); informational check Z1 lists build-server processes still alive that started during the run (never kills them). Release/ExportRelease configurations unchanged. | two consecutive full runs |

### 0.2 Public-API bypass results (finding 1)

Reflection over the public surface of Application and Infrastructure: no `StarPlayer.Domain` type, no `PlayerWorld`/`IPlayerWorldSource`/`ViewerContext`/`DevelopmentProfile`/`PlayerSheet`; public Application ports = only `IStorageDiagnostics`; Infrastructure public types = only `PlayerInfoComposition` and `SqliteStorageDiagnostics`; the only public member producing a `PlayerInfoService` is `CreateFictionalPhase1`, which takes no identity; no public member anywhere has a `PlayerId` or "controlled*" parameter; `InternalsVisibleTo` lists pinned (client absent). Behavioural: hostile `PlayerSessionOptions` JSON (`ControlledPlayerId:"p-0005"` …) through the public composition still leaves `p-0001` as the own player and `p-0005` observed-only; the view graph returned to a cheat session contains no Domain/raw type; own vs NPC results stay distinct; cheat shows no exact NPC ability or potential; changing every hidden value leaves every public response byte-identical. Client: a compiled-IL scan shows only `CompositionRoot` touches Infrastructure (and nothing touches Domain/Simulation); a source scan finds no client code naming raw-world/hidden/authority types; because Bootstrap and presenters share one assembly the guarantee is visibility, with these scans as defense in depth.
**Mutation checks** (temporary, reverted): making `PlayerWorld`/`IPlayerWorldSource` public, giving `PlayerInfoService` a public constructor, making `FixturePlayerWorldSource` public, and adding a presenter method that calls it made **9 tests fail** (surface, ports, IVT/factory, IL scan, source scans), then all passed after reverting.

### 0.3 Cross-club jersey and RuleProfile results

Jersey (Domain + pack): same player/same club overlap rejected; same player/different club overlap rejected (assign and load, including past-dated claims); different player/same club/same number overlap rejected; end day == new start day accepted (change and transfer); gap-separated transfer accepted; corrupt overlapping rows → `ActiveOn` error (built through a test-only seam), never a first row; every rejected request leaves the ledger unchanged (8 request kinds); history preserved after changes, transfers and later retirement; clean transfer through the pack loads with both rows.
RuleProfile: default/blank/invalid version ids rejected in definition, resolve and profile; invalid/duplicate/missing simplifications rejected; overlapping versions rejected, same-day handover accepted; mutating input lists after creation changes nothing; `IList.Add/Clear/RemoveAt` on public collections throws; no settable properties or public constructors on the rule types; shuffled input gives identical order/serialization; registered version resolves, unregistered fails explicitly. No rule content or KBO constants were added.

### 0.4 Test counts and re-verification

| Suite | Before remediation | After remediation | New |
|---|---:|---:|---:|
| Domain.Tests | 122 | 138 | +16 |
| Simulation.Tests | 1 | 1 | 0 |
| Application.Tests | 97 | 112 | +15 |
| Infrastructure.Tests | 34 | 38 | +4 |
| **Total** | **254** | **289** | **+35** |

All 289 pass, 0 failed, 0 skipped; all 254 earlier tests still pass (no regression). Release build 0 warnings / 0 errors. `tools/verify.ps1` was run in full **twice in a row** (run 1 14:51, run 2 14:52): both `GATE: PASS`, exit 0, 19/19 gate checks PASS each, no timeout, Z1 reports no leftover build servers. Each run: toolchain and export-template checksums, Release build, solution/configuration checks, all test suites, Godot headless C# build, Godot SQLite diagnostics 8/8, headless player-card self-check 7/7 with 0 leaks, **fresh Windows export** (E1b confirms the export folder was recreated in that run from the ExportRelease output), cold-copy exported program SQLite 8/8, and cold-copy exported player card at 2560×1440 with two screenshots. Internal Markdown links in the changed documents were checked (0 broken). The pre-fix log was not reused for the new result.

---

## Pre-remediation record (first self-verification, 254 tests)

Sections 1–8 below describe the first run **before** the Codex fixes. Where they conflict with §0 (service constructor/options, counts, ledger and rule behaviour, `FixturePlayerWorldSource` visibility) §0 and ADR-002 items 8–11 are authoritative.

## 1. What was implemented

**1-A Domain models** (`src/StarPlayer.Domain`, no new references)
- IDs extended and validated by one rule set (≤64 chars, `A-Za-z0-9_-.:`): `ClubId`, `AmateurTeamId`, `StadiumId`, `ParkFactorProfileId`, `RuleProfileId`, `RuleVersionId`, composite `BatteryPairId`; `IStringId<T>` lets serializers rebuild ids through validation.
- `Player` (identity only: name, birth date, throws/bats, body, personality traits, descriptive biography; age from birth date; **no club/number/contract/rating member**), `SkillValue` 0–100, `Ratings` (6 hitter, 4 pitcher + repertoire, 4 catcher; `PlayerKind` derived), `PlayerState`, hidden `DevelopmentProfile`, `ProficiencyProfile` (fielding-position and role proficiency, position change keeps every recorded value), `PlayerStatus` (origins, entry path, registration, league assignment, dated club affiliation, career status as separate axes with cross-field invariants), `PlayerSheet` (everything the visibility layer may see, **without** the hidden profile), `PlayerCreated`/`PlayerChangedPosition` payloads.
- `Money` (checked KRW, exact 128-bit `Scale` with explicit rounding), `InningsOuts` (112 outs ⇒ `37.1`).

**1-B Relationship / rule models**
- Catcher: `CatcherRatings` (4 tools) independent of C proficiency; `PopTimeObservation` (ms) is a different type from the 0–100 skill.
- Battery: `BatteryPairId(catcher, pitcher)`, neutral default `BatteryChemistry`.
- KBO Rule Engine boundary: `RuleArea` (10 C40 areas), `RuleCatalog`, `RuleProfile` with effective-date validation; unknown version ⇒ `RuleCatalog.version_not_found`, missing area ⇒ `RuleProfile.area_missing`. No rule values/2026 constants.
- Stadium / ParkFactor: `Stadium` (id + presentation), `ParkFactorProfile` (versioned HR/hit/XBH permille inside `ParkFactorBounds`), `ParkFactorSnapshot`, `StadiumUsage`. Venue types reference no rating type.
- Jersey: `JerseyNumberAssignment` (PlayerId+ClubId+number+half-open interval), `RetiredNumberRestriction`, immutable `JerseyNumberLedger` (range, retired restriction, one open per player/club, no overlapping holders; atomic `Change`/`Close`; released numbers reusable; history preserved).

**1-C Application + test UI**
- `PlayerVisibilityPolicy` (own player exact; others `observation-v0` grade/range/confidence), internal `ViewerContext` created only from `PlayerSessionOptions` supplied by Bootstrap, `PlayerQuery(PlayerId)` (no permission field), `PlayerPublicView` family (no Domain types), `PlayerInfoService`, `PlayerCardFormatter`, `PlayerInfoSelfCheck` (7 checks), `HiddenDataGuard`.
- Infrastructure: JSON schema v1 (`WorldDto`, `WorldJson` read/write, `FixturePlayerWorldSource`), pack embedded as a resource. `content/fictional/phase1_two_club_pack.json`: 2 fictional clubs, 10 fictional players (hitter, pitcher, two-way, two players named 박민수, elite-tools/novice-C catcher, average-tools/expert-C catcher, 1 catcher + 3 pitchers battery), 2 stadiums with 3 park profiles, jersey history, retired number.
- Godot: new `PlayerDiagnosticsScreen` (list + card + self-check, QHD), reachable from a new `선수 정보 진단` button on the existing menu (existing Phase 0 buttons unchanged); launch modes `--player-diagnostics` and `--cheat`; Bootstrap only builds services. The client still references no Domain/Simulation.

**1-D** Re-read ROADMAP Phase 1 acceptance and Common Gate G (§4), full `verify.ps1` run.

## 2. Main files

New: `src/StarPlayer.Domain/{Players,Baseball,Teams}/*`, `Common/{Money,SnakeCase}.cs`; `src/StarPlayer.Application/{Queries,Sessions,Ports/IPlayerWorldSource.cs}`; `src/StarPlayer.Infrastructure/Fixtures/*`; `content/fictional/phase1_two_club_pack.json`; `game/UI/PlayerDiagnostics/*`; 7 test files; `docs/data-model.md`, `docs/decisions/ADR-002.md`, this report and `PHASE_1_VERIFY_LOG.md`.
Modified: `Common/Ids.cs` (stricter validation, new ids), Application/Infrastructure/Application.Tests `.csproj` (embedded pack, InternalsVisibleTo, test→Infrastructure reference), `game/Bootstrap/{CompositionRoot,GameRoot,LaunchOptions}.cs`, `game/UI/MainMenu/*`, `tools/verify.ps1`.

## 3. Test results (actual, from `verify.ps1`; Release, .NET SDK 8.0.425, Godot 4.7.2 Mono, Windows 11)

| Suite | Passed / Failed / Skipped |
|---|---|
| Domain.Tests | 122 / 0 / 0 (9 are Phase 0) |
| Simulation.Tests | 1 / 0 / 0 |
| Application.Tests | 97 / 0 / 0 (includes 14 architecture + 48 determinism-detector tests from Phase 0, 32 new) |
| Infrastructure.Tests | 34 / 0 / 0 (7 SQLite tests from Phase 0, 27 new) |
| **Total** | **254 passed**, 0 failed, 0 skipped (Phase 0 had 82; no regression) |

Release build 0 warnings. Godot headless C# build, SQLite diagnostics 8/8, **player-card self-check 7/7 with `hidden_identifier_leaks=0`**, Windows export, cold-copy run of the exported game: SQLite 8/8 and the player card at 2560×1440 with screenshots (own player and an NPC) — all PASS.

Required-test map: skill <0/>100 and boundaries (`ValueObjectTests`, `FixtureWorldTests`); age boundaries incl. Feb 29; 112 outs = 37.1; same-name distinct ids; hitter/pitcher/two-way serialization; self vs NPC difference; hidden data absent from JSON/DTO/UI text; hostile client flags ignored; catcher tools vs C independence; battery pair ids; bad RuleProfile version; StadiumId stable after rename; park-factor range; duplicate/retired/overlapping jersey rejected; independent-field JSON round trip (`write(read(pack)) == pack`); Phase 0 regression.

## 4. Public-information protection (strongest evidence)

- Structural: `PlayerSheet` has no `DevelopmentProfile`; no public view type references any Domain type (reflection test); `ViewerContext` has no public constructor/setters; the service surface has no bool/viewer/Domain-id parameter.
- Differential: rewriting **every** hidden value (potential 100, growth type flipped, prime 20, decline 50, all ceilings 100) leaves list and all views byte-identical, for two different controlled players.
- All 10 players × 2 cheat settings × 10 controlled players: serialized lists/views contain no hidden identifier; cheat sessions produce views identical to normal ones.
- Hostile JSON `{PlayerId:"p-0005", IsSelf:true, CheatMode:true, Viewer:..., RevealExact:true}` yields an observed view; look-alike ids (`" p-0001"`, `"P-0001"`, name text) are not found, never "self".
- Observation never leaks a point value: for every value 0–100 the range contains it and is ≥2 wide.

## 5. JSON and Godot diagnostic UI

- JSON: pack round-trips to identical JSON; editing only state / only proficiency / only the hidden profile changes only that section; 15+ bad-content cases (out-of-range skill, unknown rule version, factor over cap, duplicate/retired/out-of-range jersey, duplicate id, unknown reference/enum, unknown or missing field, wrong format version) return located typed errors with no partial world.
- UI: actual scenes verified at QHD by screenshot (`build/verify/exported-player-card*.png`, reviewed by eye) in the exported cold-copy program; Korean text only. **미검증:** a human clicking `선수 정보 진단` → list → `뒤로` (the automated mode opens the screen by code and "clicks" items through the presenter method); **FHD** layout (Phase 5+ per Gate G item 4).

## 6. Issues, deviations, debt

- Fixed during work: ordering bug in `PlayerInfoService` (club read before the as-of date was set) found by a test; `\r\n` double line spacing in the Godot label; `verify.ps1` freshness check counted `.tscn` files as compile inputs (false B3 failure) — now `.cs/.csproj` only.
- `verify.ps1` now writes `PHASE_1_VERIFY_LOG.md` (the approved Phase 0 logs are untouched) and adds G3/E3.
- Unresolved / debt: event payloads are record types only (no JSON converters, since nothing publishes events yet); personality traits are identity data with no effect; battery pairs and stadiums are tested but not shown on the card; `JerseyNumberLedger` scans lists (fine for fixtures, revisit at Phases 17/29 scale); `PlayerStatus` currently rejects an active registered player without a club — Phase 15/16 may need a "free agent" status; `HiddenDataGuard` token "potential" is deliberately coarse.
- No Codex review performed; recommended for the visibility boundary before Phase 2 builds on it.

## 7. Phase 1 Gate verdict

Acceptance rows (ROADMAP Phase 1) — all four player scenarios give legal public DTOs ✓; independent fields round-trip ✓; NPC cannot gain exact ratings by a flag ✓; no names as join keys ✓; catcher vs C distinct in public views ✓; typed rules route to one versioned owner and unknown versions fail ✓; stadium presentation vs simulation separate ✓; C47 number is a dated club assignment ✓. Common Gate G: 1 ✓, 2 ✓, 3 N/A (before Phase 4), 4 ✓ at QHD (human click and FHD `미검증`/later), 5 ✓ (no D2 subset needed for vocabulary-only Phase 1), 6 ✓.
**Verdict: PASS (self-verified), review `검토 대기`.**

## 8. Decisions for GPT Chat review (ADR-002)

1. Hidden data excluded structurally (`PlayerSheet` without `DevelopmentProfile`) — acceptable?
2. Cheat mode does **not** reveal exact NPC current ratings in Phase 1 — confirm or specify.
3. `observation-v0` placeholder (bucket width 10 / 5 for club-mates, always contains truth) — acceptable until Phase 12?
4. `ClubId` added beside Phase 0's `TeamId` — keep both, or unify?
5. Personality trait list taken from source v1.6 as no-effect identity data; park-factor bounds (850–1150 ‰), jersey range 0–99 are fixture values, not tuning.
6. Pack embedded in Infrastructure as a Phase 1 fixture source (not the Phase 4 datapack path).
