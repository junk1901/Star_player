using System.Text.RegularExpressions;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>A closed shape check: throws <see cref="EvidenceException"/> with a JSON Pointer when the node does not have the shape.</summary>
public delegate void Shape(JNode node, string pointer);

/// <summary>
/// The closed-schema building blocks of B1-T00 §1.1-§1.2 (T01): every listed field is required, nothing unlisted is accepted, <c>?</c> means "present and may be null", absence and null are distinct,
/// nothing is defaulted, dropped or coerced, and integers keep the token grammar <c>0|[1-9][0-9]*</c> with the JSON limit <c>2^53 − 1</c> and the field's own range.
/// </summary>
public static class Schema
{
    public const string Stage = "schema";

    public static EvidenceException Bad(string pointer, string cause, string message, string code = EvidenceCodes.SchemaInvalid) =>
        EvidenceCodes.Fail(code, cause, pointer, Stage, message);

    private static readonly Regex IntToken = new("^(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant);

    public static Shape Obj(params (string Name, Shape Shape)[] fields)
    {
        var names = fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        return (n, p) =>
        {
            if (n is not JObject o)
                throw Bad(p, "Schema.type", "객체가 와야 합니다.");
            foreach (var (k, _) in o.Members)
                if (!names.Contains(k))
                    throw Bad($"{p}/{StrictJson.Token(k)}", "Schema.unknown_field", $"정의되지 않은 필드 '{k}'입니다.");
            foreach (var (name, shape) in fields)
            {
                var child = $"{p}/{StrictJson.Token(name)}";
                if (o.Get(name) is not { } v)
                    throw Bad(child, "Schema.missing_field", $"필수 필드 '{name}'가 없습니다.");
                shape(v, child);
            }
        };
    }

    public static Shape Arr(Shape item, int min = 0, int max = int.MaxValue) => (n, p) =>
    {
        if (n is not JArray a)
            throw Bad(p, "Schema.type", "배열이 와야 합니다.");
        if (a.Items.Count < min || a.Items.Count > max)
            throw Bad(p, "Schema.array_length", $"배열 길이 {a.Items.Count}이(가) 허용 범위 {min}..{(max == int.MaxValue ? "∞" : max)} 밖입니다.");
        for (var i = 0; i < a.Items.Count; i++)
            item(a.Items[i], $"{p}/{i}");
    };

    public static Shape ArrExact(Shape item, int length) => Arr(item, length, length);

    public static Shape Opt(Shape inner) => (n, p) =>
    {
        if (n is not JNull)
            inner(n, p);
    };

    public static Shape Str(Func<string, bool> ok, string what) => (n, p) =>
    {
        if (n is not JString s)
            throw Bad(p, "Schema.type", $"문자열({what})이 와야 합니다.");
        if (!ok(s.Value))
            throw Bad(p, "Schema.format", $"'{s.Value}'는 {what} 형식이 아닙니다.");
    };

    public static Shape Pattern(string regex, string what)
    {
        var r = new Regex(regex, RegexOptions.CultureInvariant);
        return Str(v => r.IsMatch(v), what);
    }

    public static Shape Const(string value) => Str(v => v == value, $"\"{value}\"");

    public static Shape OneOf(params string[] values) => Str(v => values.Contains(v, StringComparer.Ordinal), string.Join("|", values));

    public static Shape Int(long min, long max) => (n, p) =>
    {
        if (n is not JNumber num)
            throw Bad(p, "Schema.type", "정수가 와야 합니다.");
        if (!IntToken.IsMatch(num.Raw))
            throw Bad(p, "Schema.integer_token", $"정수 토큰 '{num.Raw}'는 0|[1-9][0-9]* 문법이 아닙니다(1.0·1e0·-0 거부).");
        if (num.Raw.Length > 16 || !long.TryParse(num.Raw, out var v) || v > Jcs.MaxSafeInteger)
            throw Bad(p, "Schema.integer_range", $"정수 {num.Raw}이(가) JSON 상한 2^53−1을 넘습니다.");
        if (v < min || v > max)
            throw Bad(p, "Schema.integer_range", $"정수 {v}이(가) 허용 범위 {min}..{max} 밖입니다.");
    };

    public static Shape IntConst(long value) => Int(value, value);

    public static readonly Shape Bool = (n, p) =>
    {
        if (n is not JBool)
            throw Bad(p, "Schema.type", "true/false가 와야 합니다.");
    };

    public static readonly Shape True = (n, p) =>
    {
        if (n is not JBool { Value: true })
            throw Bad(p, "Schema.format", "true여야 합니다.");
    };

    public static readonly Shape Null = (n, p) =>
    {
        if (n is not JNull)
            throw Bad(p, "Schema.format", "null이어야 합니다.");
    };

    // ---------------------------------------------------------------- the scalars of §1.2

    public static readonly Shape N = Int(0, Jcs.MaxSafeInteger);
    public static readonly Shape I = Int(0, int.MaxValue);
    public static readonly Shape P = Int(1, int.MaxValue);
    public static readonly Shape Ppm = Int(0, 1_000_000);
    public static readonly Shape U64 = Str(v => EvidenceScalars.TryParseU64(v, out _), "U64");
    public static readonly Shape D64 = Str(v => EvidenceScalars.TryParseD64(v, out _), "D64");
    public static readonly Shape D64Positive = Str(v => EvidenceScalars.TryParseD64(v, out var d) && d > 0, "양수 D64");
    public static readonly Shape H = Pattern("^[0-9a-f]{64}$", "H");
    public static readonly Shape AH = Pattern("^sha256:[0-9a-f]{64}$", "AH");
    public static readonly Shape Git = Pattern("^[0-9a-f]{40}$", "GIT");
    public static readonly Shape Id = Pattern("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", "ID");
    public static readonly Shape Run = Pattern("^[A-Za-z0-9_.-]{1,64}$", "RUN");
    public static readonly Shape Ver = Pattern("^[1-9][0-9]{0,8}$", "VER");
    public static readonly Shape Text1 = Str(IsText1, "TEXT1");
    public static readonly Shape Code = Str(v => IsText1(v) && Regex.IsMatch(v, "^[A-Z][A-Za-z0-9]*\\.[a-z0-9_]+$", RegexOptions.CultureInvariant), "CODE");
    public static readonly Shape RngSet1 = Str(IsRngSet1, "RNGSET1");
    public static readonly Shape EvCanon = Str(_ => true, "EVCANON");
    public static readonly Shape StreamId = Str(v => Regex.IsMatch(v, "^[a-z0-9_]+(/[a-z0-9_]+)*@[1-9][0-9]*$", RegexOptions.CultureInvariant) && v.IndexOf('@') <= 64, "STREAM");
    public static readonly Shape RelPath = Str(IsRelPath, "RELPATH");
    public static readonly Shape Bits64 = Pattern("^[0-9a-f]{16}$", "BITS64");
    public static readonly Shape Exact = Str(_ => true, "EXACT");
    public static readonly Shape SidUser = Pattern("^S-1-5-21(-[0-9]+){3}-[0-9]+$", "SID_USER");
    public static readonly Shape SidService = Pattern("^S-1-5-80(-[0-9]+)+$", "SID_SERVICE");

    public static bool IsText1(string v) => v.Length >= 1 && v.All(c => c > 0x1F);

    /// <summary>§1.2 RELPATH: '/'-separated, no empty / '.' / '..' segment, no ':', '\' or NUL.</summary>
    public static bool IsRelPath(string v) =>
        v.Length > 0 && !v.Contains(':') && !v.Contains('\\') && !v.Contains('\0') && v.Split('/').All(s => s is not ("" or "." or ".."));

    /// <summary>§1.2 RNGSET1: <c>rngset1|{alg}|{kdv}|{16 hex}|{context}</c>, then <c>{stream}|{16hex}|{16hex}|{draws ≥ 1}</c> in ordinal stream order, LF only, no trailing LF, no CR.</summary>
    public static bool IsRngSet1(string v)
    {
        if (v.Contains('\r') || v.EndsWith('\n'))
            return false;
        var lines = v.Split('\n');
        var head = lines[0].Split('|');
        if (head.Length != 5 || head[0] != "rngset1" || head[1].Length == 0 || head[2].Length == 0 || !Regex.IsMatch(head[3], "^[0-9a-f]{16}$") || head[4].Length == 0)
            return false;
        string? previous = null;
        foreach (var line in lines.Skip(1))
        {
            var f = line.Split('|');
            if (f.Length != 4 || !Regex.IsMatch(f[0], "^[a-z0-9_]+(/[a-z0-9_]+)*@[1-9][0-9]*$") || !Regex.IsMatch(f[1], "^[0-9a-f]{16}$") || !Regex.IsMatch(f[2], "^[0-9a-f]{16}$")
                || !EvidenceScalars.TryParseD64(f[3], out var draws) || draws < 1)
                return false;
            if (previous is not null && string.CompareOrdinal(previous, f[0]) >= 0)
                return false;
            previous = f[0];
        }

        return true;
    }

    public static string Text(JNode n) => ((JString)n).Value;

    public static long Long(JNode n) => long.Parse(((JNumber)n).Raw, System.Globalization.CultureInfo.InvariantCulture);

    public static bool IsNull(JNode n) => n is JNull;

    public static JObject Obj(JNode n) => (JObject)n;

    public static IReadOnlyList<JNode> Items(JNode n) => ((JArray)n).Items;
}
