# B-1 Traceability — 계약 조항 → 구현 → 시험

- 기준 계약: `B1-T00_FINAL_CONTRACT_DRAFT.md` (SHA-256 `1338a2f7…7d64`, [승인 기록](B1-T00_OWNER_APPROVAL_RECORD.md)), OWNER 신규 결정: [NSC-4·NSC-5](B1-T00_NSC4_NSC5_OWNER_DECISION.md), [결정 2(NSC-4 정정·T05 codec·T09 신원 조회)](B1-T00_OWNER_DECISION_2.md)
- 필드별 타입·Null 조건·독립 출처는 계약 본문(§2~§7, §13~§20)이 정본입니다. 이 문서는 조항 묶음 단위이고, **필드 단위 연결**은 Golden 실제 문서의 모든 JSON 경로(616행)를 생성한 [B1_FIELD_TRACEABILITY.md](B1_FIELD_TRACEABILITY.md)(`tools/b1-golden/fields.py`, 규칙 없는 경로가 있으면 생성 실패)에 있습니다.
- 시험 이름의 약어: `SJ`=`StrictJsonAndJcsTests`, `OS`=`ObserverSeamTests`, `OD`=`OfficialDocumentTests`, `UP`=`UnitPzlEvidenceTests`, `EH`=`EvidenceHashTests`, `AR`=`ArchiveTests`, `AT`=`ApprovalAndTrustTests`, `GD`=`GoldenTests`, `CX`=`CounterexampleTests` (이상 `tests/StarPlayer.SimHarness.Tests/Evidence/`), `VT`=`VerifierTests`, `RT`=`RegistryTests`, `CE`=`CertificationAndEnvironmentTests`, `BP`=`BootstrapAndProfileTests`, `PR`=`PipeRegistryTests`, `VX`=`VerifierCounterexampleTests`, `PS`=`Pzl6StandaloneTests`, `WO`=`WitnessOrderTests`, `TB`=`TrustedBoundaryTests`(`tests/StarPlayer.Application.Tests/`) (`tests/StarPlayer.EvidenceVerifier.Tests/`), `P6`=`OfficialPzl6VerificationTests` (`tests/StarPlayer.Infrastructure.Tests/`)

## 1. 조항별 연결

