using System.Globalization;
using System.Text;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>
/// One B-1 evidence error (B1-T00 §1.1 rule 11, §1.3): the closed top-level <see cref="Code"/> (<c>Evidence.*</c>), the cause code that keeps any existing <c>Replay.*</c> / <c>Seed.*</c> / <c>Official.*</c> /
/// <c>Trust.*</c> code, the JSON Pointer, the verification stage and the family / unit / game it concerns.
/// </summary>
public sealed record EvidenceError(string Code, string Cause, string Pointer, string Stage, string Message, string? Family = null, string? Unit = null, string? Game = null)
{
    public override string ToString() => $"{Code} ({Cause}) at '{Pointer}' [{Stage}]: {Message}";
}

/// <summary>Thrown inside the evidence readers and checks; every public entry point turns it back into an <see cref="EvidenceError"/>.</summary>
public sealed class EvidenceException(EvidenceError error) : Exception(error.ToString())
{
    public EvidenceError Error { get; } = error;
}

public static class EvidenceCodes
{
    public const string SchemaInvalid = "Evidence.schema_invalid", ContractNotApproved = "Evidence.contract_not_approved", ApprovalMismatch = "Evidence.approval_mismatch",
        ReferenceInvalid = "Evidence.reference_invalid", ReplayMismatch = "Evidence.replay_mismatch", EvaluationMismatch = "Evidence.evaluation_mismatch",
        PzlIncomplete = "Evidence.pzl_incomplete", PzlMismatch = "Evidence.pzl_mismatch", HashMismatch = "Evidence.hash_mismatch", ArchiveInvalid = "Evidence.archive_invalid",
        NotCertifiable = "Evidence.not_certifiable", RegistrationPending = "Evidence.registration_pending", RegistrationConflict = "Evidence.registration_conflict",
        RegistrationUnauthorized = "Evidence.registration_unauthorized", EnvironmentMismatch = "Evidence.environment_mismatch", TestProfileMismatch = "Evidence.test_profile_mismatch",
        AuthorizationMissing = "Evidence.authorization_missing";

    /// <summary>The closed list of 17 top-level codes (§1.3).</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        SchemaInvalid, ContractNotApproved, ApprovalMismatch, ReferenceInvalid, ReplayMismatch, EvaluationMismatch, PzlIncomplete, PzlMismatch, HashMismatch, ArchiveInvalid,
        NotCertifiable, RegistrationPending, RegistrationConflict, RegistrationUnauthorized, EnvironmentMismatch, TestProfileMismatch, AuthorizationMissing,
    ];

    public static EvidenceException Fail(string code, string cause, string pointer, string stage, string message) => new(new EvidenceError(code, cause, pointer, stage, message));
}

/// <summary>A parsed strict-JSON value. Numbers keep their exact token text; objects keep their members in source order (keys are unique by construction).</summary>
public abstract class JNode
{
    public static JNull Null { get; } = new();
}

public sealed class JNull : JNode
{
    internal JNull() { }
}

public sealed class JBool(bool value) : JNode
{
    public bool Value { get; } = value;
}

public sealed class JString(string value) : JNode
{
    public string Value { get; } = value;
}

/// <summary>A JSON number exactly as written (<see cref="Raw"/>); it is never converted through a double here.</summary>
public sealed class JNumber(string raw) : JNode
{
    public string Raw { get; } = raw;
}

/// <summary>B1 T11 R1: the items are copied into a read-only array — a tree, once built, can never be changed through a list it was built from or by casting <see cref="Items"/>.</summary>
public sealed class JArray(IReadOnlyList<JNode> items) : JNode
{
    public IReadOnlyList<JNode> Items { get; } = Array.AsReadOnly(items.ToArray());
}

public sealed class JObject : JNode
{
    private readonly Dictionary<string, JNode> _byName;

    public JObject(IReadOnlyList<KeyValuePair<string, JNode>> members)
    {
        Members = Array.AsReadOnly(members.ToArray());                                              // B1 T11 R1: copied, read-only (see JArray)
        _byName = new Dictionary<string, JNode>(StringComparer.Ordinal);
        foreach (var (k, v) in Members)
            if (!_byName.TryAdd(k, v))
                throw new ArgumentException($"중복 키 '{k}'", nameof(members));
    }

    public IReadOnlyList<KeyValuePair<string, JNode>> Members { get; }

    public bool Has(string name) => _byName.ContainsKey(name);

    public JNode this[string name] => _byName[name];

    public JNode? Get(string name) => _byName.GetValueOrDefault(name);
}

