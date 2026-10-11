using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball.Game;

/// <summary>
/// How much a rule profile is trusted. <see cref="KboOrientedUnverified"/> is the best-effort KBO-oriented baseline whose
/// individual values are not yet certified against an official source (see docs/game-engine.md); <see cref="FictionalTest"/>
/// is an explicitly invented variant used only to exercise rule-profile behaviour. Neither claims to be official 2026 KBO rules.
/// </summary>
public enum GameRulesStatus { KboOrientedUnverified, FictionalTest }

/// <summary>
/// How balls and strikes are called (C40, Pitch Zone Lite ROADMAP 1.14). <see cref="Automated"/>: the call comes from an automatic strike-zone system and
/// the catcher's Framing has exactly zero influence on it. <see cref="NonAutomated"/>: an umpire calls it and Framing may act, within later caps, only on
/// <see cref="BoundaryClass.Borderline"/> pitches. A profile that predates this field declares neither (see <see cref="GameRuleSnapshot.CalledPitchMode"/>).
/// This is virtual rule data and claims nothing about the real 2026 KBO ABS rules.
/// </summary>
public enum CalledPitchMode { Automated, NonAutomated }

/// <summary>
/// The immutable, resolved GameRules subset one game runs under (C40). All values come from data, nothing is read from a
/// live setting during play, and <see cref="ContentHash"/> identifies the exact content so a finished game can say which
/// rules it used. Only <see cref="Create"/> builds one, and its collections are read-only copies.
/// </summary>
public sealed class GameRuleSnapshot
{
    public string ProfileId { get; }
    public string Version { get; }
    public GameRulesStatus Status { get; }
    public string Provenance { get; }
    public IReadOnlyList<string> OpenItems { get; }

    public int OutsPerHalfInning { get; }
    public int BallsForWalk { get; }
    public int StrikesForStrikeout { get; }
    public int RegulationInnings { get; }

    /// <summary>Last inning that may be played (including regulation); null means extra innings continue until someone leads.</summary>
    public int? ExtraInningsLimit { get; }

    /// <summary>If the limit is reached with the score level the game ends as a tie. Required exactly when a limit exists.</summary>
    public bool TieAllowed { get; }

    public bool DesignatedHitter { get; }

    /// <summary>A pitcher may also hit as the DH (and stay as DH after leaving the mound).</summary>
    public bool TwoWayPlayerRule { get; }

    /// <summary>Outs a starter must record to be eligible for the win (15 = five innings).</summary>
    public int StarterWinMinOuts { get; }

    public int SaveMaxLead { get; }
    public int SaveMinOuts { get; }
    public int SaveLongOuts { get; }

    /// <summary>
    /// Whether the designated-hitter role can end during a game (a team's DH state is kept in <c>TeamGameState.DhActive</c>, separate from
    /// <see cref="DesignatedHitter"/>, which only says the DH exists at the start). When true the DH-takes-the-field transition of
    /// docs/game-engine.md section 4 (OBR 5.11(a)(12), an approved OBR fallback, not a verified KBO rule) is legal; on in the KBO-oriented profile.
    /// </summary>
    public bool DhMayBeForfeited { get; }

    /// <summary>
    /// A lineup fielder moving to the mound also ends the DH: OBR 5.11(a)(14) (original text read 2026-10-11; evidence rule R27, an approved OBR fallback,
    /// not a verified KBO rule). On in the KBO-oriented profile; requires <see cref="DhMayBeForfeited"/>. Who fills the vacated position is an engine
    /// simplification (evidence row C29).
    /// </summary>
    public bool FielderToMoundForfeitsDh { get; }

    /// <summary>The rule-source policy this profile was resolved under (docs/game-engine.md section 5), e.g. KBO first, 2026 OBR fallback.</summary>
    public string RulePolicyId { get; }

    /// <summary>When the home team wins in its final half-inning, runners still on base at that moment count as the team's left on base.</summary>
    public bool CountWalkOffRunnersAsLeftOnBase { get; }

