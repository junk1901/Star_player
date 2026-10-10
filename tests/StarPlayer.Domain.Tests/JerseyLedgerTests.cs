using StarPlayer.Domain.Common;
using StarPlayer.Domain.Teams;
using static StarPlayer.Domain.Tests.TestData;

namespace StarPlayer.Domain.Tests;

public class JerseyLedgerTests
{
    private static JerseyNumber N(int value) => JerseyNumber.Create(value).Value;

    private static JerseyNumberLedger Empty(params RetiredNumberRestriction[] retired) =>
        JerseyNumberLedger.Create(JerseyNumberRange.Create(N(0), N(99)).Value, retired, []).Value;

    private static JerseyNumberLedger Apply(JerseyNumberLedger ledger, Result<(JerseyNumberLedger Ledger, JerseyNumberTransition Transition)> step)
    {
        Assert.True(step.IsSuccess, step.Error?.Message);
        return step.Value.Ledger;
    }

    [Fact]
    public void Number_is_a_dated_club_specific_assignment_and_player_carries_none()
    {
        var ledger = Apply(Empty(), Empty().Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));

        var row = ledger.ActiveOn(P("p-1"), D("2030-03-01")).Value!;
        Assert.Equal((P("p-1"), C("club-a"), N(7)), (row.Player, row.Club, row.Number));
        Assert.Null(ledger.ActiveOn(P("p-1"), D("2030-02-28")).Value);
        Assert.Null(ledger.ActiveOn(P("p-2"), D("2030-03-01")).Value);
        Assert.Equal(AssignmentReason.ImportedInitial, row.Reason);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    public void Number_values_outside_the_absolute_scale_are_rejected(int value)
        => Assert.Equal("JerseyNumber.invalid", JerseyNumber.Create(value).Error!.Value.Code);

    [Fact]
    public void Number_outside_the_rule_range_is_rejected_without_mutation()
    {
        var ledger = Empty();

        var result = ledger.Assign(P("p-1"), C("club-a"), N(100), D("2030-03-01"), AssignmentReason.Rookie);

        Assert.Equal("JerseyNumberLedger.out_of_range", result.Error!.Value.Code);
        Assert.Empty(ledger.Assignments);
        Assert.False(JerseyNumberRange.Create(N(10), N(5)).IsSuccess);
    }

    [Fact]
    public void Active_duplicate_at_the_same_club_is_rejected_but_another_club_may_reuse_the_number()
    {
        var ledger = Apply(Empty(), Empty().Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));

