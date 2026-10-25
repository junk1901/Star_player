# B1 구현 기준본 목록 (검수받은 작업 트리, 파일별 SHA-256)

- 작성: Claude Code, 2026-10-25. 근거: [B1_CODE_ACCEPTANCE_TASK.md](../tasks/B1_CODE_ACCEPTANCE_TASK.md) 1절 4항
- 생성 방법: `git status --porcelain --untracked-files=all -z`에 나오는 모든 경로를 대상으로 했습니다. 대상은 추적 수정 파일과 미추적 파일이며, 이 목록의 두 파일(`B1_IMPLEMENTATION_MANIFEST.md`·`.sha256`)은 제외했습니다. 각 경로의 **작업 트리 바이트**로 SHA-256을 계산했습니다.
- 기계 검증용: [B1_IMPLEMENTATION_MANIFEST.sha256](B1_IMPLEMENTATION_MANIFEST.sha256)(경로 정렬, `sha256sum -c` 형식)
- **복원 주의:**
  - 검수받은 구현은 HEAD `257403ddae557a54c4013c17705370caeac7a2e0` 위의 **미커밋 작업 트리**이며, HEAD만으로는 복원되지 않습니다.
  - 이 목록의 SHA는 이 기계(system `core.autocrlf=true`)의 작업 트리 바이트입니다. 커밋 후 다른 설정으로 체크아웃하면 줄바꿈이 바뀌어 일부 SHA가 달라질 수 있습니다(5절).
- Stage·Commit·Push·Tag는 하지 않았습니다(6절은 OWNER 허가 후 계획).

## 1. 요약

| 분류 | 파일 수 | 추적 수정 | 미추적 |
|---|---:|---:|---:|
| 보호(D3) | 3 | 0 | 3 |
| 계약·OWNER 기록 | 43 | 0 | 43 |
| 빌드·검증 스크립트·저장소 설정 | 4 | 3 | 1 |
| 제품·도구 코드 | 36 | 7 | 29 |
| 시험 코드 | 33 | 3 | 30 |
| Golden 고정 입력·기대값 | 60 | 0 | 60 |
| Golden 독립 계산기 | 2 | 0 | 2 |
| 검수·실행 기록 | 14 | 0 | 14 |
| 작업 지시 | 8 | 0 | 8 |
| 상태·Vault 문서 | 7 | 2 | 5 |
| **합계** | **210** | **15** | **195** |

- 마지막 verify(2026-10-25 19:40:30 +09:00 `GATE: PASS` 3,041) 이후 수정된 코드·스크립트·시험·Golden 파일: **0개**. 이후 바뀐 것은 문서, 보존 원문과 `.gitattributes`(G2 규칙 추가)뿐입니다.

## 2. 전체 목록

### 보호(D3) (3)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `TASK.md` | 미추적 | 4,521 | `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825` |
| `docs/2023recordRule.pdf` | 미추적 | 845,851 | `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0` |
| `docs/2026-official-baseball-rules.pdf` | 미추적 | 1,316,921 | `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859` |