    /// <summary>
    /// The called-pitch mode this profile declares, or null when the profile does not declare one (every <c>gamerules-v1</c> profile). Null is a fact about
    /// the profile and is never read as <see cref="Game.CalledPitchMode.Automated"/> or <see cref="Game.CalledPitchMode.NonAutomated"/>, and in particular never as the real 2026 KBO ABS mode.
    /// </summary>
    public CalledPitchMode? CalledPitchMode { get; }

    /// <summary>The Pitch Zone Lite zone model this profile declares; null exactly when <see cref="CalledPitchMode"/> is null.</summary>
    public ZoneModelVersion? ZoneModel { get; }

    public bool CalledPitchModeDeclared => CalledPitchMode is not null;

    /// <summary><c>gamerules-v1</c> (the Phase 2 field set) or <c>gamerules-v2</c> (adds the called-pitch mode and the zone model, both required).</summary>
    public string RulesSchema => CalledPitchModeDeclared ? SchemaV2 : SchemaV1;

    public const string SchemaV1 = "gamerules-v1";
    public const string SchemaV2 = "gamerules-v2";

    public string ContentHash { get; }

    /// <summary>
    /// Whether the catcher's Framing may change a called pitch under this profile: false in <see cref="Game.CalledPitchMode.Automated"/> (the influence is exactly zero),
    /// true in <see cref="Game.CalledPitchMode.NonAutomated"/> (then only on borderline pitches within a cap, decided in Phase 3-E). A profile that declares no mode gives
    /// an error rather than a guess.
    /// </summary>
    public Result<bool> FramingMayChangeCalls() => CalledPitchMode switch
    {
        Game.CalledPitchMode.Automated => Result<bool>.Success(false),
        Game.CalledPitchMode.NonAutomated => Result<bool>.Success(true),
        _ => Result<bool>.Failure(new Error("GameRules.called_pitch_mode_undeclared", $"규칙 프로필 '{ProfileId}'는 호출 판정 모드를 선언하지 않았습니다.")),
    };

