using System.Text.Json;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier;

/// <summary>
/// B1 T11 R2-A (§21 INPUT_BUNDLE "소비 입력 파일별"): the inputs a verified run consumed, derived from the trusted record and the run's own loading path, never from the submitted list — the two profiles the
/// record pins (environment, PZL test) and every embedded data file the harness reads (<see cref="HarnessData.Inputs"/>, by the exact bytes of the resource stream).
/// </summary>
internal static class ConsumedInputs
{
    /// <summary>The required INPUT_BUNDLE raw SHAs, ordinal-sorted and comma-joined (a multiset: a duplicate, a missing, a substituted or an extra input all differ).</summary>
    public static string Expected(string environmentProfileSha, string testProfileSha) => string.Join(",",
        new[] { environmentProfileSha, testProfileSha }.Concat(HarnessData.Inputs.Select(i => Archive.Sha(HarnessData.ResourceBytes(i.Name)))).Order(StringComparer.Ordinal));

    /// <summary>
    /// Each embedded input is byte-identical to the repository file it is built from (below the verification root): the bytes the run read are the original's. Else <c>approval_mismatch</c>
    /// (<c>Input.embedded_differs</c>). <paramref name="resource"/> is a test seam for the resource bytes.
    /// </summary>
    public static void CheckSources(string repositoryRoot, string family, Func<string, byte[]>? resource = null)
    {
        foreach (var (name, path) in HarnessData.Inputs)
        {
            var file = Path.Combine([repositoryRoot, .. path.Split('/')]);
            if (!File.Exists(file) || !File.ReadAllBytes(file).AsSpan().SequenceEqual((resource ?? HarnessData.ResourceBytes)(name)))
                throw EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, "Input.embedded_differs", "/additional_evidence/artifacts", "inputs",
                    $"내장 입력 '{name}'의 바이트가 원본 '{path}'와 다르거나 원본이 없습니다(소비 바이트 ≠ 보존 원본, §21 INPUT_BUNDLE).");
        }
    }
}

/// <summary>
/// B1 T11 R2-B (§21 PREFLIGHT "Family·Unit별 필요 기록, 첫 경기 전"): the run's preflight records — the Family / Dominance document and one single-manifest result per unit, written by the official execution
/// before its first unit (<c>OfficialPins.WritePreflight</c>) — equal to the EXPECTED records the verifier builds itself. Neither an attestation alone nor the DIAGNOSTICS index replaces them.
/// <para>
/// R2-B value binding: the expected records come from the verifier's OWN run of the same preflight (trusted approval inputs, Clean Git at the approved SHA, a fresh output name, no game, no write), made
/// READY only where the verifier's approvals instance is not ready by construction (ExecutionPathReady = false): <c>execution_path</c> and <c>unit_preflight</c> / <c>cell_preflight</c> get the READY
/// messages of their single definitions, and each unit gets the attestation the verifier builds from the approved inputs (<see cref="OfficialPreflight.BuildAttestation"/>, also required to be the
/// unit's archived manifest attestation). They are serialized by the existing format functions and compared with the submitted JSON as a whole tree — every member name in order, every value's type
/// and value, nested objects and arrays, nothing missing, added or duplicated. Only the machine-dependent value is not compared: the absolute output path in the <c>output_directory</c> check's
/// message (still a string; that check's id, status and code are compared). Nothing submitted is used as an expected value.
/// </para>
/// </summary>
internal static class PreflightRecords
{
    /// <summary>The one check whose message is a path of the producing machine (its output directory).</summary>
    private const string MachineDependentCheck = "output_directory";