### 계약·OWNER 기록 (43)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `B1-T00_FINAL_CONTRACT_DRAFT.md` | 미추적 | 175,154 | `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` |
| `docs/contracts/b1/B1-T00_BASELINE.sha256` | 미추적 | 483 | `a9865a2f7db4bfe49ceea1e711528fb186d786750f4dde1b8ce74e948d7a9430` |
| `docs/contracts/b1/B1-T00_FINAL_CONTRACT.approved.md` | 미추적 | 175,154 | `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` |
| `docs/contracts/b1/B1-T00_NSC4_NSC5_OWNER_DECISION.md` | 미추적 | 5,250 | `af7142e534da18c8590f71016a346c5ef4664b1ba3dc1514a360e8fa4a005dcc` |
| `docs/contracts/b1/B1-T00_OWNER_APPROVAL_RECORD.md` | 미추적 | 3,351 | `cb8dd03b560abe6af198232174fe4274c8f4caf03fdfcc0d1a57286199bec2e5` |
| `docs/contracts/b1/B1-T00_OWNER_DECISION_2.md` | 미추적 | 12,339 | `fb469705a3a545d5b3c4cd84372de9f015eadb1f80005a52ec554b00aee18d51` |
| `docs/contracts/b1/B1-T00_OWNER_DECISION_3.md` | 미추적 | 7,934 | `bcb7bee78a177e2b6674982b91ed083f3416cbd4eb18f75457d67e61380035e0` |
| `docs/contracts/b1/B1_CODE_ACCEPTANCE_OWNER_DECISION.md` | 미추적 | 2,559 | `ed547ee72f1e5cd6f0aa3078a39c9033d5f0c3e324a36d4dea822e157b2a3214` |
| `docs/contracts/b1/B1_FIELD_TRACEABILITY.md` | 미추적 | 72,459 | `2914bb02fff17faa107fe79697f2bbe56482ebb2e1a99eafb49247d4eea52ca3` |
| `docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS.md` | 미추적 | 11,579 | `a1bf7f062650c2f7ee10b121364234402fe1a402091b6cb8ba0e7b0067fdf889` |
| `docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS_3.md` | 미추적 | 16,069 | `c833ff61d8ba2b10fea00e72099498ba364f0e2a10a10c075b305388553b6cea` |
| `docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS_3_FINAL.md` | 미추적 | 22,024 | `94bdf0c60679d4a6117d645dd99a1ac8310178efa0f77a8465dcfe3d32b99b3d` |
| `docs/contracts/b1/B1_TRACEABILITY.md` | 미추적 | 13,494 | `f860a778ae2d1f5e2d95ea73d278bf75bcfaaf3763c1c0b2f2696bc1283219da` |
| `docs/contracts/b1/originals/INDEX.md` | 미추적 | 15,881 | `2c30a316d40085c60411144dfd214e87c2fd7ab6cb97efeb5b2764d74014c181` |
| `docs/contracts/b1/originals/codex/C01_20261024T134703Z_request-t11-integrated-review.md` | 미추적 | 6,486 | `16d86f03d70a2be79726c0dfc3e7161ab9ada77e526ab661698a89ee3dd0baf4` |
| `docs/contracts/b1/originals/codex/C02_20261024T185557Z_verdict-1-fix-required.md` | 미추적 | 22,088 | `2c20a1023a2c8f7ed54fdf9a919a5de21565b662d7f1c067e6010cd50885c9b9` |
| `docs/contracts/b1/originals/codex/C03_20261025T011302Z_request-t11-targeted-review.md` | 미추적 | 2,497 | `4157c4aa057e535500d938782f36518751b57cf135754d295cc51e658442c6d3` |
| `docs/contracts/b1/originals/codex/C04_20261025T012037Z_verdict-2-fix-required.md` | 미추적 | 17,030 | `5e54ddd90b2a6e3e7e2e3f4d3bf6aa37a20378c0064d225091ffca975f2b3f9f` |
| `docs/contracts/b1/originals/codex/C05_20261025T083549Z_request-r1-r4-review.md` | 미추적 | 7,320 | `75a86142cd2196627f7ebed68ff2d88ea8259d8581b12a6bd4ffc0048bb05ca4` |
| `docs/contracts/b1/originals/codex/C06_20261025T084256Z_verdict-3-fix-required.md` | 미추적 | 11,995 | `63473f314ae9c23cd90fe48766e3a1f24ea1dd96e73c890f3661cad6d50e6cb7` |
| `docs/contracts/b1/originals/codex/C07_20261025T094543Z_request-r2-review.md` | 미추적 | 5,964 | `e361e06497b632fc434f317bb4645ef409fa6c0f0dbf3626957d0622b48d71df` |
| `docs/contracts/b1/originals/codex/C08_20261025T095126Z_verdict-4-fix-required.md` | 미추적 | 10,612 | `00606fb422e4b68c76cd4785f5f2d9444811debb35eedcf05e9a7128cf552b1d` |
| `docs/contracts/b1/originals/codex/C09_20261025T104601Z_request-final-review.md` | 미추적 | 7,585 | `5858a25d38335b17caf34f6509c15b483e009300c4e8142fbd6e30448c53b85a` |
| `docs/contracts/b1/originals/codex/C10_20261025T140659Z_verdict-final-pass.md` | 미추적 | 9,511 | `cd5e5cd6c3252d97fe4338cc4851c707b2013d80ace0d11d282606297be1c2e7` |
| `docs/contracts/b1/originals/codex/P01_20261024T014436Z_verdict-b1-t00-ready-for-owner-approval.md` | 미추적 | 4,739 | `2410455673e77c1074cb055e5810932d7fdd19d7f162fae7a6a67a949e2cfd38` |
| `docs/contracts/b1/originals/codex/P02_20261024T032640Z_verdict-b1-t00-source-reconciled.md` | 미추적 | 4,712 | `17237addd3f37b08b5e7753069489e616ed0b5cdbd373117fcc3ade5551d0d26` |
| `docs/contracts/b1/originals/index.json` | 미추적 | 17,891 | `98f38a6d09e05022607e669748da8cb625507ae2ca72cbc042356a536bad1b5e` |
| `docs/contracts/b1/originals/owner/O01_20261024T044515Z_b1-t00-owner-approval-record-request.txt` | 미추적 | 1,238 | `0e5afea6cf2421aba1f164aea27bac2e1802563fba31dc5f311886e50a452cbf` |
| `docs/contracts/b1/originals/owner/O02_20261024T051023Z_b1-t00-t11-batch-implementation-order.txt` | 미추적 | 5,561 | `71892ed5fbd6e9992dfca3167370fb1a6bba51fe69464d19be67700bdf762f8e` |
| `docs/contracts/b1/originals/owner/O03_20261024T052524Z_b1-t00-final-approval-and-nsc-decisions.txt` | 미추적 | 4,972 | `bf41b92850d74274401603ba3b9dded15f54cdbd8d0ebd8a8b4d885c1634bb55` |
| `docs/contracts/b1/originals/owner/O04_20261024T085449Z_pre-t11-completion-order.txt` | 미추적 | 2,517 | `c4a6ccdd0acd8725da4d23c715dfc119ab160f02fbaf4c2d6b1ebd24a28f63c6` |
| `docs/contracts/b1/originals/owner/O05_20261024T124839Z_owner-decision-2.txt` | 미추적 | 3,621 | `d5e5b5f08d534d1f0821ac89e760116da43c5ee672ae59d021b022383705680d` |
| `docs/contracts/b1/originals/owner/O06_20261024T210614Z_d3-restore-and-review-prep.txt` | 미추적 | 2,973 | `7dc6ed47d868d6c5e994462a1ddad2db315c5fd2c79b5edbecb7a6a9dd98d48d` |
| `docs/contracts/b1/originals/owner/O07_20261024T213227Z_decision-3-proposal-check.txt` | 미추적 | 2,539 | `4b74f4ebae017859a470e7d7afd6f54c56e9577d0481acfc32b4561cecdb2824` |
| `docs/contracts/b1/originals/owner/O08_20261024T230240Z_owner-decision-3.txt` | 미추적 | 6,855 | `1a7bf24aa20be82c5746e9603bbb57c34ee02774d3ccffe8b6472a6c87e52814` |
| `docs/contracts/b1/originals/owner/O09_20261025T005759Z_t11-review-task-order.txt` | 미추적 | 3,272 | `9ec6b0fc08cc2487013fa2e1f05c27194c2df27ffd53d7f1be409822c14bb56c` |
| `docs/contracts/b1/originals/owner/O10_20261025T053648Z_t11-second-fix-order.txt` | 미추적 | 7,374 | `52d81d63d033dea0ded457c0ea8451c7f135a157aeb0fb97f306ebc1ae9fd864` |
| `docs/contracts/b1/originals/owner/O11_20261025T054545Z_r2-derivation-choice.txt` | 미추적 | 446 | `b733098edd7762626e1f15d018a843caea853498f64e3e815706685d8113ce89` |
| `docs/contracts/b1/originals/owner/O12_20261025T062708Z_resume-order.txt` | 미추적 | 1,112 | `eca2d3723b4679056cdca6a633a818470ec2e6b1b4d67156025ebc1a15780346` |
| `docs/contracts/b1/originals/owner/O13_20261025T084648Z_r2-final-fix-order.txt` | 미추적 | 7,329 | `8c8c82e1a23c20933c0f3d5d665f0c4a3db0cd3dd9efbed0edd301cfdc524ba9` |
| `docs/contracts/b1/originals/owner/O14_20261025T095658Z_r2b-minor-fix-order.txt` | 미추적 | 6,913 | `11938e73c36c5a25b598bb6862a59b22563a5e5e1eca8b2144c08e568cfe95f0` |
| `docs/contracts/b1/originals/owner/O15_20261025T143058Z_code-acceptance-prep-order.txt` | 미추적 | 3,954 | `c7ad58fcffb85f71c95745c2c08af82a86d679c60cf1b476494e9ef5bfd7a7dc` |
| `docs/contracts/b1/originals/owner/O16_20261025T151044Z_code-acceptance-and-git-baseline-decision.txt` | 미추적 | 6,034 | `7fe61dbcea6af0af62ee41b7cbcc25bf8462bbb9fb63dac43dffa3bd10a7a5c7` |

### 빌드·검증 스크립트·저장소 설정 (4)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `.gitattributes` | 수정(추적) | 2,993 | `f339e2ef904d0866995890c8ac7c95db17dd0f54c6a146cdbd75fed930f9d419` |
| `StarPlayer.sln` | 수정(추적) | 8,747 | `328cef9b7b57ea54ec6fbd73570253a0589741045d9e3d1bb4e3515637a22635` |
| `tools/b1-drypilot-repro.ps1` | 미추적 | 2,337 | `e8236b63c446695a908d96aa78727814ceee6869d6b750ac096b3438c8c4ee16` |
| `tools/verify.ps1` | 수정(추적) | 28,646 | `afd792c505e818f9978d2533dac8a9b9e7c3d0ea75f9d15a807b66e1c6d79eec` |