| 계약 조항 | 필드·규칙 | 구현 (검사 위치) | 독립 출처 | 시험 |
|---|---|---|---|---|
| §1.1·§1.2 | 해석 규칙 11개, 스칼라 21종 | `Evidence/EvidenceSchema.cs` (`Schema.*`) | 규칙 자체 | SJ, OD(필드 추가·누락·null), AT |
| §1.3 | 상위 오류 17개, 원인 코드 보존 | `Evidence/StrictJson.cs` `EvidenceCodes`, `EvidenceVerifier/Verifier.cs` `Wrap` | — | VT `R01_only_official_not_ready…` |
| §2.1 | schema.id·version·contracts 5키, 지원 조합 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | 검증기 지원 조합 | OD (N06·N23·A11) |
| §2.2 | evidence_id 도출식(R-12) | `EvidenceIds`, `EvidenceRules.Header` | 도출식 | OD (N08) |
| §2.3·§2.4 | approvals 3필드, S1~S6, R-14 7축 | `EvidenceRules.UnitResult`, `Verifier.BindApproval` | 승인 기록 | OD (R-14), VT (A03), EH (A01) |
| §2.5 | additional_evidence, ArtifactRef 7필드 | `EvidenceRules.Header`, `Archive.CheckReferences` | Archive 스냅샷 | AR, EH (A12·A18) |
| §3.2~§3.3 | UnitInputs 14, GameSpec, Attestation 21 | `EvidenceShapes.UnitInputs/Attestation`, `OfficialDocuments.Inputs` | 승인 Seed 계획, 기존 Attestation Hash | OD, VT (재투영 비교) |
| §3.4~§3.6 | Technical, Stop, Counters 28, GameResult 20, 상태표 A~D | `EvidenceRules.UnitResult/GameRow`, `OfficialDocuments.Technical/Game` | 재실행(`FamilyOfficialReplay`) | OD (N03, A06, A21·A22), VT (A06) |
| §3.7~§3.8 | FamilyUnitEvaluation(R-5), OfficialMetric 16 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | `StratifiedEvaluator.Evaluate`·`FamilyOfficial.Evaluate` | OD (Calibration 투영) |
| §4 | Dominance Cell·CellEvaluation·SideResult·Witness(R-7) | `EvidenceRules.DominanceRules/Side`, `OfficialDocuments.DominanceAggregate` | 카탈로그, `DominanceEvaluator` | OD (A24·카탈로그), VT (A05) |
| §5 | Unit PZL, PzlGame 12, PzlSummary 14, EventObservation, PitchObservation 13 | `Evidence/UnitPzlEvidence.cs`, `Evidence/PzlEventObserver.cs` | 재관찰 | UP, VT |
| §6 | PZL #6 고정 20경기, 6단계 | `EvidenceVerifier/Pzl6Verifier.cs`(기본 codec `EventJsonCodec.Infrastructure`), `EvidenceRules.Pzl6Rules`, `src/StarPlayer.Infrastructure/Fixtures/EventJsonV2Codec.cs`(결정 2) | 기존 v2 Writer/Reader(공개 진입점 위임), Reducer, 재실행 | P6, PS(검증기 단독 정상·변조), TB(경계 최소 예외), EH (A10) |
| §7 | Check, Violation, TestEvidence, Deferral, R-9, coverage(NSC-6) | `EvidenceRules.UnitPzlRules`, `EvidenceHash.PhaseChecks`, `UnitPzlEvidence.Coverage` | 재관찰 | UP (N05-a·b·d, N36·N36-a), EH (A10-a, A25-a·b, N36-b) |
| §8 | 엄격 읽기, U64, 정수, F64 | `StrictJson`, `Jcs`, `EvidenceScalars` | RFC 8785, ES 규칙 | SJ (A21~A24, GV-C01·C02·EV0) |
| §9 | M(D), 4 Payload, 결속, Prefix SHA | `Evidence/EvidenceHash.cs` | 독립 Python 구현 | EH, GD |
| §10 | GAME_EVENTS 형식·레코드·결속·RNG A/B/C·digest·재관찰 | `UnitPzlEvidence.WriteBundle/ReadBundle/CompareGame` | 재관찰 | UP (A07~A09, N14, N17, N36), VT |
| §11 | PZL 12항목 매핑, 재수집 절차 | `Verifier.VerifyFamily` (#1·#5·#10·#12a 관찰), `EvidenceHash.PhaseChecks` | 재관찰 | VT, EH |
| §12 | Test Profile 형식, 필수 Case, TRX 독립 확인 | `ApprovalFormats.TestProfile`, `EvidenceVerifier/TestProfiles.cs` (`CheckRequired`·`CheckFixtures`·`CheckAssembly`·`Run`·`Trx`·`Evidence`) | Test Profile·Build Provenance(TEST_ASSEMBLY) | BP (A25 규칙·TRX 파싱), P6 (4개 시험 어셈블리 실제 하위 `dotnet test` + TRX 대조) |
| §13 | 승인 기록 v1, 카탈로그, 거부 규칙, 독립 출처, PATCH-04, 매핑 | `Evidence/ApprovalRecord.cs` | CohortRegistry, OfficialProfile.Hash, FcEvaluationV1.Hash, SeedManifestReader | AT (§13.3 9종, N28) |
| §14 | 실행 허가, 3상태 축, R01-1~R01-D, AMPV, 오류 우선순위 | `Official.cs` (`ReplayAuthority`, `VerifyEnvelope`, `BuildAttestation`), `Verifier.Authority/Ampv/Wrap` | 기존 Preflight | VT (N15·N19·N19-a·N22), 기존 SimHarness 494개 (N13-a·N18) |
| §15 | Registry DDL·연산·권한 | `EvidenceVerifier/Registry.cs` `SqliteRegistry` | — | RT, VT (A15~A17) |
| §16 | T1~T7, L2~L8 | `Evidence/TrustAnchor.cs` (T1·T2), `EvidenceVerifier/TrustBootstrap.cs` (L2~L8·T5·T6·Registry ID), `RegistryPipeClient` (T3) | 앵커, Registry | AT (N24·N25), BP (N26·N27·L2·L3), PR (Pipe 위 L2~L8·T6) |
| §17 | R03-1~R03-6 | `ApprovalFormats.Anchor/ServiceConfig`, `PipeSecurity`, `EvidenceVerifier/PipeRegistry.cs` (`RegistryPipeService`·`RegistryPipeClient`, Win32 P/Invoke) | 앵커, 서비스 구성 스냅샷 | RT (순수 규칙, MINOR-01, `get_registry_identity` 닫힌 형식·권한), PR (실제 Pipe: 왕복·역할·실제 AccessCheck 거부·선점·30초 한도·7단계 명시 신원 조회(결정 3)·끊긴 세션 폐기), AT (AT-14n) — 별도 계정 AT 실측 **미실시** |
| §18 | 환경 프로필, E2-1~E2-7, 비트 Golden | `EvidenceVerifier/ExecutionEnvironment.cs` | 승인 프로필 | VT (E2-7 재확인) — 승인 프로필·비트 Golden 값 **미생성**(운영 값) |
| §19·§20 | 인증 상태표, 영수증, 발행·등록 | `EvidenceVerifier/Receipts.cs` | Registry 재조회 | VT (A15·A16·A17, N30) |
| §21 | Archive 20역할, DAG, 보존 규칙 | `Evidence/Archive.cs`, `Evidence/EvidenceWriter.cs` | — | AR (A18~A20, N10) |
| §22 | 기존 계약 불변 | `GameExecutor.cs` 관찰 Seam | Dry Pilot 9 Run | OS (N13), Dry Pilot 재현(로그) |
| NSC-4 | 앵커 ACL B안 + 결정 2 최소 정정(구성요소별 금지 권한, INHERIT_ONLY 구분·상속 판정, 모호 SID 거부, 읽기 실패 Fail-Closed) | `AnchorAcl`(`AclTarget`, `Violation`, `Inherits`, `CheckChain`, `CheckPath`) | — | AT (SDDL 25종, 상속 10종, 후보 위치 + 공격 8종, 실제 경로·Junction) — 운영 보호 디렉터리 실측 **미검증** |
| NSC-5 | `registry_database_path`, `WINDOWS_LOCAL_ABS_PATH` | `WindowsLocalAbsPath`, `ApprovalFormats.ServiceConfig` | — | AT (17종 + 실경로 검사) |
| 결정 3 §2 | RAW_INVENTORY 형식·닫힌 Reader·영수증 결속 | `Archive.ReadInventory`, `Receipts.CheckInventory`(`Certification` 발급·Phase 자식) | 영수증 ORIGINAL source_files | `Decision3EvidenceTests`, `T11CertificationTests.Decision3_2_…` |
| 결정 3 §4 | #6 Production 최종 상태·GameFact·커버리지 | `PzlEventObserver.FinalState`, `GameExecutor`(읽기 전용 2곳), `Pzl6Verifier.FinalDifference`·`CoverageGap` | Production 실행 | `Decision3EvidenceTests`, `T11CertificationTests.Decision3_4_…`, Dry Pilot 재현 |
| 결정 3 §5 | GAME_RESULTS `simharness-game-results-v1` | `GameResultsEvidence`, `EvidenceShapes.GameResults*`, `EvidenceWriter.WriteUnits`, `Archive.CheckReferences(UnitPzl)`, `Verifier.RequiredCounts` | 부모 문서 행·재실행 투영 | `Decision3EvidenceTests`, `T11CertificationTests.Decision3_5_…`, `ArchiveTests` |
| 결정 3 §7 | #6 JSON 20개 = PZL6 | `Archive.AllowedRoles`·`CheckReferences(Pzl6)`, `Verifier.VerifyPzl6`·`RequiredCounts` | Writer 출력·표본 계획 | `T11CertificationTests.Decision3_7_…`, `Pzl6StandaloneTests` |

## 2. 반례 대응 (3부 3.2~3.4)

상태: **시험** = 해당 기대 결과를 직접 확인하는 시험 있음 · **부분** = 같은 검사 경로는 시험되지만 그 반례 그대로의 시험은 없음 · **미구현** = 검사 경로 자체가 없음

| ID | 상태 | 근거 |
|---|---|---|
| A01 | 시험 | EH `A01_a_child_document…`, EH Phase mixed set |
| A02 | 시험 | EH Phase (다른 집합·run_id 재사용) |
| A03 | 시험 | VT `A03_N15_N19…` (AMPV, 첫 경기 전) |
| A04 | 시험 | VX `A04_…` (Seed·GameId·ConfigHash·Attestation 동시 재작성 → approval_mismatch) |
| A05 | 시험 | VT `A05_A06…` (evaluation_mismatch) |
| A06 | 시험 | OD (committed_events·end_kind), VT (event_hash → replay_mismatch) |
| A07·A08·A09 | 시험 | UP |
| A10 | 시험 | EH (11번째 경기 표본), `Pzl6Rules` 고정 순서 |
| A10-a | 시험 | EH Phase #6 coverage |
| A11 | 시험 | OD (DRAFT ID, `official_evidence`, `schema_version` 키) |
| A12 | 시험 | EH (Artifact 참조만 다르면 같은 A+), VX `A12_A13_…`(같은 문서를 다른 Archive 파일로 → A+ 같음, Raw 목록 다름). OS·시각 메타데이터 변경 그 자체는 아님(이 PC에서 OS를 바꿀 수 없음) |
| A13 | 시험 | VX `A12_A13_…`(첫 스냅샷 영수증의 source_files ≠ 두 번째 스냅샷), `Receipts.Issue` 같은 ID·다른 바이트 거부(A16 경로) |
| A14 | 시험 | CE `A14_A16_…`(끊긴 기록부 → `registration_pending`) |
| A15·A16·A17 | 시험 | VT |
| A18·A19·A20 | 시험 | AR |
| A21~A24 | 시험 | SJ, OD |
| A25 | 시험 | BP `A25_…`(누락·미실행·중복·새 행 → test_profile_mismatch, FAIL 보존), P6 Phase 루트(실제 TRX) |
| A25-a·A25-b | 시험 | EH |
| A26·A27 | 시험 | CE `Section19_…`(검증된 FAIL 발급 가능, METRICS_ERROR·GAMES_FAILED 거부, NOT_EVALUATED·INCOMPLETE 거부, INSUFFICIENT_SAMPLE은 허용 조건에서만) |
| A28 | 시험 | `PhaseVerifier.RequireChildren` — P6 Phase 루트에서 자식 영수증 미등록 → `Phase.children_missing` 확인. 이 세계에서는 세 Family 모두 §19 인증 불가라 실제 Phase 영수증 발행은 없음 |
| N01·N02 | 시험 | CX `N01_…`(문서 20필드로 game-result-v1 독립 재계산, D·C·A행 실제 경기), CX `N02_…` |
| N03·N04 | 시험 | OD |
| N05·N05-a·N05-b·N05-d | 시험 | UP |
| N05-c | 시험 | CX `N05c_N05e_…`(스키마는 통과, 재투영 비교에서 `/checks/0/coverage/pitches` 차이 → `unit_pzl_differs`) |
| N05-e | 시험 | CX `N05c_N05e_…`(투구 사건이 있는 실패 경기, coverage.pitches ≠ 기술 카운터, 재관찰 일치 → 정상) |
| N06·N08 | 시험 | OD |
| N07 | 시험 | WO `N07_…`(셀당 4경기 실제 엔진 TEST_ONLY Fixture에서 Witness 생성, 순서 바꾼 목록은 스키마 통과·재평가 비교에서 `evaluation_mismatch`) |
| N09 | 시험 | VT (TEST_ONLY 영수증만) |
| N10 | 시험 | AR |
| N11 | 시험 | CE `N11_…` |
| N12 | 미생성 | 승인 환경 비트 Golden은 운영 값 |
| N13·N13-a | 시험 | OS, Dry Pilot 9 Run 재현, 기존 SimHarness 시험 |
| N14·N17·N36·N36-a·N36-b | 시험 | UP, EH |
| N15·N19·N19-a·N22 | 시험 | VT |
| N16·N29·N29-a·N30-a·N31 | 시험 | CE `E2_1_…`, `N30a_N31_…`(이 PC의 실측 스냅샷과 그로부터 만든 시험용 프로필; 승인 프로필 아님) |
| N18·N18-a~d·N21·N21-a·N22-a | 시험(기존) | 기존 SimHarness 시험 통과 |
| N22-b | 시험 | VX `N22b_…`(실제 보고서에서 output_directory 비통과·execution_path NotReady인데 AMPV 통과, 평가 검사 실패 시 AMPV 실패). `execution_path=Pass` 분기는 후보 인스턴스 A가 항상 not-ready라 구조상 도달 불가 |
| N20 | 시험 | VX `N20_…`(검증 거부 원인 `Seed.file_hash_mismatch` 보존, 생산측 Preflight도 거부) |
| N23·N24·N25·N28 | 시험 | OD, AT |
| N26 | 시험 | BP `N26_N27_…`, PR(Pipe 위 T6) |
| N27·N27-a~f | 부분 | PR: N27(Instance ID 불일치), N27-b(이름 선점 → 서비스 시작 거부), N27-c(요청 필드 거부는 RT) 실제 Pipe 시험. N27-d(재연결 후 다른 서버)·N27-e(SQLite ACL)·N27-f(Impersonation 실패)는 별도 계정·서비스 배치가 필요해 **미실시** |
| N30·N30-b | 시험 | VT (E2-7) |
| N32 | 시험 | CX `N32_…`(첫 결정에서 실패하는 시험 정책으로 실제 실행기가 B 경우를 만듦: stage exception·Commit 0, `GameStarter.Begin` 재구성 = `final_rng_canonical`, 다른 최종 RNG → pzl_mismatch) |
| N33·N34·N35 | 시험 | CX `N33_N34_…`(상태표 B·A행), CX `N35_…`(첫 rng_before 위조 → pzl_mismatch) |
| MINOR-01 | 시험 | RT `Minor01_access_check_matrix…` |
| AT-1~AT-14r | 미실시 | 별도 계정·서비스 배치 필요(계정 생성은 이 작업 범위 밖) |
