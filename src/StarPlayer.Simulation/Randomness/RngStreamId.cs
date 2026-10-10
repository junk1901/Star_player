using System.Globalization;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Randomness;

/// <summary>
/// Identity of an independent random stream: a stable name plus a stream version. The version is part of the key derivation, so changing
/// what a stream is consumed for (its draw order) means a new version and an unrelated sequence, never an edit in place.
/// Canonical text: <c>name@version</c> (e.g. <c>game/location@1</c>). Names are lower-case segments of <c>[a-z0-9_]</c> joined by <c>/</c>.
/// </summary>
public readonly record struct RngStreamId
{
    public string Name { get; private init; }

    public int Version { get; private init; }

    public string Canonical => $"{Name}@{Version.ToString(CultureInfo.InvariantCulture)}";

    public static Result<RngStreamId> Create(string? name, int version)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 64 || !name.Split('/').All(IsSegment))
            return Result<RngStreamId>.Failure(new Error("Rng.stream_name_invalid", "스트림 이름은 소문자·숫자·'_'로 된 구간을 '/'로 이은 64자 이하여야 합니다."));
        if (version < 1)
            return Result<RngStreamId>.Failure(new Error("Rng.stream_version_invalid", "스트림 버전은 1 이상이어야 합니다."));
        return Result<RngStreamId>.Success(new RngStreamId { Name = name, Version = version });
    }

    public override string ToString() => Canonical;

    internal static RngStreamId Known(string name, int version) => Create(name, version).Value;

    private static bool IsSegment(string s) => s.Length > 0 && s.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
}

/// <summary>
/// The game's stream names (ROADMAP §4.1/§6.1, §1.14). Each is used for one purpose only; adding a consumer to a stream, or any draw on
/// another stream, must not change this stream's sequence (streams are keyed independently, see <see cref="StreamKeyDerivation"/>).
/// Broadcast Commentary Lite and Live Match Viewer Lite have NO stream here and never draw from any of these.
/// <list type="bullet">
/// <item><c>game/pitch</c> pitch-level outcomes (take/swing, called pitch, strike/ball, HBP) — consumers arrive in Phase 3-C/3-D.</item>
/// <item><c>game/contact</c> contact quality and batted-ball kind.</item>
/// <item><c>game/field</c> defence result (out/hit/error) on a batted ball.</item>
/// <item><c>game/runner</c> runner advancement, steals, pickoffs, wild pitch/passed ball.</item>
/// <item><c>game/location</c> Pitch Zone Lite actual pitch location (<c>pzl-zones-v1</c>). Contract: used only for the location draw of an actual pitch,
/// drawn BEFORE that pitch's outcome draws on the other streams, in strict game pitch order, one location draw sequence per actual pitch
/// (an in-play pitch is one pitch, never two). The number of draws per pitch is fixed by the location model version defined in 3-B/3-C;
/// any change of that count or order requires <c>game/location@2</c>.</item>
/// <item><c>game/ai</c> simple legal AI strategy/substitution policy tie-breaks, only where a deterministic rule leaves a choice.</item>
/// </list>
/// </summary>
public static class GameRngStreams
{
    public static readonly RngStreamId Pitch = RngStreamId.Known("game/pitch", 1);
    public static readonly RngStreamId Contact = RngStreamId.Known("game/contact", 1);
    public static readonly RngStreamId Field = RngStreamId.Known("game/field", 1);
    public static readonly RngStreamId Runner = RngStreamId.Known("game/runner", 1);
    public static readonly RngStreamId Location = RngStreamId.Known("game/location", 1);
    public static readonly RngStreamId Ai = RngStreamId.Known("game/ai", 1);
}