### 제품·도구 코드 (36)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `src/StarPlayer.Infrastructure/Fixtures/EventJsonV2Codec.cs` | 미추적 | 789 | `37c6b57e6cbd35db161f2ccce5be4283abbae5633cacadf5fe18809f38882783` |
| `tools/StarPlayer.EvidenceVerifier/Certification.cs` | 미추적 | 25,246 | `224e28c1435bba29bf3f155c78aefab382863ad6a22e87cbcc4b137054056b4d` |
| `tools/StarPlayer.EvidenceVerifier/ExecutionEnvironment.cs` | 미추적 | 8,686 | `6b548c1b31f17ca5dd20652031bf2208205a9951785972d19f7ca19599a9eb9a` |
| `tools/StarPlayer.EvidenceVerifier/PhaseVerifier.cs` | 미추적 | 7,008 | `04b64eaeacbb0742322531eaf7edf67d86040fe89cbfda89abc81a3396c6b4dc` |
| `tools/StarPlayer.EvidenceVerifier/PipeRegistry.cs` | 미추적 | 33,924 | `2b208e8d3934c6d8a73e4e638d0a3bf7571f1309ebe290f2928cef758ff721de` |
| `tools/StarPlayer.EvidenceVerifier/Program.cs` | 미추적 | 1,237 | `2d3d8a71d3d29ee1f0b78a8b70de6ff60d3cd54c5f4ac2a6ff7de900690139e5` |
| `tools/StarPlayer.EvidenceVerifier/Pzl6Verifier.cs` | 미추적 | 17,443 | `c1ae8df208f215e46dc148133d4af66fed7622c9b6ad4489f2c58fbd0dd9af09` |
| `tools/StarPlayer.EvidenceVerifier/Receipts.cs` | 미추적 | 12,587 | `430bfc6250d1355c0c476956749de775c2462c16a0a60020540ddb726ad53f32` |
| `tools/StarPlayer.EvidenceVerifier/Registry.cs` | 미추적 | 18,707 | `c8c256e258c6e76733a7693373a3edf697333f9e33b479b3d818849cdc39b9c9` |
| `tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs` | 미추적 | 12,334 | `54167d54354a2513e1ce1aa8f31d8301f992c0bceccbe25cc3cf145f3181a73a` |
| `tools/StarPlayer.EvidenceVerifier/StarPlayer.EvidenceVerifier.csproj` | 미추적 | 1,343 | `e17274088caf8c285e01c1b6658005324a5db83848862db226e73a6255354a39` |
| `tools/StarPlayer.EvidenceVerifier/TestProfiles.cs` | 미추적 | 17,415 | `c3d582b73b61d1ff1624fcff88d919f9ee912d05a743d9cec07c3b6d5d695462` |
| `tools/StarPlayer.EvidenceVerifier/TrustBootstrap.cs` | 미추적 | 6,070 | `47f5a4c08f166df7b654f99b6f9957ca68217268cfb81f07f778c116727cf88c` |
| `tools/StarPlayer.EvidenceVerifier/Verifier.cs` | 미추적 | 38,279 | `83a82130767557b8cc6270489fd74c508255b9189dfcceada7afc65ffb44088e` |
| `tools/StarPlayer.SimHarness/DominanceOfficial.cs` | 수정(추적) | 41,861 | `5421d563016d05b874acf9a39221e891b17328be118be652fa42410dbbd2421f` |
| `tools/StarPlayer.SimHarness/Evidence/ApprovalRecord.cs` | 미추적 | 24,948 | `825048bbbff7e8dc1c55f9b94714a69d66907241337fafcfa47a7b3e608fa531` |
| `tools/StarPlayer.SimHarness/Evidence/Archive.cs` | 미추적 | 15,481 | `eb3fc9dc8df56f37e888382eae51249a1b69e0bb6016f6c0ac34e225f25a689c` |
| `tools/StarPlayer.SimHarness/Evidence/EvidenceDocuments.cs` | 미추적 | 20,966 | `47da6bcde933db493684369b4098f803e36f05f411a03e36508842fecfbb08e6` |
| `tools/StarPlayer.SimHarness/Evidence/EvidenceHash.cs` | 미추적 | 15,023 | `88eeab592a4cdf824cc24e59a6c17af88ea821af34b4c4403ef0b0122e044c67` |
| `tools/StarPlayer.SimHarness/Evidence/EvidenceRules.cs` | 미추적 | 35,871 | `c97b7183fac888f7534d78de210b1141c31010bbf5350fc492a37a5e9a384209` |
| `tools/StarPlayer.SimHarness/Evidence/EvidenceSchema.cs` | 미추적 | 8,682 | `2afdade3b7779c6d49b16dc81b4ca19906ee04b0e2ffb839f943acb9fafec0eb` |
| `tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs` | 미추적 | 6,713 | `4ab7d62d9acd7eaac45143b424434f4d74fdb76c0f8dad3b0e1e45ff0a6d8b87` |
| `tools/StarPlayer.SimHarness/Evidence/GameResultsEvidence.cs` | 미추적 | 7,009 | `797ce35201461a6d42535719d334d865b68006d52b07da9995952023df8d5509` |
| `tools/StarPlayer.SimHarness/Evidence/JDiff.cs` | 미추적 | 1,468 | `38f772fc8509f6efd11ab9950d40e34fe27c83c1a7cb141503e5c7bd63876173` |
| `tools/StarPlayer.SimHarness/Evidence/Jcs.cs` | 미추적 | 10,659 | `c036b0af772fd1487c989e9d6fd25bb84174595d8e9c170873326d3a31f9060d` |
| `tools/StarPlayer.SimHarness/Evidence/OfficialDocuments.cs` | 미추적 | 19,307 | `34f4d3c7d2fc6e97e420aac844ca18fda520ff3d6eb30df2583c93d3f4dc14fa` |
| `tools/StarPlayer.SimHarness/Evidence/PzlEventObserver.cs` | 미추적 | 5,660 | `801173eafeb0cfa6fa1d497d1eb25dbd621bb23f6baeb5cb51c56951ce0feabb` |
| `tools/StarPlayer.SimHarness/Evidence/StrictJson.cs` | 미추적 | 15,304 | `895625cd929def7f3b42756aa472cfc2ab1c12ab979c7c42e75e5a773cd1ce9b` |
| `tools/StarPlayer.SimHarness/Evidence/TrustAnchor.cs` | 미추적 | 13,236 | `695106abd7c185eeb79b6a08cffafe817ba23c771721c9fef54e8edd6096348b` |
| `tools/StarPlayer.SimHarness/Evidence/UnitPzlEvidence.cs` | 미추적 | 25,182 | `b9b99edfd658d43871ad99ef66f144e6b1fa8fbf5d95c057022c0cb23241f9f0` |
| `tools/StarPlayer.SimHarness/FamilyOfficial.cs` | 수정(추적) | 47,871 | `e41ac7cb988bb9e1cb3b25051e527e822b3c7ff85d7292ac20c1dbbd6ef56e9f` |
| `tools/StarPlayer.SimHarness/GameExecutor.cs` | 수정(추적) | 14,062 | `347a802d6f8480efdea2df074a19161d2ec47aeb07aad9e57a2266022d2d4cfc` |
| `tools/StarPlayer.SimHarness/HarnessData.cs` | 수정(추적) | 16,900 | `7dbfa922b3e6581359a18fd8eeeff1f74b5c1a6c4ea0671329a951323d2f0032` |
| `tools/StarPlayer.SimHarness/Official.cs` | 수정(추적) | 39,192 | `ff211a11fe0588bec4354bfb3d8ca47b17bef496cd8d3d71aa7e89d8787547dd` |
| `tools/StarPlayer.SimHarness/PathSafety.cs` | 수정(추적) | 21,880 | `152387d07a87360c8265137a787c2f44a7fac65ff3eb27815efbb7dc12fe4447` |
| `tools/StarPlayer.SimHarness/Replay.cs` | 수정(추적) | 23,348 | `3508f4ccc9a7d047b10ee05552ea5262bd58d09084645cfac929fc8153ce4d06` |

