using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.SimHarness;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness.Tests;

internal static class H
{
    public static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    public static T Ok<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, $"{result.Error?.Code}: {result.Error?.Message}");
        return result.Value;
    }

    public static readonly TuningProfile Tuning = Ok(HarnessData.LoadTuningV4());

    public static ParkFactorSnapshot Park(string profile = "pf-sim-neutral", int homeRuns = 1000) =>
        Ok(ParkFactorProfile.Create(Ok(ParkFactorProfileId.Create(profile)), Ok(StadiumId.Create("stadium-sim-neutral")), 1, new DateOnly(2030, 1, 1), homeRuns, 1000, 1000,
            Ok(ParkFactorBounds.Create(1, ParkFactorBounds.AbsoluteMaxPermille)))).ToSnapshot();

    public static GameRuleSnapshot Rules(bool automated = false) => Ok(HarnessData.LoadRules(automated ? HarnessData.AutomatedProfile : HarnessData.NonAutomatedProfile));

    /// <summary>A one-inning rule set with a tie at the limit (fictional): ties are common, so a legal tie can be found by a plain seed scan.</summary>
    public static GameRuleSnapshot OneInningTieRules() =>
        Ok(GameRuleSnapshot.Create("test-one-inning-tie", "t1", GameRulesStatus.FictionalTest, "harness test only", ["test"], 3, 4, 3, 1, 1, true, true, true, 15, 3, 3, 9,
            calledPitchMode: CalledPitchMode.NonAutomated, zoneModel: StarPlayer.Domain.Baseball.PitchZone.ZoneModelVersion.Pzl1));

    public static RunConfig Config(string runId = "t-run", CohortInputs? cohort = null, GameRuleSnapshot? rules = null, int budget = 3000, IGamePolicy? policy = null, string? advancement = null) =>
        Ok(RunConfig.Create(runId, "smoke", cohort ?? NeutralCohort.Create(), rules ?? Rules(), Tuning, Park(), advancement, budget, policy));

    public static GameRunResult Play(RunConfig config, ulong seed, int index = 1) => GameExecutor.Play(config, config.Spec(index, seed));

    /// <summary>The neutral cohort with some players' sheets replaced (the replacement is built from the original so only what the test changes differs).</summary>
    public static CohortInputs Variant(string id, Func<PlayerSheet, PlayerSheet?> change, TeamSetup? away = null, CohortKind kind = CohortKind.VirtualLeague)
    {
        var baseline = NeutralCohort.Create();
        var sheets = baseline.Sheets.Values.Select(s => change(s) ?? s).ToList();
        return Ok(CohortInputs.Create(id, kind, "test variant", away ?? baseline.Away, baseline.Home, sheets));
    }

    public static PlayerSheet WithRatings(PlayerSheet s, HitterRatings? hitter, PitcherRatings? pitcher, CatcherRatings? catcher) =>
        Ok(PlayerSheet.Create(s.Player, Ok(Ratings.Create(hitter, pitcher, catcher)), s.State, s.Proficiency, s.Status));

    /// <summary>The same player as a plain hitter: no pitching ratings and a fielder's preferred position (a sheet that prefers the mound must have pitching ratings).</summary>
    public static PlayerSheet AsHitterOnly(PlayerSheet s) =>
        Ok(PlayerSheet.Create(s.Player, Ok(Ratings.Create(new HitterRatings(Skill(50), Skill(50), Skill(50), Skill(50), Skill(50), Skill(50)), null, null)), s.State,
            Ok(ProficiencyProfile.Create(FieldPosition.CenterField, Array.Empty<(FieldPosition, SkillValue)>(), [])), s.Status));

    public static SkillValue Skill(int v) => Ok(SkillValue.Create(v));
}
