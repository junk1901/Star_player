using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// Codex F-01: a cohort must be an immutable snapshot of the inputs a game reads, and its <see cref="CohortInputs.InputsHash"/> must identify exactly that snapshot. The hash is computed once,
/// so any retained mutable reference (the caller's sheet list or lineup / bench lists, or the dictionary behind <c>Sheets</c>) would let the played inputs change under an unchanged hash.
/// </summary>
public class CohortImmutabilityTests
{
    private sealed record Source(List<PlayerSheet> Sheets, List<LineupSlot> AwayLineup, List<PlayerId> AwayBench, TeamSetup Away, TeamSetup Home);

    /// <summary>Caller-owned MUTABLE collections: the neutral cohort with a distinctive first away hitter (contact 90) so that a changed batting order or sheet changes the game.</summary>
    private static Source MutableSource()
    {
        var baseline = NeutralCohort.Create();
        var firstAway = baseline.Away.Lineup[0].Player;
        var sheets = baseline.Sheets.Values.Select(s => s.Id == firstAway ? WithContact(s, 90) : s).ToList();
        var lineup = baseline.Away.Lineup.ToList();
        var bench = baseline.Away.Bench.ToList();
        return new Source(sheets, lineup, bench, new TeamSetup(lineup, baseline.Away.StartingPitcher, bench), baseline.Home);
    }

    private static PlayerSheet WithContact(PlayerSheet s, int contact) => H.WithRatings(s, s.Ratings.Hitter! with { Contact = H.Skill(contact) }, s.Ratings.Pitcher, s.Ratings.Catcher);

    private static CohortInputs Build(Source source, string id = "mutable-v1") => H.Ok(CohortInputs.Create(id, CohortKind.Neutral, "F-01 test", source.Away, source.Home, source.Sheets));

    /// <summary>Every way a consumer could try to change a collection the cohort exposes; each attempt must either be impossible or throw, and the final value is what the test compares.</summary>
    private static void TryMutate(object collection, Action<object> mutate)
    {
        try { mutate(collection); }
        catch (Exception e) when (e is NotSupportedException or InvalidCastException or ArgumentException or InvalidOperationException) { }
    }

    private static string PlayedHash(CohortInputs cohort, ulong seed = 3) => H.Play(H.Config("f01", cohort), seed).ResultHash;

    [Fact]
    public void A_sheet_replaced_in_the_callers_list_after_creation_changes_neither_the_cohort_nor_its_games()
    {
        var source = MutableSource();
        var cohort = Build(source);
        var hashBefore = cohort.InputsHash;
        var playedBefore = PlayedHash(cohort);
        var first = source.Away.Lineup[0].Player;

        var index = source.Sheets.FindIndex(s => s.Id == first);
        source.Sheets[index] = WithContact(source.Sheets[index], 10);
        source.Sheets.RemoveAt(source.Sheets.FindIndex(s => s.Id == source.Home.Bench[0]));

        Assert.Equal(hashBefore, cohort.InputsHash);
        Assert.Equal(90, cohort.Sheets[first].Ratings.Hitter!.Contact.Value);
        Assert.Equal(playedBefore, PlayedHash(cohort));
    }

    [Fact]
    public void The_exposed_sheets_map_cannot_be_modified_by_casting_it_back_to_a_dictionary()
    {
        var cohort = Build(MutableSource());
        var hashBefore = cohort.InputsHash;
        var playedBefore = PlayedHash(cohort);
        var first = cohort.Away.Lineup[0].Player;
        var replacement = WithContact(cohort.Sheets[first], 5);
        var count = cohort.Sheets.Count;

        TryMutate(cohort.Sheets, o => ((Dictionary<PlayerId, PlayerSheet>)o)[first] = replacement);
        TryMutate(cohort.Sheets, o => ((IDictionary<PlayerId, PlayerSheet>)o)[first] = replacement);
        TryMutate(cohort.Sheets, o => ((IDictionary<PlayerId, PlayerSheet>)o).Remove(first));
        TryMutate(cohort.Sheets, o => ((IDictionary<PlayerId, PlayerSheet>)o).Clear());

        Assert.Equal(count, cohort.Sheets.Count);
        Assert.Equal(90, cohort.Sheets[first].Ratings.Hitter!.Contact.Value);
        Assert.Equal(hashBefore, cohort.InputsHash);
        Assert.Equal(playedBefore, PlayedHash(cohort));
    }

    [Fact]
    public void A_lineup_changed_in_the_callers_list_after_creation_changes_neither_the_cohort_nor_its_games()
    {
        var source = MutableSource();
        var cohort = Build(source);
        var hashBefore = cohort.InputsHash;
        var playedBefore = PlayedHash(cohort);
        var orderBefore = cohort.Away.Lineup.Select(s => (s.Order, s.Player, s.Position)).ToList();

        var (a, b) = (source.AwayLineup[0], source.AwayLineup[1]);
        source.AwayLineup[0] = new LineupSlot(1, b.Player, a.Position);           // swap the first two batters
        source.AwayLineup[1] = new LineupSlot(2, a.Player, b.Position);
        source.AwayLineup.RemoveAt(8);

        Assert.Equal(orderBefore, cohort.Away.Lineup.Select(s => (s.Order, s.Player, s.Position)).ToList());
        Assert.Equal(hashBefore, cohort.InputsHash);
        Assert.Equal(playedBefore, PlayedHash(cohort));
    }