    private GameRuleSnapshot(string profileId, string version, GameRulesStatus status, string provenance, IReadOnlyList<string> openItems,
        int outs, int balls, int strikes, int regulation, int? limit, bool tie, bool dh, bool twoWay, int starterMin, int saveLead, int saveMin, int saveLong,
        bool dhForfeit, bool walkOffLob, bool fielderToMound, string rulePolicy, CalledPitchMode? calledPitchMode, ZoneModelVersion? zoneModel)
    {
        ProfileId = profileId;
        Version = version;
        Status = status;
        Provenance = provenance;
        OpenItems = openItems;
        OutsPerHalfInning = outs;
        BallsForWalk = balls;
        StrikesForStrikeout = strikes;
        RegulationInnings = regulation;
        ExtraInningsLimit = limit;
        TieAllowed = tie;
        DesignatedHitter = dh;
        TwoWayPlayerRule = twoWay;
        StarterWinMinOuts = starterMin;
        SaveMaxLead = saveLead;
        SaveMinOuts = saveMin;
        SaveLongOuts = saveLong;
        DhMayBeForfeited = dhForfeit;
        CountWalkOffRunnersAsLeftOnBase = walkOffLob;
        FielderToMoundForfeitsDh = fielderToMound;
        RulePolicyId = rulePolicy;
        CalledPitchMode = calledPitchMode;
        ZoneModel = zoneModel;
        var canonical = $"{profileId}|{version}|{status}|{outs}|{balls}|{strikes}|{regulation}|{limit?.ToString() ?? "none"}|{tie}|{dh}|{twoWay}|{starterMin}|{saveLead}|{saveMin}|{saveLong}|{dhForfeit}|{walkOffLob}|{fielderToMound}|{rulePolicy}";
        // The gamerules-v1 text above is frozen: appending nothing for a profile without the new fields keeps every existing hash byte-identical.
        if (calledPitchMode is not null)
            canonical += $"|{SchemaV2}|{calledPitchMode}|{zoneModel}";
        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static Result<GameRuleSnapshot> Create(string? profileId, string? version, GameRulesStatus status, string? provenance, IEnumerable<string>? openItems,
        int outsPerHalfInning, int ballsForWalk, int strikesForStrikeout, int regulationInnings, int? extraInningsLimit, bool tieAllowed,
        bool designatedHitter, bool twoWayPlayerRule, int starterWinMinOuts, int saveMaxLead, int saveMinOuts, int saveLongOuts,
        bool dhMayBeForfeited = false, bool countWalkOffRunnersAsLeftOnBase = true, bool fielderToMoundForfeitsDh = false, string rulePolicyId = "unspecified",
        CalledPitchMode? calledPitchMode = null, ZoneModelVersion zoneModel = default)
    {
        static Result<GameRuleSnapshot> Fail(string code, string message) => Result<GameRuleSnapshot>.Failure(new Error(code, message));

        if (IdText.Validate(profileId, "GameRules.profile_id") is { } idError)
            return Fail("GameRules.profile_id_invalid", idError.Message);
        if (IdText.Validate(version, "GameRules.version") is { } versionError)
            return Fail("GameRules.version_invalid", versionError.Message);
        if (!Enum.IsDefined(status))
            return Fail("GameRules.status_invalid", "규칙 프로필 신뢰 상태가 올바르지 않습니다.");
        if (string.IsNullOrWhiteSpace(provenance))
            return Fail("GameRules.provenance_missing", "경기 규칙 출처 설명이 없습니다.");
        if (outsPerHalfInning < 1 || ballsForWalk < 1 || strikesForStrikeout < 1 || regulationInnings < 1)
            return Fail("GameRules.value_invalid", "아웃·볼·스트라이크·정규 이닝 수는 1 이상이어야 합니다.");
        if (extraInningsLimit is { } limit && limit < regulationInnings)
            return Fail("GameRules.extra_limit_invalid", "연장 한도는 정규 이닝 수 이상이어야 합니다.");
        if ((extraInningsLimit is not null) != tieAllowed)
            return Fail("GameRules.tie_inconsistent", "연장 한도가 있으면 무승부가 허용되어야 하고, 한도가 없으면 무승부를 허용할 수 없습니다.");
        if (twoWayPlayerRule && !designatedHitter)
            return Fail("GameRules.two_way_requires_dh", "투타 겸업 규칙은 지명타자 제도가 있을 때만 쓸 수 있습니다.");
        if (dhMayBeForfeited && !designatedHitter)
            return Fail("GameRules.dh_forfeit_requires_dh", "지명타자 소멸 규칙은 지명타자 제도가 있을 때만 쓸 수 있습니다.");
        if (fielderToMoundForfeitsDh && !dhMayBeForfeited)
            return Fail("GameRules.dh_forfeit_requires_dh", "야수의 투수 전환에 의한 지명타자 소멸은 지명타자 소멸 규칙이 있을 때만 쓸 수 있습니다.");
        if (IdText.Validate(rulePolicyId, "GameRules.rule_policy") is { } policyError)
            return Fail("GameRules.rule_policy_invalid", policyError.Message);
        if ((calledPitchMode is null) != (zoneModel.Value is null))
            return Fail("GameRules.pitch_model_incomplete", "호출 판정 모드와 구역 모델 버전은 함께 선언해야 합니다.");
        if (calledPitchMode is { } mode && !Enum.IsDefined(mode))
            return Fail("GameRules.called_pitch_mode_invalid", "호출 판정 모드 값이 올바르지 않습니다.");
        if (starterWinMinOuts < 0 || saveMaxLead < 1 || saveMinOuts < 0 || saveLongOuts < 0)
            return Fail("GameRules.decision_value_invalid", "승리·세이브 판정 기준 값이 올바르지 않습니다.");
        var items = (openItems ?? []).ToList();
        if (items.Any(string.IsNullOrWhiteSpace))
            return Fail("GameRules.open_item_blank", "미확인 항목 설명이 비어 있습니다.");

        return Result<GameRuleSnapshot>.Success(new GameRuleSnapshot(profileId!, version!, status, provenance, items.AsReadOnly(), outsPerHalfInning, ballsForWalk,
            strikesForStrikeout, regulationInnings, extraInningsLimit, tieAllowed, designatedHitter, twoWayPlayerRule, starterWinMinOuts, saveMaxLead, saveMinOuts, saveLongOuts, dhMayBeForfeited, countWalkOffRunnersAsLeftOnBase, fielderToMoundForfeitsDh, rulePolicyId!,
            calledPitchMode, calledPitchMode is null ? null : zoneModel));
    }
}