/// <summary>
/// The strict reader of every new B-1 format (B1-T00 §8.1): UTF-8 without BOM, malformed UTF-8 refused, a duplicate key refused BEFORE any object is stored, a lone surrogate (raw or escaped) refused,
/// RFC 8259 grammar only (no comments, no trailing comma, no NaN, whitespace = space/tab/LF/CR), nothing after the single value. No normalization, trim, newline or case change. Existing Seed /
/// Manifest / Legacy readers are not changed by it. Errors are <c>Evidence.schema_invalid</c> with a JSON Pointer.
/// </summary>
public static class StrictJson
{
    public const int MaxDepth = 128;

    private static readonly UTF8Encoding Utf8Strict = new(false, true);

    public static JNode Parse(ReadOnlySpan<byte> bytes, string stage = "strict_json")
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            throw EvidenceCodes.Fail(EvidenceCodes.SchemaInvalid, "Json.bom", "", stage, "UTF-8 BOM은 허용하지 않습니다.");
        string text;
        try
        {
            text = Utf8Strict.GetString(bytes);
        }
        catch (DecoderFallbackException e)
        {
            throw EvidenceCodes.Fail(EvidenceCodes.SchemaInvalid, "Json.utf8_invalid", "", stage, $"올바르지 않은 UTF-8입니다: {e.Message}");
        }

