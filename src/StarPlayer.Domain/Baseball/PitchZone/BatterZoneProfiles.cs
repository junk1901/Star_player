using System.Collections.Immutable;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball.PitchZone;

/// <summary>One batter's hidden tendency. Hidden exactly like <see cref="BatterZoneTendency"/>: its text form never prints the vector.</summary>
public readonly record struct BatterZoneProfileEntry(PlayerId Player, BatterZoneTendency Tendency)
{
    public override string ToString() => "BatterZoneProfileEntry(hidden)";
}

/// <summary>
/// An immutable, sparse set of batters' HIDDEN zone tendencies keyed by <see cref="PlayerId"/> (Phase 3-D, BatterTendency v2; ADR-007). A batter without an entry has no tendency:
/// <see cref="TendencyFor"/> returns <see cref="BatterZoneTendency.Neutral"/> and <see cref="Find"/> returns null; only a MISSING entry is neutral, a corrupt one is rejected when the set is created.
/// <list type="bullet">
/// <item>Source order of the entries is kept (a data pack that is read and written again keeps it).</item>
/// <item>The input is copied on creation; nothing handed out can change the set (the entries are an immutable list of immutable tendencies).</item>
/// <item>Hidden: only the trusted world holder, the data pack reader/writer and the resolver preparation may touch it. <see cref="ToString"/> prints no player and no value.</item>
/// </list>
/// </summary>
public sealed class BatterZoneProfiles
{
    public static BatterZoneProfiles Empty { get; } = new([], ImmutableDictionary<PlayerId, BatterZoneTendency>.Empty);

    private readonly ImmutableDictionary<PlayerId, BatterZoneTendency> _byPlayer;

    /// <summary>The entries in source order. Read-only.</summary>
    public IReadOnlyList<BatterZoneProfileEntry> Entries { get; }

    public int Count => Entries.Count;

    private BatterZoneProfiles(ImmutableArray<BatterZoneProfileEntry> entries, ImmutableDictionary<PlayerId, BatterZoneTendency> byPlayer)
    {
        Entries = entries;
        _byPlayer = byPlayer;
    }

    /// <summary>Copies <paramref name="entries"/>; a null set, a default player id, a null tendency or a repeated player is an error (never skipped, never neutralised).</summary>
    public static Result<BatterZoneProfiles> Create(IEnumerable<BatterZoneProfileEntry>? entries)
    {
        static Result<BatterZoneProfiles> Fail(string code, string message) => Result<BatterZoneProfiles>.Failure(new Error(code, message));

        if (entries is null)
            return Fail("BatterZoneProfiles.entries_missing", "타자 코스 성향 목록이 없습니다.");
        var list = entries.ToImmutableArray();
        var map = ImmutableDictionary.CreateBuilder<PlayerId, BatterZoneTendency>();
        foreach (var entry in list)
        {
            if (PlayerId.Create(entry.Player.Value) is { IsSuccess: false } badId)
                return Fail("BatterZoneProfiles.player_invalid", badId.Error!.Value.Message);
            if (entry.Tendency is null)
                return Fail("BatterZoneProfiles.tendency_missing", $"선수 {entry.Player}의 타자 코스 성향이 없습니다.");
            if (!map.TryAdd(entry.Player, entry.Tendency))
                return Fail("BatterZoneProfiles.duplicate_player", $"선수 {entry.Player}의 타자 코스 성향이 중복되었습니다.");
        }

        return Result<BatterZoneProfiles>.Success(new BatterZoneProfiles(list, map.ToImmutable()));
    }

    /// <summary>The player's tendency, or null when this set has none for him (resolver-side callers then use the neutral path).</summary>
    public BatterZoneTendency? Find(PlayerId player) => _byPlayer.TryGetValue(player, out var found) ? found : null;

    /// <summary>The player's tendency, or <see cref="BatterZoneTendency.Neutral"/> when he has none. Never an error: a missing entry is the only neutral case.</summary>
    public BatterZoneTendency TendencyFor(PlayerId player) => Find(player) ?? BatterZoneTendency.Neutral;

    public bool Contains(PlayerId player) => _byPlayer.ContainsKey(player);

    public override string ToString() => "BatterZoneProfiles(hidden)";
}