    public static void Verify(ApprovalRecordV1 a, string family, OfficialFamilyPlan? plan, string runId, string repositoryRoot, ArchivedFile familyRecord, IReadOnlyList<ArchivedFile> unitRecords,
        IReadOnlyList<OfficialAttestation> attestations, IReadOnlyList<string> unitIds)
    {
        var approvals = ApprovalFormats.ToOfficialApprovals(a, executionPathReady: false);
        var git = GitIntegrityPolicy.Evaluate([], a.GitCommitSha);
        var request = new DominanceOfficialRequest(repositoryRoot, runId, $"{OfficialOutput.RootRelative}/verify-{Guid.NewGuid():N}");
        PreflightReport Unit(PreflightReport rerun, string seedPath, string unit) => ReadyUnit(rerun, seedPath, approvals, runId, repositoryRoot, family, unit);
        object document;
        IReadOnlyList<(string Id, PreflightReport Report)> units;
        if (plan is not null)
        {
            var p = FamilyPreflight.Run(plan, request, approvals, git);
            var ready = p.Units.Select(u => u with { Report = Unit(u.Report, u.Pinned.SeedManifestPath, u.Pinned.Unit.Id) }).ToList();
            var expected = p with
            {
                Checks = Ready(p.Checks, family, null, ("execution_path", OfficialPreflight.ExecutionPathReadyMessage), ("unit_preflight", FamilyPreflight.AllUnitsReady(plan.Family, ready.Count))),
                Units = ready,
            };
            (document, units) = (FamilyOfficial.PreflightDocument(expected, runId), ready.Select(u => (u.Pinned.Unit.Id, u.Report)).ToList());
        }
        else
        {
            var p = DominancePreflight.Run(request, approvals, git);
            var ready = p.Cells.Select(c => c with { Report = Unit(c.Report, c.Pinned.SeedManifestPath, c.Pinned.Cell.Id) }).ToList();
            var expected = p with
            {
                Checks = Ready(p.Checks, family, null, ("execution_path", OfficialPreflight.ExecutionPathReadyMessage), ("cell_preflight", DominancePreflight.AllCellsReady)),
                Cells = ready,
            };
            (document, units) = (DominanceOfficial.PreflightDocument(expected, runId), ready.Select(c => (c.Pinned.Cell.Id, c.Report)).ToList());
        }

        if (!units.Select(u => u.Id).SequenceEqual(unitIds, StringComparer.Ordinal))
            throw Differs(family, null, "", "검증기 Preflight 재실행의 Unit 목록이 승인 계획과 다릅니다.");
        Compare(familyRecord, document, family, null);
        for (var i = 0; i < units.Count; i++)
        {
            if (units[i].Report.Attestation!.Hash() != attestations[i].Hash())
                throw Differs(family, unitIds[i], "/attestation_hash", $"{unitIds[i]}: 검증기가 승인 입력으로 만든 Attestation이 보존 UNIT_MANIFEST의 Attestation과 다릅니다.");
            Compare(unitRecords[i], OfficialOutput.Result(units[i].Report), family, unitIds[i]);
        }
    }

    /// <summary>The re-run's checks with the listed ones READY (their single-definition message); every other check must already have passed in the re-run (else the approved inputs do not pass).</summary>
    private static IReadOnlyList<PreflightCheck> Ready(IReadOnlyList<PreflightCheck> rerun, string family, string? unit, params (string Id, string Message)[] ready)
    {
        var checks = rerun.Select(c => ready.FirstOrDefault(r => r.Id == c.Id) is { Id: not null } r ? new PreflightCheck(c.Id, CheckStatus.Pass, null, r.Message) : c).ToList();
        if (checks.FirstOrDefault(c => c.Status != CheckStatus.Pass) is { } failed)
            throw Differs(family, unit, "", $"{unit ?? family}: 검증기 Preflight 재실행의 '{failed.Id}'가 승인 입력으로 통과하지 않습니다({failed.Code}).");
        return checks;
    }

