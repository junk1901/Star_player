using System.Collections.Immutable;
using System.Reflection;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.InPlayFeasibility;
using Root = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates.Root;
using States = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates;

namespace StarPlayer.Simulation.Tests.InPlay;

/// <summary>
/// Phase 3-C2B-2, Part A/B: the post-reducer verification branches (each driven with a deliberately wrong reducer verdict), the type-level separation of a pending advancement
/// from a finished candidate, and the structural promises that the new files draw nothing and persist nothing.
/// </summary>
public class InPlayVerifyAndIsolationTests
{
    private static (InPlayPlanFacts Facts, GameState State, InPlayEvent Event, Result<GameStep> Applied) Case(Root root, int diff, int occ, int outs, string batted, string result, string id = "p1")
    {
        var state = States.Make(root, diff, occ, outs);
        var facts = InPlayBasePlanner.Plan(state, batted, result).Value;
        var evt = InPlayBasePlanner.BuildEvent(facts, id, FeasibilityHarness.Location);
        return (facts, state, evt, GameReducer.Apply(state, evt));
    }

    private static string? Code(Error? e) => e?.Code;

    // ---------------------------------------------------------------- Verify (what the builder checks after the reducer)

    [Fact]
    public void A_correct_reducer_verdict_passes_verification()
    {
        var (facts, state, evt, applied) = Case(Root.GeneralTop, 0, 1, 0, "line_medium", "double");

        Assert.Null(InPlayPitchPlanning.Verify(facts, state, evt, applied));
    }

    [Fact]
    public void A_reducer_rejection_is_passed_through_with_its_own_error_and_no_candidate()
    {
        var (facts, state, evt, _) = Case(Root.GeneralTop, 0, 0, 0, "fly_hard", "out");
        var broken = GameReducer.Apply(state, evt with { Moves = ImmutableList.Create(new RunnerMove(1, 2)) });   // a move for a runner who is not there

        Assert.False(broken.IsSuccess);
        Assert.Equal(broken.Error!.Value.Code, Code(InPlayPitchPlanning.Verify(facts, state, evt, broken)));
    }

    [Fact]
    public void A_duplicate_is_never_a_new_candidate()
    {
        var (facts, state, evt, applied) = Case(Root.GeneralTop, 0, 0, 0, "fly_hard", "out");
        var again = GameReducer.Apply(applied.Value.State, evt);          // the very same event delivered again

        Assert.True(again.Value.WasDuplicate);
        Assert.Equal("InPlayCandidate.event_duplicate", Code(InPlayPitchPlanning.Verify(facts, applied.Value.State, evt, again)));
    }

    [Fact]
    public void A_receipt_that_is_not_ef2_or_belongs_to_another_id_is_invalid()
    {
        var (facts, state, evt, _) = Case(Root.GeneralTop, 0, 0, 0, "fly_hard", "out");
        var noLocation = evt with { Location = null };                                          // legacy ef1 event: a located candidate must be ef2
        var ef1 = GameReducer.Apply(state, noLocation);
        var other = GameReducer.Apply(state, evt with { Id = "p2" });                           // the verdict is for a different id

        Assert.Equal("ef1", ef1.Value.State.AppliedEvents.Last().FingerprintVersion);
        Assert.Equal("InPlayCandidate.receipt_invalid", Code(InPlayPitchPlanning.Verify(facts, state, evt, ef1)));
        Assert.Equal("InPlayCandidate.receipt_invalid", Code(InPlayPitchPlanning.Verify(facts, state, evt, other)));
    }

    [Fact]
    public void A_verdict_that_did_not_count_exactly_one_pitch_is_invalid()
    {
        var (facts, state, evt, applied) = Case(Root.GeneralTop, 0, 0, 0, "fly_hard", "out");
        var step = applied.Value;
        var noPitch = step with { State = step.State with { Away = state.Away, Home = state.Home } };      // the usage tables of the original: no pitch was counted

        Assert.Equal("InPlayCandidate.pitch_count_invalid", Code(InPlayPitchPlanning.Verify(facts, state, evt, Result<GameStep>.Success(noPitch))));
    }

