using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.Application.Queries;
using StarPlayer.Application.Sessions;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Application.Tests;

/// <summary>
/// Phase 1 visibility contract: own player exact, everyone else observed, hidden development data absent from every
/// projection (including cheat sessions), and nothing a screen sends can change who counts as "self".
/// </summary>
public class PlayerVisibilityTests
{
    private static readonly string[] AllPlayerIds = Enumerable.Range(1, 10).Select(i => $"p-{i:0000}").ToArray();

    private static PlayerInfoService Service(PlayerSessionOptions? options = null, string? json = null)
    {
        var source = json is null ? new FixturePlayerWorldSource() : new FixturePlayerWorldSource(json);
        var service = new PlayerInfoService(source, options);
        Assert.True(service.IsReady, service.LoadError);
        return service;
    }

    /// <summary>
    /// "Who is my player" is trusted data (the fixture binding), not an option: tests switch it by editing the pack's
    /// <c>controlled_player_id</c>, which is exactly the path a Phase 4 career binding will take.
    /// </summary>
    private static PlayerInfoService ServiceAs(string controlledPlayerId, bool cheat = false, string? json = null)
    {
        var node = JsonNode.Parse(json ?? FixturePlayerWorldSource.ReadEmbeddedPack())!;
        node["controlled_player_id"] = controlledPlayerId;
        return Service(new PlayerSessionOptions(cheat), node.ToJsonString());
    }

    private static PlayerPublicView View(PlayerInfoService service, string id)
    {
        var result = service.GetPlayer(new PlayerQuery(id));
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.View!;
    }

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private static SkillValue TrueValue(string id, SkillKey key) =>
        new FixturePlayerWorldSource().Load().Value.Players.Single(p => p.Id.Value == id).Ratings.Skills().Single(s => s.Key == key).Value;

    // ---- own player: exact ----

    [Fact]
    public void Own_player_sees_exact_current_numbers_for_every_ability_and_state()
    {
        var view = View(Service(), "p-0001");

        Assert.True(view.IsOwnPlayer);
        Assert.All(view.BaseAbilities.Concat(view.CurrentState).Concat(view.Proficiencies), line =>
        {
            Assert.NotNull(line.Exact);
            Assert.Null(line.Grade);
            Assert.Null(line.RangeLow);
            Assert.Null(line.Confidence);
        });
        var contact = view.BaseAbilities.Single(l => l.Key == "contact");
        Assert.Equal(TrueValue("p-0001", SkillKey.Contact).Value, contact.Exact);
        Assert.Equal("58", contact.Display);
        Assert.Equal(["contact", "power", "eye", "speed", "fielding", "arm"], view.BaseAbilities.Select(l => l.Key));
    }

    [Fact]
    public void Own_pitcher_two_way_and_catcher_views_expose_all_their_exact_sets()
    {
        var pitcher = View(ServiceAs("p-0003"), "p-0003");
        var twoWay = View(ServiceAs("p-0004"), "p-0004");
        var catcher = View(ServiceAs("p-0006"), "p-0006");

        Assert.Equal(["stuff", "control", "velocity", "endurance"], pitcher.BaseAbilities.Select(l => l.Key));
        Assert.Equal(["fastball", "slider", "changeup"], pitcher.Pitches.Select(l => l.Key));
        Assert.All(pitcher.Pitches, p => Assert.NotNull(p.Exact));
        Assert.Equal(10, twoWay.BaseAbilities.Count);
        Assert.Equal("투타 겸업", twoWay.Kind);
        Assert.Equal(["contact", "power", "eye", "speed", "fielding", "arm", "receiving_framing", "blocking", "pop_time", "game_calling"], catcher.BaseAbilities.Select(l => l.Key));
        Assert.Equal(92, catcher.BaseAbilities.Single(l => l.Key == "receiving_framing").Exact);
    }

    // ---- NPC: observed ----

