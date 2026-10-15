using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarPlayer.Application.Ports;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Domain.Teams;

namespace StarPlayer.Infrastructure.Fixtures;

/// <summary>
/// Reads and writes the Phase 1 fictional pack (JSON schema v1, and v2 since Phase 3-D). Reading maps every DTO through the Domain factories, so
/// an illegal value is reported with its location (<c>players[3] p-0004: SkillValue.out_of_range ...</c>) and no partial
/// world is returned. Writing is the exact inverse, which is what the round-trip tests rely on.
/// <para>
/// <b>Versions (D-T1).</b> The <c>format_version</c> is read first and chooses a strict DTO: 1 is <see cref="WorldDto"/> (no new field is accepted), 2 is <see cref="WorldV2Dto"/> (adds
/// the optional hidden <c>batter_zone_tendencies</c>), anything else is <c>Fixture.version_unsupported</c> and is never converted. The shared <see cref="Options"/> are not changed. The world
/// remembers its <see cref="WorldFormat"/>, so v1 stays v1 and v2 omitted / empty / filled each write back in their own shape.
/// </para>
/// <para>
/// <b>Sparse and corrupt (D-T4).</b> An absent or empty collection means every player is neutral; a player without a row is neutral. Anything wrong in a row is an error, never repaired
/// (no clamp, no normalisation, no skipped row). Error text names the row, never the values.
/// </para>
/// </summary>
internal static class WorldJson
{
    public const int FormatVersion = 1;
    public const int FormatVersionV2 = 2;
    private const string TendencyField = "batter_zone_tendencies";

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep Korean readable in the file
    };

    private sealed class FixtureException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    public static Result<PlayerWorld> Read(string json)
    {
        try
        {
            var dto = Deserialize(json)
                ?? throw new FixtureException("Fixture.json_invalid", "JSON 내용이 비어 있습니다.");
            return Result<PlayerWorld>.Success(ToDomain(dto));
        }
        catch (FixtureException exception)
        {
            return Result<PlayerWorld>.Failure(new Error(exception.Code, exception.Message));
        }
        catch (JsonException exception)
        {
            return Result<PlayerWorld>.Failure(new Error("Fixture.json_invalid", $"JSON 형식이 올바르지 않습니다: {exception.Message}"));
        }
    }

    /// <summary>Reads <c>format_version</c> first and deserializes with the matching strict DTO. A non-object root or a non-integer version falls to the v1 path, whose own errors apply.</summary>
    private static WorldDto? Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("format_version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
            return JsonSerializer.Deserialize<WorldDto>(json, Options);
        if (number == FormatVersionV2)
        {
            if (root.TryGetProperty(TendencyField, out var field) && field.ValueKind == JsonValueKind.Null)
                throw new FixtureException("Fixture.json_invalid", $"{TendencyField}: null은 허용되지 않습니다(생략하거나 배열로 쓰세요).");
            return JsonSerializer.Deserialize<WorldV2Dto>(json, Options);
        }

        if (number != FormatVersion)
            throw new FixtureException("Fixture.version_unsupported", $"지원하지 않는 픽스처 형식 버전입니다: {number} (지원: {FormatVersion}, {FormatVersionV2})");
        return JsonSerializer.Deserialize<WorldDto>(json, Options);
    }

    /// <summary>Writes the world in the format it was read from. A v1 world that holds tendencies is a programming error (nothing is dropped silently).</summary>
    public static string Write(PlayerWorld world)
    {
        var v1 = ToDto(world);
        if (world.Format.Version == FormatVersion)
        {
            if (world.Format.BatterTendenciesPresent || world.BatterTendencies.Count > 0)
                throw new InvalidOperationException("v1 형식의 세계는 타자 코스 성향을 담을 수 없습니다.");
            return JsonSerializer.Serialize(v1, Options);
        }

        if (world.Format.Version != FormatVersionV2)
            throw new InvalidOperationException($"지원하지 않는 세계 형식 버전입니다: {world.Format.Version}");
        var v2 = new WorldV2Dto(v1);
        if (world.Format.BatterTendenciesPresent)
        {
            var rows = new List<BatterZoneTendencyDto?>(world.BatterTendencies.Count); // a loop, not a lambda: no compiler-generated member may name the hidden entry type
            foreach (var e in world.BatterTendencies.Entries)
                rows.Add(new BatterZoneTendencyDto { PlayerId = e.Player.Value, ZoneModel = e.Tendency.Model.Value, ReferenceId = e.Tendency.ReferenceId, Values = e.Tendency.Values.ToList() });
            v2.BatterZoneTendencies = rows;
        }
        else if (world.BatterTendencies.Count > 0)
            throw new InvalidOperationException("타자 코스 성향이 있는데 형식 정보에는 목록이 없다고 되어 있습니다.");
        return JsonSerializer.Serialize(v2, Options);
    }

    // ---------------------------------------------------------------- JSON -> Domain

    private static T Ok<T>(Result<T> result, string where) =>
        result.IsSuccess ? result.Value : throw new FixtureException(result.Error!.Value.Code, $"{where}: {result.Error.Value.Message}");

    private static void Ok(Result result, string where)
    {
        if (!result.IsSuccess)
            throw new FixtureException(result.Error!.Value.Code, $"{where}: {result.Error.Value.Message}");
    }

    private static T Id<T>(string? text, string where) where T : struct, IStringId<T> => Ok(T.Create(text), where);

    private static SkillValue Skill(int value, string where) => Ok(SkillValue.Create(value, where), where);

    private static T EnumOf<T>(string? text, string where) where T : struct, Enum
    {
        foreach (var value in System.Enum.GetValues<T>())
            if (SnakeCase.From(value.ToString()) == text)
                return value;
        throw new FixtureException("Fixture.enum_invalid", $"{where}: '{text}'은(는) {typeof(T).Name}의 올바른 값이 아닙니다.");
    }

    private static T? OptionalEnum<T>(string? text, string where) where T : struct, Enum =>
        text is null ? null : EnumOf<T>(text, where);

    private static PlayerWorld ToDomain(WorldDto dto)
    {
        var v2 = dto as WorldV2Dto;
        if (dto.FormatVersion != (v2 is null ? FormatVersion : FormatVersionV2))
            throw new FixtureException("Fixture.version_unsupported", $"지원하지 않는 픽스처 형식 버전입니다: {dto.FormatVersion} (지원: {FormatVersion}, {FormatVersionV2})");

        var catalog = Ok(RuleCatalog.Create(dto.Rules.Catalog.Select((d, i) => Ok(RuleAreaDefinition.Create(
            EnumOf<RuleArea>(d.Area, $"rules.catalog[{i}].area"), Id<RuleVersionId>(d.Version, $"rules.catalog[{i}].version"),
            d.EffectiveFrom, d.EffectiveTo, d.Provenance,
            d.Simplifications.Select((x, j) => Ok(RuleSimplification.Create(x.Id, x.Description), $"rules.catalog[{i}].simplifications[{j}]")).ToList()), $"rules.catalog[{i}]")).ToList()), "rules.catalog");
        var profileDto = dto.Rules.Profile;
        var profile = Ok(RuleProfile.Create(Id<RuleProfileId>(profileDto.Id, "rules.profile.id"), profileDto.Name, profileDto.EffectiveOn,
            profileDto.Areas.Select((a, i) => new RuleAreaRef(EnumOf<RuleArea>(a.Area, $"rules.profile.areas[{i}].area"), Id<RuleVersionId>(a.Version, $"rules.profile.areas[{i}].version"))).ToList(),
            catalog), "rules.profile");

        var bounds = Ok(ParkFactorBounds.Create(dto.ParkFactorBounds.MinPermille, dto.ParkFactorBounds.MaxPermille), "park_factor_bounds");

        var clubs = dto.Clubs.Select((c, i) => Ok(Club.Create(Id<ClubId>(c.ClubId, $"clubs[{i}].club_id"), c.Name, c.ShortName), $"clubs[{i}]")).ToList();
        Unique(clubs.Select(c => c.Id.Value), "clubs");
        var clubIds = clubs.Select(c => c.Id).ToHashSet();

        var stadiums = dto.Stadiums.Select((s, i) => Ok(Stadium.Create(Id<StadiumId>(s.StadiumId, $"stadiums[{i}].stadium_id"),
            new StadiumPresentation(s.DisplayName, s.City, s.Capacity)), $"stadiums[{i}]")).ToList();
        Unique(stadiums.Select(s => s.Id.Value), "stadiums");
        var stadiumIds = stadiums.Select(s => s.Id).ToHashSet();

        var factorProfiles = dto.ParkFactorProfiles.Select((p, i) =>
        {
            var stadium = Id<StadiumId>(p.StadiumId, $"park_factor_profiles[{i}].stadium_id");
            if (!stadiumIds.Contains(stadium))
                throw new FixtureException("Fixture.unknown_stadium", $"park_factor_profiles[{i}]: 구장 {stadium}이(가) 없습니다.");
            return Ok(ParkFactorProfile.Create(Id<ParkFactorProfileId>(p.ProfileId, $"park_factor_profiles[{i}].profile_id"), stadium, p.Version,
                p.EffectiveFrom, p.HomeRunPermille, p.HitPermille, p.ExtraBaseHitPermille, bounds), $"park_factor_profiles[{i}]");
        }).ToList();
        Unique(factorProfiles.Select(p => $"{p.Id}@{p.Version}"), "park_factor_profiles");

        var usages = dto.StadiumUsage.Select((u, i) =>
        {
            var usage = new StadiumUsage(Id<StadiumId>(u.StadiumId, $"stadium_usage[{i}].stadium_id"), Id<ClubId>(u.ClubId, $"stadium_usage[{i}].club_id"), u.From, u.To);
            if (!stadiumIds.Contains(usage.Stadium) || !clubIds.Contains(usage.Club) || (usage.To is { } to && to <= usage.From))
                throw new FixtureException("Fixture.stadium_usage_invalid", $"stadium_usage[{i}]: 구장/구단이 없거나 기간이 올바르지 않습니다.");
            return usage;
        }).ToList();

        var players = dto.Players.Select((p, i) => ToSheet(p, i, clubIds)).ToList();
        Unique(players.Select(p => p.Id.Value), "players");
        var byId = players.ToDictionary(p => p.Id);

        var development = dto.DevelopmentProfiles.Select((d, i) => ToDevelopment(d, i, byId)).ToList();
        Unique(development.Select(d => d.PlayerId.Value), "development_profiles");
        foreach (var missing in players.Where(p => development.All(d => d.PlayerId != p.Id)))
            throw new FixtureException("Fixture.development_missing", $"development_profiles: {missing.Id} 선수의 성장 프로필이 없습니다.");

        var tendencies = ToProfiles(v2?.BatterZoneTendencies, byId);

        var range = Ok(JerseyNumberRange.Create(Ok(JerseyNumber.Create(dto.Jersey.Min), "jersey.min"), Ok(JerseyNumber.Create(dto.Jersey.Max), "jersey.max")), "jersey");
        var retired = dto.Jersey.Retired.Select((r, i) => new RetiredNumberRestriction(
            Id<ClubId>(r.ClubId, $"jersey.retired[{i}].club_id"), Ok(JerseyNumber.Create(r.Number), $"jersey.retired[{i}].number"), r.EffectiveFrom)).ToList();
        var assignments = dto.JerseyAssignments.Select((a, i) =>
        {
            var player = Id<PlayerId>(a.PlayerId, $"jersey_assignments[{i}].player_id");
            var club = Id<ClubId>(a.ClubId, $"jersey_assignments[{i}].club_id");
            if (!byId.ContainsKey(player) || !clubIds.Contains(club))
                throw new FixtureException("Fixture.unknown_reference", $"jersey_assignments[{i}]: 선수 {player} 또는 구단 {club}이(가) 없습니다.");
            return new JerseyNumberAssignment(player, club, Ok(JerseyNumber.Create(a.Number), $"jersey_assignments[{i}].number"),
                a.EffectiveFrom, a.EffectiveTo, EnumOf<AssignmentReason>(a.Reason, $"jersey_assignments[{i}].reason"));
        }).ToList();
        var ledger = Ok(JerseyNumberLedger.Create(range, retired, assignments), "jersey_assignments");

        var batteries = dto.BatteryPairs.Select((b, i) =>
        {
            var catcher = Id<PlayerId>(b.CatcherId, $"battery_pairs[{i}].catcher_id");
            var pitcher = Id<PlayerId>(b.PitcherId, $"battery_pairs[{i}].pitcher_id");
            if (!byId.TryGetValue(catcher, out var c) || c.Ratings.Catcher is null)
                throw new FixtureException("Fixture.battery_catcher_invalid", $"battery_pairs[{i}]: {catcher}은(는) 포수 능력치가 있는 선수가 아닙니다.");
            if (!byId.TryGetValue(pitcher, out var p) || p.Ratings.Pitcher is null)
                throw new FixtureException("Fixture.battery_pitcher_invalid", $"battery_pairs[{i}]: {pitcher}은(는) 투수 능력치가 있는 선수가 아닙니다.");
            return Ok(BatteryChemistry.Create(Ok(BatteryPairId.Create(catcher, pitcher), $"battery_pairs[{i}]"), b.SharedAppearances, b.LastBatteryDate), $"battery_pairs[{i}]");
        }).ToList();
        Unique(batteries.Select(b => b.Pair.Key), "battery_pairs");

        var controlled = Id<PlayerId>(dto.ControlledPlayerId, "controlled_player_id");
        if (!byId.ContainsKey(controlled))
            throw new FixtureException("Fixture.unknown_reference", $"controlled_player_id: 선수 {controlled}이(가) 없습니다.");

        return new PlayerWorld(dto.Label, dto.AsOfDate, controlled, clubs, stadiums, bounds, factorProfiles, usages, catalog, profile, players, development, ledger, batteries,
            tendencies, v2 is null ? WorldFormat.V1 : new WorldFormat(FormatVersionV2, v2.BatterZoneTendencies is not null));
    }

    /// <summary>
    /// Validates the v2 rows and builds the hidden profile set (the one trusted conversion that names the hidden type; HiddenTendencyBoundaryTests allows exactly this member). Every
    /// row must be well formed, name a known hitter once and carry a registered model and reference whose weighted sum is zero; nothing is repaired or skipped.
    /// </summary>
    private static BatterZoneProfiles ToProfiles(List<BatterZoneTendencyDto?>? rows, Dictionary<PlayerId, PlayerSheet> byId)
    {
        if (rows is null)
            return BatterZoneProfiles.Empty;
        var entries = new List<BatterZoneProfileEntry>(rows.Count);
        var ids = new List<string>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var where = $"{TendencyField}[{i}]";
            var row = rows[i] ?? throw new FixtureException("Fixture.json_invalid", $"{where}: 항목이 null입니다.");
            if (row.PlayerId is null) throw new FixtureException("Fixture.field_missing", $"{where}.player_id: 값이 없습니다.");
            if (row.ZoneModel is null) throw new FixtureException("Fixture.field_missing", $"{where}.zone_model: 값이 없습니다.");
            if (row.ReferenceId is null) throw new FixtureException("Fixture.field_missing", $"{where}.reference_id: 값이 없습니다.");
            if (row.Values is null) throw new FixtureException("Fixture.field_missing", $"{where}.values: 값이 없습니다.");

            var player = Id<PlayerId>(row.PlayerId, where + ".player_id");
            where += $" {player}";
            if (!byId.TryGetValue(player, out var sheet))
                throw new FixtureException("Fixture.unknown_reference", $"{where}: 해당 선수가 없습니다.");
            if (sheet.Ratings.Hitter is null)
                throw new FixtureException("Fixture.tendency_batter_invalid", $"{where}: 타격 능력치가 있는 선수가 아닙니다.");
            Ok(ZoneModelVersion.Create(row.ZoneModel), where + ".zone_model");
            var reference = Ok(ZoneReferenceDistribution.Resolve(row.ReferenceId), where + ".reference_id");
            entries.Add(new BatterZoneProfileEntry(player, Ok(BatterZoneTendency.Create(row.Values, reference), where)));
            ids.Add(player.Value);
        }

        Unique(ids, TendencyField);
        return Ok(BatterZoneProfiles.Create(entries), TendencyField);
    }

    private static void Unique(IEnumerable<string> keys, string where)
    {
        var duplicate = keys.GroupBy(k => k).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new FixtureException("Fixture.duplicate_id", $"{where}: '{duplicate.Key}'이(가) 중복되었습니다.");
    }

    private static PlayerSheet ToSheet(PlayerDto p, int index, HashSet<ClubId> clubIds)
    {
        var where = $"players[{index}] {p.PlayerId}";
        var id = Id<PlayerId>(p.PlayerId, where);
        var player = Ok(Player.Create(id, p.DisplayName, p.BirthDate, EnumOf<ThrowingHand>(p.Throws, where + ".throws"), EnumOf<BattingSide>(p.Bats, where + ".bats"),
            Ok(BodyMeasurements.Create(p.HeightCm, p.WeightKg), where), p.Personality.Select(t => EnumOf<PersonalityTrait>(t, where + ".personality")).ToList(), p.Biography), where);

        var hitter = p.Ratings.Hitter is { } h
            ? new HitterRatings(Skill(h.Contact, where + ".contact"), Skill(h.Power, where + ".power"), Skill(h.Eye, where + ".eye"),
                Skill(h.Speed, where + ".speed"), Skill(h.Fielding, where + ".fielding"), Skill(h.Arm, where + ".arm"))
            : null;
        var pitcher = p.Ratings.Pitcher is { } pt
            ? new PitcherRatings(Skill(pt.Stuff, where + ".stuff"), Skill(pt.Control, where + ".control"), Skill(pt.Velocity, where + ".velocity"), Skill(pt.Endurance, where + ".endurance"),
                Ok(PitchRepertoire.Create(pt.Pitches.Select(x => new PitchRating(EnumOf<PitchType>(x.Type, where + ".pitches"), Skill(x.Grade, where + ".pitches")))), where))
            : null;
        var catcher = p.Ratings.Catcher is { } c
            ? new CatcherRatings(Skill(c.ReceivingFraming, where + ".receiving_framing"), Skill(c.Blocking, where + ".blocking"), Skill(c.PopTime, where + ".pop_time"), Skill(c.GameCalling, where + ".game_calling"))
            : null;
        var ratings = Ok(Ratings.Create(hitter, pitcher, catcher), where);

        var s = p.State;
        var state = new PlayerState(Skill(s.Condition, where + ".condition"), Skill(s.Fatigue, where + ".fatigue"), Skill(s.Sharpness, where + ".sharpness"),
            Skill(s.Stress, where + ".stress"), Skill(s.Satisfaction, where + ".satisfaction"), Skill(s.RecentForm, where + ".recent_form"));

        var proficiency = Ok(ProficiencyProfile.Create(EnumOf<FieldPosition>(p.Proficiency.PreferredPosition, where + ".preferred_position"),
            p.Proficiency.Positions.Select(x => (EnumOf<FieldPosition>(x.Position, where + ".positions"), Skill(x.Value, where + ".positions"))),
            p.Proficiency.Roles.Select(x => (EnumOf<PlayerRole>(x.Role, where + ".roles"), Skill(x.Value, where + ".roles")))), where);

        var st = p.Status;
        var affiliations = st.Affiliations.Select(a => new ClubAffiliation(Id<ClubId>(a.ClubId, where + ".affiliations"), a.From, a.To)).ToList();
        if (affiliations.Any(a => !clubIds.Contains(a.Club)))
            throw new FixtureException("Fixture.unknown_reference", $"{where}: 소속 구단이 구단 목록에 없습니다.");
        var status = Ok(PlayerStatus.Create(
            st.Origins.Select(o => new OriginRecord(EnumOf<OriginType>(o.Type, where + ".origins"), o.OriginTeamId is null ? null : Id<AmateurTeamId>(o.OriginTeamId, where + ".origins"), o.From, o.To)).ToList(),
            OptionalEnum<EntryPath>(st.EntryPath, where + ".entry_path"), EnumOf<RegistrationStatus>(st.Registration, where + ".registration"),
            OptionalEnum<LeagueAssignment>(st.League, where + ".league"), affiliations, EnumOf<CareerStatus>(st.Career, where + ".career")), where);

        return Ok(PlayerSheet.Create(player, ratings, state, proficiency, status), where);
    }

    private static DevelopmentProfile ToDevelopment(DevelopmentDto d, int index, Dictionary<PlayerId, PlayerSheet> byId)
    {
        var where = $"development_profiles[{index}] {d.PlayerId}";
        var id = Id<PlayerId>(d.PlayerId, where);
        if (!byId.TryGetValue(id, out var sheet))
            throw new FixtureException("Fixture.unknown_reference", $"{where}: 해당 선수가 없습니다.");
        var profile = Ok(DevelopmentProfile.Create(id, Skill(d.TruePotential, where), EnumOf<GrowthType>(d.GrowthType, where), d.PrimeYears, d.DeclineOnsetAge,
            d.SkillCeilings.Select(c => (EnumOf<SkillKey>(c.Skill, where), Skill(c.Ceiling, where)))), where);
        Ok(profile.CheckAgainst(sheet.Ratings), where);
        return profile;
    }

    // ---------------------------------------------------------------- Domain -> JSON

    private static string Name<T>(T value) where T : struct, Enum => SnakeCase.From(value.ToString()!);

    private static WorldDto ToDto(PlayerWorld w) => new()
    {
        FormatVersion = FormatVersion,
        Label = w.Label,
        AsOfDate = w.AsOfDate,
        ControlledPlayerId = w.ControlledPlayer.Value,
        Rules = new RulesDto
        {
            Catalog = w.RuleCatalog.Definitions.Select(d => new RuleAreaDefinitionDto
            {
                Area = Name(d.Area), Version = d.Version.Value, EffectiveFrom = d.EffectiveFrom, EffectiveTo = d.EffectiveTo,
                Provenance = d.Provenance, Simplifications = d.Simplifications.Select(x => new SimplificationDto { Id = x.Id, Description = x.Description }).ToList(),
            }).ToList(),
            Profile = new RuleProfileDto
            {
                Id = w.RuleProfile.Id.Value, Name = w.RuleProfile.Name, EffectiveOn = w.RuleProfile.EffectiveOn,
                Areas = w.RuleProfile.Areas.Select(a => new RuleAreaRefDto { Area = Name(a.Area), Version = a.Version.Value }).ToList(),
            },
        },
        ParkFactorBounds = new BoundsDto { MinPermille = w.ParkFactorBounds.MinPermille, MaxPermille = w.ParkFactorBounds.MaxPermille },
        Jersey = new JerseyRulesDto
        {
            Min = w.Jerseys.AllowedRange.Min.Value, Max = w.Jerseys.AllowedRange.Max.Value,
            Retired = w.Jerseys.Restrictions.Select(r => new RetiredNumberDto { ClubId = r.Club.Value, Number = r.Number.Value, EffectiveFrom = r.EffectiveFrom }).ToList(),
        },
        Clubs = w.Clubs.Select(c => new ClubDto { ClubId = c.Id.Value, Name = c.Name, ShortName = c.ShortName }).ToList(),
        Stadiums = w.Stadiums.Select(s => new StadiumDto { StadiumId = s.Id.Value, DisplayName = s.Presentation.DisplayName, City = s.Presentation.City, Capacity = s.Presentation.Capacity }).ToList(),
        ParkFactorProfiles = w.ParkFactorProfiles.Select(p => new ParkFactorProfileDto
        {
            ProfileId = p.Id.Value, StadiumId = p.Stadium.Value, Version = p.Version, EffectiveFrom = p.EffectiveFrom,
            HomeRunPermille = p.HomeRuns.Permille, HitPermille = p.Hits.Permille, ExtraBaseHitPermille = p.ExtraBaseHits.Permille,
        }).ToList(),
        StadiumUsage = w.StadiumUsages.Select(u => new StadiumUsageDto { StadiumId = u.Stadium.Value, ClubId = u.Club.Value, From = u.From, To = u.To }).ToList(),
        Players = w.Players.Select(ToDto).ToList(),
        DevelopmentProfiles = w.Development.Select(d => new DevelopmentDto
        {
            PlayerId = d.PlayerId.Value, TruePotential = d.TruePotential.Value, GrowthType = Name(d.GrowthType), PrimeYears = d.PrimeYears, DeclineOnsetAge = d.DeclineOnsetAge,
            SkillCeilings = d.SkillCeilings.Select(c => new SkillCeilingDto { Skill = Name(c.Key), Ceiling = c.Ceiling.Value }).ToList(),
        }).ToList(),
        JerseyAssignments = w.Jerseys.Assignments.Select(a => new JerseyAssignmentDto
        {
            PlayerId = a.Player.Value, ClubId = a.Club.Value, Number = a.Number.Value, EffectiveFrom = a.EffectiveFrom, EffectiveTo = a.EffectiveTo, Reason = Name(a.Reason),
        }).ToList(),
        BatteryPairs = w.Batteries.Select(b => new BatteryPairDto
        {
            CatcherId = b.Pair.Catcher.Value, PitcherId = b.Pair.Pitcher.Value, SharedAppearances = b.SharedAppearances, LastBatteryDate = b.LastBatteryDate,
        }).ToList(),
    };

    private static PlayerDto ToDto(PlayerSheet s) => new()
    {
        PlayerId = s.Id.Value,
        DisplayName = s.Player.DisplayName,
        BirthDate = s.Player.BirthDate,
        Throws = Name(s.Player.Throws),
        Bats = Name(s.Player.Bats),
        HeightCm = s.Player.Body.HeightCm,
        WeightKg = s.Player.Body.WeightKg,
        Personality = s.Player.Personality.Select(t => Name(t)).ToList(),
        Biography = s.Player.Biography,
        Ratings = new RatingsDto
        {
            Hitter = s.Ratings.Hitter is { } h ? new HitterDto { Contact = h.Contact.Value, Power = h.Power.Value, Eye = h.Eye.Value, Speed = h.Speed.Value, Fielding = h.Fielding.Value, Arm = h.Arm.Value } : null,
            Pitcher = s.Ratings.Pitcher is { } p ? new PitcherDto
            {
                Stuff = p.Stuff.Value, Control = p.Control.Value, Velocity = p.Velocity.Value, Endurance = p.Endurance.Value,
                Pitches = p.Repertoire.Pitches.Select(x => new PitchDto { Type = Name(x.Type), Grade = x.Grade.Value }).ToList(),
            } : null,
            Catcher = s.Ratings.Catcher is { } c ? new CatcherDto { ReceivingFraming = c.ReceivingFraming.Value, Blocking = c.Blocking.Value, PopTime = c.PopTime.Value, GameCalling = c.GameCalling.Value } : null,
        },
        State = new StateDto
        {
            Condition = s.State.Condition.Value, Fatigue = s.State.Fatigue.Value, Sharpness = s.State.Sharpness.Value,
            Stress = s.State.Stress.Value, Satisfaction = s.State.Satisfaction.Value, RecentForm = s.State.RecentForm.Value,
        },
        Proficiency = new ProficiencyDto
        {
            PreferredPosition = Name(s.Proficiency.PreferredPosition),
            Positions = s.Proficiency.Positions.Select(x => new PositionProficiencyDto { Position = Name(x.Position), Value = x.Value.Value }).ToList(),
            Roles = s.Proficiency.Roles.Select(x => new RoleProficiencyDto { Role = Name(x.Role), Value = x.Value.Value }).ToList(),
        },
        Status = new StatusDto
        {
            Origins = s.Status.Origins.Select(o => new OriginDto { Type = Name(o.Type), OriginTeamId = o.OriginTeam?.Value, From = o.From, To = o.To }).ToList(),
            EntryPath = s.Status.EntryPath is { } e ? Name(e) : null,
            Registration = Name(s.Status.Registration),
            League = s.Status.League is { } l ? Name(l) : null,
            Affiliations = s.Status.Affiliations.Select(a => new AffiliationDto { ClubId = a.Club.Value, From = a.From, To = a.To }).ToList(),
            Career = Name(s.Status.Career),
        },
    };
}