### 시험 코드 (33)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `tests/StarPlayer.Application.Tests/TrustedBoundaryTests.cs` | 수정(추적) | 21,606 | `0247c6d94ee19ea1152347d22c2c6359e17d77d414942342d67d25a5c7ba4e29` |
| `tests/StarPlayer.EvidenceVerifier.Tests/BootstrapAndProfileTests.cs` | 미추적 | 7,176 | `3b32ea074ca4aac19e3ced84606db43022ab23ea7feb5ce68edf2cbdc0283790` |
| `tests/StarPlayer.EvidenceVerifier.Tests/CertificationAndEnvironmentTests.cs` | 미추적 | 9,789 | `c0a2144b9f5f1aa4353905ef14948f0f6550df996c0b1fc5ef0729263a240484` |
| `tests/StarPlayer.EvidenceVerifier.Tests/CounterexampleTests.cs` | 미추적 | 4,956 | `9f1563254193bca308e4123df3dd38b7f06581facfa0e30d030993d26cde8215` |
| `tests/StarPlayer.EvidenceVerifier.Tests/Decision3VerifierTests.cs` | 미추적 | 12,878 | `eec0d7e516448cd89e250e434ba79a9fb38623ae5c388aa9cae86075251fe5bf` |
| `tests/StarPlayer.EvidenceVerifier.Tests/PipeRegistryTests.cs` | 미추적 | 11,678 | `e1208741298f8e187a220244c7ef53e52b800e7e39461eb27aa40529df076fa6` |
| `tests/StarPlayer.EvidenceVerifier.Tests/Pzl6StandaloneTests.cs` | 미추적 | 2,729 | `063c2679b6d37b72aa6818a60497289babf20c471ce5da6cc6601ff910b86266` |
| `tests/StarPlayer.EvidenceVerifier.Tests/StarPlayer.EvidenceVerifier.Tests.csproj` | 미추적 | 737 | `00eeaa790b087ad97313c9c9421cdff5468048b62a6060343258ce4a56b2b375` |
| `tests/StarPlayer.EvidenceVerifier.Tests/T11CertificationTests.cs` | 미추적 | 18,181 | `3def37b274a88132684f32a396aec9c3e477940de8729df5111c02b85e52d1ae` |
| `tests/StarPlayer.EvidenceVerifier.Tests/T11FixR2Tests.cs` | 미추적 | 14,630 | `ef0f18f9ccc3a6ba29ecc3572f006db5a42587f8a732993449d4cf53205d2da1` |
| `tests/StarPlayer.EvidenceVerifier.Tests/T11FixTests.cs` | 미추적 | 12,337 | `f863761cc2161dc965d4ae3d053d9b6cf24bbdf9011d4bcbe2a559cf0efc3520` |
| `tests/StarPlayer.EvidenceVerifier.Tests/T11R2BValueTests.cs` | 미추적 | 7,464 | `057c8facc7fa39a3a3442aca4476de3e4f6b90e447682e838f54f0ab8c627f1a` |
| `tests/StarPlayer.EvidenceVerifier.Tests/T11R2FinalTests.cs` | 미추적 | 11,338 | `52544ec9620c32323c54793d863013a49fbc7468be38c24eaa8cdb5d6163c156` |
| `tests/StarPlayer.EvidenceVerifier.Tests/TestOnlyTrust.cs` | 미추적 | 9,226 | `551ee8dc4e4190ec3d9ee278aa8b50e62c0d60c8f77e399f8961152b68e0b62b` |
| `tests/StarPlayer.EvidenceVerifier.Tests/TestOnlyWorld.cs` | 미추적 | 19,186 | `4e687d82a6b96d5ca9083137cf6a3453d7a49889305e6a4074e405555f7371d4` |
| `tests/StarPlayer.EvidenceVerifier.Tests/VerifierTests.cs` | 미추적 | 20,805 | `39a51470f17f23830e93fd5f6ddecab48d7a7d7fc355db5f97d03781f462916a` |
| `tests/StarPlayer.EvidenceVerifier.Tests/WitnessOrderTests.cs` | 미추적 | 3,615 | `8ee02caf6a894489b9c6b7ed4215f90f5f955322b15f863c47e1ef24a3bc6315` |
| `tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs` | 미추적 | 11,929 | `8e1eca6c170212ab027f4b001acdcaa68bac8616d24cc7f48fa3ff68c3130182` |
| `tests/StarPlayer.Infrastructure.Tests/StarPlayer.Infrastructure.Tests.csproj` | 수정(추적) | 1,392 | `1097af4df11e163188647e91e632c3c713402ed335443ef698ba468fc17df0e2` |
| `tests/StarPlayer.SimHarness.Tests/DominanceOfficialTests.cs` | 수정(추적) | 30,912 | `a7c24ed3bf1aad336cbf6fb9bf852a95511a8124507d77294ee198fe51c98427` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/ApprovalAndTrustTests.cs` | 미추적 | 22,980 | `a7a21cfc0551c43fbc41d938c3ffbaab97fda744f4536f9390c30e2bf05d4fa2` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/ApprovalFixture.cs` | 미추적 | 3,529 | `e94db7c21cf3aa7a0a4b681b30c744abb6fa3064b2caaf1e608081cc1712cdd5` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/ArchiveTests.cs` | 미추적 | 11,841 | `626587bf533422bfc269c953262b0176daaa454d1f913a7ab606e19bd92c4f9f` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/CounterexampleTests.cs` | 미추적 | 13,353 | `03213d9b03484b49d09e01436c6aa60de7b4dd429b8f6ab079d41b73e691f12a` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/Decision3EvidenceTests.cs` | 미추적 | 10,218 | `b4020d4b359e31cc1a24f641a4b5c7a4274d2788575d815f0b35bfe3d6463f7e` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/EvidenceHashTests.cs` | 미추적 | 13,863 | `52cdd3b62bb16157b5ff27f9abcf054e7e8cdde03951760a2ec33444c6d15ae4` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/GoldenTests.cs` | 미추적 | 4,628 | `8f0bd1091cf7d2aee19e5f8eedc47ddae0f7b7dcaa44afa45c56c0feeaf61591` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/JEdit.cs` | 미추적 | 2,197 | `778f6c3f822c1057811e34a5062dba0e53b6cf70cfb55c8f710b0a725b5814a7` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/ObserverSeamTests.cs` | 미추적 | 7,231 | `262e9a5e02d39751155164548ee9ef7662c143ef9c2d6455ed47a148d6878ea1` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/OfficialDocumentTests.cs` | 미추적 | 12,784 | `32714ba605a8f629550d55c436e15cc6b35ee72452554f0dff3d4af69967696f` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/R2InputsAndPreflightTests.cs` | 미추적 | 3,674 | `31bc5a99f833156f47ed29f57a86e2034c413c3a09bc9106988c52de9b1a152c` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/StrictJsonAndJcsTests.cs` | 미추적 | 9,120 | `79015458c6df3b0aebf10fd4c66b629fc7725eb2241ee1d1b7f2dea9b73b25fa` |
| `tests/StarPlayer.SimHarness.Tests/Evidence/UnitPzlEvidenceTests.cs` | 미추적 | 12,819 | `f1d3d968f12eab756cc3838c06396fba193628dcb4222d768c7a5039bc1bf6bd` |

