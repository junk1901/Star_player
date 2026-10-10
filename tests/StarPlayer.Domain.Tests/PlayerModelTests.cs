using System.Reflection;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using static StarPlayer.Domain.Tests.TestData;

namespace StarPlayer.Domain.Tests;

public class PlayerModelTests
{
    [Fact]
    public void Two_players_with_the_same_name_keep_distinct_identity_and_never_compare_equal()
    {
        var a = MakePlayer("p-0001", "박민수");
        var b = MakePlayer("p-0002", "박민수");

        Assert.Equal(a.DisplayName, b.DisplayName);
        Assert.NotEqual(a.Id, b.Id);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Rename_changes_the_name_and_keeps_the_id()
    {
        var renamed = MakePlayer("p-0001", "박민수").Rename("박민석").Value;

        Assert.Equal("박민석", renamed.DisplayName);
        Assert.Equal(P("p-0001"), renamed.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" 이름")]
    [InlineData("이름 ")]
    [InlineData("이\n름")]
    public void Names_must_be_non_blank_canonical_text(string? name)
        => Assert.Equal("Player.name_invalid", Player.Create(P("p-1"), name, D("2000-01-01"), ThrowingHand.Right, BattingSide.Right, BodyMeasurements.Create(180, 80).Value, [], null).Error!.Value.Code);

    [Fact]
    public void Player_creation_validates_body_biography_and_default_id()
    {
        Assert.False(BodyMeasurements.Create(139, 80).IsSuccess);
        Assert.False(BodyMeasurements.Create(180, 201).IsSuccess);
        var body = BodyMeasurements.Create(180, 80).Value;

        Assert.Equal("Player.id_missing", Player.Create(default, "이름", D("2000-01-01"), ThrowingHand.Right, BattingSide.Right, body, [], null).Error!.Value.Code);
        Assert.Equal("Player.biography_too_long", Player.Create(P("p-1"), "이름", D("2000-01-01"), ThrowingHand.Right, BattingSide.Right, body, [], new string('가', 501)).Error!.Value.Code);
        Assert.Equal("Player.handedness_invalid", Player.Create(P("p-1"), "이름", D("2000-01-01"), (ThrowingHand)9, BattingSide.Right, body, [], null).Error!.Value.Code);
    }

    [Fact]
    public void Player_age_comes_from_birth_date_and_world_date()
    {
        var player = MakePlayer(birth: "2000-05-17");

        Assert.Equal(29, player.AgeOn(D("2030-03-01")).Value);
        Assert.Equal(30, player.AgeOn(D("2030-05-17")).Value);
    }

    [Fact]
    public void Personality_traits_are_distinct_and_sorted_descriptive_identity()
    {
        var body = BodyMeasurements.Create(180, 80).Value;
        var player = Player.Create(P("p-1"), "이름", D("2000-01-01"), ThrowingHand.Left, BattingSide.Switch, body,
            [PersonalityTrait.Sociable, PersonalityTrait.Diligent, PersonalityTrait.Sociable], "자유 입력 소개").Value;

        Assert.Equal([PersonalityTrait.Diligent, PersonalityTrait.Sociable], player.Personality);
        Assert.Equal("자유 입력 소개", player.Biography);
    }

    [Fact]
    public void Player_has_no_current_jersey_number_or_club_member()
    {
        // C47: a number is a dated PlayerId-ClubId assignment, never a timeless Player field.
        var forbidden = new[] { "Jersey", "Number", "Club", "Team", "Contract", "Potential", "Development" };
        var members = typeof(Player).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name);

        Assert.DoesNotContain(members, name => forbidden.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Events_carry_ids_and_public_facts_for_the_documented_payloads()
    {
        var sheet = PlayerSheet.Create(MakePlayer("p-7"), Ratings.Create(Hitter(), null, null).Value, State(), Proficiency(FieldPosition.Shortstop, (FieldPosition.Shortstop, 70)), ActiveStatus()).Value;

        var created = PlayerEventFactory.Created(sheet, D("2030-03-01"));

        Assert.Equal(new PlayerCreated(P("p-7"), D("2000-05-17"), PlayerKind.Hitter, FieldPosition.Shortstop, D("2030-03-01")), created);
    }

    // ---- separation of dimensions ----

    [Fact]
    public void PlayerSheet_cannot_hold_the_hidden_development_profile()
    {
        // Exclusion by construction: no sheet member (nor any member's type) is or contains DevelopmentProfile.
        var sheetTypes = typeof(PlayerSheet).GetProperties().Select(p => p.PropertyType).ToList();

        Assert.DoesNotContain(typeof(DevelopmentProfile), sheetTypes);
        Assert.All(new[] { typeof(Player), typeof(Ratings), typeof(PlayerState), typeof(ProficiencyProfile), typeof(PlayerStatus) },
            type => Assert.DoesNotContain(type.GetProperties(), p => p.PropertyType == typeof(DevelopmentProfile)));
    }

    [Fact]
    public void Base_ratings_state_and_proficiency_are_distinct_types_that_change_independently()
    {
        var ratings = Ratings.Create(Hitter(60), null, null).Value;
        var tired = State(10) with { Fatigue = S(95) };

        // A different transient state leaves base ratings untouched (value equality on the same instance).
        Assert.Equal(60, ratings.Hitter!.Contact.Value);
        Assert.Equal(95, tired.Fatigue.Value);
        Assert.NotEqual(typeof(Ratings), typeof(PlayerState));
        Assert.NotEqual(typeof(PlayerState), typeof(ProficiencyProfile));
    }

    [Fact]
    public void Sheet_requires_ratings_that_match_the_preferred_position()
    {
        var pitcherOnly = Ratings.Create(null, Pitcher(), null).Value;
        var hitterOnly = Ratings.Create(Hitter(), null, null).Value;

        Assert.Equal("PlayerSheet.pitcher_without_pitching",
            PlayerSheet.Create(MakePlayer(), hitterOnly, State(), Proficiency(FieldPosition.Pitcher), ActiveStatus()).Error!.Value.Code);
        Assert.Equal("PlayerSheet.fielder_without_hitting",
            PlayerSheet.Create(MakePlayer(), pitcherOnly, State(), Proficiency(FieldPosition.Shortstop), ActiveStatus()).Error!.Value.Code);
        Assert.True(PlayerSheet.Create(MakePlayer(), Ratings.Create(Hitter(), Pitcher(), null).Value, State(), Proficiency(FieldPosition.Pitcher), ActiveStatus()).IsSuccess);
    }

    // ---- development profile (hidden) ----

    [Fact]
    public void Development_profile_ceilings_cannot_sit_below_current_skills()
    {
        var ratings = Ratings.Create(Hitter(70), null, null).Value;
        var ok = DevelopmentProfile.Create(P("p-1"), S(90), GrowthType.Late, 6, 34, [(SkillKey.Contact, S(80)), (SkillKey.Power, S(70))]).Value;
        var low = DevelopmentProfile.Create(P("p-1"), S(90), GrowthType.Late, 6, 34, [(SkillKey.Contact, S(69))]).Value;
        var pitching = DevelopmentProfile.Create(P("p-1"), S(90), GrowthType.Late, 6, 34, [(SkillKey.Stuff, S(80))]).Value;

        Assert.True(ok.CheckAgainst(ratings).IsSuccess);
        Assert.Equal("DevelopmentProfile.ceiling_below_current", low.CheckAgainst(ratings).Error!.Value.Code);
        Assert.Equal("DevelopmentProfile.unknown_skill", pitching.CheckAgainst(ratings).Error!.Value.Code);
    }

    [Fact]
    public void Development_profile_validates_its_own_fields()
    {
        Assert.Equal("DevelopmentProfile.duplicate_ceiling",
            DevelopmentProfile.Create(P("p-1"), S(50), GrowthType.Early, 5, 33, [(SkillKey.Contact, S(60)), (SkillKey.Contact, S(70))]).Error!.Value.Code);
        Assert.Equal("DevelopmentProfile.aging_out_of_range", DevelopmentProfile.Create(P("p-1"), S(50), GrowthType.Early, 0, 33, []).Error!.Value.Code);
        Assert.Equal("DevelopmentProfile.player_missing", DevelopmentProfile.Create(default, S(50), GrowthType.Early, 5, 33, []).Error!.Value.Code);
    }

    // ---- proficiency ----

    [Fact]
    public void Returning_to_a_known_position_preserves_its_proficiency()
    {
        var id = P("p-1");
        var profile = Proficiency(FieldPosition.Shortstop, (FieldPosition.Shortstop, 82), (FieldPosition.CenterField, 35));

        var toCf = profile.ChangePosition(id, FieldPosition.CenterField, D("2030-04-01")).Value;
        var back = toCf.Profile.ChangePosition(id, FieldPosition.Shortstop, D("2030-06-01")).Value;

        Assert.Equal(new PlayerChangedPosition(id, FieldPosition.Shortstop, FieldPosition.CenterField, D("2030-04-01")), toCf.Event);
        Assert.Equal(FieldPosition.Shortstop, back.Profile.PreferredPosition);
        Assert.Equal(82, back.Profile.PositionProficiency(FieldPosition.Shortstop).Value);
        Assert.Equal(35, back.Profile.PositionProficiency(FieldPosition.CenterField).Value);
        Assert.Equal(profile.Positions, back.Profile.Positions);
    }

    [Fact]
    public void Moving_to_an_unfamiliar_position_starts_with_no_recorded_proficiency_and_invents_nothing()
    {
        var moved = Proficiency(FieldPosition.Shortstop, (FieldPosition.Shortstop, 82)).ChangePosition(P("p-1"), FieldPosition.Catcher, D("2030-04-01")).Value.Profile;

        Assert.Equal(0, moved.PositionProficiency(FieldPosition.Catcher).Value);
        Assert.Single(moved.Positions);
    }

    [Fact]
    public void Position_change_rejects_same_or_invalid_position_and_proficiency_rejects_pitcher_and_dh_entries()
    {
        var profile = Proficiency(FieldPosition.Shortstop);

        Assert.Equal("Proficiency.position_unchanged", profile.ChangePosition(P("p-1"), FieldPosition.Shortstop, D("2030-04-01")).Error!.Value.Code);
        Assert.Equal("Proficiency.position_invalid", profile.ChangePosition(P("p-1"), (FieldPosition)99, D("2030-04-01")).Error!.Value.Code);
        Assert.Equal("Proficiency.position_not_fielding", ProficiencyProfile.Create(FieldPosition.Shortstop, [(FieldPosition.Pitcher, S(50))], []).Error!.Value.Code);
        Assert.Equal("Proficiency.duplicate_position", ProficiencyProfile.Create(FieldPosition.Shortstop, [(FieldPosition.Catcher, S(50)), (FieldPosition.Catcher, S(60))], []).Error!.Value.Code);
        Assert.Equal("Proficiency.role_invalid", ProficiencyProfile.Create(FieldPosition.Shortstop, [], [(PlayerRole.Batter, S(50)), (PlayerRole.Batter, S(60))]).Error!.Value.Code);
    }

    [Fact]
    public void Catcher_tools_and_c_proficiency_are_independent_in_both_directions()
    {
        // Excellent tools / novice C and average tools / expert C are both representable and stay distinct.
        var eliteTools = Catcher(92);
        var noviceC = Proficiency(FieldPosition.Catcher, (FieldPosition.Catcher, 12));
        var averageTools = Catcher(52);
        var expertC = Proficiency(FieldPosition.Catcher, (FieldPosition.Catcher, 90));

        Assert.Equal(92, eliteTools.ReceivingFraming.Value);
        Assert.Equal(12, noviceC.PositionProficiency(FieldPosition.Catcher).Value);
        Assert.Equal(52, averageTools.ReceivingFraming.Value);
        Assert.Equal(90, expertC.PositionProficiency(FieldPosition.Catcher).Value);
        // Moving away from C and back never rewrites the four tools: they are not part of the profile at all.
        Assert.DoesNotContain(typeof(ProficiencyProfile).GetProperties(), p => p.PropertyType == typeof(CatcherRatings));
        Assert.DoesNotContain(typeof(CatcherRatings).GetProperties(), p => p.Name.Contains("Proficiency", StringComparison.Ordinal));
    }

    // ---- orthogonal status axes ----

    [Fact]
    public void Origin_entry_path_registration_and_league_are_separate_axes_and_developmental_second_team_is_legal()
    {
        var status = PlayerStatus.Create(
            [new OriginRecord(OriginType.HighSchool, AmateurTeamId.Create("hs_032").Value, null, D("2032-02-20"))],
            EntryPath.DevelopmentContract, RegistrationStatus.Development, LeagueAssignment.SecondTeam,
            [new ClubAffiliation(C("club-a"), D("2033-01-10"), null)], CareerStatus.Active).Value;

        Assert.Equal(OriginType.HighSchool, status.Origins[0].Type);
        Assert.Equal(EntryPath.DevelopmentContract, status.EntryPath);
        Assert.Equal(RegistrationStatus.Development, status.Registration);
        Assert.Equal(LeagueAssignment.SecondTeam, status.League);
        Assert.Equal(C("club-a"), status.CurrentAffiliation!.Club);
    }

    [Fact]
    public void Status_rejects_incoherent_combinations()
    {
        var hs = new[] { new OriginRecord(OriginType.HighSchool, null, null, null) };
        var open = new[] { new ClubAffiliation(C("club-a"), D("2030-01-01"), null) };

        Assert.Equal("PlayerStatus.origin_missing", PlayerStatus.Create([], null, RegistrationStatus.Amateur, null, [], CareerStatus.Active).Error!.Value.Code);
        Assert.Equal("PlayerStatus.affiliation_without_registration", PlayerStatus.Create(hs, null, RegistrationStatus.Amateur, LeagueAssignment.FirstTeam, open, CareerStatus.Active).Error!.Value.Code);
        Assert.Equal("PlayerStatus.league_missing", PlayerStatus.Create(hs, EntryPath.Draft, RegistrationStatus.Registered, null, open, CareerStatus.Active).Error!.Value.Code);
        Assert.Equal("PlayerStatus.league_without_club", PlayerStatus.Create(hs, null, RegistrationStatus.Amateur, LeagueAssignment.FirstTeam, [], CareerStatus.Active).Error!.Value.Code);
        Assert.Equal("PlayerStatus.registration_without_club", PlayerStatus.Create(hs, EntryPath.Draft, RegistrationStatus.Registered, null, [], CareerStatus.Active).Error!.Value.Code);
        Assert.True(PlayerStatus.Create(hs, null, RegistrationStatus.Amateur, null, [], CareerStatus.Active).IsSuccess);
        Assert.True(PlayerStatus.Create(hs, EntryPath.Draft, RegistrationStatus.Released, null,
            [new ClubAffiliation(C("club-a"), D("2030-01-01"), D("2031-01-01"))], CareerStatus.Active).IsSuccess);
    }

    [Fact]
    public void Affiliation_history_must_be_ordered_non_overlapping_with_only_the_last_open()
    {
        var hs = new[] { new OriginRecord(OriginType.University, null, null, null) };
        ClubAffiliation Aff(string club, string from, string? to) => new(C(club), D(from), to is null ? null : D(to));
        Assert.True(PlayerStatus.Create(hs, EntryPath.Draft, RegistrationStatus.Registered, LeagueAssignment.FirstTeam,
            [Aff("club-a", "2030-01-01", "2031-01-01"), Aff("club-b", "2031-01-01", null)], CareerStatus.Active).IsSuccess);

        Assert.Equal("PlayerStatus.affiliation_overlap", PlayerStatus.Create(hs, null, RegistrationStatus.Released, null,
            [Aff("club-a", "2030-01-01", "2031-06-01"), Aff("club-b", "2031-01-01", "2032-01-01")], CareerStatus.Active).Error!.Value.Code);
        Assert.Equal("PlayerStatus.affiliation_overlap", PlayerStatus.Create(hs, EntryPath.Draft, RegistrationStatus.Registered, LeagueAssignment.FirstTeam,
            [Aff("club-a", "2030-01-01", null), Aff("club-b", "2031-01-01", null)], CareerStatus.Active).Error!.Value.Code);
        Assert.Equal("PlayerStatus.affiliation_interval_invalid", PlayerStatus.Create(hs, null, RegistrationStatus.Released, null,
            [Aff("club-a", "2030-01-01", "2030-01-01")], CareerStatus.Active).Error!.Value.Code);
    }
}
