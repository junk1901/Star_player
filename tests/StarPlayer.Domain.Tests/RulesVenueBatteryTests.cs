using System.Reflection;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Domain.Teams;
using static StarPlayer.Domain.Tests.TestData;

namespace StarPlayer.Domain.Tests;

public class RulesVenueBatteryTests
{
    // ---- KBO Rule Engine boundary (C40) ----

    private static RuleVersionId V(string id) => RuleVersionId.Create(id).Value;

    private static RuleAreaDefinition Def(RuleArea area, string version, string from = "2000-01-01", string? to = null) =>
        RuleAreaDefinition.Create(area, V(version), D(from), to is null ? null : D(to), "fixture", [Simp("no-content", "규칙 내용 없음")]).Value;

    private static RuleSimplification Simp(string id, string description) => RuleSimplification.Create(id, description).Value;

    // game: v1 until 2031, v2 from 2031 (a timeline); roster only v1 until 2031.
    private static RuleCatalog Catalog() => RuleCatalog.Create(
        [Def(RuleArea.Game, "game-v1", "2000-01-01", "2031-01-01"), Def(RuleArea.Game, "game-v2", "2031-01-01"), Def(RuleArea.Roster, "roster-v1", "2000-01-01", "2031-01-01")]).Value;

    [Fact]
    public void Profile_resolves_one_registered_version_per_area()
    {
        var profile = RuleProfile.Create(RuleProfileId.Create("fixture-v1").Value, "고정 프로필", D("2030-03-01"),
            [new RuleAreaRef(RuleArea.Game, V("game-v1")), new RuleAreaRef(RuleArea.Roster, V("roster-v1"))], Catalog()).Value;

        Assert.Equal([RuleArea.Game, RuleArea.Roster], profile.Areas.Select(a => a.Area));
        Assert.Equal("game-v1", profile.ForArea(RuleArea.Game).Value.Version.Value);
    }

    [Fact]
    public void Unknown_rule_version_is_an_explicit_error_not_a_fallback()
    {
        var result = RuleProfile.Create(RuleProfileId.Create("fixture-v1").Value, "p", D("2030-03-01"), [new RuleAreaRef(RuleArea.Game, V("game-v9"))], Catalog());

        Assert.False(result.IsSuccess);
        Assert.Equal("RuleCatalog.version_not_found", result.Error!.Value.Code);
        Assert.Equal("RuleCatalog.version_not_found", Catalog().Resolve(RuleArea.Award, V("award-v1")).Error!.Value.Code);
        Assert.Equal("RuleCatalog.area_unknown", Catalog().Resolve((RuleArea)77, V("x")).Error!.Value.Code);
    }

    [Fact]
    public void Profile_rejects_conflicting_empty_and_not_yet_effective_compositions()
    {
        var id = RuleProfileId.Create("fixture-v1").Value;

        Assert.Equal("RuleProfile.conflicting_areas", RuleProfile.Create(id, "p", D("2032-01-01"),
            [new RuleAreaRef(RuleArea.Game, V("game-v1")), new RuleAreaRef(RuleArea.Game, V("game-v2"))], Catalog()).Error!.Value.Code);
        Assert.Equal("RuleProfile.empty", RuleProfile.Create(id, "p", D("2032-01-01"), [], Catalog()).Error!.Value.Code);
        Assert.Equal("RuleProfile.version_not_effective", RuleProfile.Create(id, "p", D("2030-01-01"), [new RuleAreaRef(RuleArea.Game, V("game-v2"))], Catalog()).Error!.Value.Code);
        Assert.Equal("RuleProfile.version_not_effective", RuleProfile.Create(id, "p", D("2032-01-01"), [new RuleAreaRef(RuleArea.Roster, V("roster-v1"))], Catalog()).Error!.Value.Code);
        Assert.Equal("RuleProfile.name_missing", RuleProfile.Create(id, " ", D("2032-01-01"), [new RuleAreaRef(RuleArea.Game, V("game-v1"))], Catalog()).Error!.Value.Code);
    }

    [Fact]
    public void A_consumer_asking_for_an_area_the_profile_lacks_gets_an_explicit_error()
    {
        var profile = RuleProfile.Create(RuleProfileId.Create("fixture-v1").Value, "p", D("2030-03-01"), [new RuleAreaRef(RuleArea.Game, V("game-v1"))], Catalog()).Value;

        Assert.Equal("RuleProfile.area_missing", profile.ForArea(RuleArea.RegularSeason).Error!.Value.Code);
    }