### Golden 고정 입력·기대값 (60)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `tests/Fixtures/b1-golden/cal/document.json` | 미추적 | 118,714 | `f50bd70c7d4596e2d770a2980a8193a2013ed6fb8056c11ded9a1cce808f076b` |
| `tests/Fixtures/b1-golden/cal/unit-00.json` | 미추적 | 18,672 | `04747459022b888ca5444624e293a31d599176d2bedd22d46ed79a6df496c6b6` |
| `tests/Fixtures/b1-golden/cal/unit-01.json` | 미추적 | 18,749 | `ec10f40385d452d9a0bc5b5838922978b220564d7a6d03f60b1f37a77aa95b4b` |
| `tests/Fixtures/b1-golden/cal/unit-02.json` | 미추적 | 18,696 | `f2ba0e5dded10f4fc2c7ee479ec511dd6c7ac35ef5324052ef847a5373bf4e7c` |
| `tests/Fixtures/b1-golden/cal/unit-03.json` | 미추적 | 18,765 | `d3af00ab0f63d8c27f8137d72ff516092b3d6a296f4d7e420eb6908f9d793741` |
| `tests/Fixtures/b1-golden/dom/document.json` | 미추적 | 377,868 | `9f5499255df80a04b5ba853400da85b66d52484dae3e19ca68c9fe882449b436` |
| `tests/Fixtures/b1-golden/dom/unit-00.json` | 미추적 | 5,358 | `63683fe06743e2d2ddc407652987890c7c71a440ae9bd75afae73937739eac6b` |
| `tests/Fixtures/b1-golden/dom/unit-01.json` | 미추적 | 5,357 | `d7f5740b85d0fa42732fb1f20753852dfdb944124e18bfd753465a17acb10432` |
| `tests/Fixtures/b1-golden/dom/unit-02.json` | 미추적 | 5,358 | `7b9f4626b0feebe19eb1fb99cc44bf49b3fdb89e918ee67c8450c015660747b7` |
| `tests/Fixtures/b1-golden/dom/unit-03.json` | 미추적 | 5,359 | `b36428a72435002628119fc5880eb6850f1c634916111f4bd81a641c91919b25` |
| `tests/Fixtures/b1-golden/dom/unit-04.json` | 미추적 | 5,357 | `de923fca93e7fb3de841fb1c760ef73c2749564f8d17aa2ddf1426ddc6fe8a5a` |
| `tests/Fixtures/b1-golden/dom/unit-05.json` | 미추적 | 5,358 | `7c9941ef1e8c5dfb5b5f6305729ab635e41b64100be2a74b8874d7cb102527cd` |
| `tests/Fixtures/b1-golden/dom/unit-06.json` | 미추적 | 5,358 | `e87286b54c1fc38f94f94022a6c8d674ce1fc7e060a3a548c4e73000c3907114` |
| `tests/Fixtures/b1-golden/dom/unit-07.json` | 미추적 | 5,358 | `2e839db8dd16340ca23b9368ecb224918842550aa7cb9589c7af7350a7f54882` |
| `tests/Fixtures/b1-golden/dom/unit-08.json` | 미추적 | 5,358 | `3e593c84369f167899ed286a8673381bf63f18e5bd8cf42572bb4266fb89bb41` |
| `tests/Fixtures/b1-golden/dom/unit-09.json` | 미추적 | 5,358 | `d28e94265df1339cbf79698e153ad09421aeb032580e55c766435b20cd0a8655` |
| `tests/Fixtures/b1-golden/dom/unit-10.json` | 미추적 | 5,358 | `ebad8c32a35e06677dabed74c69c80e32174ced945087d7bea544c25e26fe4e0` |
| `tests/Fixtures/b1-golden/dom/unit-11.json` | 미추적 | 5,358 | `c170985e40d4c870c976b3fde7b08ebf6f3926d241d87b8c1daf04247aa03c63` |
| `tests/Fixtures/b1-golden/dom/unit-12.json` | 미추적 | 5,389 | `c3e3181a25dc1f54dafcda942d923cf6ca9c7e7c5f07c250c2ccb79d54eab42d` |
| `tests/Fixtures/b1-golden/dom/unit-13.json` | 미추적 | 5,392 | `25153c7d4053aaaaf601acd20f58aa2a1af59ecccae222a80171d1c8d9da5032` |
| `tests/Fixtures/b1-golden/dom/unit-14.json` | 미추적 | 5,394 | `df0e9dd173b1df5db54a09cf3e8cf4787301cb965baf07323e96f7bc62e7f365` |
| `tests/Fixtures/b1-golden/dom/unit-15.json` | 미추적 | 5,393 | `82ad0c05e719b7e85f790681dd553de16aacf103c1217c3da5c7d99ff2a29ce1` |
| `tests/Fixtures/b1-golden/dom/unit-16.json` | 미추적 | 5,394 | `3ea4c9ba36a71b0be4eb7a51723c20fd563340c18252d20101b7922ade2975b8` |
| `tests/Fixtures/b1-golden/dom/unit-17.json` | 미추적 | 5,391 | `fb9b012a2754ccce9f551977f24f849e80626732faead3284538cee46e5b3441` |
| `tests/Fixtures/b1-golden/dom/unit-18.json` | 미추적 | 5,391 | `33ac4d9fc6e5420dbd15095f266ae1b34f84bf30f7954f10d8dff2059b1f5c53` |
| `tests/Fixtures/b1-golden/dom/unit-19.json` | 미추적 | 5,389 | `f1080b751a72fd0fa60633b341561afc057e5b43d07be307fb5a12ac09e6fbd0` |
| `tests/Fixtures/b1-golden/dom/unit-20.json` | 미추적 | 5,391 | `acd51f142ac3e1b358b3ddb476dd032b30623ebadd18afb3501f625509a2b9e9` |
| `tests/Fixtures/b1-golden/dom/unit-21.json` | 미추적 | 5,393 | `ce72a534b73839d5909c4a7aefd4ec0757fc82109e1868eaf5495c9152df8a76` |
| `tests/Fixtures/b1-golden/dom/unit-22.json` | 미추적 | 5,393 | `bcfc46933f9c7f7a3c72bbb9e00c0727815ce8aff6a5d2fe06934010ec004cf0` |
| `tests/Fixtures/b1-golden/dom/unit-23.json` | 미추적 | 5,393 | `70be44f025c77c4d61ca787829b4a6db583f80a41da6c9e4d3e0f236a8118b2d` |
| `tests/Fixtures/b1-golden/dom/unit-24.json` | 미추적 | 5,374 | `c09431aa4e6c25fc154cf96e56a5dc27162030239b293f6e79d82ba904d8449d` |
| `tests/Fixtures/b1-golden/dom/unit-25.json` | 미추적 | 5,372 | `25d585ffc8f863590abe08bee3febaefd421f487d36095fe29c2f6f114e75ef5` |
| `tests/Fixtures/b1-golden/dom/unit-26.json` | 미추적 | 5,375 | `cc7922f4b2f40f788583dd3b02ffaa70658a4ad175d2d732b2d1ad58c39d4ce4` |
| `tests/Fixtures/b1-golden/dom/unit-27.json` | 미추적 | 5,374 | `0cc2980402e2fa843b7f25b05a210536067006c54580476283c080d806dfd9b8` |
| `tests/Fixtures/b1-golden/dom/unit-28.json` | 미추적 | 5,374 | `97aee2ef0f2763c160463a54d90ebaf04c4438df919d7afc0a276f785359e060` |
| `tests/Fixtures/b1-golden/dom/unit-29.json` | 미추적 | 5,374 | `990627588dd7030f8a6ed093c7229b394c95071391272b57e9eb44b293550f48` |
| `tests/Fixtures/b1-golden/dom/unit-30.json` | 미추적 | 5,373 | `4b906fad45d073cee12457ca7fe21f09ac2b7aa66a89b94c6f02b92229c805e5` |
| `tests/Fixtures/b1-golden/dom/unit-31.json` | 미추적 | 5,372 | `c7bc90c249175603c4769904fe388e55c13440ca9ed9e799b06379966f0a1d83` |
| `tests/Fixtures/b1-golden/dom/unit-32.json` | 미추적 | 5,374 | `196c06ecd8c62d8d1684b2e9db2761456a90216054cae6bc75144b4d79ad88b4` |
| `tests/Fixtures/b1-golden/dom/unit-33.json` | 미추적 | 5,375 | `fe0654527c55af7bf449c3628dbf4afb9dfeabe3e99b993051bd4d1a544d3b56` |
| `tests/Fixtures/b1-golden/dom/unit-34.json` | 미추적 | 5,375 | `d7b7c4853d31b2ee4bad52b23d32cd4e04e516d13f5a1a79a8a68d109dea2f86` |
| `tests/Fixtures/b1-golden/dom/unit-35.json` | 미추적 | 5,374 | `7d7285308f637790db83c95829f9233e89b288103f381d359587f471f3b7f3b8` |
| `tests/Fixtures/b1-golden/dom/unit-36.json` | 미추적 | 5,411 | `402d7690db5ffea0d0f3f634e4c4773bd43b5ee67993f5ab5bf72ece3b2f1ead` |
| `tests/Fixtures/b1-golden/dom/unit-37.json` | 미추적 | 5,409 | `81295473f52416d70219bab81b27529d7780da05af4f15f1a66604a6b450e92f` |
| `tests/Fixtures/b1-golden/dom/unit-38.json` | 미추적 | 5,407 | `69f318bee8dce656ff7736edffc51afee377fb8c7439fcf9ddbd485ddda018bf` |
| `tests/Fixtures/b1-golden/dom/unit-39.json` | 미추적 | 5,409 | `b7c58c90be745248966823c63f6ee97c72d930a888aadbf64de03d8d6c293302` |
| `tests/Fixtures/b1-golden/dom/unit-40.json` | 미추적 | 5,407 | `5b7ff96bc151e9c07a5bfbad059ff6e1a45c35a56bcf5c8bf8b7a551afe6924b` |
| `tests/Fixtures/b1-golden/dom/unit-41.json` | 미추적 | 5,409 | `00a8561ea319eb56b1cf5d1b55f74134a87f74d3efc7648939a99d2d0df83482` |
| `tests/Fixtures/b1-golden/dom/unit-42.json` | 미추적 | 5,405 | `f8b65cfc340b4b9a24ce7fbbd6848891d4656ad5fcac2b7f9ace3130844b1049` |
| `tests/Fixtures/b1-golden/dom/unit-43.json` | 미추적 | 5,340 | `2d65de6d2fafc1c66f6b6ff0d9aaf7d7ad8de4e5356c697b90b6c07d1a850427` |
| `tests/Fixtures/b1-golden/dom/unit-44.json` | 미추적 | 5,410 | `e6dbc0dbf4cc110465e62d700f16f286fc126f9d73a8b401316c9cb65089dbc7` |
| `tests/Fixtures/b1-golden/dom/unit-45.json` | 미추적 | 5,409 | `3cf7c8a6a5bd69c2de7ec18338f81216d8b307fa9aa4d8d1e079a845bb76d5be` |
| `tests/Fixtures/b1-golden/dom/unit-46.json` | 미추적 | 5,409 | `7bea6edf5f86bf738ad67199d7b240afa42bd464e98c8619382f880036b33a9a` |
| `tests/Fixtures/b1-golden/dom/unit-47.json` | 미추적 | 5,409 | `d05f32d08e98d2775e46afad268428bb845ff99cc4fc602b1b6efc622c8e1f0e` |
| `tests/Fixtures/b1-golden/expected.json` | 미추적 | 5,943 | `a07d0afa44765fb55212a17b3022d3975fe5711da1612b5524b679d01d00710e` |
| `tests/Fixtures/b1-golden/int/document.json` | 미추적 | 402,756 | `82637e1f4266ede52b16f87baa2796349966cdd78b5f47c2083569cccd6fa7b0` |
| `tests/Fixtures/b1-golden/int/pzl6.json` | 미추적 | 14,080 | `ff802fd518bb1d0da61136f204d19ba597d3fef3e3fb37ff431110ac677f3056` |
| `tests/Fixtures/b1-golden/int/unit-00.json` | 미추적 | 134,525 | `1d97128c8389bf0c4439120ea767b2f46bf660099fabdb50c4d2c73b75d76294` |
| `tests/Fixtures/b1-golden/int/unit-01.json` | 미추적 | 134,690 | `f3b2e80eb235fcd3d4dc244781d181e4b8def418ace2d52e0a474206cf38fada` |
| `tests/Fixtures/b1-golden/phase-input.json` | 미추적 | 4,203 | `3b33d798d12a75946e41f2471e7303a07bbf9b71e39755cdad305fb5a9657812` |

