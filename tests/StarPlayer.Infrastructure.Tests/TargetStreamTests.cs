using System.Globalization;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C2A: the new stream <c>game/target@1</c>. Expected sequences come from the independent Python oracle (own PCG32 + kd1), never from the code under test.
/// The six earlier streams keep their meaning and their phase3a golden vectors (those tests are unchanged and still run in Simulation.Tests).
/// </summary>
public class TargetStreamTests
{
    private static readonly RngStreamId[] Seven =
        [GameRngStreams.Pitch, GameRngStreams.Contact, GameRngStreams.Field, GameRngStreams.Runner, GameRngStreams.Location, GameRngStreams.Ai, GameRngStreams.Target];

    private static uint Hex32(string text) => Convert.ToUInt32(text.Replace("0x", ""), 16);

    private static RngStreamId Id(string canonical)
    {
        var parts = canonical.Split('@');
        return RngStreamId.Create(parts[0], int.Parse(parts[1], CultureInfo.InvariantCulture)).Value;
    }

    [Fact]
    public void The_target_stream_is_game_target_version_1_and_the_set_is_the_documented_seven()
    {
        Assert.Equal("game/target", GameRngStreams.Target.Name);
        Assert.Equal(1, GameRngStreams.Target.Version);
        Assert.Equal("game/target@1", GameRngStreams.Target.Canonical);
        Assert.Equal(7, Seven.Select(s => s.Canonical).Distinct().Count());
        // The earlier six are unchanged, in their documented order.
        Assert.Equal(["game/pitch@1", "game/contact@1", "game/field@1", "game/runner@1", "game/location@1", "game/ai@1"], Seven.Take(6).Select(s => s.Canonical));
    }

