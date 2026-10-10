using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Teams;

/// <summary>A jersey number value. The legal range for a club/era comes from rules data (<see cref="JerseyNumberRange"/>), not from here.</summary>
public readonly record struct JerseyNumber : IComparable<JerseyNumber>
{
    public const int AbsoluteMax = 999;

    public int Value { get; private init; }

    public static Result<JerseyNumber> Create(int value) =>
        value is < 0 or > AbsoluteMax
            ? Result<JerseyNumber>.Failure(new Error("JerseyNumber.invalid", $"등번호는 0~{AbsoluteMax} 범위의 정수여야 합니다."))
            : Result<JerseyNumber>.Success(new JerseyNumber { Value = value });

    public int CompareTo(JerseyNumber other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Inclusive legal number range supplied by the applicable roster/legacy rule data.</summary>
public readonly record struct JerseyNumberRange
{
    public JerseyNumber Min { get; private init; }
    public JerseyNumber Max { get; private init; }

    public static Result<JerseyNumberRange> Create(JerseyNumber min, JerseyNumber max) =>
        min.Value > max.Value
            ? Result<JerseyNumberRange>.Failure(new Error("JerseyNumberRange.inverted", "등번호 허용 범위의 최솟값이 최댓값보다 큽니다."))
            : Result<JerseyNumberRange>.Success(new JerseyNumberRange { Min = min, Max = max });

    public bool Contains(JerseyNumber number) => number.Value >= Min.Value && number.Value <= Max.Value;
}

/// <summary>Why an interval opened or closed. <c>Release</c> and <c>Retirement</c> only close; the rest only open.</summary>
public enum AssignmentReason { ImportedInitial, Rookie, PlayerRequest, ClubDecision, Transfer, FreeAgency, Release, Retirement }

/// <summary>
/// C47: a number belongs to a PlayerId–ClubId pair for a half-open interval [<see cref="EffectiveFrom"/>, <see cref="EffectiveTo"/>);
/// <c>EffectiveTo == null</c> means still active. Player has no number field; this row is the only place a number lives.
/// </summary>
public sealed record JerseyNumberAssignment(PlayerId Player, ClubId Club, JerseyNumber Number, DateOnly EffectiveFrom, DateOnly? EffectiveTo, AssignmentReason Reason)
{
    /// <summary>Half-open: the start day is included, the end day is not (so a number may change hands on the same day).</summary>
    public bool IsActiveOn(DateOnly date) => date >= EffectiveFrom && (EffectiveTo is null || date < EffectiveTo);
}

/// <summary>
/// C31 vocabulary: a club-specific retired number. It restricts <i>new</i> assignments starting on or after
/// <see cref="EffectiveFrom"/>; assignments that started earlier stay valid history.
/// </summary>
public sealed record RetiredNumberRestriction(ClubId Club, JerseyNumber Number, DateOnly EffectiveFrom);

/// <summary>Result of an atomic close/open: what interval was closed and what (if anything) opened.</summary>
public sealed record JerseyNumberTransition(JerseyNumberAssignment? Closed, JerseyNumberAssignment? Opened, AssignmentReason Reason);

/// <summary>
/// Immutable C47 ledger. Every mutator returns a new ledger or a failure and never changes this instance, so a rejected
/// request cannot leave a partial change and a change always closes the previous interval in the same step.
/// Rules checked: legal range, club-retired restriction, a player holds at most one number at a time <b>across all
/// clubs</b>, and no two players hold the same number at the same club over overlapping intervals. Intervals are
/// half-open [from, to): an interval ending on a day and another starting that same day do not overlap. Released
/// numbers can be reused, and closed rows are permanent history.
/// </summary>
public sealed class JerseyNumberLedger
{
    private readonly IReadOnlyList<RetiredNumberRestriction> _restrictions;

    public JerseyNumberRange AllowedRange { get; }
    public IReadOnlyList<JerseyNumberAssignment> Assignments { get; }
    public IReadOnlyList<RetiredNumberRestriction> Restrictions => _restrictions;

    private JerseyNumberLedger(JerseyNumberRange range, IEnumerable<RetiredNumberRestriction> restrictions, IEnumerable<JerseyNumberAssignment> assignments)
    {
        AllowedRange = range;
        _restrictions = restrictions.ToList().AsReadOnly(); // defensive copy; callers cannot mutate through the property
        Assignments = assignments.ToList().AsReadOnly();
    }

    /// <summary>Test seam: builds a ledger without validation so the defensive checks on corrupt data can be exercised.</summary>
    internal static JerseyNumberLedger FromRowsUnchecked(JerseyNumberRange range, IEnumerable<RetiredNumberRestriction> restrictions, IEnumerable<JerseyNumberAssignment> rows) =>
        new(range, restrictions, rows);

    public static Result<JerseyNumberLedger> Create(JerseyNumberRange range, IEnumerable<RetiredNumberRestriction> restrictions, IEnumerable<JerseyNumberAssignment> existing)
    {
        var ledger = new JerseyNumberLedger(range, restrictions.ToList(), []);
        foreach (var row in existing.OrderBy(a => a.EffectiveFrom).ThenBy(a => a.Player.Value, StringComparer.Ordinal))
        {
            if (row.EffectiveTo is { } to && to <= row.EffectiveFrom)
                return Fail("JerseyNumberLedger.interval_invalid", $"{row.Player} 선수의 등번호 기간 끝은 시작보다 늦어야 합니다.");
            var opened = ledger.Open(row);
            if (!opened.IsSuccess)
                return Result<JerseyNumberLedger>.Failure(opened.Error!.Value);
            ledger = opened.Value;
        }

        return Result<JerseyNumberLedger>.Success(ledger);
    }

    public Result<(JerseyNumberLedger Ledger, JerseyNumberTransition Transition)> Assign(PlayerId player, ClubId club, JerseyNumber number, DateOnly from, AssignmentReason reason)
    {
        if (reason is AssignmentReason.Release or AssignmentReason.Retirement)
            return Fail2("JerseyNumberLedger.reason_cannot_open", "해제/은퇴 사유로는 등번호를 새로 배정할 수 없습니다.");
        var opened = Open(new JerseyNumberAssignment(player, club, number, from, null, reason));
        return opened.IsSuccess
            ? Result<(JerseyNumberLedger, JerseyNumberTransition)>.Success((opened.Value, new JerseyNumberTransition(null, opened.Value.Assignments.First(a => a.Player == player && a.Club == club && a.EffectiveFrom == from), reason)))
            : Result<(JerseyNumberLedger, JerseyNumberTransition)>.Failure(opened.Error!.Value);
    }

    /// <summary>Closes the player's open number at this club on <paramref name="effective"/> and opens the new one that day.</summary>
    public Result<(JerseyNumberLedger Ledger, JerseyNumberTransition Transition)> Change(PlayerId player, ClubId club, JerseyNumber newNumber, DateOnly effective, AssignmentReason reason)
    {
        if (reason is AssignmentReason.Release or AssignmentReason.Retirement)
            return Fail2("JerseyNumberLedger.reason_cannot_open", "해제/은퇴 사유로는 등번호를 새로 배정할 수 없습니다.");
        var current = OpenFor(player, club);
        if (current is null)
            return Fail2("JerseyNumberLedger.no_open_assignment", "변경할 현재 등번호 배정이 없습니다.");
        if (current.Number == newNumber)
            return Fail2("JerseyNumberLedger.same_number", "이미 같은 등번호를 사용 중입니다.");

        var closed = CloseOpen(current, effective);
        if (!closed.IsSuccess)
            return Result<(JerseyNumberLedger, JerseyNumberTransition)>.Failure(closed.Error!.Value);
        var opened = closed.Value.Open(new JerseyNumberAssignment(player, club, newNumber, effective, null, reason));
        if (!opened.IsSuccess)
            return Result<(JerseyNumberLedger, JerseyNumberTransition)>.Failure(opened.Error!.Value);
        var closedRow = opened.Value.Assignments.First(a => a.Player == player && a.Club == club && a.EffectiveFrom == current.EffectiveFrom);
        var openedRow = opened.Value.Assignments.First(a => a.Player == player && a.Club == club && a.EffectiveFrom == effective && a.EffectiveTo is null);
        return Result<(JerseyNumberLedger, JerseyNumberTransition)>.Success((opened.Value, new JerseyNumberTransition(closedRow, openedRow, reason)));
    }

    public Result<(JerseyNumberLedger Ledger, JerseyNumberTransition Transition)> Close(PlayerId player, ClubId club, DateOnly effective, AssignmentReason reason)
    {
        if (reason is not (AssignmentReason.Release or AssignmentReason.Retirement or AssignmentReason.Transfer or AssignmentReason.FreeAgency))
            return Fail2("JerseyNumberLedger.reason_cannot_close", "등번호 종료 사유는 해제, 은퇴, 이적, FA 중 하나여야 합니다.");
        var current = OpenFor(player, club);
        if (current is null)
            return Fail2("JerseyNumberLedger.no_open_assignment", "종료할 현재 등번호 배정이 없습니다.");
        var closed = CloseOpen(current, effective);
        if (!closed.IsSuccess)
            return Result<(JerseyNumberLedger, JerseyNumberTransition)>.Failure(closed.Error!.Value);
        var closedRow = closed.Value.Assignments.First(a => a.Player == player && a.Club == club && a.EffectiveFrom == current.EffectiveFrom);
        return Result<(JerseyNumberLedger, JerseyNumberTransition)>.Success((closed.Value, new JerseyNumberTransition(closedRow, null, reason)));
    }

    /// <summary>
    /// The assignment active for this player on <paramref name="date"/> (any club): success with null when there is none,
    /// and an explicit conflict error (never an arbitrary first row) if corrupt data has more than one.
    /// </summary>
    public Result<JerseyNumberAssignment?> ActiveOn(PlayerId player, DateOnly date)
    {
        var active = Assignments.Where(a => a.Player == player && a.IsActiveOn(date)).ToList();
        return active.Count > 1
            ? Result<JerseyNumberAssignment?>.Failure(new Error("JerseyNumberLedger.conflicting_active_rows", $"{player} 선수의 {date:yyyy-MM-dd} 등번호가 {active.Count}개 겹쳐 있어 현재 번호를 정할 수 없습니다."))
            : Result<JerseyNumberAssignment?>.Success(active.SingleOrDefault());
    }

    public IReadOnlyList<JerseyNumberAssignment> History(PlayerId player) =>
        Assignments.Where(a => a.Player == player).OrderBy(a => a.EffectiveFrom).ToList();

    public bool IsRestricted(ClubId club, JerseyNumber number, DateOnly date) =>
        _restrictions.Any(r => r.Club == club && r.Number == number && date >= r.EffectiveFrom);

    /// <summary>Legal numbers at this club on <paramref name="date"/>, ascending: in range, not retired, not held by anyone.</summary>
    public IReadOnlyList<JerseyNumber> AvailableNumbers(ClubId club, DateOnly date)
    {
        var taken = Assignments.Where(a => a.Club == club && a.IsActiveOn(date)).Select(a => a.Number).ToHashSet();
        var result = new List<JerseyNumber>();
        for (var n = AllowedRange.Min.Value; n <= AllowedRange.Max.Value; n++)
        {
            var number = JerseyNumber.Create(n).Value;
            if (!taken.Contains(number) && !IsRestricted(club, number, date))
                result.Add(number);
        }

        return result;
    }

    private JerseyNumberAssignment? OpenFor(PlayerId player, ClubId club) =>
        Assignments.FirstOrDefault(a => a.Player == player && a.Club == club && a.EffectiveTo is null);

    private Result<JerseyNumberLedger> Open(JerseyNumberAssignment row)
    {
        if (row.Player == default || row.Club == default)
            return Fail("JerseyNumberLedger.identity_missing", "선수 ID와 구단 ID가 모두 필요합니다.");
        if (!AllowedRange.Contains(row.Number))
            return Fail("JerseyNumberLedger.out_of_range", $"등번호 {row.Number}은(는) 허용 범위 밖입니다.");
        if (IsRestricted(row.Club, row.Number, row.EffectiveFrom))
            return Fail("JerseyNumberLedger.retired_number", $"등번호 {row.Number}은(는) 이 구단의 영구결번입니다.");
        if (Assignments.Any(a => a.Player == row.Player && a.Club == row.Club && SameWindow(a, row)))
            return Fail("JerseyNumberLedger.player_already_has_number", "이 선수는 같은 구단에서 이미 다른 등번호를 사용 중입니다.");
        if (Assignments.Any(a => a.Player == row.Player && a.Club != row.Club && SameWindow(a, row)))
            return Fail("JerseyNumberLedger.player_at_other_club", "이 선수는 같은 기간에 다른 구단의 등번호를 이미 사용 중입니다. 이적 전에 이전 구단 번호를 종료해야 합니다.");
        if (Assignments.Any(a => a.Club == row.Club && a.Number == row.Number && SameWindow(a, row)))
            return Fail("JerseyNumberLedger.number_in_use", $"등번호 {row.Number}은(는) 같은 구단의 다른 선수가 사용 중입니다.");
        return Result<JerseyNumberLedger>.Success(With(Assignments.Append(row)));
    }

    private Result<JerseyNumberLedger> CloseOpen(JerseyNumberAssignment current, DateOnly effective)
    {
        if (effective <= current.EffectiveFrom)
            return Fail("JerseyNumberLedger.close_before_start", "종료일은 배정 시작일보다 늦어야 합니다.");
        return Result<JerseyNumberLedger>.Success(With(Assignments.Select(a => a == current ? a with { EffectiveTo = effective } : a)));
    }

    // Two intervals overlap when each starts before the other ends.
    private static bool SameWindow(JerseyNumberAssignment existing, JerseyNumberAssignment row) =>
        (existing.EffectiveTo is null || existing.EffectiveTo > row.EffectiveFrom) && (row.EffectiveTo is null || row.EffectiveTo > existing.EffectiveFrom);

    private JerseyNumberLedger With(IEnumerable<JerseyNumberAssignment> rows) =>
        new(AllowedRange, _restrictions, rows.OrderBy(a => a.Player.Value, StringComparer.Ordinal).ThenBy(a => a.Club.Value, StringComparer.Ordinal).ThenBy(a => a.EffectiveFrom).ToList());

    private static Result<JerseyNumberLedger> Fail(string code, string message) => Result<JerseyNumberLedger>.Failure(new Error(code, message));

    private static Result<(JerseyNumberLedger, JerseyNumberTransition)> Fail2(string code, string message) =>
        Result<(JerseyNumberLedger, JerseyNumberTransition)>.Failure(new Error(code, message));
}
