using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Tests;

/// <summary>Phase 3-D (BatterTendency v2): the immutable, sparse, hidden set of per-player zone tendencies.</summary>
public class BatterZoneProfilesTests
{
    private static PlayerId P(string id) => PlayerId.Create(id).Value;

    private static BatterZoneTendency Make(params int[] values) => BatterZoneTendency.Create(values, ZoneReferenceDistribution.UniformV1).Value;

    private static readonly BatterZoneTendency A = Make(60, -60, 0, 0, 0, 0, 0, 0, 0);
    private static readonly BatterZoneTendency B = Make(0, 0, 0, 0, -90, 90, 0, 0, 0);

    private static BatterZoneProfiles Set(params (string Id, BatterZoneTendency Tendency)[] rows) =>
        BatterZoneProfiles.Create(rows.Select(r => new BatterZoneProfileEntry(P(r.Id), r.Tendency))).Value;

    // ---- neutral / lookup ----

    [Fact]
    public void The_empty_set_has_no_entry_and_every_player_is_neutral()
    {
        var empty = BatterZoneProfiles.Empty;

        Assert.Equal(0, empty.Count);
        Assert.Empty(empty.Entries);
        Assert.Null(empty.Find(P("p-0001")));
        Assert.Same(BatterZoneTendency.Neutral, empty.TendencyFor(P("p-0001")));
        Assert.False(empty.Contains(P("p-0001")));
        Assert.Equal(0, BatterZoneProfiles.Create([]).Value.Count);
    }

    [Fact]
    public void A_listed_player_gets_his_own_tendency_and_an_unlisted_player_is_neutral()
    {
        var set = Set(("p-0001", A), ("p-0002", B));

        Assert.Equal(A, set.TendencyFor(P("p-0001")));
        Assert.Equal(B, set.TendencyFor(P("p-0002")));
        Assert.Same(A, set.Find(P("p-0001")));
        Assert.Null(set.Find(P("p-0003")));
        Assert.Same(BatterZoneTendency.Neutral, set.TendencyFor(P("p-0003")));
        Assert.True(set.Contains(P("p-0002")));
        Assert.False(set.Contains(P("p-0003")));
        Assert.NotEqual(set.TendencyFor(P("p-0001")), set.TendencyFor(P("p-0002")));
    }

    [Fact]
    public void An_id_that_only_looks_like_another_is_a_different_player()
    {
        var set = Set(("p-0001", A));

        Assert.Same(BatterZoneTendency.Neutral, set.TendencyFor(P("p-00010")));
        Assert.Same(BatterZoneTendency.Neutral, set.TendencyFor(P("P-0001"))); // ids are exact, case-sensitive text
    }

    [Fact]
    public void An_explicit_all_zero_entry_is_kept_as_an_entry_and_is_equal_to_neutral_in_effect()
    {
        var set = Set(("p-0001", Make(0, 0, 0, 0, 0, 0, 0, 0, 0)));

        Assert.True(set.Contains(P("p-0001")));
        Assert.Equal(1, set.Count);
        Assert.True(set.TendencyFor(P("p-0001")).IsNeutral);
        Assert.Equal(BatterZoneTendency.Neutral, set.TendencyFor(P("p-0001")));
    }

    [Fact]
    public void Source_order_of_the_entries_is_kept()
    {
        var set = Set(("p-0009", A), ("p-0002", B), ("p-0005", A));

        Assert.Equal(["p-0009", "p-0002", "p-0005"], set.Entries.Select(e => e.Player.Value));
    }

    // ---- validation ----

    [Fact]
    public void A_repeated_player_is_rejected_and_nothing_is_kept()
    {
        var made = BatterZoneProfiles.Create([new BatterZoneProfileEntry(P("p-0001"), A), new BatterZoneProfileEntry(P("p-0001"), B)]);

        Assert.False(made.IsSuccess);
        Assert.Equal("BatterZoneProfiles.duplicate_player", made.Error!.Value.Code);
    }

    [Fact]
    public void A_null_set_a_default_player_and_a_missing_tendency_are_rejected()
    {
        Assert.Equal("BatterZoneProfiles.entries_missing", BatterZoneProfiles.Create(null).Error!.Value.Code);
        Assert.Equal("BatterZoneProfiles.player_invalid", BatterZoneProfiles.Create([new BatterZoneProfileEntry(default, A)]).Error!.Value.Code);
        Assert.Equal("BatterZoneProfiles.tendency_missing", BatterZoneProfiles.Create([new BatterZoneProfileEntry(P("p-0001"), null!)]).Error!.Value.Code);
        Assert.Equal("BatterZoneProfiles.player_invalid", BatterZoneProfiles.Create([default(BatterZoneProfileEntry)]).Error!.Value.Code);
    }