    /// <summary>A unit's expected READY report: its re-run checks made ready (execution path, the attestation the verifier builds from the approved inputs) with that attestation.</summary>
    private static PreflightReport ReadyUnit(PreflightReport rerun, string seedPath, OfficialApprovals approvals, string runId, string root, string family, string unit)
    {
        if (rerun.Manifest is not { } m || rerun.Rules is null || rerun.Tuning is null || CohortRegistry.Resolve(m.CohortId) is not { IsSuccess: true } cohort
            || PolicyRegistry.Resolve(m.PolicyId) is not { IsSuccess: true } policy)
            throw Differs(family, unit, "", $"{unit}: 검증기 Preflight 재실행에서 승인 입력을 읽지 못했습니다.");
        var built = OfficialPreflight.BuildAttestation(new OfficialRequest(root, seedPath, runId, null), approvals, m, cohort.Value, rerun.Rules, rerun.Tuning, policy.Value);
        if (!built.IsSuccess)
            throw Differs(family, unit, "", $"{unit}: 검증기가 Attestation을 만들 수 없습니다({built.Error!.Value.Code}).");
        return rerun with
        {
            Checks = Ready(rerun.Checks, family, unit, ("execution_path", OfficialPreflight.ExecutionPathReadyMessage), ("attestation", built.Value.Hash())), Attestation = built.Value,
        };
    }

    private static EvidenceException Differs(string family, string? unit, string pointer, string message) =>
        new(new EvidenceError(EvidenceCodes.ApprovalMismatch, "Preflight.record_differs", pointer, "preflight", message, family, unit));

    /// <summary>The submitted record against the expected one, as whole JSON trees; the first differing JSON Pointer is the error's.</summary>
    private static void Compare(ArchivedFile record, object expected, string family, string? unit)
    {
        JsonElement got;
        try
        {
            using var d = JsonDocument.Parse(record.Bytes);
            got = d.RootElement.Clone();
        }
        catch (JsonException e)
        {
            throw Differs(family, unit, "", $"'{record.ArtifactId}'를 Preflight 문서로 읽을 수 없습니다: {e.Message}");
        }

        var want = JsonSerializer.SerializeToElement(expected, RunReport.Json);
        var checks = want.TryGetProperty("checks", out var top) ? ("/checks", top) : ("/preflight/checks", want.GetProperty("preflight").GetProperty("checks"));
        var machineDependent = checks.Item2.EnumerateArray().Select((c, i) => (c, i)).Where(x => x.c.GetProperty("id").GetString() == MachineDependentCheck)
            .Select(x => $"{checks.Item1}/{x.i}/message").ToHashSet(StringComparer.Ordinal);
        if (FirstDifference(got, want, "", machineDependent) is { } pointer)
            throw Differs(family, unit, pointer, $"{unit ?? family}: Preflight 기록이 검증기가 독립 재실행으로 만든 기대 기록과 '{(pointer.Length == 0 ? "/" : pointer)}'에서 다릅니다(이름·순서·타입·값).");
    }

    /// <summary>The first JSON Pointer where <paramref name="got"/> differs from <paramref name="want"/> (member names in order — so nothing missing, added or duplicated —, kinds, values), or null.</summary>
    private static string? FirstDifference(JsonElement got, JsonElement want, string path, IReadOnlySet<string> machineDependent)
    {
        if (machineDependent.Contains(path))
            return got.ValueKind == JsonValueKind.String ? null : path;
        if (got.ValueKind != want.ValueKind)
            return path;
        switch (want.ValueKind)
        {
            case JsonValueKind.Object:
                var g = got.EnumerateObject().ToList();
                var w = want.EnumerateObject().ToList();
                if (!g.Select(p => p.Name).SequenceEqual(w.Select(p => p.Name), StringComparer.Ordinal))
                    return path;
                for (var i = 0; i < w.Count; i++)
                    if (FirstDifference(g[i].Value, w[i].Value, $"{path}/{w[i].Name}", machineDependent) is { } d)
                        return d;
                return null;
            case JsonValueKind.Array:
                if (got.GetArrayLength() != want.GetArrayLength())
                    return path;
                for (var i = 0; i < want.GetArrayLength(); i++)
                    if (FirstDifference(got[i], want[i], $"{path}/{i}", machineDependent) is { } d)
                        return d;
                return null;
            case JsonValueKind.String:
                return got.GetString() == want.GetString() ? null : path;
            default:
                return got.GetRawText() == want.GetRawText() ? null : path;
        }
    }
}
