using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StarPlayer.Simulation.Randomness;

/// <summary>One stream's restorable position: where it is (<see cref="State"/>, <see cref="Increment"/>) and how many raw draws it has consumed.</summary>
public sealed record RngStreamSnapshot(RngStreamId Stream, ulong State, ulong Increment, long Draws);

/// <summary>
/// Everything needed to restore a <see cref="GameRandom"/> exactly, in the versioned format <see cref="RngVersions.SnapshotFormat"/> (<c>rngset1</c>):
/// format/algorithm/key-derivation versions, the master seed and context key (a stream that was never created is derived from them later),
/// and one entry per stream that has drawn at least once (a stream that was only looked up or created is omitted), sorted by the stream's canonical text (ordinal).
/// <para>
/// Canonical text (the persisted/hashed form; Phase 4 stores these fields): line 1
/// <c>rngset1|{algorithm}|{key derivation}|{master seed, 16 lower-case hex}|{context key}</c>, then one line per stream, in order:
/// <c>{name}@{version}|{state, 16 hex}|{increment, 16 hex}|{draws, decimal}</c>, lines joined by <c>\n</c> (no trailing newline).
/// <see cref="StateHash"/> is the lower-case hex SHA-256 of the UTF-8 canonical text.
/// </para>
/// </summary>
public sealed record GameRandomSnapshot(
    string FormatVersion,
    string AlgorithmVersion,
    string KeyDerivationVersion,
    ulong MasterSeed,
    string ContextKey,
    ImmutableArray<RngStreamSnapshot> Streams)
{
    public string Canonical()
    {
        var text = new StringBuilder();
        text.Append(FormatVersion).Append('|').Append(AlgorithmVersion).Append('|').Append(KeyDerivationVersion).Append('|')
            .Append(Hex(MasterSeed)).Append('|').Append(ContextKey);
        foreach (RngStreamSnapshot s in Streams)
            text.Append('\n').Append(s.Stream.Canonical).Append('|').Append(Hex(s.State)).Append('|').Append(Hex(s.Increment)).Append('|')
                .Append(s.Draws.ToString(CultureInfo.InvariantCulture));
        return text.ToString();
    }

    public string StateHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical()))).ToLowerInvariant();

    private static string Hex(ulong value) => value.ToString("x16", CultureInfo.InvariantCulture);
}