### Golden 독립 계산기 (2)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `tools/b1-golden/fields.py` | 미추적 | 10,222 | `6c7f45d23e6e8415c1a606198889d96fdb459829b24901d0e002c11c81060771` |
| `tools/b1-golden/golden.py` | 미추적 | 6,535 | `c2cfed76469f8946e9c4f539aeb45dc7d050300702c0dbea1c839952e769b592` |

### 검수·실행 기록 (14)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `docs/reports/B1_DECISION3_REPORT.md` | 미추적 | 11,769 | `08e02639898494189ccef89755bf6ca6f606c5e7dfdea431f6aa8c3fbc516ccc` |
| `docs/reports/B1_IMPLEMENTATION_LOG.md` | 미추적 | 19,123 | `2fa62ab1b32622fea153ba53842d628aef41ea701dc0fe2a8a2d76db7f9dfeea` |
| `docs/reports/B1_INTEGRATED_REPORT.md` | 미추적 | 22,066 | `57c7a1ee347a7d114f4fb39e3879074b94e12d5820d52d540f759ad92f3555c5` |
| `docs/reports/B1_OWNER_ACCEPTANCE_DRAFT.md` | 미추적 | 7,301 | `d4d9b1d24989157ffe0a548cfbc0741e7248f681a5ed98023e72c61c709de61b` |
| `docs/reports/B1_T11_CODEX_FINAL_PASS_RECEIVED.md` | 미추적 | 3,956 | `093df922bb6713cbec5765e4f427910fd75b17b51c9817b1d32b0f94ed0d2ca6` |
| `docs/reports/B1_T11_CODEX_REVIEW_RECEIVED.md` | 미추적 | 22,028 | `e36fb586c403e9abe5539214b40cd36ca13b8f6ba14e187e1eedfb3d86a61d14` |
| `docs/reports/B1_T11_FIX_R2_REPORT.md` | 미추적 | 18,739 | `405b2d3fd032a171be5c1468c471da2ac0d74488a4add14e11536da9bf3abc39` |
| `docs/reports/B1_T11_FIX_R2_VERIFY_LOG.md` | 미추적 | 6,319 | `2c6de56f216d980865434bbedb64a8a9298423b75169d6d5961eca481abf031d` |
| `docs/reports/B1_T11_FIX_REPORT.md` | 미추적 | 18,069 | `f8e915401533446cc51700b2642d918bc2df0f1c2b7a6d760a300a6b8e30a17c` |
| `docs/reports/B1_T11_R2B_VALUE_BINDING_REPORT.md` | 미추적 | 14,186 | `4afd359acc35fa0b4a6952e5daabcb96ac83f72678f8d3e6a84b30cacfb6f0ff` |
| `docs/reports/B1_T11_R2B_VALUE_VERIFY_LOG.md` | 미추적 | 6,319 | `565d6737fabfc3eba10705c71e9041baf6e1dc03f7a330de6d4007dfe746f92e` |
| `docs/reports/B1_T11_R2_FINAL_FIX_REPORT.md` | 미추적 | 19,142 | `4a8d2fd602e4411be1972cf4dc006169f65bfe4cca0c7dd64d29b6af83b703a6` |
| `docs/reports/B1_T11_R2_FINAL_VERIFY_LOG.md` | 미추적 | 6,319 | `101ffcbe1a375a3f1b924d46170a91b00c34b019a18ee80ef201ae37c88dc249` |
| `docs/reports/B1_VERIFY_LOG.md` | 미추적 | 6,319 | `725a250ee6f9d8b1700f2389d529e1aa32ea4052ac3e5b78f03f640b67d89244` |

