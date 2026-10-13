using System.Reflection;
using System.Text.RegularExpressions;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.Randomness;

namespace StarPlayer.Simulation.Tests.Authority;

/// <summary>Phase 3-C3-A, O-7/O-4: the authority types are public with internal construction and name no hidden candidate type, and no code path of C3-A can confirm a pitch.</summary>
public class AuthorityBoundaryTests
{
    private static readonly Type[] AuthorityTypes = [typeof(AuthoritativeGame), typeof(GameCursor), typeof(StagedCommit), typeof(CommitRecord), typeof(CommitGate)];

    private static readonly Type[] Hidden =
    [
        typeof(PreparedPitch), typeof(NonInPlayPitchCandidate), typeof(InPlayPitchCandidate), typeof(PendingAdvancement), typeof(BaseInPlayPlan),
        typeof(InPlayAudit), typeof(AdvancementAudit), typeof(PitchBranchDraft), typeof(PitchBranchStep),
    ];

    private static IEnumerable<Type> Mentions(Type type)
    {
        yield return type;
        if (type.HasElementType)
            foreach (var inner in Mentions(type.GetElementType()!))
                yield return inner;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                foreach (var inner in Mentions(argument))
                    yield return inner;
    }

    [Fact]
    public void The_authority_types_are_public_sealed_and_cannot_be_constructed_outside_simulation()
    {
        foreach (var type in AuthorityTypes)
        {
            Assert.True(type.IsPublic, type.Name);
            Assert.True(type.IsSealed, type.Name);                       // a static class is abstract + sealed
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        }

        Assert.True(typeof(CommitGate).IsAbstract);
    }

    [Fact]
    public void The_public_factories_of_the_aggregate_do_not_exist_only_internal_ones_do()
    {
        var publicStatics = typeof(AuthoritativeGame).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        Assert.Empty(publicStatics);
        Assert.NotNull(typeof(AuthoritativeGame).GetMethod("StartNew", BindingFlags.NonPublic | BindingFlags.Static));
        Assert.NotNull(typeof(AuthoritativeGame).GetMethod("Restore", BindingFlags.NonPublic | BindingFlags.Static));
    }

    [Fact]
    public void No_public_member_of_the_authority_types_names_a_candidate_prepared_pitch_draft_or_audit_type()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var leaks = new List<string>();
        foreach (var type in AuthorityTypes)
        {
            foreach (var field in type.GetFields(all).Where(f => Mentions(f.FieldType).Any(Hidden.Contains)))
                leaks.Add($"{type.Name}.{field.Name}");
            foreach (var property in type.GetProperties(all).Where(p => Mentions(p.PropertyType).Any(Hidden.Contains)))
                leaks.Add($"{type.Name}.{property.Name}");
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
                if (method.GetParameters().Any(p => Mentions(p.ParameterType).Any(Hidden.Contains)) || (method is MethodInfo m && Mentions(m.ReturnType).Any(Hidden.Contains)))
                    leaks.Add($"{type.Name}.{method.Name}");
        }

        Assert.Empty(leaks);
    }

    [Fact]
    public void The_commit_record_has_exactly_the_approved_members_and_no_stat_delta_or_audit()
    {
        var names = typeof(CommitRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).Order().ToArray();

        Assert.Equal(["Event", "EventSequence", "Facts", "Finalized", "Receipt", "RevisionAfter", "RevisionBefore", "RngStateHashAfter", "RngStateHashBefore"], names);
    }

    [Fact]
    public void The_c3a_sources_reference_no_pitch_preparation_candidate_planning_or_random_draw()
    {
        string[] files = ["AuthoritativeGame.cs", "CommitGate.cs", "GameCursor.cs", "StagedCommit.cs", "CommitRecord.cs"];
        string[] forbidden =
        [
            "PreparedPitch", "PitchCandidate", "PendingAdvancement", "PitchPreparation", "InPlayPitchPlanning", "AdvancementPlanning", "PitchBranch",
            "GameRandom.Create", ".Stream(", "NextBounded", "Atomically", "System.Random", "DateTime",
        ];
        foreach (var file in files)
        {
            var path = Path.Combine(RngFixture.Root, "src", "StarPlayer.Simulation", "Baseball", file);
            var code = Regex.Replace(Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\r\n]*", "");
            foreach (var word in forbidden)
                Assert.DoesNotContain(word, code);
        }
    }

    [Fact]
    public void The_gate_names_only_the_three_allowed_event_types()
    {
        var path = Path.Combine(RngFixture.Root, "src", "StarPlayer.Simulation", "Baseball", "CommitGate.cs");
        var code = File.ReadAllText(path);

        Assert.Contains("gameEvent is not (ApproachEvent or RunnerPlayEvent or SubstitutionEvent)", code);
        foreach (var refused in new[] { "PitchEvent", "TakeEvent", "InPlayEvent" })
            Assert.DoesNotMatch(new Regex(@"^(?!\s*(//|///)).*\b" + refused + @"\b", RegexOptions.Multiline), code);
    }
}
