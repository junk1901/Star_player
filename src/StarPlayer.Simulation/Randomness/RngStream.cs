namespace StarPlayer.Simulation.Randomness;

/// <summary>
/// One independent stream of a <see cref="GameRandom"/>. Only <see cref="GameRandom.Stream"/> creates it; its position changes only through
/// its own draws or a committed <see cref="GameRandom.Atomically{T}"/> scope. Drawing is refused (<see cref="InvalidOperationException"/>, nothing
/// consumed) while the owner is locked by a running atomic scope and after the owner was a working copy whose scope has ended. Reading
/// <see cref="Draws"/> is always allowed. Single serialized writer; no thread safety.
/// </summary>
public sealed class RngStream
{
    private readonly Pcg32 _generator;
    private readonly GameRandom _owner;

    internal RngStream(RngStreamId id, Pcg32 generator, GameRandom owner)
    {
        Id = id;
        _generator = generator;
        _owner = owner;
    }

    public RngStreamId Id { get; }

    /// <summary>Raw 32-bit outputs consumed so far, rejected bounded outputs included.</summary>
    public long Draws => _generator.Draws;

    public uint NextUInt32()
    {
        _owner.EnsureWritable();
        return _generator.NextUInt32();
    }

    /// <summary>Unbiased integer in <c>[0, bound)</c> by rejection sampling; <paramref name="bound"/> 0 throws <see cref="ArgumentOutOfRangeException"/>.</summary>
    public uint NextBounded(uint bound)
    {
        _owner.EnsureWritable();
        return _generator.NextBounded(bound);
    }

    internal RngStreamSnapshot Snapshot() => new(Id, _generator.State, _generator.Increment, _generator.Draws);

    internal Pcg32 Generator => _generator;
}
