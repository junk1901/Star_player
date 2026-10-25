using System.Globalization;
using System.Numerics;
using System.Text;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>
/// RFC 8785 JSON Canonicalization (B1-T00 §8.1, T02): object members sorted by their UTF-16 code units, strings escaped as ECMAScript <c>JSON.stringify</c> does (the two-character escapes, other C0
/// controls as lowercase <c>\u00xx</c>, everything else literal), numbers through <see cref="Es"/>, UTF-8 without BOM and without a trailing newline.
/// </summary>
public static class Jcs
{
    /// <summary>The largest JSON integer an evidence number may carry (§8.3, <c>2^53 − 1</c>).</summary>
    public const long MaxSafeInteger = 9_007_199_254_740_991;

    public static byte[] Bytes(JNode node) => new UTF8Encoding(false).GetBytes(Text(node));

    public static string Text(JNode node)
    {
        var b = new StringBuilder();
        Write(b, node);
        return b.ToString();
    }

    /// <summary>The JCS of an array written element by element (<c>"[" + elements joined by "," + "]"</c>, §10.5), for a digest that is computed while streaming.</summary>
    public static void WriteArray(Stream output, IEnumerable<JNode> items)
    {
        output.WriteByte((byte)'[');
        var first = true;
        foreach (var item in items)
        {
            if (!first)
                output.WriteByte((byte)',');
            first = false;
            var bytes = Bytes(item);
            output.Write(bytes, 0, bytes.Length);
        }

        output.WriteByte((byte)']');
    }

    private static void Write(StringBuilder b, JNode node)
    {
        switch (node)
        {
            case JNull: b.Append("null"); break;
            case JBool v: b.Append(v.Value ? "true" : "false"); break;
            case JString v: Quote(b, v.Value); break;
            case JNumber v: b.Append(NumberText(v.Raw)); break;
            case JArray a:
                b.Append('[');
                for (var i = 0; i < a.Items.Count; i++)
                {
                    if (i > 0)
                        b.Append(',');
                    Write(b, a.Items[i]);
                }

                b.Append(']');
                break;
            case JObject o:
                b.Append('{');
                var first = true;
                foreach (var (k, v) in o.Members.OrderBy(m => m.Key, StringComparer.Ordinal))   // ordinal = UTF-16 code unit order (RFC 8785 §3.2.3)
                {
                    if (!first)
                        b.Append(',');
                    first = false;
                    Quote(b, k);
                    b.Append(':');
                    Write(b, v);
                }

                b.Append('}');
                break;
            default:
                throw new ArgumentException($"알 수 없는 JSON 노드 {node.GetType().Name}", nameof(node));
        }
    }

    private static string NumberText(string raw)
    {
        var value = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (Math.Floor(value) == value && Math.Abs(value) > MaxSafeInteger)
            throw new ArgumentOutOfRangeException(nameof(raw), $"정수 {raw}는 JSON 증거 상한 2^53−1을 넘습니다(§8.3).");
        return Es(value);
    }

    private static void Quote(StringBuilder b, string s)
    {
        b.Append('"');
        foreach (var c in s)
            switch (c)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\b': b.Append("\\b"); break;
                case '\f': b.Append("\\f"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        b.Append(c);
                    break;
            }

        b.Append('"');
    }

    /// <summary>
    /// ECMAScript Number::toString of a finite binary64 (RFC 8785 §3.2.2.3, B1-T00 §8.4): the shortest decimal that reads back to the same bits, the nearest such decimal and on a tie the even one,
    /// plain notation for <c>10^-6 ≤ |x| &lt; 10^21</c>, otherwise a lowercase <c>e</c> with a signed exponent; both zeros are <c>"0"</c>. The digits come from exact integer arithmetic, not from
    /// <c>ToString("R")</c>.
    /// </summary>
    public static string Es(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "NaN·Infinity는 F64 FINITE 값이 아닙니다.");
        if (value == 0)
            return "0";
        if (value < 0)
            return "-" + Es(-value);
        var (digits, exponent) = Shortest(value);
        var s = digits.ToString(CultureInfo.InvariantCulture);
        var k = s.Length;
        var n = exponent + k;                                       // value = 0.s × 10^n
        if (k <= n && n <= 21)
            return s + new string('0', n - k);
        if (0 < n && n <= 21)
            return s[..n] + "." + s[n..];
        if (-6 < n && n <= 0)
            return "0." + new string('0', -n) + s;
        var e = n - 1;
        var sign = e < 0 ? "-" : "+";
        return (k == 1 ? s : s[0] + "." + s[1..]) + "e" + sign + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The shortest round-tripping significand (no trailing zeros) and its power of ten: <c>value = digits × 10^exponent</c>.</summary>
    internal static (BigInteger Digits, int Exponent) Shortest(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var rawExp = (int)((bits >> 52) & 0x7FF);
        var frac = bits & 0xFFFFFFFFFFFFFL;
        BigInteger mantissa = rawExp == 0 ? frac : frac | (1L << 52);
        var e2 = (rawExp == 0 ? 1 : rawExp) - 1075;                 // value = mantissa × 2^e2
        BigInteger num = e2 >= 0 ? mantissa << e2 : mantissa, den = e2 >= 0 ? BigInteger.One : BigInteger.One << -e2;

        var e10 = (int)Math.Floor(Math.Log10(value));               // an estimate, corrected exactly below
        while (Compare(num, den, e10) < 0)
            e10--;
        while (Compare(num, den, e10 + 1) >= 0)
            e10++;

        for (var p = 1; p <= 17; p++)
        {
            var (d, e) = Round(num, den, e10 - p + 1, p);
            if (double.Parse($"{d}E{e}", NumberStyles.Float, CultureInfo.InvariantCulture) == value)
            {
                while (!d.IsZero && d % 10 == 0)
                {
                    d /= 10;
                    e++;
                }

                return (d, e);
            }
        }

        throw new InvalidOperationException("17자리 안에서 왕복하는 십진 표현을 찾지 못했습니다.");
    }