    [Fact]
    public void A_verdict_that_disagrees_with_the_official_record_of_the_plan_is_invalid()
    {
        var (singleFacts, state, _, _) = Case(Root.GeneralTop, 0, 4, 0, "line_medium", "single");
        var doubleEvent = InPlayBasePlanner.BuildEvent(InPlayBasePlanner.Plan(state, "line_medium", "double").Value, "p1", FeasibilityHarness.Location);

        // the plan says a single (the runner on third scores and the batter stops on first); the reducer's verdict is for a double
        Assert.Equal("InPlayCandidate.official_record_invalid",
            Code(InPlayPitchPlanning.Verify(singleFacts, state, doubleEvent, GameReducer.Apply(state, doubleEvent))));

        // the plan expects the game to end by a walk-off; the verdict is for an ordinary inning
        var (walkFacts, _, _, _) = Case(Root.WalkOff, 0, 4, 0, "line_medium", "single");
        var ordinary = States.Make(Root.GeneralTop, 0, 4, 0);
        var ordinaryEvent = InPlayBasePlanner.BuildEvent(InPlayBasePlanner.Plan(ordinary, "line_medium", "single").Value, "p1", FeasibilityHarness.Location);
        Assert.True(walkFacts.WalkOffEndsGame);
        Assert.Equal("InPlayCandidate.official_record_invalid", Code(InPlayPitchPlanning.Verify(walkFacts, ordinary, ordinaryEvent, GameReducer.Apply(ordinary, ordinaryEvent))));
    }

    [Fact]
    public void A_verdict_that_lost_a_runners_or_the_batters_move_is_invalid()
    {
        var (facts, state, evt, applied) = Case(Root.GeneralTop, 0, 2, 0, "ground_hard", "out");     // a runner on second holds: two moves (runner and batter)
        var step = applied.Value;
        var play = step.NewPlays.Single();

        Assert.Equal(2, play.Moves.Count);
        Assert.Null(InPlayPitchPlanning.Verify(facts, state, evt, applied));
        var lost = step with { NewPlays = ImmutableList.Create(play with { Moves = play.Moves.RemoveAt(0) }) };
        Assert.Equal("InPlayCandidate.official_record_invalid", Code(InPlayPitchPlanning.Verify(facts, state, evt, Result<GameStep>.Success(lost))));
    }

    [Fact]
    public void A_walk_off_plan_is_verified_against_a_final_home_win()
    {
        var (facts, state, evt, applied) = Case(Root.WalkOff, 0, 4, 0, "line_medium", "double");

        Assert.Null(InPlayPitchPlanning.Verify(facts, state, evt, applied));
        Assert.Equal(GameStatus.Final, applied.Value.State.Status);
        Assert.Equal(GameOutcome.HomeWin, applied.Value.State.Outcome);
    }

    // ---------------------------------------------------------------- type-level separation of pending advancement and candidate

    private static readonly Type[] OutcomeTypes = [typeof(InPlayPitchCandidate), typeof(PendingAdvancement)];

