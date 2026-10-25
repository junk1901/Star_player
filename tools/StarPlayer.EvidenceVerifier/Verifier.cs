using System.Text.Json;
using StarPlayer.Domain.Common;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.EvidenceVerifier;

/// <summary>The trusted inputs of one family verification: the approval record and execution authorization (from the registry), the repository root holding the pinned seed files, the archive root and
/// the reference of the submitted official document. There is no outside result directory (B1-T11-03): the replay reads only what the archive snapshot verified.</summary>
public sealed record FamilyVerificationRequest(
    ApprovalRecordV1 Approval, JObject? Authorization, string Family, string RepositoryRoot, string ArchiveRoot, JObject DocumentReference, EventJsonCodec? Codec = null);

/// <summary>A verified family: the validated documents, the payload and its A+, and either the eligible statuses or the §19 reason no receipt is issued (verification and certification are separate axes).</summary>
public sealed record VerifiedFamily(JObject Document, IReadOnlyList<JObject> PzlUnits, JObject? Pzl6, EvidencePayload Payload, string? TechnicalStatus, string? StatisticalStatus, EvidenceError? Ineligible, bool TestOnly,
    IReadOnlyList<JObject> SourceFiles)
{
    /// <summary>The verified family and the approval record it was verified under (set by the verifier).</summary>
    public string Family { get; init; } = "";

    public ApprovalRecordV1? Approval { get; init; }
}

/// <summary>
/// T06b: the integrated verification of one family (B1-T00 §11.3, §13-§14, §16 ④-⑨ after the trust bootstrap). Order: authorization state → AMPV → archive snapshot (one read) → closed schemas and the
/// DAG → approval binding S3-S6 → the EXISTING replay (<see cref="FamilyOfficialReplay"/> / <see cref="DominanceOfficialReplay"/>, never replaced, with an observing seam and the R01 authority) → the
/// documents re-projected from the re-execution and compared field by field → PZL re-observation of every game and bundle row → #6 → payload → eligibility. Existing cause codes are kept; only
/// <c>Official.not_ready</c> and the TEST_ONLY misuse become <c>Evidence.authorization_missing</c> (R01-C). Nothing here calls an Execute path (R01-2).
/// </summary>
public static class Verifier
{
    private static EvidenceException Fail(string code, string cause, string pointer, string stage, string message, string? family = null, string? unit = null) =>
        new(new EvidenceError(code, cause, pointer, stage, message, family, unit));

    // ---------------------------------------------------------------- §14 authorization and AMPV

    /// <summary>§14.2 / §14.3: the replay authority and the verifier-only approvals instance. No authorization record → <c>authorization_missing</c> (N15, N19).</summary>
    public static (ReplayAuthority Authority, OfficialApprovals Approvals) Authority(ApprovalRecordV1 a, JObject? authorization, string family, bool registryTrusted, bool ampvPassed)
    {
        if (authorization is null || !Items(authorization["families"]).Select(Text).Contains(family, StringComparer.Ordinal))
            throw Fail(EvidenceCodes.AuthorizationMissing, "Authorization.missing", "", "authorization", $"{family}에 대한 실행 허가 기록이 없습니다.", family);
        var testOnly = a.TestOnly || ((JBool)authorization["test_only"]).Value;
        if (testOnly)
            return (ReplayAuthority.TestOnlyVerification(Items(authorization["families"]).Select(Text)), ApprovalFormats.ToOfficialApprovals(a, executionPathReady: false));
        // R01-1: all five conditions; B-1 never makes OfficialApprovals.Current ready
        var ready = registryTrusted && ampvPassed && !a.TestOnly && !((JBool)authorization["test_only"]).Value;
        return (ReplayAuthority.OfficialExecution, ApprovalFormats.ToOfficialApprovals(a, ready));
    }

