namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The result of <see cref="CommitGate.Stage"/>: the aggregate the work was based on, the aggregate to publish and the record of the commit. It is immutable and
/// changes nothing by existing; only <see cref="GameCursor.Publish"/> can make <see cref="Next"/> current, and only while the cursor still holds
/// <see cref="Expected"/> by reference, so a staged commit succeeds at most once. <c>Next.LastCommit</c> and <see cref="Record"/> are the same instance.
/// </summary>
public sealed class StagedCommit
{
    internal StagedCommit(AuthoritativeGame expected, AuthoritativeGame next, CommitRecord record)
    {
        Expected = expected;
        Next = next;
        Record = record;
    }

    public AuthoritativeGame Expected { get; }

    public AuthoritativeGame Next { get; }

    public CommitRecord Record { get; }
}
