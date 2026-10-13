using System.Collections.Immutable;
using System.Reflection;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C3-B (B-03): the structural preview comparison, member by member. Every state member of the five compared types has one entry in <see cref="Tampers"/> that changes ONLY that
/// member of a real scoring single's step, and the comparer must see it. A test pins that the entries are exactly the members the types have, so a new domain field fails here (and in the
/// Simulation inventory test) until the comparer and this table are reviewed. The step is real: it comes from the real builders; the comparison is against a copy tampered with <c>with</c>.
/// </summary>
public class PreviewComparisonTests
{
    private static readonly PlayerId Zz = PlayerId.Create("zz-other").Value;

    private static readonly Lazy<GameStep> Step = new(() =>
    {
        var d = Situation(4, 0, true);
        var found = FindBaseInPlay(d.State, d.LastIssued, c => c.Plan.OfficialResult == InPlayResult.Single && c.UnconfirmedStep.NewPlays.Count == 1 && c.UnconfirmedStep.NewPlays[0].Runs.Count == 1);
        return found.Candidate.UnconfirmedStep;
    });

    private static T Next<T>(T value) where T : struct, Enum
    {
        var all = Enum.GetValues<T>();
        return all[(Array.IndexOf(all, value) + 1) % all.Length];
    }

    private static GameStep S(GameStep s, Func<GameState, GameState> edit) => s with { State = edit(s.State) };

    private static GameStep Away(GameStep s, Func<TeamGameState, TeamGameState> edit) => S(s, st => st with { Away = edit(st.Away) });

    private static GameStep Play(GameStep s, Func<ScoredPlay, ScoredPlay> edit) =>
        s with { State = s.State with { Plays = s.State.Plays.SetItem(s.State.Plays.Count - 1, edit(s.State.Plays[^1])) }, NewPlays = s.NewPlays.SetItem(0, edit(s.NewPlays[0])) };