    /// <summary>§14.6 AMPV over the candidate instance A (ExecutionPathReady always false): pin scan, plan size, every unit's existing preflight classified by the fixed table, and the attestation built directly.</summary>
    public static EvidenceError? Ampv(ApprovalRecordV1 approval, string family, string root)
    {
        var a = ApprovalFormats.ToOfficialApprovals(approval, executionPathReady: false);
        EvidenceError Err(string cause, string message) => new(EvidenceCodes.ApprovalMismatch, cause, "", "ampv", message, family);
        var units = new List<string>();
        if (family == DominanceMatrix.Family)
        {
            var scan = DominancePreflight.PinnedCells(root, a);
            if (scan.Status != CheckStatus.Pass)
                return Err(scan.Code ?? "Official.not_ready", scan.Message);
            var size = DominancePreflight.PlanSizeCheck(a, scan);
            if (size.Status != CheckStatus.Pass)
                return Err(size.Code ?? "Official.not_ready", size.Message);
            units.AddRange(scan.Cells.Select(c => c.SeedManifestPath));
        }
        else
        {
            var plan = family == "fc3-cal" ? OfficialFamilyPlan.Calibration : OfficialFamilyPlan.Integration;
            var scan = FamilyPreflight.PinnedUnits(plan, root, a);
            if (scan.Status != CheckStatus.Pass)
                return Err(scan.Code ?? "Official.not_ready", scan.Message);
            units.AddRange(scan.Units.Select(u => u.SeedManifestPath));
        }

        string[] evaluated = ["model_tuning", "seed_manifest", "family", "rules", "policy", "cohort", "cohort_hash", "game_ids", "execution_policy"];
        string[] executionOnly = ["cli", "git_integrity", "git_sha", "output_directory"];
        var runId = Text(approval.Family(family)["run_id"]);
        var git = GitIntegrityPolicy.Evaluate([], approval.GitCommitSha);
        foreach (var path in units)
        {
            var report = OfficialPreflight.Run(new OfficialRequest(root, path, runId, null), a, git);
            foreach (var c in report.Checks)
            {
                if (evaluated.Contains(c.Id))
                {
                    if (c.Status != CheckStatus.Pass)
                        return Err(c.Code ?? "Official.not_ready", $"{path}: {c.Id} {c.Message}");
                }
                else if (c.Id == "execution_path")
                {
                    if ((c.Status, c.Code) != (CheckStatus.NotReady, "Official.not_ready"))
                        return Err("Ampv.execution_path", $"{path}: execution_path는 (NotReady, Official.not_ready)여야 합니다(받은 값 {c.Status}).");
                }
                else if (c.Id == "attestation")
                {
                    if ((c.Status, c.Code) != (CheckStatus.NotReady, "Official.input_unavailable"))
                        return Err("Ampv.attestation", $"{path}: attestation은 (NotReady, Official.input_unavailable)이어야 합니다.");
                }
                else if (!executionOnly.Contains(c.Id))
                    return Err("Ampv.unknown_check", $"{path}: AMPV 분류표에 없는 검사 '{c.Id}'입니다.");
            }

            var m = report.Manifest!;
            var built = OfficialPreflight.BuildAttestation(new OfficialRequest(root, path, runId, null), a, m, CohortRegistry.Resolve(m.CohortId).Value, report.Rules!, report.Tuning!,
                PolicyRegistry.Resolve(m.PolicyId).Value);
            if (!built.IsSuccess)
                return Err(built.Error!.Value.Code, built.Error.Value.Message);
        }

        return null;
    }

    /// <summary>R01-C: only an actual <c>Official.not_ready</c> (and the TEST_ONLY misuse) is an authorization error; every other replay error keeps its cause under its own class.</summary>
    public static EvidenceError Wrap(Error e, string family, bool gamesExecuted) => e.Code switch
    {
        "Official.not_ready" or "Trust.test_only_flag_misuse" => new(EvidenceCodes.AuthorizationMissing, e.Code, "", "replay", e.Message, family),
        _ when e.Code.StartsWith("Seed.", StringComparison.Ordinal) || e.Code.EndsWith("seed_manifest_hash_mismatch", StringComparison.Ordinal) || e.Code.EndsWith("approval_record_mismatch", StringComparison.Ordinal)
            => new(EvidenceCodes.ApprovalMismatch, e.Code, "", "replay", e.Message, family),
        _ when e.Code.Contains("evaluation", StringComparison.Ordinal) => new(EvidenceCodes.EvaluationMismatch, e.Code, "", "replay", e.Message, family),
        _ when e.Code.Contains("hash_mismatch", StringComparison.Ordinal) && !gamesExecuted => new(EvidenceCodes.HashMismatch, e.Code, "", "replay", e.Message, family),
        _ => new(EvidenceCodes.ReplayMismatch, e.Code, "", "replay", e.Message, family),
    };

    // ---------------------------------------------------------------- the family pipeline

    /// <summary>B1 T11 R1: the bytes of a result at the end of its verification (the canonical payload copied, every document / list as its JCS).</summary>
    private sealed record Frozen(byte[] Canonical, string Hash, byte[] Document, byte[] Units, byte[] Pzl6, byte[] Payload, byte[] Sources);

    private static Frozen Freeze(VerifiedFamily v) => new(v.Payload.Canonical.ToArray(), v.Payload.Hash, Jcs.Bytes(v.Document), Jcs.Bytes(J.A(v.PzlUnits.Cast<JNode>().ToArray())),
        Jcs.Bytes(v.Pzl6 is null ? JNode.Null : v.Pzl6), Jcs.Bytes(v.Payload.Payload), Jcs.Bytes(J.A(v.SourceFiles.Cast<JNode>().ToArray())));

    /// <summary>Results produced by <see cref="VerifyFamily"/> with their verification snapshot — a record made or changed anywhere else (<c>new</c>, <c>with</c>) is not a verification result and cannot be
    /// certified (B1-T11-01).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VerifiedFamily, Frozen> Produced = new();

    internal static bool IsVerified(VerifiedFamily v) => Produced.TryGetValue(v, out _);

