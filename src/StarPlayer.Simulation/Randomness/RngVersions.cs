namespace StarPlayer.Simulation.Randomness;

/// <summary>
/// Explicit version names of the fixed random-number contract (docs/decisions/ADR-004.md). A change to any behaviour behind one of these
/// names must introduce a NEW name; an existing name never changes meaning, so a stored snapshot can always be judged against its version.
/// </summary>
public static class RngVersions
{
    /// <summary>PCG32 XSH-RR 64/32 with the reference seeding procedure and the reference bounded-integer rejection sampling.</summary>
    public const string Algorithm = "pcg32-xsh-rr-v1";

    /// <summary>SHA-256 derivation of a stream's (initstate, initseq) from (master seed, context key, stream id).</summary>
    public const string KeyDerivation = "kd1";

    /// <summary>Canonical text and field set of <see cref="GameRandomSnapshot"/>.</summary>
    public const string SnapshotFormat = "rngset1";
}
