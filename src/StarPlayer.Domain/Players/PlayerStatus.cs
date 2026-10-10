using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Players;

/// <summary>원래 출신 경로. Never changes because of how the player later enters pro baseball.</summary>
public enum OriginType { HighSchool, University, Overseas, OtherProspect }

/// <summary>프로 진입 방식. There is no separate developmental-player database: 육성선수 is an entry path plus a registration status.</summary>
public enum EntryPath { Draft, DevelopmentContract, Other }

/// <summary>등록 신분 (source v1.6 §102.12). Distinct from first/second-team level and from club.</summary>
public enum RegistrationStatus { Amateur, DraftCandidate, Development, Registered, Released }

/// <summary>1군 / 2군 소속. Not a registration status: 육성선수 + 2군 is a legal combination.</summary>
public enum LeagueAssignment { FirstTeam, SecondTeam }

public enum CareerStatus { Active, Retired }

/// <summary>One entry of the ordered origin history (C01). The organization is optional because some origins have none.</summary>
public sealed record OriginRecord(OriginType Type, AmateurTeamId? OriginTeam, DateOnly? From, DateOnly? To);

/// <summary>A dated club affiliation interval, [From, To). <c>To == null</c> means current. Not a jersey number and not a contract.</summary>
public sealed record ClubAffiliation(ClubId Club, DateOnly From, DateOnly? To);

/// <summary>
/// Orthogonal status axes (ROADMAP §3.1): origin history, entry path, registration, league assignment, dated club
/// affiliation and career status are separate fields and are never collapsed into one enum.
/// </summary>
public sealed record PlayerStatus
{
    public IReadOnlyList<OriginRecord> Origins { get; }
    public EntryPath? EntryPath { get; }
    public RegistrationStatus Registration { get; }
    public LeagueAssignment? League { get; }
    public IReadOnlyList<ClubAffiliation> Affiliations { get; }
    public CareerStatus Career { get; }

    private PlayerStatus(IReadOnlyList<OriginRecord> origins, EntryPath? entryPath, RegistrationStatus registration, LeagueAssignment? league, IReadOnlyList<ClubAffiliation> affiliations, CareerStatus career)
    {
        Origins = origins;
        EntryPath = entryPath;
        Registration = registration;
        League = league;
        Affiliations = affiliations;
        Career = career;
    }

    public ClubAffiliation? CurrentAffiliation => Affiliations.FirstOrDefault(a => a.To is null);

    public static Result<PlayerStatus> Create(IEnumerable<OriginRecord>? origins, EntryPath? entryPath, RegistrationStatus registration,
        LeagueAssignment? league, IEnumerable<ClubAffiliation>? affiliations, CareerStatus career)
    {
        var origin = (origins ?? []).ToList();
        if (origin.Count == 0)
            return Fail("PlayerStatus.origin_missing", "출신 이력이 하나 이상 필요합니다.");
        if (origin.Any(o => !Enum.IsDefined(o.Type)) || (entryPath is { } e && !Enum.IsDefined(e)) || !Enum.IsDefined(registration)
            || (league is { } l && !Enum.IsDefined(l)) || !Enum.IsDefined(career))
            return Fail("PlayerStatus.enum_invalid", "상태 값이 올바르지 않습니다.");

        var club = (affiliations ?? []).OrderBy(a => a.From).ToList();
        for (var i = 0; i < club.Count; i++)
        {
            var a = club[i];
            if (a.To is { } to && to <= a.From)
                return Fail("PlayerStatus.affiliation_interval_invalid", "구단 소속 기간의 끝은 시작보다 늦어야 합니다.");
            if (i + 1 < club.Count && (a.To is null || a.To > club[i + 1].From))
                return Fail("PlayerStatus.affiliation_overlap", "구단 소속 기간이 겹치거나, 끝나지 않은 소속 뒤에 다른 소속이 있습니다.");
        }

        var open = club.Any(a => a.To is null);
        var professional = registration is RegistrationStatus.Development or RegistrationStatus.Registered;
        if (open && !professional)
            return Fail("PlayerStatus.affiliation_without_registration", "구단에 소속된 선수는 육성선수 또는 정식선수로 등록되어 있어야 합니다.");
        if (open && league is null)
            return Fail("PlayerStatus.league_missing", "구단에 소속된 선수는 1군/2군 소속이 필요합니다.");
        if (!open && league is not null)
            return Fail("PlayerStatus.league_without_club", "구단 소속이 없는 선수에게 1군/2군 소속을 줄 수 없습니다.");
        if (!open && professional && career == CareerStatus.Active)
            return Fail("PlayerStatus.registration_without_club", "현역 등록 선수는 현재 소속 구단이 필요합니다.");

        return Result<PlayerStatus>.Success(new PlayerStatus(origin.AsReadOnly(), entryPath, registration, league, club.AsReadOnly(), career));
    }

    private static Result<PlayerStatus> Fail(string code, string message) => Result<PlayerStatus>.Failure(new Error(code, message));
}