    /// <summary>
    /// B1 T11 R1: a result produced here, rebuilt from its verification snapshot only (fresh parses of the frozen bytes, a copy of the canonical payload), after its public data was found still exactly
    /// that snapshot — anything changed since (a canonical byte, a list, a document) is <c>not_certifiable</c>, never issued with the verified A+.
    /// </summary>
    internal static VerifiedFamily Snapshot(VerifiedFamily v)
    {
        if (!Produced.TryGetValue(v, out var f))
            throw Fail(EvidenceCodes.NotCertifiable, "Certification.unverified_result", "", "certification", "검증기가 만든 결과가 아닙니다.", v.Family);
        var now = Freeze(v);
        if (!now.Canonical.AsSpan().SequenceEqual(f.Canonical) || now.Hash != f.Hash || !now.Document.AsSpan().SequenceEqual(f.Document) || !now.Units.AsSpan().SequenceEqual(f.Units)
            || !now.Pzl6.AsSpan().SequenceEqual(f.Pzl6) || !now.Payload.AsSpan().SequenceEqual(f.Payload) || !now.Sources.AsSpan().SequenceEqual(f.Sources))
            throw Fail(EvidenceCodes.NotCertifiable, "Certification.result_mutated", "", "certification", "검증 뒤 결과의 Canonical·문서·원본 목록이 바뀌었습니다(검증 당시 스냅샷과 다름).", v.Family);
        static IReadOnlyList<JObject> List(byte[] b) => Array.AsReadOnly(Items(StrictJson.Parse(b)).Select(Obj).ToArray());
        var payload = new EvidencePayload(v.Payload.Kind, (JObject)StrictJson.Parse(f.Payload), f.Canonical.ToArray(), f.Hash);
        return new VerifiedFamily((JObject)StrictJson.Parse(f.Document), List(f.Units), StrictJson.Parse(f.Pzl6) as JObject, payload, v.TechnicalStatus, v.StatisticalStatus, v.Ineligible, v.TestOnly,
            List(f.Sources)) { Family = v.Family, Approval = v.Approval };
    }