    [Fact]
    public void Key_derivation_and_the_first_draws_match_the_independent_python_vectors()
    {
        foreach (var v in PitchBranchFixture.Golden.GetProperty("target_vectors").EnumerateArray())
        {
            var seed = PitchBranchFixture.Hex64(v.GetProperty("seed"));
            var context = v.GetProperty("context").GetString()!;
            Assert.Equal("game/target@1", v.GetProperty("stream").GetString());
            var (state, sequence) = StreamKeyDerivation.Derive(seed, context, GameRngStreams.Target);
            Assert.Equal(PitchBranchFixture.Hex64(v.GetProperty("initstate")), state);
            Assert.Equal(PitchBranchFixture.Hex64(v.GetProperty("initseq")), sequence);

            var raw = new Pcg32(state, sequence);
            Assert.Equal(v.GetProperty("u32").EnumerateArray().Select(e => Hex32(e.GetString()!)), Enumerable.Range(0, 6).Select(_ => raw.NextUInt32()));

            var stream = GameRandom.Create(seed, context).Value.Stream(GameRngStreams.Target);
            var expected = v.GetProperty("bounded_1000000").EnumerateArray().Select(e => e.GetUInt32()).ToArray();
            var cumulative = v.GetProperty("draws_after_each").EnumerateArray().Select(e => e.GetInt64()).ToArray();
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], stream.NextBounded(ProbabilityScale.RollBound));
                Assert.Equal(cumulative[i], stream.Draws);
            }
        }
    }

    [Fact]
    public void The_target_stream_is_a_different_sequence_from_every_other_stream_and_the_other_sequences_match_python()
    {
        var random = GameRandom.Create(0, "game:KBO-T:fx-0001").Value;
        var target = Enumerable.Range(0, 4).Select(_ => random.Stream(GameRngStreams.Target).NextUInt32()).ToArray();
        foreach (var v in PitchBranchFixture.Golden.GetProperty("independence_vectors").EnumerateArray())
        {
            var expected = v.GetProperty("u32").EnumerateArray().Select(e => Hex32(e.GetString()!)).ToArray();
            var other = GameRandom.Create(0, "game:KBO-T:fx-0001").Value.Stream(Id(v.GetProperty("stream").GetString()!));
            Assert.Equal(expected, Enumerable.Range(0, 4).Select(_ => other.NextUInt32())); // the six earlier streams are unchanged
            Assert.NotEqual(expected, target);
        }
    }

    [Fact]
    public void Drawing_on_the_target_stream_never_changes_another_stream_and_the_other_way_round()
    {
        const ulong seed = 20261012;
        const string context = "game:3c2a:independence";
        foreach (var other in Seven.Where(s => s != GameRngStreams.Target))
        {
            var plain = GameRandom.Create(seed, context).Value;
            var expectedOther = Enumerable.Range(0, 12).Select(_ => plain.Stream(other).NextBounded(ProbabilityScale.RollBound)).ToArray();
            var expectedTarget = Enumerable.Range(0, 12).Select(_ => plain.Stream(GameRngStreams.Target).NextBounded(ProbabilityScale.RollBound)).ToArray();

            var mixed = GameRandom.Create(seed, context).Value;
            var gotOther = new uint[12];
            var gotTarget = new uint[12];
            for (var i = 0; i < 12; i++)
            {
                gotTarget[i] = mixed.Stream(GameRngStreams.Target).NextBounded(ProbabilityScale.RollBound);
                gotOther[i] = mixed.Stream(other).NextBounded(ProbabilityScale.RollBound);
            }

            Assert.Equal(expectedOther, gotOther);
            Assert.Equal(expectedTarget, gotTarget);
        }
    }

    [Fact]
    public void A_rejected_raw_output_makes_draws_exceed_calls_without_changing_the_call_count()
    {
        var v = PitchBranchFixture.Golden.GetProperty("rejection_vector");
        var stream = GameRandom.Create(PitchBranchFixture.Hex64(v.GetProperty("seed")), v.GetProperty("context").GetString()!).Value.Stream(GameRngStreams.Target);
        var calls = v.GetProperty("calls").GetInt32();
        var values = v.GetProperty("bounded_1000000").EnumerateArray().Select(e => e.GetUInt32()).ToArray();
        var perCall = v.GetProperty("raw_draws_per_call").EnumerateArray().Select(e => e.GetInt64()).ToArray();

        for (var i = 0; i < calls; i++)
        {
            var before = stream.Draws;
            Assert.Equal(values[i], stream.NextBounded(ProbabilityScale.RollBound));
            Assert.Equal(perCall[i], stream.Draws - before);
        }

        Assert.Equal(v.GetProperty("raw_draws").GetInt64(), stream.Draws);
        Assert.True(stream.Draws > calls, "the vector must contain a rejection");
        Assert.Equal(2, perCall[v.GetProperty("rejected_call_index").GetInt32()]);
    }

    [Fact]
    public void The_snapshot_orders_the_target_stream_by_its_canonical_text_and_restores_to_the_same_continuation()
    {
        var random = GameRandom.Create(20261012, "game:3c2a:snapshot").Value;
        foreach (var stream in new[] { GameRngStreams.Target, GameRngStreams.Runner, GameRngStreams.Ai, GameRngStreams.Pitch })
            random.Stream(stream).NextBounded(ProbabilityScale.RollBound);

        var snapshot = random.Snapshot();
        Assert.Equal(["game/ai@1", "game/pitch@1", "game/runner@1", "game/target@1"], snapshot.Streams.Select(s => s.Stream.Canonical));

        var restored = GameRandom.Restore(snapshot, snapshot.StateHash).Value;
        for (var i = 0; i < 6; i++)
            Assert.Equal(random.Stream(GameRngStreams.Target).NextBounded(ProbabilityScale.RollBound), restored.Stream(GameRngStreams.Target).NextBounded(ProbabilityScale.RollBound));
        Assert.Equal(random.Snapshot().Canonical(), restored.Snapshot().Canonical());
    }

    [Fact]
    public void Looking_up_the_target_stream_without_drawing_does_not_change_the_snapshot()
    {
        var random = GameRandom.Create(1, "game:3c2a:lookup").Value;
        var before = random.Snapshot().StateHash;
        _ = random.Stream(GameRngStreams.Target).Draws;
        Assert.Equal(before, random.Snapshot().StateHash);
        Assert.Empty(random.Snapshot().Streams);
    }

    [Fact]
    public void The_algorithm_key_derivation_and_snapshot_format_names_are_unchanged()
    {
        Assert.Equal("pcg32-xsh-rr-v1", RngVersions.Algorithm);
        Assert.Equal("kd1", RngVersions.KeyDerivation);
        Assert.Equal("rngset1", RngVersions.SnapshotFormat);
        Assert.Equal(1_000_000u, ProbabilityScale.RollBound);
    }
}