    /// <summary>Sign of <c>num/den − 10^e</c>.</summary>
    private static int Compare(BigInteger num, BigInteger den, int e) =>
        e >= 0 ? num.CompareTo(den * BigInteger.Pow(10, e)) : (num * BigInteger.Pow(10, -e)).CompareTo(den);

    /// <summary><c>num/den / 10^e</c> rounded half-even to an integer of <paramref name="p"/> digits (a carry to <c>10^p</c> moves one place up).</summary>
    private static (BigInteger Digits, int Exponent) Round(BigInteger num, BigInteger den, int e, int p)
    {
        BigInteger n = num, d = den;
        if (e >= 0)
            d *= BigInteger.Pow(10, e);
        else
            n *= BigInteger.Pow(10, -e);
        var q = BigInteger.DivRem(n, d, out var r);
        var twice = r * 2;
        if (twice > d || (twice == d && !q.IsEven))
            q += 1;
        if (q == BigInteger.Pow(10, p))
            return (BigInteger.Pow(10, p - 1), e + 1);
        return (q, e);
    }
}

/// <summary>The canonical scalar texts of §1.2 / §8.2 / §8.3 that are strings on the wire.</summary>
public static class EvidenceScalars
{
    /// <summary>§8.2: <c>0 | [1-9][0-9]{0,19}</c> and at most <c>18446744073709551615</c>.</summary>
    public static bool TryParseU64(string? text, out ulong value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text) || text.Length > 20 || !text.All(char.IsAsciiDigit) || (text.Length > 1 && text[0] == '0'))
            return false;
        return ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public static string U64(ulong value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>D64: a canonical non-negative decimal string up to <c>9223372036854775807</c> (raw RNG draws).</summary>
    public static bool TryParseD64(string? text, out long value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text) || text.Length > 19 || !text.All(char.IsAsciiDigit) || (text.Length > 1 && text[0] == '0'))
            return false;
        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public static string D64(long value) => value >= 0 ? value.ToString(CultureInfo.InvariantCulture) : throw new ArgumentOutOfRangeException(nameof(value));

    /// <summary>The F64 object of §8.4 for a finite value.</summary>
    public static JObject F64(double value) => J.O(("state", J.S("FINITE")), ("value", J.S(Jcs.Es(value))), ("reason", JNode.Null));

    /// <summary>The F64 object of a Holm critical value after the sequence stopped (§4).</summary>
    public static JObject F64HolmStopped() => J.O(("state", J.S("NOT_APPLICABLE")), ("value", JNode.Null), ("reason", J.S("HOLM_STOPPED")));

    /// <summary>The big-endian hex of the IEEE754 bits (<c>BITS64</c>).</summary>
    public static string Bits64(double value) => ((ulong)BitConverter.DoubleToInt64Bits(value)).ToString("x16", CultureInfo.InvariantCulture);
}

/// <summary>Short constructors for evidence documents.</summary>
public static class J
{
    public static JString S(string value) => new(value);

    public static JNode Sn(string? value) => value is null ? JNode.Null : new JString(value);

    public static JNumber N(long value) => new(value.ToString(CultureInfo.InvariantCulture));

    public static JNode Nn(long? value) => value is { } v ? N(v) : JNode.Null;

    public static JBool B(bool value) => new(value);

    public static JArray A(IEnumerable<JNode> items) => new(items.ToList());

    public static JArray A(params JNode[] items) => new(items);

    public static JObject O(params (string Name, JNode Value)[] members) => new(members.Select(m => new KeyValuePair<string, JNode>(m.Name, m.Value)).ToList());
}
