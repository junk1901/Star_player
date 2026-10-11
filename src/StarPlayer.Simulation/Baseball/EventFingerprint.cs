using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// Deterministic identity of an event's content, used to tell a harmless re-delivery (same id, same content) from an id conflict
/// (same id, other content). The canonical text depends only on the event's values, never on object identity, process, OS, culture,
/// <c>GetHashCode</c>, JSON member order or list order of unordered collections.
/// <para>
/// Format (<see cref="Version"/> <c>ef1</c>): <c>ef1|kind|name=value|name=value...</c>, fields in the fixed order written below. Strings are
/// escaped (<c>\</c>, <c>|</c>, <c>=</c>, <c>,</c>, <c>[</c>, <c>]</c>, <c>{</c>, <c>}</c>, <c>;</c> prefixed with a backslash); enums use their member
/// NAME (renaming a member requires a new version); integers are invariant-culture decimals; booleans are <c>1</c>/<c>0</c>; an absent value
/// is <c>~</c>. Collections of moves, credits and errors are unordered sets: each element is canonicalised and the elements are sorted
/// ordinally. A null list and an empty list are the same, except <c>StrikeoutCredits</c> where null means "default catcher putout".
/// The fingerprint is the lower-case hex SHA-256 of the UTF-8 canonical text. Any change to this format must change <see cref="Version"/>.
/// </para>
/// <para>
/// Format <see cref="VersionV2"/> <c>ef2</c> (Phase 3-C1) is used only for an event that carries a pitch location (<c>PitchEvent</c>/<c>TakeEvent</c> through
/// <c>PitchSpec.Location</c>, and <c>InPlayEvent.Location</c>): <c>ef2|kind|</c> + exactly the <c>ef1</c> field list of that event + <c>|zone_model=..|zone=..|side=..|boundary=..</c>
/// in that order, with the same escaping and enum rules. Every other event keeps the <c>ef1</c> text and hash byte-for-byte; an <c>ef1</c> receipt is never converted.
/// </para>
/// </summary>
public static class EventFingerprint
{
    public const string Version = "ef1";

    public const string VersionV2 = "ef2";

    /// <summary>The fingerprint version that identifies this event: <c>ef2</c> when it carries a pitch location, otherwise <c>ef1</c>.</summary>
    public static string VersionOf(GameEvent e) => GameEventLocation.Of(e) is null ? Version : VersionV2;

    public static EventReceipt Receipt(GameEvent e) => new(e.Id, KindName(e), VersionOf(e), Hash(Canonical(e)));

    public static string KindName(GameEvent e) => e switch
    {
        ApproachEvent => "approach",
        PitchEvent => "pitch",
        TakeEvent => "take",
        InPlayEvent => "in_play",
        RunnerPlayEvent => "runner_play",
        SubstitutionEvent => "substitution",
        _ => throw new ArgumentOutOfRangeException(nameof(e), "알 수 없는 사건 종류입니다."),
    };

    /// <summary>The canonical text that is hashed (public so tests can show exactly what is and is not part of an event's identity).</summary>
    public static string Canonical(GameEvent e)
    {
        string[] fields = e switch
        {
            ApproachEvent a => [F("approach", Name(a.Approach))],
            PitchEvent p => [F("spec", Spec(p.Spec))],
            TakeEvent t => [F("spec", Spec(t.Spec))],
            InPlayEvent i => new[]
            {
                F("batted", Name(i.Batted)), F("result", Name(i.Result)), F("sacrifice", Flag(i.Sacrifice)), F("moves", Moves(i.Moves)),
                F("batter_credits", Credits(i.BatterCredits)), F("errors", Errors(i.Errors)), F("batter_sequence", Int(i.BatterSequence)),
            },
            RunnerPlayEvent r => [F("kind", Name(r.Kind)), F("moves", Moves(r.Moves)), F("errors", Errors(r.Errors))],
            SubstitutionEvent s => new[]
            {
                F("kind", Name(s.Kind)), F("team", Name(s.Team)), F("player", s.Player == default ? "~" : Esc(s.Player.Value)), F("base", Int(s.Base)),
                F("slot", Int(s.Slot)), F("slot2", Int(s.Slot2)),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(e), "알 수 없는 사건 종류입니다."),
        };
        if (GameEventLocation.Of(e) is { } l)
            fields = [.. fields, F("zone_model", Esc(l.Model.Value)), F("zone", Esc(l.Zone.Value)), F("side", Name(l.Side)), F("boundary", Name(l.Boundary))];
        return $"{VersionOf(e)}|{KindName(e)}|{string.Join("|", fields)}";
    }

    private static string Hash(string canonical) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

    private static string F(string name, string value) => $"{name}={value}";

    private static string Name<T>(T value) where T : struct, Enum => value.ToString();

    private static string Int(int? value) => value is null ? "~" : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "1" : "0";

    private static string Esc(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\\' or '|' or '=' or ',' or '[' or ']' or '{' or '}' or ';')
                sb.Append('\\');
            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string Set(IEnumerable<string> items) => "[" + string.Join(",", items.OrderBy(x => x, StringComparer.Ordinal)) + "]";

    private static string Credit(PositionCredit c) => $"{{{Name(c.Position)};{Name(c.Kind)}}}";

    private static string Credits(System.Collections.Immutable.ImmutableList<PositionCredit>? credits) => Set((credits ?? []).Select(Credit));

    private static string Errors(System.Collections.Immutable.ImmutableList<FieldPosition>? errors) => Set((errors ?? []).Select(Name));

    private static string Move(RunnerMove m) =>
        $"{{{m.From.ToString(CultureInfo.InvariantCulture)};{m.To.ToString(CultureInfo.InvariantCulture)};{Name(m.Cause)};{(m.OutKind is { } k ? Name(k) : "~")};{Credits(m.Credits)};" +
        $"{Int(m.Sequence)};{Int(m.OutAt)};{(m.Method is { } method ? Name(method) : "~")}}}";

    private static string Moves(System.Collections.Immutable.ImmutableList<RunnerMove>? moves) => Set((moves ?? []).Select(Move));

    private static string Spec(PitchSpec s) =>
        $"{{{Name(s.Outcome)};{Name(s.Mishap)};{Moves(s.Moves)};{Name(s.DroppedThirdStrike)};{(s.StrikeoutCredits is null ? "~" : Credits(s.StrikeoutCredits))}}}";
}