### 작업 지시 (8)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `docs/tasks/2026-10-25_B1_T11_FIX_REQUIRED_TASK.md` | 미추적 | 6,544 | `90ec63b9c759171f52ff8c7eaecaaf23f7bdb53713a331d1c2d77644fca9f5f5` |
| `docs/tasks/B1_CODE_ACCEPTANCE_TASK.md` | 미추적 | 3,921 | `0c7bde4835a95d77a7dde05690505d2a18ae8ec8917c992cccc85a19b5c18ff7` |
| `docs/tasks/B1_OPERATIONAL_GATE_PLAN.md` | 미추적 | 6,534 | `b9f8314ab5092dbca6e27f3f4235c4e9d92e39ce08befd3e2a718ce5a2949e10` |
| `docs/tasks/B1_T11_FIX_R2_TASK.md` | 미추적 | 13,246 | `b502cc7baec8c5dad0472d1a8d60feb313924e4d5e56dcda0304350d18b3224f` |
| `docs/tasks/B1_T11_R2B_VALUE_BINDING_TASK.md` | 미추적 | 9,834 | `3cc38f1497eb771925303090102a81d94b955b200ef3ffc0f3037d29649efa03` |
| `docs/tasks/B1_T11_R2_FINAL_FIX_TASK.md` | 미추적 | 14,488 | `6dbff953e917d5a68503a27bf63026238d7f99ee60d6ce45a18883ed9c904e69` |
| `docs/tasks/B1_T11_REVIEW_TASK.md` | 미추적 | 18,728 | `758598d49b0a2258e9eca98e2ee349c70fc3a3f1a61ae64666bfd1c487806f26` |
| `docs/tasks/README.md` | 미추적 | 1,787 | `4b90995aa3f97f89649e48e8b411381fdc0de9147b67eab490fb8f3e7b40bfa2` |

### 상태·Vault 문서 (7)

| 경로 | 상태 | 바이트 | SHA-256 |
|---|---|---:|---|
| `CURRENT_STATE.md` | 수정(추적) | 182,451 | `f1bb99281e84521bec784161dd8e55b181f11df60d4719e1348be865abf2db40` |
| `Star/00_Dashboard.md` | 수정(추적) | 69,066 | `038b3671a73774902ae3b45c1ee4ba930341fbc447e63b128757cc66dfa1a1c2` |
| `Star/01_Phase_Reviews/Phase_03F_B1_Review.md` | 미추적 | 11,095 | `0d474215877894702d5f41244a72ab93155d96889df87dccb3973e80518811d3` |
| `Star/02_Decisions/2026-10-24_B1_Owner_Decision_2.md` | 미추적 | 1,128 | `4919c5ced00f098bc158a868b060668d362a16c6b64e8dcbf7180aac8be54e88` |
| `Star/02_Decisions/2026-10-24_B1_T00_Owner_Approval.md` | 미추적 | 1,069 | `9b60b2cb6491f6e7d703cae8510888909daa1c663c26eea68dfe54af8ec93214` |
| `Star/02_Decisions/2026-10-25_B1_Owner_Decision_3.md` | 미추적 | 788 | `7b293b4a8b891e927c4ffc7409e23961e47ec671a0c63ea98cd0488c09adc29d` |
| `Star/03_Issues/2026-10-24_B1_Open_Items.md` | 미추적 | 6,408 | `cc1ae30b4dc4933790c418aaca4d7b75b3ee67eed92348af25aa3c1cfdad92b8` |