    private static readonly Dictionary<string, Func<GameStep, GameStep>> Tampers = new()
    {
        // GameState
        ["GameState.GameId"] = s => S(s, st => st with { GameId = "other" }),
        ["GameState.Rules"] = s => S(s, st => st with { Rules = ReferenceEquals(st.Rules, Automated) ? NonAutomated : Automated }),
        ["GameState.Inning"] = s => S(s, st => st with { Inning = st.Inning + 1 }),
        ["GameState.Half"] = s => S(s, st => st with { Half = Next(st.Half) }),
        ["GameState.Outs"] = s => S(s, st => st with { Outs = st.Outs + 1 }),
        ["GameState.Count"] = s => S(s, st => st with { Count = new PitchCountState(2, 2) }),
        ["GameState.Bases"] = s => S(s, st => st with { Bases = st.Bases with { Third = st.Bases.First } }),
        ["GameState.AwayRuns"] = s => S(s, st => st with { AwayRuns = st.AwayRuns + 1 }),
        ["GameState.HomeRuns"] = s => S(s, st => st with { HomeRuns = st.HomeRuns + 1 }),
        ["GameState.AwayLine"] = s => S(s, st => st with { AwayLine = st.AwayLine.Add(0) }),
        ["GameState.HomeLine"] = s => S(s, st => st with { HomeLine = st.HomeLine.Add(0) }),
        ["GameState.Stage"] = s => S(s, st => st with { Stage = Next(st.Stage) }),
        ["GameState.Approach"] = s => S(s, st => st with { Approach = st.Approach is null ? ApproachKind.Normal : null }),
        ["GameState.WalkChargeOverride"] = s => S(s, st => st with { WalkChargeOverride = Zz }),
        ["GameState.Away"] = s => Away(s, t => t with { Pitcher = Zz }),
        ["GameState.Home"] = s => S(s, st => st with { Home = st.Home with { Pitcher = Zz } }),
        ["GameState.Plays"] = s => S(s, st => st with { Plays = st.Plays.RemoveAt(st.Plays.Count - 1) }),
        ["GameState.Stats"] = s => S(s, st => st with { Stats = st.Stats.RemoveAt(0) }),
        ["GameState.Substitutions"] = s => S(s, st => st with { Substitutions = st.Substitutions.Add(new SubstitutionRecord("x", 1, InningHalf.Top, TeamSide.Away, SubstitutionKind.PinchHit, Zz, Zz, 1, FieldPosition.Catcher)) }),
        ["GameState.AppliedEvents"] = s => S(s, st => st with { AppliedEvents = st.AppliedEvents.RemoveAt(st.AppliedEvents.Count - 1) }),
        ["GameState.NextRunnerKey"] = s => S(s, st => st with { NextRunnerKey = st.NextRunnerKey + 1 }),
        ["GameState.Status"] = s => S(s, st => st with { Status = Next(st.Status) }),
        ["GameState.Outcome"] = s => S(s, st => st with { Outcome = st.Outcome is null ? GameOutcome.Tie : null }),
        ["GameState.Final"] = s => S(s, st => st with { Final = new FinalizedGame(st.GameId, GameOutcome.Tie, 0, 0, "x", "y", null!) }),

        // TeamGameState (the away team stands for both: the comparer treats them with one routine)
        ["TeamGameState.Lineup"] = s => Away(s, t => t with { Lineup = t.Lineup.Reverse() }),
        ["TeamGameState.Pitcher"] = s => Away(s, t => t with { Pitcher = Zz }),
        ["TeamGameState.StartingPitcher"] = s => Away(s, t => t with { StartingPitcher = Zz }),
        ["TeamGameState.PitcherUsages"] = s => Away(s, t => t with { PitcherUsages = t.PitcherUsages.Add(new PitcherUsage(Zz, 9, 1, InningHalf.Top, 0, 0, 0, 0, 0)) }),
        ["TeamGameState.Bench"] = s => Away(s, t => t with { Bench = t.Bench.Reverse() }),
        ["TeamGameState.Removed"] = s => Away(s, t => t with { Removed = t.Removed.Add(Zz) }),
        ["TeamGameState.Appeared"] = s => Away(s, t => t with { Appeared = t.Appeared.Add(Zz) }),
        ["TeamGameState.EverInLineup"] = s => Away(s, t => t with { EverInLineup = t.EverInLineup.Reverse() }),
        ["TeamGameState.NextBatterIndex"] = s => Away(s, t => t with { NextBatterIndex = t.NextBatterIndex + 1 }),
        ["TeamGameState.DhActive"] = s => Away(s, t => t with { DhActive = !t.DhActive }),
        ["TeamGameState.StartingDh"] = s => Away(s, t => t with { StartingDh = t.StartingDh is null ? Zz : null }),

        // ScoredPlay (the last play, in both the state's list and the step's new plays)
        ["ScoredPlay.Id"] = s => Play(s, p => p with { Id = "x" }),
        ["ScoredPlay.EventId"] = s => Play(s, p => p with { EventId = "x" }),
        ["ScoredPlay.Sequence"] = s => Play(s, p => p with { Sequence = p.Sequence + 1 }),
        ["ScoredPlay.Inning"] = s => Play(s, p => p with { Inning = p.Inning + 1 }),
        ["ScoredPlay.Half"] = s => Play(s, p => p with { Half = Next(p.Half) }),
        ["ScoredPlay.OutsBefore"] = s => Play(s, p => p with { OutsBefore = p.OutsBefore + 1 }),
        ["ScoredPlay.OutsAfter"] = s => Play(s, p => p with { OutsAfter = p.OutsAfter + 1 }),
        ["ScoredPlay.Batting"] = s => Play(s, p => p with { Batting = Next(p.Batting) }),
        ["ScoredPlay.Kind"] = s => Play(s, p => p with { Kind = Next(p.Kind) }),
        ["ScoredPlay.Batter"] = s => Play(s, p => p with { Batter = Zz }),
        ["ScoredPlay.Pitcher"] = s => Play(s, p => p with { Pitcher = Zz }),
        ["ScoredPlay.BattingTeamPitcher"] = s => Play(s, p => p with { BattingTeamPitcher = Zz }),
        ["ScoredPlay.Catcher"] = s => Play(s, p => p with { Catcher = Zz }),
        ["ScoredPlay.Result"] = s => Play(s, p => p with { Result = Next(p.Result!.Value) }),
        ["ScoredPlay.Batted"] = s => Play(s, p => p with { Batted = Next(p.Batted) }),
        ["ScoredPlay.FinalCount"] = s => Play(s, p => p with { FinalCount = new PitchCountState(3, 2) }),
        ["ScoredPlay.Moves"] = s => Play(s, p => p with { Moves = p.Moves.RemoveAt(0) }),
        ["ScoredPlay.Credits"] = s => Play(s, p => p with { Credits = p.Credits.Add(new FielderCredit(FieldPosition.Catcher, Zz, CreditKind.Assist)) }),
        ["ScoredPlay.Errors"] = s => Play(s, p => p with { Errors = p.Errors.Add(FieldPosition.Catcher) }),
        ["ScoredPlay.Runs"] = s => Play(s, p => p with { Runs = p.Runs.RemoveAt(0) }),
        ["ScoredPlay.AwayRunsAfter"] = s => Play(s, p => p with { AwayRunsAfter = p.AwayRunsAfter + 1 }),
        ["ScoredPlay.HomeRunsAfter"] = s => Play(s, p => p with { HomeRunsAfter = p.HomeRunsAfter + 1 }),
        ["ScoredPlay.IsDoublePlay"] = s => Play(s, p => p with { IsDoublePlay = !p.IsDoublePlay }),

        // GameStep and GameStatDelta
        ["GameStep.State"] = s => S(s, st => st with { Outs = st.Outs + 1 }),
        ["GameStep.Delta"] = s => s with { Delta = s.Delta with { Entries = s.Delta.Entries.RemoveAt(0) } },
        ["GameStep.NewPlays"] = s => s with { NewPlays = s.NewPlays.Clear() },
        ["GameStep.Facts"] = s => s with { Facts = s.Facts.Clear() },
        ["GameStep.WasDuplicate"] = s => s with { WasDuplicate = !s.WasDuplicate },
        ["GameStatDelta.SourceEventId"] = s => s with { Delta = s.Delta with { SourceEventId = "other" } },
        ["GameStatDelta.Entries"] = s => s with { Delta = s.Delta with { Entries = s.Delta.Entries.RemoveAt(0) } },
    };

