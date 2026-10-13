using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.Randomness;

namespace StarPlayer.Simulation.Tests.Authority;

/// <summary>
/// Phase 3-C3-B (game-engine 27.18 B-03, B-04, B-05, B-07): what the structure guarantees. The C3-A <c>AuthorityBoundaryTests</c> are unchanged and still pin <c>CommitGate</c>; these add the
/// pitch entry, the evidence, the exact set of error codes and the field inventories that force the preview comparer to be reviewed when a domain type grows.
/// </summary>
public class PitchCommitBoundaryTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static string Code(string file)
    {
        var path = Path.Combine(RngFixture.Root, "src", "StarPlayer.Simulation", "Baseball", file);
        return Regex.Replace(Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\r\n]*", "");
    }

    // ---------------------------------------------------------------- the pitch entry

    [Fact]
    public void The_pitch_entry_is_a_public_static_class_whose_only_public_members_are_the_two_stage_overloads_returning_a_staged_commit()
    {
        var type = typeof(PitchCommitGate);

        Assert.True(type.IsPublic && type.IsAbstract && type.IsSealed);                                         // static
        var publicMembers = type.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).ToArray();
        var methods = publicMembers.OfType<MethodInfo>().ToArray();
        Assert.Equal(publicMembers.Length, methods.Length);
        Assert.Equal(2, methods.Length);
        Assert.All(methods, m =>
        {
            Assert.Equal("Stage", m.Name);
            Assert.Equal(typeof(Result<StagedCommit>), m.ReturnType);
            Assert.DoesNotContain(m.GetParameters(), p => p.ParameterType == typeof(GameEvent) || p.ParameterType.Name == nameof(PendingAdvancement) || p.ParameterType.Name == nameof(InPlayPlanOutcome)
                || p.ParameterType.Name == nameof(BaseInPlayPlan) || p.ParameterType.Name == "CommitEvidence");
        });
    }

    [Fact]
    public void Evidence_is_an_internal_sealed_class_with_no_public_member_no_callback_and_no_general_permission_flag()
    {
        var type = typeof(PitchCommitGate).Assembly.GetType("StarPlayer.Simulation.Baseball.CommitEvidence")!;

        Assert.NotNull(type);
        Assert.True(type.IsNotPublic && type.IsSealed);
        Assert.Empty(type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        foreach (var field in type.GetFields(All))
        {
            Assert.False(typeof(Delegate).IsAssignableFrom(field.FieldType), field.Name);                      // no supplied verification function
            Assert.False(field.FieldType == typeof(bool) && !field.IsInitOnly, field.Name);                    // no settable permission flag
        }

        foreach (var method in type.GetMethods(All))
            Assert.DoesNotContain(method.GetParameters(), p => typeof(Delegate).IsAssignableFrom(p.ParameterType));
        foreach (var constructor in type.GetConstructors(All))
            Assert.DoesNotContain(constructor.GetParameters(), p => typeof(Delegate).IsAssignableFrom(p.ParameterType));
        Assert.All(type.GetFields(All).Where(f => !f.IsStatic), f => Assert.True(f.IsInitOnly, f.Name));      // immutable once made
    }

    [Fact]
    public void No_authority_type_names_the_evidence_so_it_cannot_be_returned_or_stored()
    {
        foreach (var type in new[] { typeof(AuthoritativeGame), typeof(GameCursor), typeof(StagedCommit), typeof(CommitRecord), typeof(PitchCommitGate) })
        {
            var members = type.GetFields(All).Select(f => f.FieldType)
                .Concat(type.GetProperties(All).Select(p => p.PropertyType))
                .Concat(type.GetMethods(All).Select(m => m.ReturnType))
                .Concat(type.GetMethods(All).SelectMany(m => m.GetParameters()).Select(p => p.ParameterType));
            Assert.DoesNotContain(members, t => t.Name == "CommitEvidence");
        }
    }

    [Fact]
    public void The_pitch_entry_and_the_evidence_build_no_official_value_and_run_no_reducer_swap_or_draw()
    {
        string[] officialOnlyForTheCore = ["GameReducer.Apply", "GameReducer.Finalize", "Interlocked", "new AuthoritativeGame", "new StagedCommit", "new CommitRecord", "GameCursor", ".Publish("];
        foreach (var word in officialOnlyForTheCore)
            Assert.DoesNotContain(word, Code("PitchCommitGate.cs"));

        string[] neverDraws = ["GameReducer.Apply", "GameReducer.Finalize", "Interlocked", "new AuthoritativeGame", "new StagedCommit", "new CommitRecord", "Atomically", ".Stream(", ".NextBounded(", ".NextUInt32(",
            "GameRandom.Create", "System.Random", "DateTime", "GameCursor"];
        foreach (var word in neverDraws)
            Assert.DoesNotContain(word, Code("CommitEvidence.cs"));
    }

    [Fact]
    public void The_pitch_entry_reaches_the_common_core_and_nothing_else_makes_a_staged_commit()
    {
        var entry = Code("PitchCommitGate.cs");
        Assert.Equal(2, Regex.Matches(entry, @"CommitGate\.Stage\(").Count);                                       // both overloads end in the one core

        var makers = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RngFixture.Root, "src", "StarPlayer.Simulation"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            if (Regex.IsMatch(File.ReadAllText(file), @"new\s+StagedCommit\s*\(|new\s+CommitRecord\s*\("))
                makers.Add(Path.GetFileName(file));
        Assert.Equal(["CommitGate.cs"], makers.Order().ToArray());          // the one core
    }

    [Fact]
    public void The_core_asks_the_evidence_for_its_checks_and_keeps_the_c3a_guard_and_order_comments()
    {
        var core = File.ReadAllText(Path.Combine(RngFixture.Root, "src", "StarPlayer.Simulation", "Baseball", "CommitGate.cs"));
        var code = Code("CommitGate.cs");

        Assert.Contains("gameEvent is not (ApproachEvent or RunnerPlayEvent or SubstitutionEvent)", core);
        foreach (var call in new[] { "evidence.CheckPrepared(", "evidence.CheckLink(", "evidence?.CheckApplied(", "evidence?.ComparePreview(", "evidence?.CheckRng(", "evidence.Covers(" })
            Assert.Contains(call, code);
        var order = new[] { "evidence.CheckPrepared(", "evidence.CheckLink(", "Commit.event_id_in_use", "GameReducer.Apply(", "evidence?.CheckApplied(", "evidence?.ComparePreview(", "evidence?.CheckRng(", "GameReducer.Finalize(", "new CommitRecord(" }
            .Select(w => code.IndexOf(w, StringComparison.Ordinal)).ToArray();
        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.Order().ToArray(), order);                                                          // 7a, 7b, 8, 9, 9 pitch part, 10a, 10b, 11, 12
    }

    // ---------------------------------------------------------------- exactly five new error codes

    [Fact]
    public void The_commit_error_codes_are_the_sixteen_of_c3a_plus_exactly_the_five_of_c3b()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in new[] { "CommitGate.cs", "CommitEvidence.cs", "PitchCommitGate.cs", "GameCursor.cs", "AuthoritativeGame.cs", "StagedCommit.cs", "CommitRecord.cs" })
            foreach (Match m in Regex.Matches(Code(file), "\"(Commit\\.[a-z_]+)\""))
                found.Add(m.Groups[1].Value);

        string[] c3a =
        [
            "Commit.authority_missing", "Commit.event_missing", "Commit.event_kind_not_supported", "Commit.authority_inconsistent", "Commit.event_game_mismatch", "Commit.event_id_invalid",
            "Commit.already_committed", "Commit.event_sequence_gap", "Commit.event_id_in_use", "Commit.receipt_mismatch", "Commit.finalize_failed", "Commit.final_state_incomplete",
            "Commit.revision_exhausted", "Commit.staged_missing", "Commit.revision_stale", "Commit.cas_lost",
        ];
        string[] c3b = ["Commit.candidate_missing", "Commit.candidate_link_mismatch", "Commit.preview_mismatch", "Commit.rng_evidence_mismatch", "Commit.pitch_count_invalid"];
        Assert.Equal(c3a.Concat(c3b).Order(StringComparer.Ordinal).ToArray(), found.ToArray());
    }

    // ---------------------------------------------------------------- field inventories (B-03): a new domain field fails here until the comparer and its tests are reviewed

    /// <summary>The state members of a record: public instance properties with an init accessor (the positional ones), excluding computed get-only properties.</summary>
    internal static string[] Members(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.SetMethod is not null && p.Name != "EqualityContract").Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();

    private static void Pinned(string[] expected, Type type) => Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), Members(type));

    [Fact]
    public void The_game_state_members_are_pinned() =>
        Pinned(["Approach", "Away", "AwayLine", "AwayRuns", "AppliedEvents", "Bases", "Count", "Final", "GameId", "Half", "Home", "HomeLine", "HomeRuns", "Inning", "NextRunnerKey", "Outcome", "Outs",
            "Plays", "Rules", "Stage", "Stats", "Status", "Substitutions", "WalkChargeOverride"], typeof(GameState));

    [Fact]
    public void The_team_play_step_and_delta_members_are_pinned()
    {
        Pinned(["Appeared", "Bench", "DhActive", "EverInLineup", "Lineup", "NextBatterIndex", "Pitcher", "PitcherUsages", "Removed", "StartingDh", "StartingPitcher"], typeof(TeamGameState));
        Pinned(["AwayRunsAfter", "Batted", "Batter", "Batting", "BattingTeamPitcher", "Catcher", "Credits", "Errors", "EventId", "FinalCount", "Half", "HomeRunsAfter", "Id", "Inning", "IsDoublePlay",
            "Kind", "Moves", "OutsAfter", "OutsBefore", "Pitcher", "Result", "Runs", "Sequence"], typeof(ScoredPlay));
        Pinned(["Delta", "Facts", "NewPlays", "State", "WasDuplicate"], typeof(GameStep));
        Pinned(["Entries", "SourceEventId"], typeof(GameStatDelta));
    }

    [Fact]
    public void The_records_inside_the_compared_collections_hold_no_collection_so_their_value_equality_is_structural()
    {
        Type[] leaves =
        [
            typeof(BaseRunner), typeof(RunnerState), typeof(LineupSlot), typeof(PitcherUsage), typeof(SubstitutionRecord), typeof(EventReceipt), typeof(StatEntry), typeof(RunnerMoveRecord),
            typeof(FielderCredit), typeof(ScoredRun), typeof(PitchCountState), typeof(PlateAppearanceCompleted), typeof(RunnerPlayCompleted), typeof(SubstitutionMade), typeof(GameCompleted),
        ];
        foreach (var leaf in leaves)
            foreach (var property in leaf.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name != "EqualityContract"))
            {
                var t = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                Assert.False(t != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(t), $"{leaf.Name}.{property.Name} is a collection");
                Assert.False(t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ImmutableArray<>), $"{leaf.Name}.{property.Name}");
            }
    }
}
