namespace StarPlayer.SimHarness.Evidence;

/// <summary>One unit's observed run for the producer: the unit id, its run (same config / plan / result hash as the official run) and the per-game observations.</summary>
public sealed record ObservedUnit(string UnitId, RunResult Run, IReadOnlyList<ObservedGame> Games);

/// <summary>
/// The producer's write path of the official evidence (T03 / T04b "쓰기 지점", §21 작성 순서): per unit its GAME_EVENTS bundle, then its Unit PZL document; the caller's #6 document; the approved seed files;
/// then the Family / Dominance document referencing all of them. Every file is new (never overwritten), flushed and read back (<see cref="Archive.WriteNew"/>). The DRAFT index of the run is not touched.
/// Official execution itself is not connected (Official Execution NOT_READY); this is used by the TEST_ONLY worlds and the Golden.
/// </summary>
public static class EvidenceWriter
{
    /// <summary>Observes a finished unit by an identical re-run (deterministic: same config and plan) and refuses a different result hash.</summary>
    public static ObservedUnit Observe(string unitId, RunResult official, Func<RunConfig, IReadOnlyList<GameSpec>, Func<GameSpec, GameRunResult>, RunResult>? runner = null)
    {
        var observers = new Dictionary<int, PzlEventObserver>();
        GameRunResult Play(GameSpec s) => GameExecutor.Play(official.Config, s, default, null, observers[s.GameIndex] = new PzlEventObserver());
        var run = runner is not null ? runner(official.Config, official.Specs, Play)
            : official.StopAware ? SimRunner.RunStopAware(official.Config, official.Plan!, null, default, null, Play) : SimRunner.RunSpecs(official.Config, official.Specs, null, default, Play);
        if (run.RunResultHash != official.RunResultHash || run.ConfigHash != official.ConfigHash)
            throw EvidenceCodes.Fail(EvidenceCodes.ReplayMismatch, "Evidence.observation_rerun_differs", "", "producer", $"{unitId}: 관찰용 재실행 결과 Hash가 공식 실행과 다릅니다.");
        return new ObservedUnit(unitId, run, run.Games.Select(g => new ObservedGame(g, observers[g.Game.GameIndex])).ToList());
    }

    /// <summary>Writes every unit's GAME_EVENTS and GAME_RESULTS bundles and its Unit PZL document (referencing both) and returns their UNIT_PZL references in unit order (with the documents).</summary>
    public static IReadOnlyList<(JObject Reference, JObject Document)> WriteUnits(string archiveRoot, EvidenceContext ctx, string family, string gitSha, IReadOnlyList<ObservedUnit> units)
    {
        var written = new List<(JObject, JObject)>();
        foreach (var u in units)
        {
            var dir = $"units/{u.UnitId.ToLowerInvariant()}";
            var bundle = Archive.WriteNew(archiveRoot, $"{dir}/game-events.jsonl", UnitPzlEvidence.Bundle(ctx, family, u.Run.Config.RunId, u.UnitId, u.Games),
                $"game-events-{u.UnitId.ToLowerInvariant()}", "GAME_EVENTS", u.UnitId);
            var results = Archive.WriteNew(archiveRoot, $"{dir}/game-results.jsonl", GameResultsEvidence.Bundle(ctx, family, u.UnitId, u.Run),        // OWNER decision 3 §5
                $"game-results-{u.UnitId.ToLowerInvariant()}", "GAME_RESULTS", u.UnitId);
            var doc = UnitPzlEvidence.Document(ctx, family, u.UnitId, gitSha, u.Run, u.Games, [bundle, results]);
            var reference = Archive.WriteNew(archiveRoot, $"{dir}/unit-pzl.json", Jcs.Bytes(doc), $"unit-pzl-{u.UnitId.ToLowerInvariant()}", "UNIT_PZL", u.UnitId);
            written.Add((reference, doc));
        }

        return written;
    }

    /// <summary>Copies each approved seed file into the archive as the unit's SEED_MANIFEST (its raw SHA is the attestation pin, §21).</summary>
    public static IReadOnlyList<JObject> WriteSeeds(string archiveRoot, string repositoryRoot, IEnumerable<(string UnitId, string SeedManifestPath)> units) =>
        units.Select(u => Archive.WriteNew(archiveRoot, $"inputs/{u.SeedManifestPath}", File.ReadAllBytes(Path.Combine([repositoryRoot, .. u.SeedManifestPath.Split('/')])),
            $"seed-{u.UnitId.ToLowerInvariant()}", "SEED_MANIFEST", u.UnitId)).ToList();

    /// <summary>The archive prefix of a run's own files: <c>run/&lt;run_id&gt;/&lt;path in the result directory&gt;</c> (§21 UNIT_MANIFEST per unit and the run index; file names are B-4's).</summary>
    public static string RunPath(string runId, string pathInResultDirectory) => $"run/{runId}/{pathInResultDirectory}";

    /// <summary>
    /// B1-T11-03: copies the run's DRAFT index (DIAGNOSTICS) and every unit manifest (UNIT_MANIFEST, which carries the attestation) into the archive byte for byte, so the verifier replays exactly the
    /// archived, verified bytes and never reads the result directory itself. B1 T11 R2-B: with the run's preflight records (PREFLIGHT: the Family record and one per unit).
    /// </summary>
    public static IReadOnlyList<JObject> WriteRun(string archiveRoot, string resultDirectory, string runId, string resultFile, IEnumerable<(string UnitId, string UnitDirectory)> units)
    {
        byte[] Read(string rel) => File.ReadAllBytes(Path.Combine([resultDirectory, .. rel.Split('/')]));
        var list = new List<JObject>
        {
            Archive.WriteNew(archiveRoot, RunPath(runId, resultFile), Read(resultFile), $"run-index-{runId}", "DIAGNOSTICS"),
            Archive.WriteNew(archiveRoot, RunPath(runId, OfficialOutput.PreflightFile), Read(OfficialOutput.PreflightFile), $"preflight-{runId}", "PREFLIGHT"),   // B1 T11 R2-B: the Family record
        };
        foreach (var (unitId, unitDirectory) in units)
        {
            list.Add(Archive.WriteNew(archiveRoot, RunPath(runId, $"{unitDirectory}/manifest.json"), Read($"{unitDirectory}/manifest.json"), $"manifest-{unitId.ToLowerInvariant()}",
                "UNIT_MANIFEST", unitId));
            var preflight = $"{OfficialOutput.PreflightDirectory}/{OfficialOutput.UnitPreflightName(unitId)}";                                    // and the unit's own record
            list.Add(Archive.WriteNew(archiveRoot, RunPath(runId, preflight), Read(preflight), $"preflight-{unitId.ToLowerInvariant()}", "PREFLIGHT", unitId));
        }

        return list;
    }

    /// <summary>B1 T11 R3: the artifact id is the archive path without its extension (unique in the archive, since a path is never written twice), never the role — two families' documents never share one.</summary>
    public static JObject WriteDocument(string archiveRoot, JObject document, string role, string name) =>
        Archive.WriteNew(archiveRoot, name, Jcs.Bytes(document), Path.ChangeExtension(name, null).Replace('/', '.'), role);
}
