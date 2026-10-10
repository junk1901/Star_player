using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Tests.Randomness;

public class GameIdentityTests
{
    [Fact]
    public void Game_id_is_a_pure_function_of_its_parts()
    {
        Assert.Equal("game:KBO-T:fx-0001", GameIds.Create("KBO-T", "fx-0001").Value);
        Assert.Equal(GameIds.Create("KBO-T", "fx-0001").Value, GameIds.Create("KBO-T", "fx-0001").Value);
    }

    [Fact]
    public void Different_parts_never_collide()
    {
        var ids = new List<string>();
        foreach (string c in new[] { "a", "a-b", "a.b", "ab" })
            foreach (string f in new[] { "b", "b-a", "1", "a" })
                ids.Add(GameIds.Create(c, f).Value);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        // ':' is not allowed inside a part, so ("a:b","c") and ("a","b:c") cannot both map to game:a:b:c.
        Assert.False(GameIds.Create("a:b", "c").IsSuccess);
        Assert.False(GameIds.Create("a", "b:c").IsSuccess);
    }

    [Theory]
    [InlineData(null, "f")]
    [InlineData("c", null)]
    [InlineData("", "f")]
    [InlineData("c", "")]
    [InlineData("c d", "f")]
    [InlineData("c", "한글")]
    public void Invalid_game_id_parts_are_rejected(string? competition, string? fixture)
    {
        Assert.Equal("Id.game_part_invalid", GameIds.Create(competition, fixture).Error!.Value.Code);
    }

    [Fact]
    public void Over_long_game_ids_are_rejected_and_the_limit_is_exact()
    {
        Assert.Equal("Id.game_too_long", GameIds.Create(new string('a', 30), new string('b', 30)).Error!.Value.Code);
        Assert.True(GameIds.Create(new string('a', 29), new string('b', 29)).IsSuccess); // "game:" + 29 + ":" + 29 = 64
    }

    [Fact]
    public void Event_ids_follow_the_sequence_and_are_unique()
    {
        var seq = GameEventIds.Create("game:KBO-T:fx-0001").Value;
        string[] ids = Enumerable.Range(0, 2000).Select(_ => seq.Next().Value).ToArray();
        Assert.Equal("game:KBO-T:fx-0001:e000001", ids[0]);
        Assert.Equal("game:KBO-T:fx-0001:e002000", ids[^1]);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ids.OrderBy(i => i, StringComparer.Ordinal), ids);
        Assert.Equal(2000, seq.LastIssued);
    }

    [Fact]
    public void Two_runs_of_the_same_game_issue_the_same_ids()
    {
        string[] Run() { var s = GameEventIds.Create("game:A:1").Value; return Enumerable.Range(0, 50).Select(_ => s.Next().Value).ToArray(); }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Resuming_from_a_saved_sequence_never_reissues_an_id()
    {
        var first = GameEventIds.Create("game:A:1").Value;
        var issued = Enumerable.Range(0, 10).Select(_ => first.Next().Value).ToList();
        var resumed = GameEventIds.Create("game:A:1", first.LastIssued).Value;
        issued.AddRange(Enumerable.Range(0, 10).Select(_ => resumed.Next().Value));
        Assert.Equal(20, issued.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("game:A:1:e000020", issued[^1]);
    }

    [Fact]
    public void Sequence_exhaustion_fails_instead_of_wrapping_and_burns_nothing()
    {
        var s = GameEventIds.Create("game:A:1", GameEventIds.MaxSequence - 1).Value;
        Assert.Equal("game:A:1:e999999", s.Next().Value);
        Assert.Equal("Id.sequence_exhausted", s.Next().Error!.Value.Code);
        Assert.Equal(GameEventIds.MaxSequence, s.LastIssued);
    }

    [Fact]
    public void Bad_event_id_inputs_are_rejected()
    {
        Assert.Equal("Id.game_invalid", GameEventIds.Create(null).Error!.Value.Code);
        Assert.Equal("Id.game_invalid", GameEventIds.Create("bad id").Error!.Value.Code);
        Assert.Equal("Id.sequence_invalid", GameEventIds.Create("game:A:1", -1).Error!.Value.Code);
        Assert.Equal("Id.sequence_invalid", GameEventIds.Create("game:A:1", GameEventIds.MaxSequence + 1).Error!.Value.Code);
    }

    [Fact]
    public void ParseSequence_accepts_only_canonical_ids_of_this_game()
    {
        var s = GameEventIds.Create("game:A:1").Value;
        Assert.Equal(42, s.ParseSequence("game:A:1:e000042").Value);
        foreach (string? bad in new[] { null, "", "game:A:2:e000042", "game:A:1:e42", "game:A:1:e0000042", "game:A:1:e000000", "game:A:1:e-00042", "game:A:1:e00004x", "game:A:1:e000042 " })
            Assert.Equal("Id.event_id_invalid", s.ParseSequence(bad).Error!.Value.Code);
    }

    [Fact]
    public void Generating_ids_neither_consumes_nor_changes_game_randomness()
    {
        // The id types take no random source at all; this proves it from the outside: interleaving id generation with draws changes nothing.
        var plain = GameRandom.Create(1, "game:A:1").Value;
        var withIds = GameRandom.Create(1, "game:A:1").Value;
        var ids = GameEventIds.Create("game:A:1").Value;

        var expected = new List<uint>();
        var actual = new List<uint>();
        for (int i = 0; i < 40; i++)
        {
            expected.Add(plain.Stream(GameRngStreams.Pitch).NextUInt32());
            string before = withIds.Snapshot().Canonical();
            Assert.True(ids.Next().IsSuccess);
            Assert.True(GameIds.Create("KBO-T", $"fx-{i:0000}").IsSuccess);
            Assert.Equal(before, withIds.Snapshot().Canonical());
            actual.Add(withIds.Stream(GameRngStreams.Pitch).NextUInt32());
        }
        Assert.Equal(expected, actual);
        Assert.Equal(plain.Snapshot().StateHash, withIds.Snapshot().StateHash);
    }
}