    [Fact]
    public void An_invalid_vector_cannot_reach_the_set_because_the_tendency_factory_rejects_it_first()
    {
        Assert.Equal("BatterZoneTendency.value_out_of_range", BatterZoneTendency.Create([101, -101, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Error!.Value.Code);
        Assert.Equal("BatterZoneTendency.length_invalid", BatterZoneTendency.Create([1, -1], ZoneReferenceDistribution.UniformV1).Error!.Value.Code);
        Assert.Equal("BatterZoneTendency.not_average_neutral", BatterZoneTendency.Create([1, 0, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Error!.Value.Code);
        Assert.Equal("ZoneReferenceDistribution.id_unknown", ZoneReferenceDistribution.Resolve("pzl-ref-other").Error!.Value.Code);
    }

    [Fact]
    public void Average_neutrality_is_judged_by_the_registered_reference_weights_not_by_a_hard_coded_uniform_sum()
    {
        // under weights [2,1,1,...] the vector [1,-2,0...] balances (2*1 + 1*-2 = 0) although its plain sum is -1; under the uniform reference it does not
        var weighted = ZoneReferenceDistribution.CreateUnregisteredForTest("t-weighted", [2, 1, 1, 1, 1, 1, 1, 1, 1]).Value;
        int[] values = [1, -2, 0, 0, 0, 0, 0, 0, 0];

        var underWeighted = BatterZoneTendency.Create(values, weighted);
        var underUniform = BatterZoneTendency.Create(values, ZoneReferenceDistribution.UniformV1);

        Assert.True(underWeighted.IsSuccess);
        Assert.Equal("t-weighted", underWeighted.Value.ReferenceId);
        Assert.Equal("BatterZoneTendency.not_average_neutral", underUniform.Error!.Value.Code);
        Assert.NotEqual(underWeighted.Value, BatterZoneTendency.Create([1, -1, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Value);
    }

    // ---- immutability ----

    [Fact]
    public void Changing_the_input_list_after_creation_does_not_change_the_set()
    {
        var input = new List<BatterZoneProfileEntry> { new(P("p-0001"), A) };
        var set = BatterZoneProfiles.Create(input).Value;

        input.Add(new BatterZoneProfileEntry(P("p-0002"), B));
        input[0] = new BatterZoneProfileEntry(P("p-0001"), B);
        input.Clear();

        Assert.Equal(1, set.Count);
        Assert.Equal(A, set.TendencyFor(P("p-0001")));
        Assert.False(set.Contains(P("p-0002")));
    }

    [Fact]
    public void Changing_the_input_value_array_after_creating_the_tendency_does_not_change_the_set()
    {
        int[] values = [60, -60, 0, 0, 0, 0, 0, 0, 0];
        var tendency = BatterZoneTendency.Create(values, ZoneReferenceDistribution.UniformV1).Value;
        var set = Set(("p-0001", tendency));

        values[0] = -100;
        values[8] = 7;

        Assert.Equal(60, set.TendencyFor(P("p-0001")).Values[0]);
        Assert.Equal(0, set.TendencyFor(P("p-0001")).Values[8]);
    }

    [Fact]
    public void The_entries_and_the_values_cannot_be_written_through_a_cast()
    {
        var set = Set(("p-0001", A));

        Assert.Throws<NotSupportedException>(() => ((IList<BatterZoneProfileEntry>)set.Entries).Add(new BatterZoneProfileEntry(P("p-0002"), B)));
        Assert.Throws<NotSupportedException>(() => ((IList<BatterZoneProfileEntry>)set.Entries)[0] = new BatterZoneProfileEntry(P("p-0002"), B));
        Assert.Throws<NotSupportedException>(() => ((IList<int>)set.TendencyFor(P("p-0001")).Values)[0] = 5);
        Assert.Empty(ImmutabilityAudit.Violations(set));
        Assert.Equal(60, set.TendencyFor(P("p-0001")).Values[0]);
        Assert.Empty(ImmutabilityAudit.Violations(BatterZoneProfiles.Empty));
    }

    [Fact]
    public void Two_sets_built_from_the_same_rows_are_independent_but_equal_in_content()
    {
        var one = Set(("p-0001", A), ("p-0002", B));
        var two = Set(("p-0001", A), ("p-0002", B));

        Assert.NotSame(one, two);
        Assert.Equal(one.Entries.Select(e => (e.Player, e.Tendency)), two.Entries.Select(e => (e.Player, e.Tendency)));
    }

    // ---- hidden text form ----

    [Fact]
    public void The_text_forms_print_no_player_and_no_value()
    {
        var set = Set(("p-4242", Make(77, -77, 0, 0, 0, 0, 0, 0, 0)));

        Assert.Equal("BatterZoneProfiles(hidden)", set.ToString());
        Assert.Equal("BatterZoneProfileEntry(hidden)", set.Entries[0].ToString());
        Assert.Equal("BatterZoneProfiles(hidden)", BatterZoneProfiles.Empty.ToString());
        foreach (var text in new[] { set.ToString(), set.Entries[0].ToString(), $"{set}", $"{set.Entries[0]}" })
        {
            Assert.DoesNotContain("77", text);
            Assert.DoesNotContain("4242", text);
        }
    }
}