        var p = new Parser(text, stage);
        p.Ws();
        var value = p.Value("", 0);
        p.Ws();
        if (!p.End)
            throw p.Fail("Json.trailing_content", "", "하나의 JSON 값 뒤에 다른 내용이 있습니다.");
        return value;
    }

    public static bool TryParse(ReadOnlySpan<byte> bytes, out JNode? value, out EvidenceError? error, string stage = "strict_json")
    {
        try
        {
            value = Parse(bytes, stage);
            error = null;
            return true;
        }
        catch (EvidenceException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    /// <summary>RFC 6901 escaping of one reference token.</summary>
    public static string Token(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private sealed class Parser(string s, string stage)
    {
        private int _i;

        public bool End => _i >= s.Length;

        public EvidenceException Fail(string cause, string pointer, string message) =>
            EvidenceCodes.Fail(EvidenceCodes.SchemaInvalid, cause, pointer, stage, $"{message} (문자 위치 {_i})");

        public void Ws()
        {
            while (_i < s.Length && s[_i] is ' ' or '\t' or '\n' or '\r')
                _i++;
        }

        public JNode Value(string ptr, int depth)
        {
            if (depth > MaxDepth)
                throw Fail("Json.too_deep", ptr, $"중첩이 {MaxDepth}단계를 넘습니다.");
            if (End)
                throw Fail("Json.unexpected_end", ptr, "값이 없습니다.");
            switch (s[_i])
            {
                case '{': return Object(ptr, depth);
                case '[': return Array(ptr, depth);
                case '"': return new JString(String(ptr));
                case 't': Literal("true", ptr); return new JBool(true);
                case 'f': Literal("false", ptr); return new JBool(false);
                case 'n': Literal("null", ptr); return JNode.Null;
                default:
                    if (s[_i] == '-' || char.IsAsciiDigit(s[_i]))
                        return Number(ptr);
                    throw Fail("Json.syntax", ptr, $"예상하지 못한 문자 '{s[_i]}'입니다.");
            }
        }

        private void Literal(string word, string ptr)
        {
            if (string.CompareOrdinal(s, _i, word, 0, word.Length) != 0)
                throw Fail("Json.syntax", ptr, $"'{word}'가 아닙니다.");
            _i += word.Length;
        }

        private JObject Object(string ptr, int depth)
        {
            _i++;
            var members = new List<KeyValuePair<string, JNode>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Ws();
            if (!End && s[_i] == '}')
            {
                _i++;
                return new JObject(members);
            }

            while (true)
            {
                Ws();
                if (End || s[_i] != '"')
                    throw Fail("Json.syntax", ptr, "객체 키가 와야 합니다.");
                var key = String(ptr);
                var child = ptr + "/" + Token(key);
                if (!seen.Add(key))
                    throw Fail("Json.duplicate_key", child, $"같은 키 '{key}'가 두 번 나옵니다.");
                Ws();
                if (End || s[_i] != ':')
                    throw Fail("Json.syntax", child, "':'가 와야 합니다.");
                _i++;
                Ws();
                members.Add(new(key, Value(child, depth + 1)));
                Ws();
                if (End)
                    throw Fail("Json.unexpected_end", ptr, "객체가 닫히지 않았습니다.");
                if (s[_i] == ',')
                {
                    _i++;
                    continue;
                }

                if (s[_i] == '}')
                {
                    _i++;
                    return new JObject(members);
                }

                throw Fail("Json.syntax", ptr, "',' 또는 '}'가 와야 합니다.");
            }
        }

        private JArray Array(string ptr, int depth)
        {
            _i++;
            var items = new List<JNode>();
            Ws();
            if (!End && s[_i] == ']')
            {
                _i++;
                return new JArray(items);
            }

            while (true)
            {
                Ws();
                items.Add(Value($"{ptr}/{items.Count}", depth + 1));
                Ws();
                if (End)
                    throw Fail("Json.unexpected_end", ptr, "배열이 닫히지 않았습니다.");
                if (s[_i] == ',')
                {
                    _i++;
                    continue;
                }

                if (s[_i] == ']')
                {
                    _i++;
                    return new JArray(items);
                }

                throw Fail("Json.syntax", ptr, "',' 또는 ']'가 와야 합니다.");
            }
        }

        private string String(string ptr)
        {
            _i++;
            var b = new StringBuilder();
            while (true)
            {
                if (End)
                    throw Fail("Json.unexpected_end", ptr, "문자열이 닫히지 않았습니다.");
                var c = s[_i];
                if (c == '"')
                {
                    _i++;
                    return b.ToString();
                }

                if (c < 0x20)
                    throw Fail("Json.control_character", ptr, "문자열 안의 제어 문자는 escape해야 합니다.");
                if (c == '\\')
                {
                    _i++;
                    if (End)
                        throw Fail("Json.unexpected_end", ptr, "escape가 끝나지 않았습니다.");
                    var e = s[_i++];
                    switch (e)
                    {
                        case '"': b.Append('"'); break;
                        case '\\': b.Append('\\'); break;
                        case '/': b.Append('/'); break;
                        case 'b': b.Append('\b'); break;
                        case 'f': b.Append('\f'); break;
                        case 'n': b.Append('\n'); break;
                        case 'r': b.Append('\r'); break;
                        case 't': b.Append('\t'); break;
                        case 'u':
                            var u = Hex4(ptr);
                            if (char.IsHighSurrogate(u))
                            {
                                if (_i + 6 > s.Length || s[_i] != '\\' || s[_i + 1] != 'u')
                                    throw Fail("Json.lone_surrogate", ptr, "짝이 없는 상위 surrogate escape입니다.");
                                _i += 2;
                                var low = Hex4(ptr);
                                if (!char.IsLowSurrogate(low))
                                    throw Fail("Json.lone_surrogate", ptr, "상위 surrogate 뒤에 하위 surrogate가 없습니다.");
                                b.Append(u).Append(low);
                            }
                            else if (char.IsLowSurrogate(u))
                                throw Fail("Json.lone_surrogate", ptr, "짝이 없는 하위 surrogate escape입니다.");
                            else
                                b.Append(u);
                            break;
                        default:
                            throw Fail("Json.syntax", ptr, $"알 수 없는 escape '\\{e}'입니다.");
                    }

                    continue;
                }

                // a strict UTF-8 decode cannot yield a lone surrogate, but a pair is copied as a pair
                b.Append(c);
                _i++;
            }
        }

        private char Hex4(string ptr)
        {
            if (_i + 4 > s.Length || !ushort.TryParse(s.AsSpan(_i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v))
                throw Fail("Json.syntax", ptr, "\\u 뒤에 16진수 4자리가 와야 합니다.");
            _i += 4;
            return (char)v;
        }

        private JNumber Number(string ptr)
        {
            var start = _i;
            if (s[_i] == '-')
                _i++;
            if (End || !char.IsAsciiDigit(s[_i]))
                throw Fail("Json.number_syntax", ptr, "숫자가 와야 합니다.");
            if (s[_i] == '0')
                _i++;
            else
                while (!End && char.IsAsciiDigit(s[_i]))
                    _i++;
            if (!End && s[_i] == '.')
            {
                _i++;
                if (End || !char.IsAsciiDigit(s[_i]))
                    throw Fail("Json.number_syntax", ptr, "소수점 뒤에 숫자가 와야 합니다.");
                while (!End && char.IsAsciiDigit(s[_i]))
                    _i++;
            }

            if (!End && s[_i] is 'e' or 'E')
            {
                _i++;
                if (!End && s[_i] is '+' or '-')
                    _i++;
                if (End || !char.IsAsciiDigit(s[_i]))
                    throw Fail("Json.number_syntax", ptr, "지수에 숫자가 와야 합니다.");
                while (!End && char.IsAsciiDigit(s[_i]))
                    _i++;
            }

            return new JNumber(s[start.._i]);
        }
    }
}
