using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The separate entry for confirming a pitch candidate (C3-B, game-engine 27.18 B-04, alternative A): <c>PitchCommitGate</c> to an internal <see cref="CommitEvidence"/> to the common
/// <see cref="CommitGate"/> core to <see cref="StagedCommit"/> to <see cref="GameCursor.Publish"/>. It only couples the candidate to the inputs of THIS call; it performs no official
/// step itself. Event-id checks, the reducer re-run, finalization, the revision and the staged commit are made only by the core, and publication stays the cursor's one compare-and-swap.
/// <para>
/// The caller supplies the tuning profile and the two player sheets because the aggregate holds neither (as in 3-C2B-1); their trust stays the caller's. Phase 3-D v2 adds the game's
/// immutable <see cref="PlayerInputSet"/> the same way: the evidence compares it with the set the pitch was prepared with (identity and the frozen fielders), through the unchanged
/// <see cref="PitchPreparation.CheckPrepared"/> step 7a, before the reducer re-run and the compare-and-swap; nothing is checked or changed after the swap. A pending advancement has no
/// entry at all: only a finished <see cref="InPlayPitchCandidate"/> (resolved or base) can be confirmed. Nothing is drawn here: the RNG position adopted is the candidate's own,
/// proved against the aggregate's by the core (O-2 step 10b). A failure or a cancellation changes nothing.
/// </para>
/// </summary>
public static class PitchCommitGate
{
    /// <summary>Stages a non-in-play candidate (a pitch or an explicit take) together with the <see cref="PreparedPitch"/> it was built from.</summary>
    public static Result<StagedCommit> Stage(AuthoritativeGame? expected, PreparedPitch? prepared, NonInPlayPitchCandidate? candidate, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, CancellationToken cancellation = default, PlayerInputSet? playerInputs = null) =>
        Stage(expected, prepared, candidate, tuning, batter, pitcher, null, cancellation, playerInputs);

    /// <summary>The same, with the core's test hook (it sees each stage name before the cancellation token is checked at it).</summary>
    internal static Result<StagedCommit> Stage(AuthoritativeGame? expected, PreparedPitch? prepared, NonInPlayPitchCandidate? candidate, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, Action<string>? checkpoint, CancellationToken cancellation, PlayerInputSet? playerInputs = null)
    {
        var evidence = CommitEvidence.ForNonInPlay(expected, prepared, candidate, tuning, batter, pitcher, playerInputs);
        return CommitGate.Stage(expected, evidence.Event, evidence, checkpoint, cancellation);
    }

    /// <summary>Stages a finished in-play candidate (base or with its advancement resolved); its <see cref="PreparedPitch"/> is <c>candidate.Plan.Prepared</c>.</summary>
    public static Result<StagedCommit> Stage(AuthoritativeGame? expected, InPlayPitchCandidate? candidate, TuningProfile? tuning, PlayerSheet? batter, PlayerSheet? pitcher,
        CancellationToken cancellation = default, PlayerInputSet? playerInputs = null) =>
        Stage(expected, candidate, tuning, batter, pitcher, null, cancellation, playerInputs);

    /// <summary>The same, with the core's test hook.</summary>
    internal static Result<StagedCommit> Stage(AuthoritativeGame? expected, InPlayPitchCandidate? candidate, TuningProfile? tuning, PlayerSheet? batter, PlayerSheet? pitcher,
        Action<string>? checkpoint, CancellationToken cancellation, PlayerInputSet? playerInputs = null)
    {
        var evidence = CommitEvidence.ForInPlay(expected, candidate, tuning, batter, pitcher, playerInputs);
        return CommitGate.Stage(expected, evidence.Event, evidence, checkpoint, cancellation);
    }
}