## 3. D3 보호 파일 (기준 목록 `docs/contracts/b1/B1-T00_BASELINE.sha256`)

| 경로 | SHA-256 | 상태 |
|---|---|---|
| `B1-T00_FINAL_CONTRACT_DRAFT.md` | `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` | 일치 |
| `docs/contracts/b1/B1-T00_FINAL_CONTRACT.approved.md` | `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` | 일치 |
| `TASK.md` | `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825` | 일치 |
| `docs/2023recordRule.pdf` | `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0` | 일치 |
| `docs/2026-official-baseball-rules.pdf` | `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859` | 일치 |

## 4. 빌드 입력 파일의 실제 바이트 (추적 파일, 변경 없음)

| 입력 | 작업 트리 바이트·SHA-256·CRLF 수 | HEAD blob 바이트·SHA-256·CRLF 수 |
|---|---|---|
| `content/tuning/virtual_tuning_v4.json` | 26,505 · `992d7e2487cc53ca0ab2d8f64ce4ec66f76b2430b834d276f47378c2e3d88c1c` · 159 | 26,346 · `20692a32230160c7112a097351eb6fa7d5369022cd53cd5649dadbe14602b9cb` · 0 |
| `content/rules/game_rules_profiles_v2.json` | 2,671 · `b016b37bd94e1fcf7594525cfe3d6b590853619b4a9e13de5683a7bfc6bcab45` · 0 | 2,671 · `b016b37bd94e1fcf7594525cfe3d6b590853619b4a9e13de5683a7bfc6bcab45` · 0 |

- SimHarness가 내장하는 바이트는 **작업 트리 바이트**입니다. 시험 `R2A_the_input_list_is_exactly…`가 리소스 = 작업 트리 파일을 단정합니다.
- 튜닝 파일은 system `core.autocrlf=true` 때문에 작업 트리가 CRLF, blob이 LF입니다. 운영 빌드 입력 고정은 [운영 Gate 계획](../tasks/B1_OPERATIONAL_GATE_PLAN.md) G2입니다.

## 5. G2 개행 정책 적용 후 체크아웃 재현 예측

OWNER 결정 G2(2026-10-26)에 따라 `.gitattributes`에 규칙을 추가했습니다.

- 정확한 바이트(`-text`/`binary`): D3 보호 파일, 계약 기록·원문, B1 검수 기록·로그, Golden, 내장 입력 JSON 2개
- 명시적 개행: B1 신규 디렉터리와 그 밖의 검수 파일에 작업 트리 그대로 `eol=lf`/`eol=crlf`
- 일괄 정규화는 하지 않았습니다.

아래는 각 파일의 규칙에 따라 다른 기계·설정에서 체크아웃했을 때 작업 트리 바이트가 그대로 나오는지 예측한 결과입니다.

| 규칙 | 파일 수 | 재현 위험 |
|---|---:|---:|
| -text/binary(그대로) | 128 | 0 |
| eol=crlf | 2 | 0 |
| eol=lf | 80 | 0 |

재현 위험 파일: **0개**.

- 튜닝 JSON은 `-text`로 **검수된 작업 트리 바이트(CRLF)**를 그대로 저장합니다. 그 결과 저장소 blob이 LF에서 CRLF로 바뀌며, 이것이 G2 Commit의 유일한 개행 변화입니다.
- 규칙 JSON은 blob과 작업 트리가 같아 변화가 없습니다.
- 이 정책은 B1 코드 기준본의 바이트 보존용입니다. 운영 Build Freeze(운영 Gate G2 후속)와는 별개입니다.

## 6. Commit·Tag (OWNER 조건부 허가, 2026-10-26)

1. **Commit 1(G2):** `.gitattributes`와 튜닝 JSON 재정규화(`git add --renormalize`, 개행 바이트만)
2. **Commit 2(기준본):** 이 목록의 모든 경로와 목록 파일 2개를 **허용 목록으로** Stage(`git add -A` 미사용). D3 PDF 2개 포함, 7절 산출물 제외.
3. **Stage 점검:**
   - Stage 목록 = 허용 목록
   - 각 blob 바이트 = 작업 트리 바이트(`eol=crlf` 파일은 CRLF→LF 정규화만)
   - D3 blob SHA = 승인 SHA
   - 비밀정보 검사
4. **깨끗한 체크아웃 재검증:** 이 목록 SHA, D3, `tools/verify.ps1`, Dry Pilot 9 Run, Golden, 문서 링크, 추적 파일 변경·개행
5. **Tag:** 모두 통과하면 Commit 2에 annotated Tag(B1 코드 수용 범위 이름)를 만듭니다. 결과는 Tag 메시지와 Commit 이후 기록에 남기고, 불일치가 있으면 Tag를 만들지 않습니다.
6. Remote Push는 하지 않습니다(별도 OWNER 허가 필요).

## 7. 포함하지 않는 생성 산출물 (git-ignored)

- `**/bin/`·`**/obj/`(빌드 출력), `build/`(verify 원시 로그·Export·Dry Pilot 재현·시험 임시 출력), `.godot/`. 커밋 대상이 아닙니다.
- 증거로 보존을 권장하는 원시 파일(현재 SHA):

| 경로 | 바이트 | SHA-256 |
|---|---:|---|
| `build/verify/verify-result.json` | 9,058 | `99aeb2bd79d8f719f5f2769f560d528bdf5508d6905cc783df7010f250ae873c` |
| `build/verify/U4.log` | 432 | `0e079227cd595f8bc261e3c3d28fabaed4aa77d29e3cc9e6383fe9549c3f62af` |
| `build/verify/U5.log` | 420 | `017c0331362138f4c97582c52b9a98e9a00d3578ab3d48dcd3ce7bb835398085` |
| `build/verify/U6.log` | 438 | `e4667df8874e0633c1670f5ee4681580f182a2c0ac7351048b7a5a0e25722362` |

- Dry Pilot 재현 출력 `build/b1-t04a-repro/`: 9개 Run 디렉터리. 비교 기준 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1/`
- 장기 보존 위치는 운영 Archive 결정(B-4)과 함께 정합니다.

## 8. 다시 확인하는 방법

```bash
sha256sum -c docs/reports/B1_IMPLEMENTATION_MANIFEST.sha256
```

```bash
sha256sum -c docs/contracts/b1/B1-T00_BASELINE.sha256
```

목록을 다시 만들 때는 같은 `git status` 명령으로 경로를 모으고, 작업 트리 바이트의 SHA-256을 계산합니다. 파일이 바뀌면 이 문서와 `.sha256`을 함께 다시 생성합니다.
