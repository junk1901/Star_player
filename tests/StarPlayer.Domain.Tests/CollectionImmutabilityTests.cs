using System.Reflection;
using System.Runtime.CompilerServices;
using StarPlayer.Domain.Players;
using static StarPlayer.Domain.Tests.TestData;

namespace StarPlayer.Domain.Tests;

/// <summary>
/// Phase 2-A: the Domain collections that Phase 2 reads for game state must not be changeable after validation, neither
/// through the caller's original list nor through an IList cast of the public property (Codex Phase 1 Medium note).
/// </summary>
public class CollectionImmutabilityTests
{
    private static void AssertFrozen<T>(IReadOnlyList<T> exposed, T sample)
    {
        var list = Assert.IsAssignableFrom<IList<T>>(exposed); // casting is possible, mutating must not be
        var before = exposed.ToList();

        Assert.Throws<NotSupportedException>(() => list.Add(sample));
        Assert.Throws<NotSupportedException>(() => list.Clear());
        if (exposed.Count > 0)
        {
            Assert.Throws<NotSupportedException>(() => list.RemoveAt(0));
            Assert.Throws<NotSupportedException>(() => list[0] = sample);
        }

        Assert.Equal(before, exposed);
    }

    private static bool IsInitOnlyOrReadOnly(PropertyInfo p) =>
        p.SetMethod is null || p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    [Fact]
    public void Pitch_repertoire_copies_its_input_and_cannot_be_changed_through_the_public_list()
    {
        var input = new List<PitchRating> { new(PitchType.Slider, S(40)), new(PitchType.Fastball, S(60)) };
        var repertoire = PitchRepertoire.Create(input).Value;

        input.Clear();
        input.Add(new PitchRating(PitchType.Curve, S(1)));

        Assert.Equal([PitchType.Fastball, PitchType.Slider], repertoire.Pitches.Select(p => p.Type));
        AssertFrozen(repertoire.Pitches, new PitchRating(PitchType.Sinker, S(5)));
        Assert.Equal("PitchRepertoire.empty", PitchRepertoire.Create(null).Error!.Value.Code);
    }

    [Fact]
    public void Proficiency_copies_its_input_and_freezes_positions_and_roles()
    {
        var positions = new List<(FieldPosition, SkillValue)> { (FieldPosition.Catcher, S(90)) };
        var roles = new List<(PlayerRole, SkillValue)> { (PlayerRole.Batter, S(70)) };
        var profile = ProficiencyProfile.Create(FieldPosition.Catcher, positions, roles).Value;

        positions.Add((FieldPosition.Shortstop, S(10)));
        roles.Clear();

        Assert.Single(profile.Positions);
        Assert.Single(profile.Roles);
        AssertFrozen(profile.Positions, (FieldPosition.Shortstop, S(1)));
        AssertFrozen(profile.Roles, (PlayerRole.Batter, S(1)));
        Assert.True(ProficiencyProfile.Create(FieldPosition.Catcher, null, null).IsSuccess);
        // the explicit validated path still works after the failed mutation attempts
        Assert.Equal(FieldPosition.Shortstop, profile.ChangePosition(P("p-1"), FieldPosition.Shortstop, D("2030-01-01")).Value.Profile.PreferredPosition);
        Assert.Single(profile.Positions);
    }

    [Fact]
    public void Player_status_copies_its_input_and_freezes_origins_and_affiliations()
    {
        var origins = new List<OriginRecord> { new(OriginType.HighSchool, null, null, null) };
        var affiliations = new List<ClubAffiliation> { new(C("club-a"), D("2030-01-01"), null) };
        var status = PlayerStatus.Create(origins, EntryPath.Draft, RegistrationStatus.Registered, LeagueAssignment.FirstTeam, affiliations, CareerStatus.Active).Value;

        origins.Clear();
        affiliations.Add(new ClubAffiliation(C("club-b"), D("2031-01-01"), null)); // would be an overlapping open affiliation

        Assert.Single(status.Origins);
        Assert.Single(status.Affiliations);
        Assert.Equal(C("club-a"), status.CurrentAffiliation!.Club);
        AssertFrozen(status.Origins, new OriginRecord(OriginType.University, null, null, null));
        AssertFrozen(status.Affiliations, new ClubAffiliation(C("club-b"), D("2032-01-01"), null));
        Assert.Equal("PlayerStatus.origin_missing", PlayerStatus.Create(null, null, RegistrationStatus.Amateur, null, null, CareerStatus.Active).Error!.Value.Code);
    }

    [Fact]
    public void Development_profile_copies_its_input_and_freezes_ceilings()
    {
        var ceilings = new List<(SkillKey, SkillValue)> { (SkillKey.Contact, S(80)) };
        var profile = DevelopmentProfile.Create(P("p-1"), S(90), GrowthType.Late, 6, 34, ceilings).Value;

        ceilings.Clear();
        ceilings.Add((SkillKey.Power, S(1)));

        Assert.Equal([SkillKey.Contact], profile.SkillCeilings.Select(c => c.Key));
        AssertFrozen(profile.SkillCeilings, (SkillKey.Power, S(1)));
        Assert.True(DevelopmentProfile.Create(P("p-1"), S(90), GrowthType.Late, 6, 34, null).IsSuccess);
    }

    [Fact]
    public void Player_copies_personality_and_freezes_it()
    {
        var traits = new List<PersonalityTrait> { PersonalityTrait.Sociable, PersonalityTrait.Diligent };
        var player = Player.Create(P("p-1"), "홍길동", D("2000-01-01"), ThrowingHand.Right, BattingSide.Right, BodyMeasurements.Create(180, 80).Value, traits, "").Value;

        traits.Clear();

        Assert.Equal([PersonalityTrait.Diligent, PersonalityTrait.Sociable], player.Personality);
        AssertFrozen(player.Personality, PersonalityTrait.Frugal);
        Assert.Equal(player.Personality, player.Rename("김철수").Value.Personality);
    }

    [Fact]
    public void Skills_returns_a_fresh_list_each_time_so_a_cast_mutation_cannot_leak()
    {
        var ratings = Ratings.Create(Hitter(), Pitcher(), Catcher(50)).Value;

        var first = ratings.Skills();
        ((IList<(SkillKey, SkillValue)>)first).Clear();

        Assert.Equal(14, ratings.Skills().Count);
    }

    [Fact]
    public void Elements_held_in_these_collections_are_immutable()
    {
        // Every element type reachable from the frozen lists is a record/struct whose properties are get-only or init-only.
        var elementTypes = new[] { typeof(PitchRating), typeof(OriginRecord), typeof(ClubAffiliation), typeof(PersonalityTrait) };

        foreach (var type in elementTypes.Where(t => !t.IsEnum))
            Assert.All(type.GetProperties(), p => Assert.True(IsInitOnlyOrReadOnly(p), $"{type.Name}.{p.Name} is mutable"));
        Assert.All(typeof(HitterRatings).GetProperties(), p => Assert.True(IsInitOnlyOrReadOnly(p)));
        Assert.All(typeof(PlayerState).GetProperties(), p => Assert.True(IsInitOnlyOrReadOnly(p)));
        // The aggregate types themselves expose no setter at all.
        foreach (var type in new[] { typeof(Player), typeof(PitchRepertoire), typeof(ProficiencyProfile), typeof(PlayerStatus), typeof(DevelopmentProfile), typeof(Ratings), typeof(PlayerSheet) })
            Assert.All(type.GetProperties(), p => Assert.True(p.SetMethod is null, $"{type.Name}.{p.Name} has a setter"));
    }
}
