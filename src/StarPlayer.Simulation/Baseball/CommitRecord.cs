using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The immutable record of one confirmed official event (C3-A, game-engine 27.16 O-9). It holds only what verification and later storage need and names no
/// candidate, prepared pitch, draft or audit type. It is built before the compare-and-swap and is the very instance that becomes the next
/// <see cref="AuthoritativeGame.LastCommit"/> and is returned by <see cref="GameCursor.Publish"/>. <c>GameStatDelta</c> and the Phase 4 persistence format are later contracts.
/// </summary>
public sealed class CommitRecord
{
    internal CommitRecord(GameEvent confirmed, EventReceipt receipt, int eventSequence, long revisionBefore, long revisionAfter,
        string rngStateHashBefore, string rngStateHashAfter, ImmutableList<GameFact> facts, bool finalized)
    {
        Event = confirmed;
        Receipt = receipt;
        EventSequence = eventSequence;
        RevisionBefore = revisionBefore;
        RevisionAfter = revisionAfter;
        RngStateHashBefore = rngStateHashBefore;
        RngStateHashAfter = rngStateHashAfter;
        Facts = facts;
        Finalized = finalized;
    }

    public GameEvent Event { get; }

    public EventReceipt Receipt { get; }

    /// <summary>The sequence of the confirmed event's id; also the aggregate's <c>LastEventSequence</c> after this commit.</summary>
    public int EventSequence { get; }

    public long RevisionBefore { get; }

    public long RevisionAfter { get; }

    /// <summary>Equal to <see cref="RngStateHashAfter"/> for an event that draws no random number (every C3-A event).</summary>
    public string RngStateHashBefore { get; }

    public string RngStateHashAfter { get; }

    /// <summary>The reducer's facts, plus <c>GameCompleted</c> once when this commit finalized the game.</summary>
    public ImmutableList<GameFact> Facts { get; }

    /// <summary>True when the confirmed event ended the game and the same commit finalized it.</summary>
    public bool Finalized { get; }
}