    [Fact]
    public void A_bench_changed_in_the_callers_list_after_creation_changes_neither_the_cohort_nor_its_games()
    {
        var source = MutableSource();
        var cohort = Build(source);
        var hashBefore = cohort.InputsHash;
        var benchBefore = cohort.Away.Bench.ToList();

        source.AwayBench.Reverse();
        source.AwayBench.Add(cohort.Away.StartingPitcher);

        Assert.Equal(benchBefore, cohort.Away.Bench.ToList());
        Assert.Equal(hashBefore, cohort.InputsHash);
    }

    [Fact]
    public void The_exposed_lineup_and_bench_cannot_be_modified_by_casting_them_back_to_a_list_or_an_array()
    {
        var cohort = Build(MutableSource());
        var hashBefore = cohort.InputsHash;
        var playedBefore = PlayedHash(cohort);
        var lineupBefore = cohort.Away.Lineup.ToList();
        var benchBefore = cohort.Away.Bench.ToList();
        var other = lineupBefore[1];

        foreach (var team in new[] { cohort.Away, cohort.Home })
        {
            TryMutate(team.Lineup, o => ((List<LineupSlot>)o)[0] = other);
            TryMutate(team.Lineup, o => ((LineupSlot[])o)[0] = other);
            TryMutate(team.Lineup, o => ((IList<LineupSlot>)o)[0] = other);
            TryMutate(team.Lineup, o => ((IList<LineupSlot>)o).Clear());
            TryMutate(team.Bench, o => ((List<PlayerId>)o).Clear());
            TryMutate(team.Bench, o => ((PlayerId[])o)[0] = team.StartingPitcher);
            TryMutate(team.Bench, o => ((IList<PlayerId>)o).Add(team.StartingPitcher));
        }

        Assert.Equal(lineupBefore, cohort.Away.Lineup.ToList());
        Assert.Equal(benchBefore, cohort.Away.Bench.ToList());
        Assert.Equal(9, cohort.Home.Lineup.Count);
        Assert.Equal(2, cohort.Home.Bench.Count);
        Assert.Equal(hashBefore, cohort.InputsHash);
        Assert.Equal(playedBefore, PlayedHash(cohort));
    }

    [Fact]
    public void The_hash_describes_the_snapshot_the_cohort_exposes_and_plays_so_rebuilding_from_it_gives_the_same_hash()
    {
        var cohort = Build(MutableSource());
        var rebuilt = H.Ok(CohortInputs.Create(cohort.CohortId, cohort.Kind, cohort.Description, cohort.Away, cohort.Home, cohort.Sheets.Values));

        Assert.Equal(cohort.InputsHash, rebuilt.InputsHash);
        Assert.Equal(cohort.InputSet.Id, rebuilt.InputSet.Id);
        Assert.Equal(PlayedHash(cohort), PlayedHash(rebuilt));
        Assert.Equal(Build(MutableSource()).InputsHash, cohort.InputsHash);          // independent but identical inputs: identical hash
    }

    [Fact]
    public void Changed_inputs_in_a_new_cohort_give_a_different_hash_and_the_input_set_id_does_not_cover_contact()
    {
        var baseline = Build(MutableSource());
        var contact = MutableSource();
        var index = contact.Sheets.FindIndex(s => s.Id == contact.Away.Lineup[0].Player);
        contact.Sheets[index] = WithContact(contact.Sheets[index], 5);
        var changedContact = Build(contact);

        var order = MutableSource();
        (order.AwayLineup[0], order.AwayLineup[1]) = (new LineupSlot(1, order.AwayLineup[1].Player, order.AwayLineup[0].Position), new LineupSlot(2, order.AwayLineup[0].Player, order.AwayLineup[1].Position));
        var changedOrder = Build(order);

        var bench = MutableSource();
        bench.AwayBench.Reverse();
        var changedBench = Build(bench);

        Assert.NotEqual(baseline.InputsHash, changedContact.InputsHash);
        Assert.Equal(baseline.InputSet.Id, changedContact.InputSet.Id);              // PlayerInputSet content v3 is unchanged: only InputsHash can see contact
        Assert.NotEqual(baseline.InputsHash, changedOrder.InputsHash);
        Assert.NotEqual(baseline.InputsHash, changedBench.InputsHash);

        // a rating the model does read at pitch level (the control of the away starter) changes the played game and the hash alike
        var control = MutableSource();
        var ci = control.Sheets.FindIndex(s => s.Id == control.Away.StartingPitcher);
        var sp = control.Sheets[ci];
        control.Sheets[ci] = H.WithRatings(sp, sp.Ratings.Hitter, sp.Ratings.Pitcher! with { Control = H.Skill(5) }, sp.Ratings.Catcher);
        var changedControl = Build(control);
        Assert.NotEqual(baseline.InputsHash, changedControl.InputsHash);
        Assert.NotEqual(PlayedHash(baseline), PlayedHash(changedControl));
    }

    [Fact]
    public void A_cohort_with_an_empty_slot_in_its_team_setup_is_refused_not_thrown()
    {
        var source = MutableSource();
        var broken = new TeamSetup([.. source.Away.Lineup.Take(8), null!], source.Away.StartingPitcher, source.Away.Bench);
        var result = CohortInputs.Create("broken", CohortKind.Neutral, "x", broken, source.Home, source.Sheets);
        Assert.False(result.IsSuccess);
        Assert.Equal("Harness.cohort_incomplete", result.Error!.Value.Code);
    }
}
