using System.Collections.Immutable;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Randomness;

/// <summary>
/// The independent random streams of ONE game (or any other context key), derived from a master seed. Streams are created lazily and each
/// depends only on (master seed, context key, stream id) — see <see cref="StreamKeyDerivation"/> — so the order in which streams are first
/// used, or draws on any other stream, never changes a stream's sequence.
/// <para>
/// <b>Single serialized writer.</b> An instance, its streams and its scopes are not thread-safe and no concurrent use is supported or
/// detected; the caller serializes all access (ROADMAP §4.1, one serialized writer).
/// </para>
/// <para>
/// <b>Snapshots</b> contain only streams that have drawn at least once. Looking up a stream (<see cref="Stream"/>, reading
/// <see cref="RngStream.Draws"/>) is not use and never changes <see cref="GameRandomSnapshot.StateHash"/>; a stream that is absent from a snapshot
/// is, by definition, at its derived start.
/// </para>
/// <para>
/// <b>Atomic scope: what <see cref="Atomically{T}"/> guarantees.</b> Only the in-memory RNG state of THIS instance. Work runs on a private copy; a
/// successful, non-cancelled result publishes the copy's positions, anything else leaves this instance exactly as it was. While a scope runs this
/// instance is locked: drawing from it or from any stream obtained from it, looking up a stream, or starting another scope on it or on the copy
/// throws <see cref="InvalidOperationException"/> before anything changes (reading <see cref="Snapshot"/> is allowed and shows the last committed
/// state). After the scope ends the copy and its streams are closed and cannot draw. It does NOT cover event-id counters, <c>GameReducer</c> state,
/// event receipts, other objects or external side effects, and it does not publish "after a durable commit": it publishes when the work returns.
/// Combining it with the reducer, ids and the database transaction is the Phase 3-C/4 contract recorded in ADR-004.
/// </para>
/// </summary>
public sealed class GameRandom
{
    private readonly Dictionary<RngStreamId, RngStream> _streams = new();
    private bool _locked;
    private bool _isWorkCopy;
    private bool _closed;

    private GameRandom(ulong masterSeed, string contextKey)
    {
        MasterSeed = masterSeed;
        ContextKey = contextKey;
    }

    public ulong MasterSeed { get; }

    public string ContextKey { get; }

    public static Result<GameRandom> Create(ulong masterSeed, string? contextKey) =>
        StreamKeyDerivation.ValidateContext(contextKey) is { } error
            ? Result<GameRandom>.Failure(error)
            : Result<GameRandom>.Success(new GameRandom(masterSeed, contextKey!));

    /// <summary>The stream with this identity, created from its derived key on first use (creation alone does not change the snapshot).</summary>
    public RngStream Stream(RngStreamId id)
    {
        if (id.Name is null)
            throw new ArgumentException("기본값 스트림 ID는 쓸 수 없습니다.", nameof(id));
        EnsureWritable();
        if (!_streams.TryGetValue(id, out RngStream? stream))
        {
            stream = new RngStream(id, Seeded(id), this);
            _streams.Add(id, stream);
        }
        return stream;
    }

    /// <summary>
    /// The restorable state: format, versions, master seed, context key and, sorted by canonical stream text (ordinal), every stream that has
    /// drawn at least once. Streams never drawn from are omitted (they derive from the seed), so touching a stream does not change the hash.
    /// </summary>
    public GameRandomSnapshot Snapshot() => new(
        RngVersions.SnapshotFormat, RngVersions.Algorithm, RngVersions.KeyDerivation, MasterSeed, ContextKey,
        _streams.Values.Where(s => s.Draws > 0).Select(s => s.Snapshot()).OrderBy(s => s.Stream.Canonical, StringComparer.Ordinal).ToImmutableArray());