    [Fact]
    public void Catalog_validates_its_definitions()
    {
        Assert.Equal("RuleCatalog.duplicate_version", RuleCatalog.Create([Def(RuleArea.Game, "v1"), Def(RuleArea.Game, "v1")]).Error!.Value.Code);
        Assert.Equal("RuleCatalog.window_invalid", RuleAreaDefinition.Create(RuleArea.Game, V("v1"), D("2030-01-01"), D("2030-01-01"), "fixture", []).Error!.Value.Code);
        Assert.Equal("RuleCatalog.provenance_missing", RuleAreaDefinition.Create(RuleArea.Game, V("v1"), D("2000-01-01"), null, " ", []).Error!.Value.Code);
    }

    [Fact]
    public void The_ten_c40_areas_exist_and_the_domain_carries_no_2026_rule_constants()
    {
        Assert.Equal(10, Enum.GetValues<RuleArea>().Length);
        // Phase 1 is vocabulary only: the rule types hold no numeric rule values (outs per inning, roster sizes, ...).
        var numericMembers = new[] { typeof(RuleProfile), typeof(RuleAreaDefinition), typeof(RuleCatalog), typeof(RuleSimplification) }
            .SelectMany(t => t.GetProperties()).Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(long));
        Assert.Empty(numericMembers);
    }

    // ---- immutability and validation hardening (Codex review) ----

    [Fact]
    public void Default_or_invalid_rule_version_ids_are_rejected_everywhere_a_version_is_required()
    {
        var game = RuleArea.Game;

        Assert.True(default(RuleVersionId).IsDefault);
        Assert.Equal("RuleVersionId.invalid", RuleAreaDefinition.Create(game, default, D("2000-01-01"), null, "fixture", []).Error!.Value.Code);
        Assert.Equal("RuleVersionId.invalid", Catalog().Resolve(game, default).Error!.Value.Code);
        Assert.Equal("RuleVersionId.invalid", RuleProfile.Create(RuleProfileId.Create("p-1").Value, "p", D("2030-03-01"), [new RuleAreaRef(game, default)], Catalog()).Error!.Value.Code);
        Assert.Equal("RuleVersionId.invalid_character", RuleVersionId.Create("v 1").Error!.Value.Code);
        Assert.Equal("RuleVersionId.empty", RuleVersionId.Create(" ").Error!.Value.Code);
        Assert.Equal("RuleCatalog.area_unknown", RuleAreaDefinition.Create((RuleArea)99, V("v1"), D("2000-01-01"), null, "fixture", []).Error!.Value.Code);
    }

    [Fact]
    public void Simplifications_need_a_unique_id_and_a_description_and_the_list_itself()
    {
        Assert.Equal("RuleSimplification.id_invalid", RuleSimplification.Create("", "설명").Error!.Value.Code);
        Assert.Equal("RuleSimplification.id_invalid", RuleSimplification.Create("a b", "설명").Error!.Value.Code);
        Assert.Equal("RuleSimplification.description_invalid", RuleSimplification.Create("s-1", " ").Error!.Value.Code);
        Assert.Equal("RuleSimplification.description_invalid", RuleSimplification.Create("s-1", null).Error!.Value.Code);
        Assert.Equal("RuleSimplification.description_invalid", RuleSimplification.Create("s-1", new string('가', 201)).Error!.Value.Code);
        Assert.Equal("RuleSimplification.duplicate_id",
            RuleAreaDefinition.Create(RuleArea.Game, V("v1"), D("2000-01-01"), null, "fixture", [Simp("s-1", "a"), Simp("s-1", "b")]).Error!.Value.Code);
        Assert.Equal("RuleSimplification.missing",
            RuleAreaDefinition.Create(RuleArea.Game, V("v1"), D("2000-01-01"), null, "fixture", [Simp("s-1", "a"), null!]).Error!.Value.Code);
        Assert.Equal("RuleCatalog.simplifications_missing", RuleAreaDefinition.Create(RuleArea.Game, V("v1"), D("2000-01-01"), null, "fixture", null).Error!.Value.Code);
    }

    [Fact]
    public void Versions_of_one_area_may_not_overlap_but_may_hand_over_on_the_same_day()
    {
        Assert.Equal("RuleCatalog.overlapping_versions", RuleCatalog.Create([Def(RuleArea.Game, "a", "2000-01-01"), Def(RuleArea.Game, "b", "2031-01-01")]).Error!.Value.Code);
        Assert.Equal("RuleCatalog.overlapping_versions", RuleCatalog.Create([Def(RuleArea.Game, "a", "2000-01-01", "2032-01-01"), Def(RuleArea.Game, "b", "2031-01-01")]).Error!.Value.Code);
        Assert.True(RuleCatalog.Create([Def(RuleArea.Game, "a", "2000-01-01", "2031-01-01"), Def(RuleArea.Game, "b", "2031-01-01"), Def(RuleArea.Roster, "r", "2000-01-01")]).IsSuccess);
        Assert.Equal("RuleCatalog.definition_missing", RuleCatalog.Create([Def(RuleArea.Game, "a"), null!]).Error!.Value.Code);
        Assert.Equal("RuleCatalog.definitions_missing", RuleCatalog.Create(null).Error!.Value.Code);
    }

    [Fact]
    public void Changing_the_input_lists_after_creation_does_not_change_the_catalog_or_profile()
    {
        var simplifications = new List<RuleSimplification> { Simp("s-1", "원래 설명") };
        var definitions = new List<RuleAreaDefinition> { RuleAreaDefinition.Create(RuleArea.Game, V("game-v1"), D("2000-01-01"), null, "fixture", simplifications).Value };
        var refs = new List<RuleAreaRef> { new(RuleArea.Game, V("game-v1")) };

        var catalog = RuleCatalog.Create(definitions).Value;
        var profile = RuleProfile.Create(RuleProfileId.Create("p-1").Value, "p", D("2030-03-01"), refs, catalog).Value;
        simplifications.Add(Simp("s-2", "나중에 추가"));
        definitions.Add(Def(RuleArea.Roster, "roster-v1"));
        refs.Add(new RuleAreaRef(RuleArea.Roster, V("roster-v1")));

        Assert.Single(catalog.Definitions);
        Assert.Single(catalog.Definitions[0].Simplifications);
        Assert.Single(profile.Areas);
        Assert.Equal("RuleCatalog.version_not_found", catalog.Resolve(RuleArea.Roster, V("roster-v1")).Error!.Value.Code);
    }

    [Fact]
    public void Public_collections_cannot_be_used_to_change_catalog_profile_or_definition_state()
    {
        var catalog = RuleCatalog.Create([Def(RuleArea.Game, "game-v1")]).Value;
        var profile = RuleProfile.Create(RuleProfileId.Create("p-1").Value, "p", D("2030-03-01"), [new RuleAreaRef(RuleArea.Game, V("game-v1"))], catalog).Value;

        Assert.Throws<NotSupportedException>(() => ((IList<RuleAreaDefinition>)catalog.Definitions).Add(Def(RuleArea.Roster, "r")));
        Assert.Throws<NotSupportedException>(() => ((IList<RuleAreaDefinition>)catalog.Definitions).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<RuleAreaDefinition>)profile.Areas).RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => ((IList<RuleSimplification>)catalog.Definitions[0].Simplifications).Add(Simp("x", "y")));
        // The nested definition handed out by a profile is the catalog's own immutable object: nothing on it is settable.
        Assert.All(new[] { typeof(RuleAreaDefinition), typeof(RuleSimplification), typeof(RuleProfile), typeof(RuleCatalog) },
            t => Assert.All(t.GetProperties(), p => Assert.True(p.SetMethod is null, $"{t.Name}.{p.Name} is settable")));
        Assert.Empty(typeof(RuleAreaDefinition).GetConstructors());
        Assert.Same(catalog.Definitions[0], profile.Areas[0]);
    }

    [Fact]
    public void Same_snapshot_inputs_in_any_order_give_the_same_catalog_and_profile_order()
    {
        var a = RuleCatalog.Create([Def(RuleArea.Roster, "r1"), Def(RuleArea.Game, "g1")]).Value;
        var b = RuleCatalog.Create([Def(RuleArea.Game, "g1"), Def(RuleArea.Roster, "r1")]).Value;

        Assert.Equal(a.Definitions.Select(d => (d.Area, d.Version.Value)), b.Definitions.Select(d => (d.Area, d.Version.Value)));
        var id = RuleProfileId.Create("p-1").Value;
        var p1 = RuleProfile.Create(id, "p", D("2030-03-01"), [new RuleAreaRef(RuleArea.Roster, V("r1")), new RuleAreaRef(RuleArea.Game, V("g1"))], a).Value;
        var p2 = RuleProfile.Create(id, "p", D("2030-03-01"), [new RuleAreaRef(RuleArea.Game, V("g1")), new RuleAreaRef(RuleArea.Roster, V("r1"))], b).Value;
        Assert.Equal(p1.Areas.Select(x => x.Area), p2.Areas.Select(x => x.Area));
    }

    [Fact]
    public void Valid_registered_version_resolves_and_unregistered_version_fails()
    {
        var catalog = Catalog();

        Assert.Equal("game-v2", catalog.Resolve(RuleArea.Game, V("game-v2")).Value.Version.Value);
        Assert.Equal("RuleCatalog.version_not_found", catalog.Resolve(RuleArea.Game, V("game-v3")).Error!.Value.Code);
        Assert.Equal("RuleCatalog.version_not_found", catalog.Resolve(RuleArea.Award, V("game-v1")).Error!.Value.Code);
    }

    // ---- Stadium / ParkFactor (C46) ----

    private static ParkFactorBounds Bounds() => ParkFactorBounds.Create(850, 1150).Value;

    [Fact]
    public void Stadium_id_survives_a_name_change_and_presentation_is_separate_from_factors()
    {
        var id = StadiumId.Create("stadium-01").Value;
        var stadium = Stadium.Create(id, new StadiumPresentation("별빛 스타디움", "한강시", 25000)).Value;

        var renamed = stadium.WithPresentation(stadium.Presentation with { DisplayName = "새별빛 스타디움" }).Value;

        Assert.Equal(id, renamed.Id);
        Assert.Equal("새별빛 스타디움", renamed.Presentation.DisplayName);
        Assert.DoesNotContain(typeof(Stadium).GetProperties(), p => p.PropertyType == typeof(ParkFactor) || p.PropertyType == typeof(ParkFactorProfile));
        Assert.Equal("Stadium.name_invalid", stadium.WithPresentation(stadium.Presentation with { DisplayName = " " }).Error!.Value.Code);
        Assert.Equal("Stadium.capacity_invalid", stadium.WithPresentation(stadium.Presentation with { Capacity = 0 }).Error!.Value.Code);
    }

    [Theory]
    [InlineData(850, 1000, 1150, true)]
    [InlineData(1000, 1000, 1000, true)]
    [InlineData(849, 1000, 1000, false)]
    [InlineData(1000, 1151, 1000, false)]
    [InlineData(1000, 1000, 0, false)]
    [InlineData(1000, 1000, -50, false)]
    public void Park_factors_must_stay_inside_the_cap(int hr, int hit, int xbh, bool valid)
    {
        var result = ParkFactorProfile.Create(ParkFactorProfileId.Create("pf-1").Value, StadiumId.Create("stadium-01").Value, 1, D("2030-01-01"), hr, hit, xbh, Bounds());

        Assert.Equal(valid, result.IsSuccess);
        if (!valid)
            Assert.Equal("ParkFactor.out_of_bounds", result.Error!.Value.Code);
    }

    [Fact]
    public void Cap_itself_must_be_sane_and_a_profile_needs_identity_version_and_a_cap()
    {
        Assert.False(ParkFactorBounds.Create(0, 1100).IsSuccess);
        Assert.False(ParkFactorBounds.Create(1001, 1100).IsSuccess);
        Assert.False(ParkFactorBounds.Create(900, 999).IsSuccess);
        Assert.False(ParkFactorBounds.Create(900, 3001).IsSuccess);
        var stadium = StadiumId.Create("stadium-01").Value;
        var profile = ParkFactorProfileId.Create("pf-1").Value;

        Assert.Equal("ParkFactorProfile.version_invalid", ParkFactorProfile.Create(profile, stadium, 0, D("2030-01-01"), 1000, 1000, 1000, Bounds()).Error!.Value.Code);
        Assert.Equal("ParkFactorProfile.identity_missing", ParkFactorProfile.Create(profile, default, 1, D("2030-01-01"), 1000, 1000, 1000, Bounds()).Error!.Value.Code);
        Assert.Equal("ParkFactorProfile.bounds_missing", ParkFactorProfile.Create(profile, stadium, 1, D("2030-01-01"), 1000, 1000, 1000, default).Error!.Value.Code);
    }

    [Fact]
    public void Snapshot_keeps_its_resolved_values_when_a_newer_profile_version_exists()
    {
        var stadium = StadiumId.Create("stadium-01").Value;
        var id = ParkFactorProfileId.Create("pf-1").Value;
        var v1 = ParkFactorProfile.Create(id, stadium, 1, D("2030-01-01"), 1100, 1050, 1080, Bounds()).Value;
        var v2 = ParkFactorProfile.Create(id, stadium, 2, D("2032-01-01"), 900, 950, 920, Bounds()).Value;

        var played = v1.ToSnapshot();

        Assert.Equal(1, played.Version);
        Assert.Equal(1100, played.HomeRuns.Permille);
        Assert.Equal(2, v2.ToSnapshot().Version);
        Assert.Equal(1100, played.HomeRuns.Permille);
    }

    [Fact]
    public void Park_factor_vocabulary_cannot_touch_player_ratings()
    {
        // The venue assembly graph must not reference rating types: factors are game context, not player modifiers.
        var venueTypes = new[] { typeof(Stadium), typeof(ParkFactorProfile), typeof(ParkFactorSnapshot), typeof(ParkFactorBounds), typeof(StadiumUsage) };
        var referenced = venueTypes.SelectMany(t => t.GetProperties().Select(p => p.PropertyType)).ToList();

        Assert.DoesNotContain(referenced, t => t == typeof(Ratings) || t == typeof(HitterRatings) || t == typeof(PitcherRatings) || t == typeof(SkillValue));
        Assert.DoesNotContain(typeof(Ratings).GetProperties(), p => p.PropertyType == typeof(ParkFactor));
    }

    [Fact]
    public void Stadium_usage_is_a_dated_club_reference_not_ownership()
    {
        var usage = new StadiumUsage(StadiumId.Create("stadium-01").Value, C("club-a"), D("2020-01-01"), null);

        Assert.Equal(C("club-a"), usage.Club);
        Assert.Null(usage.To);
        Assert.DoesNotContain(typeof(Stadium).GetProperties(), p => p.PropertyType == typeof(ClubId));
        Assert.DoesNotContain(typeof(Club).GetProperties(), p => p.PropertyType == typeof(StadiumId));
    }

    // ---- Battery pair (C41) ----

    [Fact]
    public void One_catcher_with_three_pitchers_gives_three_distinct_pairs()
    {
        var catcher = P("catcher-1");
        var pairs = new[] { "pitcher-1", "pitcher-2", "pitcher-3" }.Select(p => BatteryPairId.Create(catcher, P(p)).Value).ToList();

        Assert.Equal(3, pairs.Distinct().Count());
        Assert.Equal(3, pairs.Select(p => p.Key).Distinct().Count());
        Assert.All(pairs, p => Assert.Equal(catcher, p.Catcher));
        Assert.Equal("catcher-1/pitcher-2", pairs[1].Key);
    }

    [Fact]
    public void Pair_is_role_ordered_and_rejects_the_same_or_missing_player()
    {
        Assert.NotEqual(BatteryPairId.Create(P("a"), P("b")).Value, BatteryPairId.Create(P("b"), P("a")).Value);
        Assert.Equal("BatteryPairId.same_player", BatteryPairId.Create(P("a"), P("a")).Error!.Value.Code);
        Assert.Equal("BatteryPairId.identity_missing", BatteryPairId.Create(default, P("a")).Error!.Value.Code);
    }

    [Fact]
    public void Default_battery_context_is_neutral_and_invents_no_history()
    {
        var neutral = BatteryChemistry.Neutral(BatteryPairId.Create(P("c"), P("p")).Value);

        Assert.True(neutral.IsNeutral);
        Assert.Equal(0, neutral.SharedAppearances);
        Assert.Null(neutral.LastBatteryDate);
        Assert.Equal("BatteryChemistry.date_mismatch", BatteryChemistry.Create(neutral.Pair, 0, D("2030-01-01")).Error!.Value.Code);
        Assert.Equal("BatteryChemistry.date_mismatch", BatteryChemistry.Create(neutral.Pair, 3, null).Error!.Value.Code);
        Assert.Equal("BatteryChemistry.negative_appearances", BatteryChemistry.Create(neutral.Pair, -1, null).Error!.Value.Code);
        Assert.False(BatteryChemistry.Create(neutral.Pair, 3, D("2030-01-01")).Value.IsNeutral);
    }

    [Fact]
    public void Battery_chemistry_is_not_a_catcher_property_and_not_friendship()
    {
        // Pair data lives on its own type; neither the catcher's ratings nor proficiency carry a chemistry field.
        var carriers = new[] { typeof(CatcherRatings), typeof(ProficiencyProfile), typeof(Ratings), typeof(Player), typeof(PlayerState) };

        Assert.DoesNotContain(carriers.SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance)), m => m.Name.Contains("Chemistry", StringComparison.OrdinalIgnoreCase)
            || m.Name.Contains("Friend", StringComparison.OrdinalIgnoreCase));
    }
}