    [Fact]
    public void Another_player_gets_grade_range_and_confidence_and_never_an_exact_number()
    {
        var view = View(Service(), "p-0005");

        Assert.False(view.IsOwnPlayer);
        Assert.All(view.BaseAbilities.Concat(view.CurrentState).Concat(view.Proficiencies).Select(l => (l.Exact, l.Grade, l.RangeLow, l.RangeHigh, l.Confidence)), line =>
        {
            Assert.Null(line.Exact);
            Assert.NotNull(line.Grade);
            Assert.NotNull(line.RangeLow);
            Assert.NotNull(line.RangeHigh);
            Assert.NotNull(line.Confidence);
        });
        Assert.All(view.Pitches, p => Assert.Null(p.Exact));
    }

    [Fact]
    public void Observed_range_always_contains_the_real_value_but_is_never_a_single_number()
    {
        var service = Service();
        var world = new FixturePlayerWorldSource().Load().Value;

        foreach (var id in AllPlayerIds.Where(id => id != "p-0001"))
        {
            var view = View(service, id);
            var sheet = world.Players.Single(p => p.Id.Value == id);
            foreach (var (key, value) in sheet.Ratings.Skills())
            {
                var line = view.BaseAbilities.Single(l => l.Key == SnakeCase.From(key.ToString()));
                Assert.InRange(value.Value, line.RangeLow!.Value, line.RangeHigh!.Value);
                Assert.True(line.RangeHigh > line.RangeLow, $"{id} {key} range collapsed to one number");
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Observation_policy_covers_the_whole_scale_without_leaking_a_point_value(bool detailed)
    {
        var quality = detailed ? ObservationQuality.Detailed : ObservationQuality.Standard;
        for (var v = SkillValue.Min; v <= SkillValue.Max; v++)
        {
            var seen = ObservationPolicy.Observe(SkillValue.Create(v).Value, quality);

            Assert.InRange(v, seen.Low, seen.High);
            Assert.True(seen.High > seen.Low, $"value {v} produced a zero-width range");
            Assert.InRange(seen.High - seen.Low, 1, 11);
            Assert.Equal(seen, ObservationPolicy.Observe(SkillValue.Create(v).Value, quality)); // pure: no RNG, no clock
        }
    }

    [Fact]
    public void Clubmates_are_observed_in_more_detail_than_other_clubs_but_still_never_exactly()
    {
        var service = Service(); // own player is a 한강 player
        var clubmate = View(service, "p-0002");
        var rival = View(service, "p-0005");

        Assert.Equal("높음", clubmate.BaseAbilities[0].Confidence);
        Assert.Equal("보통", rival.BaseAbilities[0].Confidence);
        Assert.True(clubmate.BaseAbilities[0].RangeHigh - clubmate.BaseAbilities[0].RangeLow < rival.BaseAbilities[0].RangeHigh - rival.BaseAbilities[0].RangeLow);
        Assert.All(clubmate.BaseAbilities, l => Assert.Null(l.Exact));
    }

    [Fact]
    public void Catcher_tools_and_c_proficiency_are_separate_lines_with_different_values_in_own_and_observed_views()
    {
        var own = View(ServiceAs("p-0006"), "p-0006");
        var observed = View(Service(), "p-0006");

        foreach (var view in new[] { own, observed })
        {
            Assert.Contains(view.BaseAbilities, l => l.Key == "receiving_framing" && l.Group == "포수 능력");
            Assert.Contains(view.Proficiencies, l => l.Key == "position_catcher" && l.Group == "수비 포지션 숙련도");
            Assert.DoesNotContain(view.Proficiencies, l => l.Group == "포수 능력");
        }

        Assert.Equal(92, own.BaseAbilities.Single(l => l.Key == "receiving_framing").Exact);
        Assert.Equal(18, own.Proficiencies.Single(l => l.Key == "position_catcher").Exact);
        // The elite-tools / novice-C contrast survives observation: high band for the tools, low band for C.
        Assert.True(observed.BaseAbilities.Single(l => l.Key == "receiving_framing").RangeLow >= 80);
        Assert.True(observed.Proficiencies.Single(l => l.Key == "position_catcher").RangeHigh <= 30);
        var expertC = View(Service(), "p-0007");
        Assert.True(expertC.Proficiencies.Single(l => l.Key == "position_catcher").RangeLow >= 80);
        Assert.True(expertC.BaseAbilities.Single(l => l.Key == "receiving_framing").RangeHigh <= 60);
    }

    // ---- hidden development data ----

    [Fact]
    public void No_view_type_can_carry_hidden_fields_because_none_references_the_domain()
    {
        var viewTypes = typeof(PlayerPublicView).Assembly.GetTypes()
            .Where(t => t.Namespace == "StarPlayer.Application.Queries" && t.IsPublic && (t.Name.EndsWith("View", StringComparison.Ordinal) || t.Name is "AbilityLine" or "PitchLine" or "PlayerListItem" or "PlayerQueryResult" or "PlayerQuery"))
            .ToList();

        Assert.NotEmpty(viewTypes);
        foreach (var type in viewTypes)
        foreach (var property in type.GetProperties())
        {
            var inner = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments() : [property.PropertyType];
            Assert.All(inner, t => Assert.False(t.Namespace?.StartsWith("StarPlayer.Domain", StringComparison.Ordinal) == true, $"{type.Name}.{property.Name} exposes {t.FullName}"));
            Assert.Empty(HiddenDataGuard.FindLeaks(property.Name));
        }
    }

    [Fact]
    public void Serialized_views_for_every_viewer_and_cheat_setting_contain_no_hidden_identifiers()
    {
        foreach (var controlled in AllPlayerIds)
        foreach (var cheat in new[] { false, true })
        {
            var service = ServiceAs(controlled, cheat);
            var payload = Json(service.ListPlayers()) + Json(service.GetSummary()!) + string.Concat(AllPlayerIds.Select(id => Json(View(service, id))));

            Assert.Empty(HiddenDataGuard.FindLeaks(payload));
            Assert.DoesNotContain("development", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("growth", payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Hidden_guard_actually_detects_hidden_identifiers_and_ignores_normal_payloads()
    {
        Assert.Contains("TruePotential", HiddenDataGuard.FindLeaks("{\"true_potential\":91}"));
        Assert.Contains("GrowthType", HiddenDataGuard.FindLeaks("growth_type=late"));
        Assert.Contains("SkillCeilings", HiddenDataGuard.FindLeaks("\"SkillCeilings\":[]"));
        Assert.Contains("Potential", HiddenDataGuard.FindLeaks("potential"));
        Assert.NotEmpty(HiddenDataGuard.FindLeaks("DevelopmentProfile"));
        Assert.Empty(HiddenDataGuard.FindLeaks(Json(View(Service(), "p-0005"))));
        Assert.Empty(HiddenDataGuard.FindLeaks(null));
    }

    [Fact]
    public void Changing_every_hidden_value_changes_nothing_a_viewer_can_see()
    {
        var edited = JsonNode.Parse(FixturePlayerWorldSource.ReadEmbeddedPack())!;
        foreach (var profile in edited["development_profiles"]!.AsArray())
        {
            profile!["true_potential"] = 100;
            profile["growth_type"] = profile["growth_type"]!.GetValue<string>() == "early" ? "late" : "early";
            profile["prime_years"] = 20;
            profile["decline_onset_age"] = 50;
            foreach (var ceiling in profile["skill_ceilings"]!.AsArray())
                ceiling!["ceiling"] = 100;
        }

        foreach (var controlled in new[] { "p-0001", "p-0006" })
        {
            var original = ServiceAs(controlled);
            var changed = ServiceAs(controlled, false, edited.ToJsonString());

            Assert.Equal(Json(original.ListPlayers()), Json(changed.ListPlayers()));
            foreach (var id in AllPlayerIds)
                Assert.Equal(Json(View(original, id)), Json(View(changed, id)));
        }
    }

    [Fact]
    public void Cheat_mode_changes_neither_exactness_nor_hidden_exposure()
    {
        var normal = Service(new PlayerSessionOptions(false));
        var cheat = Service(new PlayerSessionOptions(true));

        foreach (var id in AllPlayerIds)
            Assert.Equal(Json(View(normal, id)), Json(View(cheat, id)));
        Assert.All(AllPlayerIds.Where(id => id != "p-0001"), id => Assert.All(View(cheat, id).BaseAbilities, l => Assert.Null(l.Exact)));
        Assert.True(cheat.GetSummary()!.CheatMode);
        Assert.False(normal.GetSummary()!.CheatMode);
    }

    // ---- nothing a client sends can change authority ----

    [Fact]
    public void A_query_carries_only_an_id_so_there_is_no_flag_to_tamper_with()
    {
        var properties = typeof(PlayerQuery).GetProperties();

        Assert.Equal(["PlayerId"], properties.Select(p => p.Name));
        Assert.Equal(typeof(string), properties[0].PropertyType);
    }

    [Fact]
    public void Hostile_client_payload_with_permission_flags_still_yields_an_observed_view()
    {
        const string hostile = "{\"PlayerId\":\"p-0005\",\"IsSelf\":true,\"IsOwnPlayer\":true,\"CheatMode\":true,\"Viewer\":\"p-0001\",\"ViewerContext\":{\"ControlledPlayer\":\"p-0005\"},\"Exact\":true,\"RevealExact\":true,\"Admin\":true}";
        var query = JsonSerializer.Deserialize<PlayerQuery>(hostile)!;

        var view = View(Service(), query.PlayerId);

        Assert.False(view.IsOwnPlayer);
        Assert.All(view.BaseAbilities, l => Assert.Null(l.Exact));
        Assert.DoesNotMatch(@"""Exact"":\d", Json(view)); // no exact value anywhere in the payload
    }

    [Fact]
    public void Viewer_context_cannot_be_built_or_changed_from_outside_the_application_assembly()
    {
        var context = typeof(ViewerContext);

        Assert.Empty(typeof(PlayerInfoService).GetConstructors(BindingFlags.Public | BindingFlags.Instance)); // only Infrastructure's composition can build one

        Assert.False(context.IsPublic);
        Assert.Empty(context.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(context.GetProperties(), p => Assert.True(p.SetMethod is null, $"{p.Name} is settable"));
        // The service's public surface speaks DTOs and strings only: no ViewerContext, no Domain ids, no bool permission parameter.
        var surface = typeof(PlayerInfoService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (var method in surface)
        {
            Assert.DoesNotContain(method.GetParameters(), p => p.ParameterType == typeof(bool) || p.ParameterType == typeof(ViewerContext) || p.ParameterType == typeof(PlayerId));
            Assert.NotEqual(typeof(ViewerContext), method.ReturnType);
        }
    }

    [Fact]
    public void A_same_name_player_or_a_look_alike_id_is_never_treated_as_self()
    {
        var service = Service(); // own player p-0001 "박민수"; p-0005 is another "박민수"
        var namesake = View(service, "p-0005");

        Assert.Equal("박민수", namesake.DisplayName);
        Assert.False(namesake.IsOwnPlayer);
        Assert.Null(namesake.BaseAbilities[0].Exact);
        foreach (var lookalike in new[] { " p-0001", "p-0001 ", "P-0001", "p-0001\n", "p-00001", "", "박민수", "p-0001;p-0005"})
        {
            var result = service.GetPlayer(new PlayerQuery(lookalike));
            Assert.False(result.IsSuccess, $"'{lookalike}' resolved to a player");
            Assert.Equal("PlayerInfo.player_not_found", result.ErrorCode);
        }

        Assert.Equal("PlayerInfo.player_not_found", service.GetPlayer(new PlayerQuery(null!)).ErrorCode);
        Assert.Equal("PlayerInfo.player_not_found", service.GetPlayer(null!).ErrorCode);
    }

    [Fact]
    public void Exactly_one_listed_player_is_the_viewers_own_and_ordering_ignores_ratings()
    {
        var list = Service().ListPlayers();

        Assert.Equal(10, list.Count);
        Assert.Single(list, p => p.IsOwnPlayer);
        Assert.Equal("p-0001", list.Single(p => p.IsOwnPlayer).PlayerId);
        Assert.Equal(list.OrderBy(p => p.ClubName == "한강 스타즈" ? 0 : 1).Select(p => p.ClubName), list.Select(p => p.ClubName));
        Assert.Equal(list.Where(p => p.ClubName == "한강 스타즈").Select(p => p.PlayerId).Order(StringComparer.Ordinal), list.Where(p => p.ClubName == "한강 스타즈").Select(p => p.PlayerId));
    }

    // ---- session options / load failure ----

    [Fact]
    public void Controlled_player_comes_from_the_trusted_session_and_switching_it_swaps_who_is_exact()
    {
        var asPitcher = ServiceAs("p-0003");

        Assert.True(View(asPitcher, "p-0003").IsOwnPlayer);
        Assert.NotNull(View(asPitcher, "p-0003").BaseAbilities[0].Exact);
        Assert.False(View(asPitcher, "p-0001").IsOwnPlayer);
        Assert.Null(View(asPitcher, "p-0001").BaseAbilities[0].Exact);
        Assert.Equal("정우진", asPitcher.GetSummary()!.OwnPlayerName);
    }

    [Fact]
    public void Session_options_carry_no_identity_and_tampered_options_cannot_change_who_is_my_player()
    {
        // The only option is the cheat flag; there is no member through which an id could be supplied.
        Assert.Equal(["CheatMode"], typeof(PlayerSessionOptions).GetProperties().Where(p => p.DeclaringType == typeof(PlayerSessionOptions) && p.Name != "EqualityContract").Select(p => p.Name));
        const string hostile = "{\"CheatMode\":true,\"ControlledPlayerId\":\"p-0005\",\"ControlledPlayer\":\"p-0005\",\"PlayerId\":\"p-0005\"}";
        var tampered = JsonSerializer.Deserialize<PlayerSessionOptions>(hostile)!;

        var viaPublicComposition = PlayerInfoComposition.CreateFictionalPhase1(tampered);

        Assert.True(viaPublicComposition.IsReady);
        Assert.Equal("박민수", viaPublicComposition.GetSummary()!.OwnPlayerName);
        Assert.True(View(viaPublicComposition, "p-0001").IsOwnPlayer); // the pack's binding, not the payload
        Assert.False(View(viaPublicComposition, "p-0005").IsOwnPlayer);
        Assert.All(View(viaPublicComposition, "p-0005").BaseAbilities, l => Assert.Null(l.Exact));
        Assert.True(viaPublicComposition.GetSummary()!.CheatMode); // cheat may be set; it still reveals nothing extra
    }

    [Theory]
    [InlineData("p-9999")]
    [InlineData("bad id")]
    public void Unknown_or_malformed_controlled_player_in_the_binding_leaves_the_service_not_ready_and_exposes_nothing(string controlled)
    {
        var node = JsonNode.Parse(FixturePlayerWorldSource.ReadEmbeddedPack())!;
        node["controlled_player_id"] = controlled;

        var service = new PlayerInfoService(new FixturePlayerWorldSource(node.ToJsonString()));

        Assert.False(service.IsReady);
        Assert.NotNull(service.LoadError);
        Assert.Empty(service.ListPlayers());
        Assert.Null(service.GetSummary());
        Assert.Equal("PlayerInfo.not_ready", service.GetPlayer(new PlayerQuery("p-0001")).ErrorCode);
    }

    [Fact]
    public void A_corrupt_pack_leaves_the_service_not_ready_with_a_korean_message_instead_of_throwing()
    {
        var service = new PlayerInfoService(new FixturePlayerWorldSource("{ broken"));

        Assert.False(service.IsReady);
        Assert.Contains("불러오지 못했습니다", service.LoadError);
        Assert.Empty(service.ListPlayers());
    }

    // ---- projection details ----

    [Fact]
    public void View_shows_age_club_current_number_and_separate_status_axes()
    {
        var service = Service();
        var own = View(service, "p-0001");
        var renumbered = View(service, "p-0002");
        var developmental = View(service, "p-0006");
        var twoWay = View(service, "p-0004");

        Assert.Equal(23, own.Age);
        Assert.Equal("2006-04-12", own.BirthDate);
        Assert.Equal("한강 스타즈", own.ClubName);
        Assert.Equal("7", own.CurrentJerseyNumber);
        Assert.Equal("2024-10-01", own.JerseySince);
        Assert.Equal("6", renumbered.CurrentJerseyNumber);
        Assert.Equal("2028-03-01", renumbered.JerseySince);
        Assert.Equal("육성선수", developmental.Registration);
        Assert.Equal("2군", developmental.League);
        Assert.Equal("육성선수 계약", developmental.EntryPath);
        Assert.Equal("고교", developmental.Origin);
        Assert.Equal("해외파", twoWay.Origin);
        Assert.Equal("기타 진입", twoWay.EntryPath);
        Assert.Equal("1군", own.League);
        Assert.Equal("우투좌타", own.Handedness);
    }

    [Fact]
    public void Base_ability_state_and_proficiency_are_separate_groups_in_the_view()
    {
        var view = View(Service(), "p-0001");

        Assert.DoesNotContain(view.BaseAbilities, l => l.Group is "현재 상태" or "수비 포지션 숙련도" or "역할 숙련도");
        Assert.All(view.CurrentState, l => Assert.Equal("현재 상태", l.Group));
        Assert.Equal(["condition", "fatigue", "sharpness", "stress", "satisfaction", "recent_form"], view.CurrentState.Select(l => l.Key));
        Assert.Contains(view.Proficiencies, l => l.Key == "position_center_field");
        Assert.Contains(view.Proficiencies, l => l.Key == "role_batter");
        Assert.Equal("중견수", view.PreferredPosition);
    }

    [Fact]
    public void Every_domain_enum_value_has_a_nonblank_korean_label()
    {
        static void All<T>(Func<T, string> label) where T : struct, Enum
        {
            foreach (var value in Enum.GetValues<T>())
                Assert.False(string.IsNullOrWhiteSpace(label(value)), $"{typeof(T).Name}.{value} has no label");
        }

        All<SkillKey>(KoreanLabels.Skill);
        All<SkillKey>(KoreanLabels.SkillGroup);
        All<PitchType>(KoreanLabels.Pitch);
        All<StateKey>(KoreanLabels.State);
        All<FieldPosition>(KoreanLabels.Position);
        All<PlayerRole>(KoreanLabels.Role);
        All<PlayerKind>(KoreanLabels.Kind);
        All<PersonalityTrait>(KoreanLabels.Personality);
        All<OriginType>(KoreanLabels.Origin);
        All<RegistrationStatus>(KoreanLabels.Registration);
        All<CareerStatus>(KoreanLabels.Career);
        Assert.Equal("정식선수", KoreanLabels.Registration(RegistrationStatus.Registered));
        foreach (var path in Enum.GetValues<EntryPath>())
            Assert.NotEmpty(KoreanLabels.Entry(path));
        foreach (var league in Enum.GetValues<LeagueAssignment>())
            Assert.NotEmpty(KoreanLabels.League(league));
        Assert.NotEmpty(KoreanLabels.Entry(null));
        Assert.NotEmpty(KoreanLabels.League(null));
    }

    [Fact]
    public void Player_facing_text_in_views_is_korean_not_english_enum_names()
    {
        var view = View(Service(), "p-0004");
        var text = string.Join(" ", view.Kind, view.Handedness, view.Registration, view.League, view.Origin, view.EntryPath, view.PreferredPosition,
            string.Join(" ", view.BaseAbilities.Select(l => l.Label + l.Group)), string.Join(" ", view.Pitches.Select(l => l.Label)),
            string.Join(" ", view.Personality), string.Join(" ", view.Proficiencies.Select(l => l.Label)));

        Assert.DoesNotMatch("[A-Za-z]{3,}", text);
    }

    [Fact]
    public void Summary_names_the_data_as_fictional_and_the_observation_model_version()
    {
        var summary = Service().GetSummary()!;

        Assert.Contains("가상", summary.Label);
        Assert.Equal("2030-03-01", summary.AsOfDate);
        Assert.Equal("observation-v0", summary.ObservationModel);
        Assert.Equal(10, summary.PlayerCount);
        Assert.Equal(2, summary.ClubCount);
    }
}