    /// <summary>
    /// Structure-only restore. Explicitly rejects an unknown format/algorithm/key-derivation version, an invalid context or stream, a duplicate
    /// stream, streams that are not in canonical order, a stream entry without draws, an even increment, a negative draw count, an increment that
    /// is not the one derived for that stream, and a state that is not the state reached after exactly <c>Draws</c> draws from the derived start.
    /// <para>
    /// <b>Limit:</b> this overload cannot know which streams SHOULD be present. A snapshot from which a consumed stream was removed is still
    /// internally consistent and restores with that stream back at its start. Whoever loads a persisted snapshot must use
    /// <see cref="Restore(GameRandomSnapshot?, string)"/> with the hash that was committed with the game state.
    /// </para>
    /// </summary>
    public static Result<GameRandom> Restore(GameRandomSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Streams.IsDefault)
            return Fail("Rng.snapshot_missing", "난수 스냅샷이 없습니다.");
        if (snapshot.FormatVersion != RngVersions.SnapshotFormat)
            return Fail("Rng.snapshot_format_unsupported", $"지원하지 않는 난수 스냅샷 형식입니다: {snapshot.FormatVersion}");
        if (snapshot.AlgorithmVersion != RngVersions.Algorithm)
            return Fail("Rng.algorithm_unsupported", $"지원하지 않는 난수 알고리즘 버전입니다: {snapshot.AlgorithmVersion}");
        if (snapshot.KeyDerivationVersion != RngVersions.KeyDerivation)
            return Fail("Rng.key_derivation_unsupported", $"지원하지 않는 키 유도 버전입니다: {snapshot.KeyDerivationVersion}");
        Result<GameRandom> created = Create(snapshot.MasterSeed, snapshot.ContextKey);
        if (!created.IsSuccess)
            return created;
        GameRandom restored = created.Value;
        string? previous = null;
        foreach (RngStreamSnapshot s in snapshot.Streams)
        {
            Result<RngStreamId> id = RngStreamId.Create(s.Stream.Name, s.Stream.Version);
            if (!id.IsSuccess)
                return Result<GameRandom>.Failure(id.Error!.Value);
            if (restored._streams.ContainsKey(id.Value))
                return Fail("Rng.stream_duplicate", $"스냅샷에 같은 스트림이 두 번 있습니다: {id.Value.Canonical}");
            if (previous is not null && string.CompareOrdinal(previous, id.Value.Canonical) > 0)
                return Fail("Rng.stream_order_noncanonical", $"스냅샷의 스트림이 정규 순서(정규 텍스트의 서수 오름차순)가 아닙니다: {id.Value.Canonical}");
            if (s.Draws == 0)
                return Fail("Rng.stream_unused", $"소비 횟수가 0인 스트림은 스냅샷에 넣지 않습니다: {id.Value.Canonical}");
            Result<Pcg32> position = Pcg32.FromState(s.State, s.Increment, s.Draws);
            if (!position.IsSuccess)
                return Result<GameRandom>.Failure(position.Error!.Value);
            Pcg32 expected = restored.Seeded(id.Value);
            if (expected.Increment != s.Increment)
                return Fail("Rng.increment_mismatch", $"스트림 증분이 시드에서 유도한 값과 다릅니다: {id.Value.Canonical}");
            expected.Advance((ulong)s.Draws);
            if (expected.State != s.State)
                return Fail("Rng.state_mismatch", $"스트림 상태가 소비 횟수와 맞지 않습니다: {id.Value.Canonical}");
            restored._streams.Add(id.Value, new RngStream(id.Value, position.Value, restored));
            previous = id.Value.Canonical;
        }
        return Result<GameRandom>.Success(restored);
    }

    /// <summary>
    /// Restore against a separately committed expectation. <paramref name="expectedStateHash"/> is the <see cref="GameRandomSnapshot.StateHash"/> of
    /// the LIVE state, recorded at commit time (never recomputed from the stored snapshot being checked). After the structure-only checks, the state
    /// that was actually rebuilt must hash to exactly that value, so a removed, added, altered or reordered stream — also one that leaves the stored
    /// snapshot self-consistent — is rejected with <c>Rng.state_hash_mismatch</c>. A malformed expectation is <c>Rng.expected_hash_invalid</c>.
    /// <para>
    /// The hash detects accidental corruption and lost or substituted parts. It is NOT an authentication or tamper-proofing mechanism (no secret, anyone
    /// can compute it). Phase 4 must write this value in the same storage transaction as the game state and event receipts (ADR-004); until then no
    /// persisted snapshot exists. Failure creates nothing and changes no existing instance.
    /// </para>
    /// </summary>
    public static Result<GameRandom> Restore(GameRandomSnapshot? snapshot, string? expectedStateHash)
    {
        if (!IsHash(expectedStateHash))
            return Fail("Rng.expected_hash_invalid", "기대 해시는 소문자 16진수 64자여야 합니다.");
        Result<GameRandom> restored = Restore(snapshot);
        if (!restored.IsSuccess)
            return restored;
        return string.Equals(restored.Value.Snapshot().StateHash, expectedStateHash, StringComparison.Ordinal)
            ? restored
            : Fail("Rng.state_hash_mismatch", "복원한 난수 상태가 함께 확정된 기대 해시와 다릅니다(스트림 누락·추가·변조 가능).");
    }

    /// <summary>
    /// Runs <paramref name="work"/> on a private copy of every stream. Only a successful result that was not cancelled is published to this
    /// instance (existing <see cref="RngStream"/> objects keep their identity and take the new position). A failed result returns unchanged;
    /// an exception or cancellation (<see cref="OperationCanceledException"/>) propagates with this instance untouched, and the lock is always
    /// released. A scope started while another scope is running on this instance, or on a working copy, throws
    /// <see cref="InvalidOperationException"/>. Guarantee limits: see the class remarks (memory RNG state only).
    /// </summary>
    public Result<T> Atomically<T>(Func<GameRandom, Result<T>> work, CancellationToken cancellation = default)
    {
        if (_locked || _isWorkCopy || _closed)
            throw new InvalidOperationException("이미 작업 범위가 진행 중이거나 끝난 난수에서는 새 작업 범위를 시작할 수 없습니다(중첩 금지).");
        cancellation.ThrowIfCancellationRequested();
        var copy = new GameRandom(MasterSeed, ContextKey) { _isWorkCopy = true };
        foreach (RngStream s in _streams.Values)
            copy._streams.Add(s.Id, new RngStream(s.Id, s.Generator.Clone(), copy));

        _locked = true;
        try
        {
            Result<T> result;
            try
            {
                result = work(copy);
            }
            finally
            {
                copy._closed = true; // a stream that escaped the scope can no longer draw
            }
            cancellation.ThrowIfCancellationRequested();
            if (!result.IsSuccess)
                return result;

            foreach (RngStream s in copy._streams.Values)
            {
                if (_streams.TryGetValue(s.Id, out RngStream? existing))
                    existing.Generator.CopyFrom(s.Generator);
                else
                    _streams.Add(s.Id, new RngStream(s.Id, s.Generator.Clone(), this));
            }
            return result;
        }
        finally
        {
            _locked = false;
        }
    }

    internal void EnsureWritable()
    {
        if (_locked)
            throw new InvalidOperationException("작업 범위가 진행 중인 동안 원본 난수는 쓸 수 없습니다. 작업에 전달된 복사본을 사용하세요.");
        if (_closed)
            throw new InvalidOperationException("작업 범위가 이미 끝난 복사본입니다.");
    }

    private Pcg32 Seeded(RngStreamId id)
    {
        (ulong initState, ulong initSeq) = StreamKeyDerivation.Derive(MasterSeed, ContextKey, id);
        return new Pcg32(initState, initSeq);
    }

    private static bool IsHash(string? s) =>
        s is { Length: 64 } && s.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result<GameRandom> Fail(string code, string message) => Result<GameRandom>.Failure(new Error(code, message));
}