    public static VerifiedFamily VerifyFamily(FamilyVerificationRequest r, bool registryTrusted = false)
    {
        var a = r.Approval;
        var family = r.Family;
        var kind = family == DominanceMatrix.Family ? EvidenceKind.Dominance : EvidenceKind.Family;

        // ⑧ authorization, AMPV (before any game)
        if (Ampv(a, family, r.RepositoryRoot) is { } ampv)
            throw new EvidenceException(ampv);
        var (authority, approvals) = Authority(a, r.Authorization, family, registryTrusted, ampvPassed: true);

        // one snapshot of the document, then of EVERY reference of it and of the documents below it (B1-T11-03): Unit PZL, PZL6 and their own references, each artifact id one reference
        var docFile = Archive.Snapshot(r.ArchiveRoot, [r.DocumentReference]).Values.Single();
        var doc = EvidenceRules.Validate(kind, docFile.Bytes);
        Archive.CheckReferences(kind, doc);
        var refs = Items(Obj(doc["additional_evidence"])["artifacts"]).Select(Obj).ToList();
        var snapshot = new Dictionary<string, ArchivedFile>(StringComparer.Ordinal) { [docFile.ArtifactId] = docFile };
        void Take(IEnumerable<JObject> references, string owner)
        {
            var fresh = new List<JObject>();
            foreach (var x in references)
            {
                var id = Text(x["artifact_id"]);
                if (snapshot.TryGetValue(id, out var known))
                {
                    if (!Jcs.Bytes(known.Reference).AsSpan().SequenceEqual(Jcs.Bytes(x)))
                        throw Fail(EvidenceCodes.ArchiveInvalid, "Archive.artifact_id_conflict", $"/additional_evidence/artifacts/{StrictJson.Token(id)}", "archive",
                            $"{owner}의 artifact_id '{id}'가 다른 문서의 같은 ID와 다른 참조입니다.", family);
                }
                else if (fresh.All(f => Text(f["artifact_id"]) != id))
                    fresh.Add(x);
            }

            foreach (var (id, f) in Archive.Snapshot(r.ArchiveRoot, fresh))
                snapshot[id] = f;
        }

        Take(refs, "Family/Dominance 문서");
        var unitDocs = new List<JObject>();
        var bundles = new List<UnitPzlEvidence.ParsedBundle>();
        var results = new List<GameResultsEvidence.Parsed>();
        foreach (var f in snapshot.Values.Where(f => f.Role == "UNIT_PZL").OrderBy(f => Items(Obj(doc["additional_evidence"])["pzl_unit_ids"]).Select(Text).ToList().IndexOf(
                     EvidenceIds.UnitPzl(a.ValidationSetId, family, Text(f.Reference["unit_id"])))).ToList())
        {
            var u = EvidenceRules.Validate(EvidenceKind.UnitPzl, f.Bytes);
            Archive.CheckReferences(EvidenceKind.UnitPzl, u);
            var unitRefs = Items(Obj(u["additional_evidence"])["artifacts"]).Select(Obj).ToList();
            Take(unitRefs, $"Unit PZL {Text(Obj(u["plan"])["unit_id"])}");                                  // every reference, not only GAME_EVENTS
            var bundle = UnitPzlEvidence.ReadBundle(snapshot[Text(unitRefs.Single(x => Text(x["role"]) == "GAME_EVENTS")["artifact_id"])].Bytes);
            BindBundleHeader(bundle.Header, u);
            unitDocs.Add(u);
            bundles.Add(bundle);
            results.Add(GameResultsEvidence.Read(snapshot[Text(unitRefs.Single(x => Text(x["role"]) == "GAME_RESULTS")["artifact_id"])].Bytes));   // OWNER decision 3 §5 (format)
        }

        JObject? pzl6 = null;
        var pzl6Files = snapshot.Values.Where(f => f.Role == "PZL6" && f.Reference["game_id"] is JNull).ToList();                  // the #6 document (its 20 JSON files carry a game_id)
        if (family == "fc3-int")
        {
            pzl6 = pzl6Files.Count == 1 ? EvidenceRules.Validate(EvidenceKind.Pzl6, pzl6Files[0].Bytes)
                : throw Fail(EvidenceCodes.PzlIncomplete, "Evidence.pzl6_missing", "/additional_evidence/artifacts", "archive", "Integration 증거에는 #6 문서가 정확히 하나 있어야 합니다.", family);
            Archive.CheckReferences(EvidenceKind.Pzl6, pzl6);
            Take(Items(Obj(pzl6["additional_evidence"])["artifacts"]).Select(Obj), "#6 문서");
        }
        else if (pzl6Files.Count != 0)
            throw Fail(EvidenceCodes.ReferenceInvalid, "Evidence.pzl6_unexpected", "/additional_evidence/artifacts", "archive", "#6 문서는 Integration 증거에만 있습니다.", family);

        RequiredCounts(snapshot, kind, family, a);

        // S3-S6 against the approval record (each document separately; never byte-compared with each other)
        foreach (var d in new[] { doc }.Concat(unitDocs).Concat(pzl6 is null ? [] : new[] { pzl6 }))
            BindApproval(d, a, family);
        BindUnits(doc, a, family, kind);

        // the run's own files from the snapshot: exactly the index and one manifest per unit, materialized byte for byte into a new verifier-owned directory for the existing replay (§20.2 1)
        var plan = kind == EvidenceKind.Family ? (family == "fc3-cal" ? OfficialFamilyPlan.Calibration : OfficialFamilyPlan.Integration) : null;
        var unitDirectories = plan is not null ? plan.Units.Select(OfficialFamilyPlan.UnitDirectory).ToList() : DominanceMatrix.Cells.Select(DominancePreflight.CellDirectory).ToList();
        var unitIdsInPlan = plan is not null ? plan.Units.Select(u => u.Id).ToList() : DominanceMatrix.Cells.Select(c => c.Id).ToList();
        var runId = Text(a.Family(family)["run_id"]);
        var runPrefix = EvidenceWriter.RunPath(runId, "");
        var index = RunFile(snapshot, "DIAGNOSTICS", null, EvidenceWriter.RunPath(runId, plan is not null ? FamilyOfficial.ResultFile : DominanceOfficial.ResultFile), family);
        var manifests = unitIdsInPlan.Select((id, i) => RunFile(snapshot, "UNIT_MANIFEST", id, EvidenceWriter.RunPath(runId, $"{unitDirectories[i]}/manifest.json"), family)).ToList();

        // B1 T11 R2-A: the consumed embedded inputs are their repository originals; R2-B: the preflight records are this run's READY preflight (before any game is replayed)
        ConsumedInputs.CheckSources(r.RepositoryRoot, family);
        PreflightRecords.Verify(a, family, plan, runId, r.RepositoryRoot, RunFile(snapshot, "PREFLIGHT", null, EvidenceWriter.RunPath(runId, OfficialOutput.PreflightFile), family),
            unitIdsInPlan.Select(id => RunFile(snapshot, "PREFLIGHT", id, EvidenceWriter.RunPath(runId, $"{OfficialOutput.PreflightDirectory}/{OfficialOutput.UnitPreflightName(id)}"), family)).ToList(),
            manifests.Select(m => Attestation(m.Bytes)).ToList(), unitIdsInPlan);

        var replayRoot = Path.Combine(Path.GetTempPath(), "starplayer-verifier", Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var f in manifests.Append(index))
                Archive.WriteNew(replayRoot, Text(f.Reference["relative_path"])[runPrefix.Length..], f.Bytes, f.ArtifactId, f.Role);

            // the existing replay, observing every game it re-executes
            var observers = new Dictionary<(string Key, int Index), PzlEventObserver>();
            GameRunResult Play(RunConfig c, GameSpec s) => GameExecutor.Play(c, s, default, null, observers[(c.CompetitionKey, s.GameIndex)] = new PzlEventObserver());
            IReadOnlyList<RunResult> reruns;
            JObject expected;
            var ctx = new EvidenceContext(a.ValidationSetId, a.Id, a.RawSha256, a.FreezeId, Text(a.Ref("build_provenance")["id"]),
                kind == EvidenceKind.Family ? Text(Obj(a.Record["combination_policy_clauses"])[family == "fc3-cal" ? "calibration" : "integration"]) : null);
            if (kind == EvidenceKind.Dominance)
            {
                var outcome = DominanceOfficialReplay.Verify(replayRoot, approvals, r.RepositoryRoot, default,
                    (c, specs, ct) => SimRunner.RunSpecs(c, specs, null, ct, s => Play(c, s)), authority);
                if (outcome.Error is { } e)
                    throw new EvidenceException(Wrap(e, family, outcome.GamesExecuted));
                reruns = outcome.Reruns!;
                expected = OfficialDocuments.Dominance(runId, reruns.Select((run, i) => (run, Attestation(manifests[i].Bytes))).ToList(), outcome.Evaluation!, ctx, refs);
            }
            else
            {
                var outcome = FamilyOfficialReplay.Verify(plan!, replayRoot, approvals, r.RepositoryRoot, default, Play, authority);
                if (outcome.Error is { } e)
                    throw new EvidenceException(Wrap(e, family, outcome.GamesExecuted));
                reruns = outcome.Reruns!;
                expected = OfficialDocuments.Family(plan!, runId, reruns.Select((run, i) => (run, Attestation(manifests[i].Bytes))).ToList(), outcome.Evaluation!, ctx, refs);
            }

            Compare(doc, expected, family);

            // PZL: every Unit PZL document and bundle row against the re-observation
            var unitIds = kind == EvidenceKind.Dominance ? DominanceMatrix.Cells.Select(c => c.Id).ToList() : Items(Obj(doc["plan"])["unit_ids"]).Select(Text).ToList();
            if (unitDocs.Count != reruns.Count)
                throw Fail(EvidenceCodes.PzlIncomplete, "Evidence.pzl_units_missing", "/additional_evidence/pzl_unit_ids", "pzl_reobservation", "Unit PZL 문서가 Unit마다 하나씩 있지 않습니다.", family);
            for (var i = 0; i < reruns.Count; i++)
            {
                var run = reruns[i];
                var games = run.Games.Select(g => new ObservedGame(g, observers.GetValueOrDefault((run.Config.CompetitionKey, g.Game.GameIndex)))).ToList();
                var unitArtifacts = Items(Obj(unitDocs[i]["additional_evidence"])["artifacts"]).Select(Obj).ToList();
                var reprojected = UnitPzlEvidence.Document(ctx, family, unitIds[i], a.GitCommitSha, run, games, unitArtifacts);
                if (bundles[i].Games.Count != games.Count)
                    throw Fail(games.Count > bundles[i].Games.Count ? EvidenceCodes.PzlIncomplete : EvidenceCodes.PzlMismatch, "Evidence.bundle_games", "", "pzl_reobservation",
                        $"{unitIds[i]}: 번들 경기 {bundles[i].Games.Count}개 ≠ 재실행 {games.Count}개(실패 경기 누락 포함, N05).", family, unitIds[i]);
                var docGames = Items(unitDocs[i]["games"]).Select(Obj).ToList();
                if (docGames.Count != games.Count)
                    throw Fail(EvidenceCodes.PzlIncomplete, "Evidence.pzl_games_missing", "/games", "pzl_reobservation", $"{unitIds[i]}: Unit PZL games가 실행 경기 전부가 아닙니다(N05).", family, unitIds[i]);
                for (var k = 0; k < games.Count; k++)
                    if (UnitPzlEvidence.CompareGame(bundles[i].Games[k], docGames[k], games[k]) is { } diff)
                        throw new EvidenceException(diff with { Family = family, Unit = unitIds[i] });
                if (JDiff.First(EvidenceHash.M(unitDocs[i]), EvidenceHash.M(reprojected)) is { } p)
                    throw Fail(EvidenceCodes.PzlMismatch, "Evidence.unit_pzl_differs", p, "pzl_reobservation", $"{unitIds[i]}: Unit PZL 문서가 재관찰 투영과 다릅니다({p}).", family, unitIds[i]);
                // OWNER decision 3 §5 (content): every GameResult of the unit's GAME_RESULTS = the parent document's row = the re-projection
                var unitTechnical = Obj(Obj(Items(doc["units"])[i])["technical"]);
                var expectedRows = Items(Obj(Obj(Items(expected["units"])[i])["technical"])["games"]).Select(Obj).ToList();
                if (GameResultsEvidence.Compare(results[i], unitDocs[i], unitTechnical, expectedRows) is { } gr)
                    throw new EvidenceException(gr with { Family = family });
            }

            if (pzl6 is not null)
                VerifyPzl6(pzl6, doc, reruns, r.Codec, family, snapshot);

            var payload = kind == EvidenceKind.Dominance ? EvidenceHash.Dominance(doc, unitDocs) : EvidenceHash.Family(doc, unitDocs, pzl6);
            (string? technical, string? statistical, EvidenceError? ineligible) = (null, null, null);
            try
            {
                (technical, statistical) = Receipts.Eligibility(doc, ((JBool)Obj(a.Record["certification_allowances"])["insufficient_sample"]).Value);
                Receipts.PzlEligibility(unitDocs, pzl6);
            }
            catch (EvidenceException e)
            {
                ineligible = e.Error;
            }

            var sources = snapshot.Values.Select(f => (JObject)JEditMembers.With(f.Reference, "origin", J.S("ORIGINAL"))).OrderBy(x => Text(x["artifact_id"]), StringComparer.Ordinal).ToList();
            var verified = new VerifiedFamily(doc, Array.AsReadOnly(unitDocs.ToArray()), pzl6, payload, technical, statistical, ineligible, authority.TestOnly, Array.AsReadOnly(sources.ToArray()))
                { Family = family, Approval = a };
            Produced.Add(verified, Freeze(verified));                                                      // B1 T11 R1: the snapshot issuance uses
            return verified;
        }
        finally
        {
            try
            {
                Directory.Delete(replayRoot, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// §21 roles and counts over the WHOLE snapshot (B1-T11-03, OWNER decision 3 §5 / §7): per unit exactly one UNIT_PZL, GAME_EVENTS, GAME_RESULTS (game_id null), SEED_MANIFEST and UNIT_MANIFEST; no
    /// GAME_RESULTS with a game_id; Integration exactly one #6 document (PZL6, game_id null) and its 20 JSON files (PZL6, game_id set), the other families none. Else <c>archive_invalid</c>.
    /// </summary>
    private static void RequiredCounts(IReadOnlyDictionary<string, ArchivedFile> snapshot, EvidenceKind kind, string family, ApprovalRecordV1 a)
    {
        var units = kind == EvidenceKind.Dominance ? DominanceMatrix.Cells.Select(c => c.Id).ToList()
            : (family == "fc3-cal" ? OfficialFamilyPlan.Calibration : OfficialFamilyPlan.Integration).Units.Select(u => u.Id).ToList();
        int Count(string role, string? unit, bool? game) => snapshot.Values.Count(f => f.Role == role && (unit is null || (f.Reference["unit_id"] is JString { Value: var u } && u == unit))
            && (game is null || (f.Reference["game_id"] is JString) == game));
        EvidenceException Bad(string message) => Fail(EvidenceCodes.ArchiveInvalid, "Archive.required_count", "/additional_evidence/artifacts", "archive", message, family);
        foreach (var u in units)
            foreach (var role in new[] { "UNIT_PZL", "GAME_EVENTS", "GAME_RESULTS", "SEED_MANIFEST", "UNIT_MANIFEST" })
                if (Count(role, u, role == "GAME_RESULTS" ? false : null) != 1)
                    throw Bad($"Unit '{u}'의 {role}가 정확히 1개가 아닙니다(§21).");
        if (Count("GAME_RESULTS", null, true) != 0)
            throw Bad("GAME_RESULTS는 Unit bundle(game_id null)뿐입니다(#6 JSON은 PZL6, OWNER 결정 3 §7).");
        var integration = family == "fc3-int";
        if (Count("PZL6", null, false) != (integration ? 1 : 0) || Count("PZL6", null, true) != (integration ? 20 : 0))
            throw Bad("PZL6은 Integration에만 문서 1개 + 고정 20경기 JSON 20개입니다(OWNER 결정 3 §7).");

        // B1 T11 R2 (§21, OWNER choice "승인 Pin 기준"): the originals derived from the TRUSTED approval record and the run's own loading path, never from the submitted list — exactly one
        // APPROVAL_RECORD of its raw SHA, one BUILD_PROVENANCE of its pin, the INPUT_BUNDLE of every consumed input (R2-A: the two pinned profiles and every embedded data file, ConsumedInputs), all of
        // the validation set (no unit / game); the PREFLIGHT records (R2-B: the Family record and exactly one per unit, their content checked against the verifier's own preflight before the replay)
        // and no REGRESSION_PROOF (a Phase input). Certification re-checks the same against the session's own trust.
        string Shas(string role) => string.Join(",", snapshot.Values.Where(f => f.Role == role).Select(f => (f.Reference["unit_id"] is JNull && f.Reference["game_id"] is JNull ? "" : "unit:")
            + Text(f.Reference["raw_sha256"])).Order(StringComparer.Ordinal));
        (string Role, string Expected)[] required =
        [
            ("APPROVAL_RECORD", a.RawSha256), ("BUILD_PROVENANCE", Text(a.Ref("build_provenance")["raw_sha256"])),
            ("INPUT_BUNDLE", ConsumedInputs.Expected(Text(a.Ref("environment_profile")["raw_sha256"]), Text(a.Ref("pzl_test_profile")["raw_sha256"]))), ("REGRESSION_PROOF", ""),
        ];
        EvidenceException Missing(string role, string expected, string got) => Fail(EvidenceCodes.ArchiveInvalid, "Archive.required_original", "/additional_evidence/artifacts", "archive",
            $"{role} 원본이 승인 기록·실제 입력 경로에서 도출한 필수 원본과 다릅니다(§21, 기대 [{expected}], 받은 값 [{got}]).", family);
        foreach (var (role, expected) in required)
            if (Shas(role) != expected)
                throw Missing(role, expected, Shas(role));
        var preflights = snapshot.Values.Where(f => f.Role == "PREFLIGHT").ToList();
        var perUnit = units.Select(u => preflights.Count(f => f.Reference["unit_id"] is JString { Value: var x } && x == u)).ToList();
        if (preflights.Count != units.Count + 1 || perUnit.Any(n => n != 1) || preflights.Count(f => f.Reference["unit_id"] is JNull) != 1 || preflights.Any(f => f.Reference["game_id"] is not JNull))
            throw Missing("PREFLIGHT", $"Family 1 + Unit별 1 = {units.Count + 1}", $"{preflights.Count}");
    }

    /// <summary>B1-T11-03 §21: exactly one snapshot file of this role, unit and run path (the replay input), else <c>archive_invalid</c>.</summary>
    private static ArchivedFile RunFile(IReadOnlyDictionary<string, ArchivedFile> snapshot, string role, string? unitId, string relativePath, string family)
    {
        var found = snapshot.Values.Where(f => f.Role == role && Text(f.Reference["relative_path"]) == relativePath).ToList();
        if (found.Count != 1 || (found[0].Reference["unit_id"] is JString { Value: var u } ? u : null) != unitId)
            throw Fail(EvidenceCodes.ArchiveInvalid, "Archive.run_file_missing", "/additional_evidence/artifacts", "archive",
                $"실행 파일 '{relativePath}'({role}{(unitId is null ? "" : $", {unitId}")})가 Archive 참조에 정확히 하나 있어야 합니다(§21, Replay 입력).", family);
        return found[0];
    }

    /// <summary>The attestation inside a manifest, parsed from the SAME snapshot bytes the replay read (never a second read of a path).</summary>
    private static OfficialAttestation Attestation(byte[] manifest)
    {
        using var m = JsonDocument.Parse(manifest);
        return OfficialAttestation.FromJson(m.RootElement.GetProperty("official").GetProperty("attestation")).Value;
    }

    public static void Compare(JObject submitted, JObject expected, string family)
    {
        if (JDiff.First(EvidenceHash.M(submitted), EvidenceHash.M(expected)) is not { } p)
            return;
        var code = p switch
        {
            _ when p.Contains("/inputs", StringComparison.Ordinal) => EvidenceCodes.ApprovalMismatch,
            _ when p.Contains("/technical", StringComparison.Ordinal) => EvidenceCodes.ReplayMismatch,
            _ when p.Contains("/evaluation", StringComparison.Ordinal) || p.StartsWith("/aggregate_evaluation", StringComparison.Ordinal) => EvidenceCodes.EvaluationMismatch,
            _ => EvidenceCodes.ReferenceInvalid,
        };
        throw Fail(code, "Evidence.document_differs", p, "reprojection", $"제출 문서가 재실행으로 다시 만든 문서와 '{p}'에서 다릅니다.", family);
    }

    /// <summary>§2.4 S3-S6 for one document against the approval record.</summary>
    public static void BindApproval(JObject d, ApprovalRecordV1 a, string family)
    {
        var contracts = Obj(Obj(d["schema"])["contracts"]);
        if (EvidenceShapes.ContractKeys.Any(k => Text(contracts[k]) != Text(Obj(a.Record["contracts"])[k])))
            throw Fail(EvidenceCodes.ContractNotApproved, "Evidence.contracts_not_approved", "/schema/contracts", "approval", "문서 계약 조합이 승인 기록과 다릅니다(S3).", family);
        var ap = Obj(d["approvals"]);
        if ((Text(ap["approval_record_id"]), Text(ap["approval_record_sha256"]), Text(ap["freeze_id"])) != (a.Id, a.RawSha256, a.FreezeId))
            throw Fail(EvidenceCodes.ApprovalMismatch, "Evidence.approvals_mismatch", "/approvals", "approval", "approvals가 확정 승인 참조와 다릅니다(S4).", family);
        var ex = Obj(d["execution"]);
        if ((Text(ex["validation_set_id"]), Text(ex["run_id"]), Text(ex["family"]), Text(ex["git_commit_sha"]), Text(ex["build_provenance_id"]))
            != (a.ValidationSetId, Text(a.Family(family)["run_id"]), family, a.GitCommitSha, Text(a.Ref("build_provenance")["id"])))
            throw Fail(EvidenceCodes.ApprovalMismatch, "Evidence.execution_mismatch", "/execution", "approval", "execution이 승인 기록과 다릅니다(S5, A01·A02).", family);
    }

    /// <summary>The units against the record's UnitApprovals: seed path / SHA pin, cohort, plan size, and the family's combination clause (A03, A04).</summary>
    private static void BindUnits(JObject doc, ApprovalRecordV1 a, string family, EvidenceKind kind)
    {
        var approved = a.Units(family);
        var units = Items(doc["units"]).Select(Obj).ToList();
        for (var i = 0; i < units.Count; i++)
        {
            var att = Obj(Obj(units[i]["inputs"])["attestation"]);
            var u = approved[i];
            if ((Text(att["seed_manifest_path"]), Text(att["seed_manifest_sha256"]), Text(att["cohort_id"]), Long(Obj(units[i]["inputs"])["planned_games"]))
                != (Text(u["seed_manifest_path"]), Text(u["seed_manifest_sha256"]), Text(u["cohort_id"]), Long(u["planned_games"])))
                throw Fail(EvidenceCodes.ApprovalMismatch, "Evidence.unit_approval_mismatch", $"/units/{i}/inputs", "approval", $"{Text(u["unit_id"])}: Seed Pin·Cohort·계획 크기가 승인 기록과 다릅니다(A03).", family);
        }

        if (kind == EvidenceKind.Family)
        {
            var clause = Text(Obj(a.Record["combination_policy_clauses"])[family == "fc3-cal" ? "calibration" : "integration"]);
            if (Text(Obj(doc["plan"])["combination_policy_clause_id"]) != clause)
                throw Fail(EvidenceCodes.ApprovalMismatch, "Evidence.clause_mismatch", "/plan/combination_policy_clause_id", "approval", "결합 정책 조항 ID가 승인 기록과 다릅니다.", family);
        }
    }

    private static void BindBundleHeader(JObject header, JObject unitDoc)
    {
        var ex = Obj(unitDoc["execution"]);
        if (!Jcs.Bytes(header["contracts"]).AsSpan().SequenceEqual(Jcs.Bytes(Obj(unitDoc["schema"])["contracts"])) || Text(header["family"]) != Text(ex["family"])
            || Text(header["run_id"]) != Text(ex["run_id"]) || Text(header["validation_set_id"]) != Text(ex["validation_set_id"]) || Text(header["unit_id"]) != Text(Obj(unitDoc["plan"])["unit_id"])
            || Text(header["unit_evidence_id"]) != Text(ex["evidence_id"]))
            throw Fail(EvidenceCodes.ReferenceInvalid, "Evidence.bundle_header", "/line/1", "bundle", "GAME_EVENTS Header가 Unit PZL 문서와 다릅니다(§10.3).");
    }

    private static void VerifyPzl6(JObject pzl6, JObject integration, IReadOnlyList<RunResult> reruns, EventJsonCodec? codec, string family, IReadOnlyDictionary<string, ArchivedFile> snapshot)
    {
        codec ??= EventJsonCodec.Infrastructure;                                                     // OWNER decision 2: the real codec, bound in the verifier itself
        var samples = Enumerable.Range(0, 20).Select(i =>
        {
            var run = reruns[i < 10 ? 0 : 1];
            var spec = run.Specs[i % 10];
            var game = (JObject)Items(Obj(Obj(Items(integration["units"])[i < 10 ? 0 : 1])["technical"])["games"])[i % 10];
            return new Pzl6Sample(i < 10 ? "automated" : "non-automated", run.Config, spec, game);
        }).ToList();
        // §21 PZL6 "+ 20경기 JSON bundle": one GAME_RESULTS reference per fixed sample, its bytes the existing v2 Writer's output for that game (B1-T11-03)
        // OWNER decision 3 §7: role PZL6, game_id set, id pzl6-json-<mode>-<index:D2>; unit, game and index those of the approved first-10-per-mode plan (the seed through the sample, checked below)
        var json = Items(Obj(pzl6["additional_evidence"])["artifacts"]).Select(Obj).Where(x => Text(x["role"]) == "PZL6" && x["game_id"] is JString).ToList();
        var bundle = Pzl6Verifier.Bundle(samples, codec);
        if (json.Count != bundle.Count || json.Zip(bundle).Select((p, i) => (p, i)).Any(x => Text(x.p.First["unit_id"]) != x.p.Second.UnitId || Text(x.p.First["game_id"]) != x.p.Second.GameId
                || Text(x.p.First["artifact_id"]) != $"pzl6-json-{x.p.Second.UnitId}-{samples[x.i].Spec.GameIndex:D2}"))
            throw Fail(EvidenceCodes.PzlIncomplete, "Evidence.pzl6_bundle_missing", "/additional_evidence/artifacts", "pzl6", "#6 문서는 고정 20경기 각각의 v2 JSON(PZL6)을 표본 순서대로 참조해야 합니다.", family);
        foreach (var (x, b) in json.Zip(bundle))
            if (!snapshot[Text(x["artifact_id"])].Bytes.AsSpan().SequenceEqual(b.Json))
                throw Fail(EvidenceCodes.PzlMismatch, "Evidence.pzl6_bundle_differs", $"/additional_evidence/artifacts/{StrictJson.Token(Text(x["artifact_id"]))}", "pzl6",
                    $"{b.GameId}: 보존된 #6 JSON이 기존 v2 Writer 출력과 다릅니다.", family);
        var expected = Pzl6Verifier.Document(integration, samples, codec, Items(Obj(pzl6["additional_evidence"])["artifacts"]).Select(Obj).ToList());
        if (JDiff.First(EvidenceHash.M(pzl6), EvidenceHash.M(expected)) is { } p)
            throw Fail(EvidenceCodes.PzlMismatch, "Evidence.pzl6_differs", p, "pzl6", $"#6 문서가 검증기 재계산과 '{p}'에서 다릅니다(A10·A10-a).", family);
    }
}

internal static class JEditMembers
{
    public static JObject With(JObject o, string name, JNode value) => new([.. o.Members.Where(m => m.Key != name), new KeyValuePair<string, JNode>(name, value)]);
}