    private static IEnumerable<string> StateMembers(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.SetMethod is not null && p.Name != "EqualityContract").Select(p => $"{type.Name}.{p.Name}");

    public static IEnumerable<object[]> Names() => Tampers.Keys.Select(k => new object[] { k });

    [Fact]
    public void The_table_has_exactly_one_entry_for_every_state_member_of_the_five_compared_types()
    {
        var members = new[] { typeof(GameState), typeof(TeamGameState), typeof(ScoredPlay), typeof(GameStep), typeof(GameStatDelta) }.SelectMany(StateMembers).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(members, Tampers.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void An_identical_step_is_not_a_difference_and_a_separate_run_of_the_reducer_is_identical_to_the_first()
    {
        var step = Step.Value;

        Assert.Null(PreviewComparison.FirstDifference(step, step));
        Assert.Null(PreviewComparison.FirstDifference(step, step with { }));
        Assert.Null(PreviewComparison.FirstDifference(null, null));
        Assert.Equal("Step", PreviewComparison.FirstDifference(step, null));
        // two structurally equal steps whose collections are different instances (the comparer must not compare ImmutableList references)
        static ImmutableList<T> Copy<T>(ImmutableList<T> list) => ImmutableList.CreateRange(list.ToList());   // a new list instance with the same elements
        var rebuilt = step with { NewPlays = Copy(step.NewPlays), Facts = Copy(step.Facts), State = step.State with { Plays = Copy(step.State.Plays), Stats = Copy(step.State.Stats) } };
        Assert.NotSame(step.NewPlays, rebuilt.NewPlays);
        Assert.Null(PreviewComparison.FirstDifference(step, rebuilt));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Changing_only_one_member_is_a_difference_and_names_a_path(string member)
    {
        var step = Step.Value;

        var difference = PreviewComparison.FirstDifference(Tampers[member](step), step);

        Assert.NotNull(difference);
        Assert.StartsWith("Step.", difference);
        Assert.NotNull(PreviewComparison.FirstDifference(step, Tampers[member](step)));          // symmetric
    }

    [Fact]
    public void Order_matters_inside_a_compared_collection_and_the_first_difference_is_reported()
    {
        var step = Step.Value;
        var two = S(step, st => st with { Away = st.Away with { Bench = st.Away.Bench.Reverse() } });

        Assert.Equal("Step.State.Away.Bench[0]", PreviewComparison.FirstDifference(two, step));
        // two differences: the earlier member in comparison order wins
        var both = S(two, st => st with { Outs = st.Outs + 1 });
        Assert.Equal("Step.State.Outs", PreviewComparison.FirstDifference(both, step));
    }
}