    private static IEnumerable<Type> MemberTypes(Type t)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var f in t.GetFields(all)) yield return f.FieldType;
        foreach (var p in t.GetProperties(all)) yield return p.PropertyType;
        foreach (var m in t.GetMethods(all)) { yield return m.ReturnType; foreach (var a in m.GetParameters()) yield return a.ParameterType; }
        foreach (var c in t.GetConstructors(all)) foreach (var a in c.GetParameters()) yield return a.ParameterType;
    }

    [Fact]
    public void A_pending_advancement_is_not_and_cannot_become_a_candidate()
    {
        Assert.False(typeof(InPlayPitchCandidate).IsAssignableFrom(typeof(PendingAdvancement)));
        Assert.False(typeof(PendingAdvancement).IsAssignableFrom(typeof(InPlayPitchCandidate)));
        Assert.Equal(typeof(InPlayPlanOutcome), typeof(PendingAdvancement).BaseType);
        Assert.Equal(typeof(InPlayPlanOutcome), typeof(InPlayPitchCandidate).BaseType);
        Assert.True(typeof(PendingAdvancement).IsSealed && typeof(InPlayPitchCandidate).IsSealed);
        Assert.True(typeof(InPlayPlanOutcome).IsAbstract);
        // the closed hierarchy: nobody outside the assembly can derive a third kind of outcome
        Assert.DoesNotContain(typeof(InPlayPlanOutcome).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), c => c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly);
    }

    [Fact]
    public void A_pending_advancement_has_no_event_id_receipt_step_or_adoptable_rng()
    {
        var types = MemberTypes(typeof(PendingAdvancement)).Concat(MemberTypes(typeof(InPlayPlanOutcome))).ToList();

        Assert.DoesNotContain(types, t => typeof(GameEvent).IsAssignableFrom(t));
        Assert.DoesNotContain(types, t => t == typeof(GameStep));
        Assert.DoesNotContain(types, t => t == typeof(EventReceipt));
        Assert.DoesNotContain(types, t => t == typeof(InPlayPitchCandidate));
        var names = typeof(PendingAdvancement).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(p => p.Name).ToList();
        Assert.DoesNotContain("Event", names);
        Assert.DoesNotContain("EventId", names);
        Assert.DoesNotContain("UnconfirmedStep", names);
        Assert.DoesNotContain("NextRng", names);                       // the RNG position is named for what it is: the position AFTER the pitch draw, not an adoptable next position
        Assert.DoesNotContain("Receipt", names);
        Assert.Contains("RngAfterPitchDraw", names);
        Assert.Contains("ContextKeys", names);
        Assert.Contains("IsComposite", names);
    }

    [Fact]
    public void The_candidate_alone_carries_event_id_step_and_next_rng()
    {
        var names = typeof(InPlayPitchCandidate).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(p => p.Name).ToList();

        Assert.Contains("Event", names);
        Assert.Contains("EventId", names);
        Assert.Contains("UnconfirmedStep", names);
        Assert.Contains("NextRng", names);
        Assert.Equal(typeof(InPlayEvent), typeof(InPlayPitchCandidate).GetProperty("Event")!.PropertyType);
    }

    [Fact]
    public void The_new_result_types_have_no_public_constructor_and_no_public_setter()
    {
        foreach (var type in new[] { typeof(BaseInPlayPlan), typeof(InPlayPlanOutcome), typeof(InPlayPitchCandidate), typeof(PendingAdvancement) })
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
            Assert.All(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), p => Assert.True(p.SetMethod is null || p.SetMethod.IsPrivate));
        }
    }

    [Fact]
    public void The_public_entry_points_take_no_draft_and_the_core_planner_is_internal()
    {
        var entry = typeof(InPlayPitchPlanning).GetMethods(BindingFlags.Public | BindingFlags.Static).ToList();

        Assert.Equal(["Build", "PlanBase"], entry.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.DoesNotContain(entry.SelectMany(m => m.GetParameters()), p => p.ParameterType == typeof(PitchBranchDraft));
        Assert.All(entry, m => Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(PreparedPitch)));
        Assert.False(typeof(InPlayBasePlanner).IsPublic);
        Assert.False(typeof(InPlayTemplateCatalog).IsPublic);
        Assert.False(typeof(InPlayPlanFacts).IsPublic);
    }

    // ---------------------------------------------------------------- the new source files draw nothing and persist nothing

    private static readonly string[] NewSources = ["InPlayTemplateCatalog.cs", "InPlayBasePlanner.cs", "InPlayOfficialCredit.cs", "InPlayCandidateTypes.cs", "InPlayPitchPlanning.cs"];

    [Fact]
    public void The_new_production_files_draw_no_random_number_read_no_clock_and_persist_nothing()
    {
        var dir = Path.Combine(FeasibilityData.Root, "src", "StarPlayer.Simulation", "Baseball");
        foreach (var name in NewSources)
        {
            var text = File.ReadAllText(Path.Combine(dir, name));
            foreach (var forbidden in new[] { "NextBounded", ".Stream(", "GameRandom.", "RandomNumberGenerator", "new Random", "DateTime", "DateTimeOffset", "Guid.", "Environment.", "System.IO", "Sqlite", "HttpClient", "GameEventIds.Create" })
                if (name != "InPlayPitchPlanning.cs" || forbidden != "GameEventIds.Create")   // the builder issues the WORKING id on a private counter (and nothing else)
                    Assert.False(text.Contains(forbidden, StringComparison.Ordinal), $"{name} mentions '{forbidden}'");
            Assert.False(text.Contains("game/runner", StringComparison.Ordinal), $"{name} touches the advancement stream");
            Assert.False(text.Contains("InPlayPlanner", StringComparison.Ordinal), $"{name} refers to the test-only planner");
            Assert.False(text.Contains("InPlayFeasibility", StringComparison.Ordinal), $"{name} refers to the feasibility tooling");
        }
    }

    [Fact]
    public void The_builder_issues_working_ids_only_on_a_private_counter_and_never_adopts_an_rng_position()
    {
        var text = File.ReadAllText(Path.Combine(FeasibilityData.Root, "src", "StarPlayer.Simulation", "Baseball", "InPlayPitchPlanning.cs"));

        Assert.Contains("GameEventIds.Create", text);
        Assert.Contains(".Next()", text);
        Assert.DoesNotContain("Restore(", text);                       // the stale-preparation checks share PitchPreparation's contract; nothing here restores or advances a generator
        Assert.DoesNotContain("Snapshot()", text);
    }

    [Fact]
    public void The_shared_stale_check_is_the_one_C2B_1_uses()
    {
        var text = File.ReadAllText(Path.Combine(FeasibilityData.Root, "src", "StarPlayer.Simulation", "Baseball", "PitchPreparation.cs"));

        Assert.Contains("internal static Error? CheckPrepared(", text);
        Assert.True(System.Text.RegularExpressions.Regex.Matches(text, @"CheckPrepared\(").Count >= 2);     // defined once, used by the non-in-play builder
        Assert.Contains("PitchPreparation.CheckPrepared(", File.ReadAllText(Path.Combine(FeasibilityData.Root, "src", "StarPlayer.Simulation", "Baseball", "InPlayPitchPlanning.cs")));
    }
}