        Assert.Equal("JerseyNumberLedger.number_in_use", ledger.Assign(P("p-2"), C("club-a"), N(7), D("2030-04-01"), AssignmentReason.Rookie).Error!.Value.Code);
        Assert.True(ledger.Assign(P("p-2"), C("club-b"), N(7), D("2030-04-01"), AssignmentReason.Rookie).IsSuccess);
        Assert.Single(ledger.Assignments);
    }

    [Fact]
    public void Second_open_assignment_for_one_player_and_club_is_rejected()
    {
        var ledger = Apply(Empty(), Empty().Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));

        var result = ledger.Assign(P("p-1"), C("club-a"), N(8), D("2030-06-01"), AssignmentReason.PlayerRequest);

        Assert.Equal("JerseyNumberLedger.player_already_has_number", result.Error!.Value.Code);
    }

    [Fact]
    public void Retired_number_is_rejected_for_the_retiring_club_only_and_only_for_new_assignments()
    {
        var retired = new RetiredNumberRestriction(C("club-a"), N(1), D("2030-01-01"));
        var ledger = Empty(retired);

        Assert.Equal("JerseyNumberLedger.retired_number", ledger.Assign(P("p-1"), C("club-a"), N(1), D("2030-03-01"), AssignmentReason.Rookie).Error!.Value.Code);
        Assert.True(ledger.Assign(P("p-1"), C("club-b"), N(1), D("2030-03-01"), AssignmentReason.Rookie).IsSuccess);
        // Before the retirement date the number was still legal (history is not rewritten).
        Assert.True(ledger.Assign(P("p-3"), C("club-a"), N(1), D("2029-03-01"), AssignmentReason.ImportedInitial).IsSuccess);
        Assert.DoesNotContain(N(1), ledger.AvailableNumbers(C("club-a"), D("2030-03-01")));
        Assert.Contains(N(1), ledger.AvailableNumbers(C("club-b"), D("2030-03-01")));
    }

    [Fact]
    public void Historical_use_survives_a_later_retirement_when_loading_a_ledger()
    {
        var history = new JerseyNumberAssignment(P("p-1"), C("club-a"), N(1), D("2020-01-01"), D("2028-01-01"), AssignmentReason.ImportedInitial);
        var retired = new RetiredNumberRestriction(C("club-a"), N(1), D("2029-01-01"));
        var ledger = JerseyNumberLedger.Create(JerseyNumberRange.Create(N(0), N(99)).Value, [retired], [history]).Value;

        Assert.Equal(N(1), ledger.History(P("p-1")).Single().Number);

        var tooLate = history with { EffectiveFrom = D("2030-01-01"), EffectiveTo = null };
        Assert.Equal("JerseyNumberLedger.retired_number",
            JerseyNumberLedger.Create(JerseyNumberRange.Create(N(0), N(99)).Value, [retired], [tooLate]).Error!.Value.Code);
    }

    [Fact]
    public void Change_closes_the_previous_interval_and_opens_the_new_one_in_one_step()
    {
        var ledger = Apply(Empty(), Empty().Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));

        var step = ledger.Change(P("p-1"), C("club-a"), N(17), D("2031-03-01"), AssignmentReason.PlayerRequest);
        var changed = Apply(ledger, step);

        Assert.Equal(N(7), step.Value.Transition.Closed!.Number);
        Assert.Equal(D("2031-03-01"), step.Value.Transition.Closed!.EffectiveTo);
        Assert.Equal(N(17), step.Value.Transition.Opened!.Number);
        Assert.Equal(2, changed.History(P("p-1")).Count);
        Assert.Equal(N(7), changed.ActiveOn(P("p-1"), D("2031-02-28")).Value!.Number);
        Assert.Equal(N(17), changed.ActiveOn(P("p-1"), D("2031-03-01")).Value!.Number);
        Assert.Single(changed.Assignments, a => a.EffectiveTo is null);
        // The earlier ledger value is unchanged: nothing was mutated in place.
        Assert.Null(ledger.Assignments.Single().EffectiveTo);
    }

    [Fact]
    public void Failed_change_leaves_the_previous_interval_open()
    {
        var start = Apply(Empty(), Empty().Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));
        var ledger = Apply(start, start.Assign(P("p-2"), C("club-a"), N(9), D("2030-03-01"), AssignmentReason.ImportedInitial));

        var clash = ledger.Change(P("p-1"), C("club-a"), N(9), D("2031-03-01"), AssignmentReason.PlayerRequest);

        Assert.Equal("JerseyNumberLedger.number_in_use", clash.Error!.Value.Code);
        Assert.Equal(2, ledger.Assignments.Count);
        Assert.All(ledger.Assignments, a => Assert.Null(a.EffectiveTo));
        Assert.Equal("JerseyNumberLedger.same_number", ledger.Change(P("p-1"), C("club-a"), N(7), D("2031-03-01"), AssignmentReason.PlayerRequest).Error!.Value.Code);
        Assert.Equal("JerseyNumberLedger.close_before_start", ledger.Change(P("p-1"), C("club-a"), N(8), D("2030-03-01"), AssignmentReason.PlayerRequest).Error!.Value.Code);
        Assert.Equal("JerseyNumberLedger.no_open_assignment", ledger.Change(P("p-3"), C("club-a"), N(8), D("2031-03-01"), AssignmentReason.PlayerRequest).Error!.Value.Code);
    }

    [Fact]
    public void Change_to_a_retired_number_is_rejected_and_keeps_the_old_number()
    {
        var retired = new RetiredNumberRestriction(C("club-a"), N(1), D("2030-01-01"));
        var start = Empty(retired);
        var ledger = Apply(start, start.Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));

        Assert.Equal("JerseyNumberLedger.retired_number", ledger.Change(P("p-1"), C("club-a"), N(1), D("2031-03-01"), AssignmentReason.PlayerRequest).Error!.Value.Code);
        Assert.Null(ledger.Assignments.Single().EffectiveTo);
    }

    [Fact]
    public void Release_transfer_and_free_agency_close_the_old_interval_and_the_new_club_assigns_independently()
    {
        var start = Empty();
        var ledger = Apply(start, start.Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));

        var closed = ledger.Close(P("p-1"), C("club-a"), D("2031-01-01"), AssignmentReason.Transfer);
        var afterClose = Apply(ledger, closed);
        var atB = Apply(afterClose, afterClose.Assign(P("p-1"), C("club-b"), N(7), D("2031-01-01"), AssignmentReason.Transfer));

        Assert.Equal(D("2031-01-01"), closed.Value.Transition.Closed!.EffectiveTo);
        Assert.Null(closed.Value.Transition.Opened);
        Assert.Equal(C("club-b"), atB.ActiveOn(P("p-1"), D("2031-06-01")).Value!.Club);
        Assert.Equal(C("club-a"), atB.ActiveOn(P("p-1"), D("2030-12-31")).Value!.Club);
        Assert.Equal("JerseyNumberLedger.reason_cannot_close", ledger.Close(P("p-1"), C("club-a"), D("2031-01-01"), AssignmentReason.Rookie).Error!.Value.Code);
        Assert.Equal("JerseyNumberLedger.reason_cannot_open", ledger.Assign(P("p-9"), C("club-a"), N(8), D("2031-01-01"), AssignmentReason.Release).Error!.Value.Code);
    }

    [Fact]
    public void A_released_number_can_be_reused_by_another_player_and_the_first_holder_keeps_history()
    {
        var start = Empty();
        var held = Apply(start, start.Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));
        var released = Apply(held, held.Close(P("p-1"), C("club-a"), D("2031-01-01"), AssignmentReason.Release));

        Assert.Contains(N(7), released.AvailableNumbers(C("club-a"), D("2031-01-01")));
        Assert.DoesNotContain(N(7), released.AvailableNumbers(C("club-a"), D("2030-06-01")));
        var reused = Apply(released, released.Assign(P("p-2"), C("club-a"), N(7), D("2031-02-01"), AssignmentReason.Rookie));
        Assert.Equal(N(7), reused.History(P("p-1")).Single().Number);
        Assert.Equal(P("p-2"), reused.Assignments.Single(a => a.EffectiveTo is null).Player);
        // A past-dated claim that overlaps the first holder's interval is still a duplicate.
        Assert.Equal("JerseyNumberLedger.number_in_use", released.Assign(P("p-3"), C("club-a"), N(7), D("2030-09-01"), AssignmentReason.Rookie).Error!.Value.Code);
    }

    [Fact]
    public void Loading_rejects_overlapping_or_inverted_history_rows()
    {
        var range = JerseyNumberRange.Create(N(0), N(99)).Value;
        var a = new JerseyNumberAssignment(P("p-1"), C("club-a"), N(7), D("2020-01-01"), D("2022-01-01"), AssignmentReason.ImportedInitial);

        Assert.Equal("JerseyNumberLedger.number_in_use", JerseyNumberLedger.Create(range, [], [a, a with { Player = P("p-2"), EffectiveFrom = D("2021-01-01"), EffectiveTo = null }]).Error!.Value.Code);
        Assert.Equal("JerseyNumberLedger.interval_invalid", JerseyNumberLedger.Create(range, [], [a with { EffectiveTo = D("2020-01-01") }]).Error!.Value.Code);
        Assert.Equal("JerseyNumberLedger.identity_missing", JerseyNumberLedger.Create(range, [], [a with { Player = default }]).Error!.Value.Code);
        Assert.True(JerseyNumberLedger.Create(range, [], [a, a with { Player = P("p-2"), EffectiveFrom = D("2022-01-01"), EffectiveTo = null }]).IsSuccess);
    }

    [Fact]
    public void Same_day_handover_is_legal_because_intervals_are_half_open()
    {
        var range = JerseyNumberRange.Create(N(0), N(99)).Value;
        var first = new JerseyNumberAssignment(P("p-1"), C("club-a"), N(7), D("2020-01-01"), D("2022-01-01"), AssignmentReason.ImportedInitial);
        var second = new JerseyNumberAssignment(P("p-2"), C("club-a"), N(7), D("2022-01-01"), null, AssignmentReason.Rookie);

        var ledger = JerseyNumberLedger.Create(range, [], [second, first]).Value;

        Assert.Equal(P("p-1"), ledger.ActiveOn(P("p-1"), D("2021-12-31")).Value!.Player);
        Assert.Equal(P("p-2"), ledger.ActiveOn(P("p-2"), D("2022-01-01")).Value!.Player);
        Assert.Null(ledger.ActiveOn(P("p-1"), D("2022-01-01")).Value);
    }

    // ---- cross-club overlap, explicit conflicts, immutability (Codex review) ----

    private static JerseyNumberAssignment Row(string player, string club, int number, string from, string? to, AssignmentReason reason = AssignmentReason.ImportedInitial) =>
        new(P(player), C(club), N(number), D(from), to is null ? null : D(to), reason);

    private static JerseyNumberRange Range() => JerseyNumberRange.Create(N(0), N(99)).Value;

    [Fact]
    public void Same_player_same_club_overlapping_periods_are_rejected_when_assigning_and_when_loading()
    {
        var start = Apply(Empty(), Empty().Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));

        Assert.Equal("JerseyNumberLedger.player_already_has_number", start.Assign(P("p-1"), C("club-a"), N(8), D("2031-01-01"), AssignmentReason.PlayerRequest).Error!.Value.Code);
        Assert.Equal("JerseyNumberLedger.player_already_has_number",
            JerseyNumberLedger.Create(Range(), [], [Row("p-1", "club-a", 7, "2030-01-01", "2031-06-01"), Row("p-1", "club-a", 8, "2031-01-01", null)]).Error!.Value.Code);
    }

    [Fact]
    public void Same_player_different_club_overlapping_periods_are_rejected_when_assigning_and_when_loading()
    {
        var start = Apply(Empty(), Empty().Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));

        var assign = start.Assign(P("p-1"), C("club-b"), N(7), D("2031-01-01"), AssignmentReason.Transfer);

        Assert.Equal("JerseyNumberLedger.player_at_other_club", assign.Error!.Value.Code);
        Assert.Single(start.Assignments);
        Assert.Equal("JerseyNumberLedger.player_at_other_club",
            JerseyNumberLedger.Create(Range(), [], [Row("p-1", "club-a", 7, "2030-01-01", "2031-06-01"), Row("p-1", "club-b", 9, "2031-01-01", null)]).Error!.Value.Code);
        // Past-dated claim that overlaps the old interval is rejected too.
        Assert.Equal("JerseyNumberLedger.player_at_other_club", start.Assign(P("p-1"), C("club-b"), N(9), D("2030-09-01"), AssignmentReason.Transfer).Error!.Value.Code);
    }

    [Fact]
    public void Different_players_same_club_same_number_overlapping_periods_stay_forbidden()
    {
        Assert.Equal("JerseyNumberLedger.number_in_use",
            JerseyNumberLedger.Create(Range(), [], [Row("p-1", "club-a", 7, "2030-01-01", "2031-06-01"), Row("p-2", "club-a", 7, "2031-01-01", null)]).Error!.Value.Code);
    }

    [Fact]
    public void Previous_end_day_equal_to_new_start_day_is_a_legal_handover_for_changes_and_transfers()
    {
        var ledger = JerseyNumberLedger.Create(Range(), [], [Row("p-1", "club-a", 7, "2030-01-01", "2031-01-01"), Row("p-1", "club-b", 7, "2031-01-01", null)]).Value;

        Assert.Equal(C("club-a"), ledger.ActiveOn(P("p-1"), D("2030-12-31")).Value!.Club);
        Assert.Equal(C("club-b"), ledger.ActiveOn(P("p-1"), D("2031-01-01")).Value!.Club);
        var viaApi = Apply(Empty(), Empty().Assign(P("p-1"), C("club-a"), N(7), D("2030-01-01"), AssignmentReason.ImportedInitial));
        var closed = Apply(viaApi, viaApi.Close(P("p-1"), C("club-a"), D("2031-01-01"), AssignmentReason.Transfer));
        Assert.True(closed.Assign(P("p-1"), C("club-b"), N(7), D("2031-01-01"), AssignmentReason.Transfer).IsSuccess);
        Assert.Equal("JerseyNumberLedger.player_at_other_club", closed.Assign(P("p-1"), C("club-b"), N(7), D("2030-12-31"), AssignmentReason.Transfer).Error!.Value.Code);
    }

    [Fact]
    public void A_transfer_between_clubs_with_gapped_periods_is_legal_and_keeps_both_rows()
    {
        var ledger = JerseyNumberLedger.Create(Range(), [], [Row("p-1", "club-a", 7, "2030-01-01", "2031-01-01"), Row("p-1", "club-b", 12, "2031-03-01", null)]).Value;

        Assert.Null(ledger.ActiveOn(P("p-1"), D("2031-02-01")).Value);
        Assert.Equal([7, 12], ledger.History(P("p-1")).Select(a => a.Number.Value));
    }

    [Fact]
    public void ActiveOn_reports_an_explicit_conflict_for_corrupt_overlapping_rows_instead_of_picking_the_first()
    {
        var corrupt = JerseyNumberLedger.FromRowsUnchecked(Range(), [], [Row("p-1", "club-a", 7, "2030-01-01", null), Row("p-1", "club-b", 9, "2030-06-01", null)]);

        var conflict = corrupt.ActiveOn(P("p-1"), D("2030-07-01"));

        Assert.False(conflict.IsSuccess);
        Assert.Equal("JerseyNumberLedger.conflicting_active_rows", conflict.Error!.Value.Code);
        Assert.True(corrupt.ActiveOn(P("p-1"), D("2030-03-01")).IsSuccess); // before the second row starts there is no ambiguity
        Assert.Equal(7, corrupt.ActiveOn(P("p-1"), D("2030-03-01")).Value!.Number.Value);
        Assert.Null(corrupt.ActiveOn(P("p-9"), D("2030-07-01")).Value);
    }

    [Fact]
    public void Every_rejected_request_leaves_the_existing_ledger_unchanged()
    {
        var start = Empty(new RetiredNumberRestriction(C("club-a"), N(1), D("2030-01-01")));
        var ledger = Apply(start, start.Assign(P("p-1"), C("club-a"), N(7), D("2030-03-01"), AssignmentReason.ImportedInitial));
        var before = ledger.Assignments.ToList();

        var rejected = new[]
        {
            ledger.Assign(P("p-1"), C("club-b"), N(7), D("2030-06-01"), AssignmentReason.Transfer).Error!.Value.Code,
            ledger.Assign(P("p-2"), C("club-a"), N(7), D("2030-06-01"), AssignmentReason.Rookie).Error!.Value.Code,
            ledger.Assign(P("p-2"), C("club-a"), N(1), D("2030-06-01"), AssignmentReason.Rookie).Error!.Value.Code,
            ledger.Assign(P("p-2"), C("club-a"), N(100), D("2030-06-01"), AssignmentReason.Rookie).Error!.Value.Code,
            ledger.Change(P("p-1"), C("club-a"), N(7), D("2031-01-01"), AssignmentReason.PlayerRequest).Error!.Value.Code,
            ledger.Change(P("p-1"), C("club-a"), N(1), D("2031-01-01"), AssignmentReason.PlayerRequest).Error!.Value.Code,
            ledger.Close(P("p-1"), C("club-a"), D("2030-03-01"), AssignmentReason.Release).Error!.Value.Code,
            ledger.Close(P("p-1"), C("club-b"), D("2031-01-01"), AssignmentReason.Release).Error!.Value.Code,
        };

        Assert.Equal(8, rejected.Length);
        Assert.Equal(before, ledger.Assignments);
        Assert.Null(ledger.Assignments.Single().EffectiveTo);
    }

    [Fact]
    public void Past_numbers_stay_in_history_after_changes_transfers_and_a_later_retirement()
    {
        var retired = new RetiredNumberRestriction(C("club-a"), N(7), D("2033-01-01"));
        var ledger = JerseyNumberLedger.Create(Range(), [retired],
            [Row("p-1", "club-a", 7, "2030-01-01", "2031-01-01"), Row("p-1", "club-a", 8, "2031-01-01", "2032-01-01", AssignmentReason.PlayerRequest), Row("p-1", "club-b", 9, "2032-01-01", null, AssignmentReason.Transfer)]).Value;

        Assert.Equal([7, 8, 9], ledger.History(P("p-1")).Select(a => a.Number.Value));
        Assert.Equal(7, ledger.ActiveOn(P("p-1"), D("2030-06-01")).Value!.Number.Value);
        Assert.True(ledger.IsRestricted(C("club-a"), N(7), D("2034-01-01")));
    }

    [Fact]
    public void Ledger_collections_are_defensive_read_only_copies()
    {
        var restriction = new RetiredNumberRestriction(C("club-a"), N(1), D("2030-01-01"));
        var rows = new List<JerseyNumberAssignment> { Row("p-1", "club-a", 7, "2030-01-01", null) };
        var restrictions = new List<RetiredNumberRestriction> { restriction };
        var ledger = JerseyNumberLedger.Create(Range(), restrictions, rows).Value;

        rows.Add(Row("p-2", "club-a", 8, "2030-01-01", null));
        restrictions.Clear();

        Assert.Single(ledger.Assignments);
        Assert.Single(ledger.Restrictions);
        Assert.Throws<NotSupportedException>(() => ((IList<JerseyNumberAssignment>)ledger.Assignments).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<RetiredNumberRestriction>)ledger.Restrictions).Add(restriction));
    }

    [Fact]
    public void Available_numbers_are_in_ascending_stable_order()
    {
        var ledger = JerseyNumberLedger.Create(JerseyNumberRange.Create(N(0), N(5)).Value, [new RetiredNumberRestriction(C("club-a"), N(2), D("2020-01-01"))],
            [new JerseyNumberAssignment(P("p-1"), C("club-a"), N(4), D("2025-01-01"), null, AssignmentReason.ImportedInitial)]).Value;

        Assert.Equal([0, 1, 3, 5], ledger.AvailableNumbers(C("club-a"), D("2030-01-01")).Select(n => n.Value));
    }
}
