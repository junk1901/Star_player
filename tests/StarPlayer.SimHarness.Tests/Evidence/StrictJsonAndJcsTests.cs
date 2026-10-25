using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>B1-T01 strict reader and B1-T02 JCS / U64 / F64 (B1-T00 §8, §3.1 GV-C01·C02·EV0, A21~A24). Expected bytes and digests are the contract's, not this implementation's output.</summary>
public class StrictJsonAndJcsTests
{
    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    private static string Prefixed(string prefix, byte[] c) => Sha([.. Encoding.ASCII.GetBytes(prefix + "\n"), .. c]);

    private static EvidenceError Refused(string json) => Refused(Encoding.UTF8.GetBytes(json));

    private static EvidenceError Refused(byte[] bytes)
    {
        Assert.False(StrictJson.TryParse(bytes, out _, out var error));
        return error!;
    }

    [Fact]
    public void Gv_c01_is_the_contract_bytes_and_its_three_prefixed_digests()
    {
        var c = Jcs.Bytes(J.O(("seed", J.S(EvidenceScalars.U64(ulong.MaxValue)))));
        Assert.Equal("{\"seed\":\"18446744073709551615\"}", Encoding.UTF8.GetString(c));
        Assert.Equal(31, c.Length);
        Assert.Equal("dddc693f1087dccd961870d3e6f79997db44912d90c27b94906e43e56f8b8305", Sha(c));
        Assert.Equal("038327f726000c97325b4fcb4a49d435a3ca7997c331aa2752d2690f2d2890c9", Prefixed("STARPLAYER-APLUS-FAMILY-V1", c));
        Assert.Equal("5dacde4078f227d414373f550b3be24a731ce32e59fd33a63033fbabfcf79fb4", Prefixed("STARPLAYER-APLUS-DOMINANCE-V1", c));
        Assert.Equal("a6aa2c4fedee603823e2f2e01e28d33805f5b772d754be2e4092db0fce6d019d", Prefixed("STARPLAYER-APLUS-PHASE-V1", c));
    }

    [Fact]
    public void Gv_c02_sorts_its_keys_writes_the_f64_extremes_and_matches_every_digest()
    {
        // members given out of order on purpose: JCS sorts them
        var c = Jcs.Bytes(J.O(("negative_zero", J.S(Jcs.Es(-0.0))), ("min", J.S(Jcs.Es(double.Epsilon))), ("max", J.S(Jcs.Es(double.MaxValue)))));
        Assert.Equal("{\"max\":\"1.7976931348623157e+308\",\"min\":\"5e-324\",\"negative_zero\":\"0\"}", Encoding.UTF8.GetString(c));
        Assert.Equal(68, c.Length);
        Assert.Equal("8081fa9c30d301d53e313cc594722efa308f67a04d079512aca5c255876a773f", Sha(c));
        Assert.Equal("28bebb45cbb16006668ec6460619a6d33dd15afe8479c0fa20fd8058575ca705", Prefixed("STARPLAYER-APLUS-FAMILY-V1", c));
        Assert.Equal("e581ac6c69c0d5d877f4ced827e51a373c63ac412d301eeb1083c4c154700c24", Prefixed("STARPLAYER-APLUS-DOMINANCE-V1", c));
        Assert.Equal("7d69df8a9cd3ad9d7d59a5bcc0bc74f81fc210a985cd986c214942f100d5fe61", Prefixed("STARPLAYER-APLUS-PHASE-V1", c));
    }

    [Fact]
    public void Gv_ev0_the_empty_event_array_digest()
    {
        var stream = new MemoryStream();
        Jcs.WriteArray(stream, []);
        Assert.Equal("ed666f17bb1a25a68a4457864105ec21fb879a61b7c3919422e1918b14e2ed6e", Prefixed("STARPLAYER-APLUS-PZL-EVENTS-V1", stream.ToArray()));
    }

    [Theory]
    [InlineData("8000000000000000", "0")]                         // −0
    [InlineData("0000000000000001", "5e-324")]                    // smallest subnormal
    [InlineData("7fefffffffffffff", "1.7976931348623157e+308")]   // largest finite
    [InlineData("3eb0c6f7a0b5ed8d", "0.000001")]                  // 10^-6
    [InlineData("3e7ad7f29abcaf48", "1e-7")]                      // 10^-7
    [InlineData("444b1ae4d6e2ef50", "1e+21")]                     // 10^21
    [InlineData("43143ff3c1cb0959", "1424953923781206.2")]
    [InlineData("430c6bf526340000", "1000000000000000")]          // 1e15 (S00.5 §7 boundary; .NET "R" writes 1E+15)
    [InlineData("3ee4f8b588e368f1", "0.00001")]                   // 1e-5
    [InlineData("437b69b4ba630f35", "123456789012345680")]        // 1.2345678901234568e17
    public void F64_value_strings_follow_the_ecmascript_rule_for_these_bits(string bits, string expected)
    {
        var value = BitConverter.Int64BitsToDouble(unchecked((long)ulong.Parse(bits, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        Assert.Equal(expected, Jcs.Es(value));
        Assert.Equal(bits, EvidenceScalars.Bits64(value));
    }

    [Fact]
    public void The_f64_rule_is_not_the_dotnet_round_trip_format()
    {
        // where .NET 8 "R" switches to E notation the ES rule does not (and the reverse never happens below 1e21)
        // (measured .NET 8 output: 1E+20, 1E+21, 1E-05, 1E-06, 1.2345678901234568E+17)
        foreach (var (v, es) in new[] { (1e20, "100000000000000000000"), (1e21, "1e+21"), (1e-5, "0.00001"), (1e-6, "0.000001"), (1.2345678901234568e17, "123456789012345680") })
        {
            Assert.Equal(es, Jcs.Es(v));
            Assert.NotEqual(v.ToString("R", CultureInfo.InvariantCulture), Jcs.Es(v));
        }
        // every shortest string reads back to the very same bits
        var random = new Random(20261024);
        for (var i = 0; i < 20_000; i++)
        {
            var v = BitConverter.Int64BitsToDouble(random.NextInt64());
            if (!double.IsFinite(v))
                continue;
            var text = Jcs.Es(v);
            Assert.Equal(v == 0 ? 0 : v, double.Parse(text, CultureInfo.InvariantCulture));
            // shortest: the same digits with one fewer significant digit never reads back
            Assert.True(text.TrimStart('-').Split('e')[0].Replace(".", "").Trim('0').Length <= 17);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Jcs.Es(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Jcs.Es(double.PositiveInfinity));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("18446744073709551615", true)]
    [InlineData("18446744073709551616", false)]   // 2^64
    [InlineData("00", false)]
    [InlineData("01", false)]
    [InlineData("+1", false)]
    [InlineData("-0", false)]
    [InlineData(" 1", false)]
    [InlineData("1 ", false)]
    [InlineData("1e3", false)]
    [InlineData("1.0", false)]
    [InlineData("", false)]
    public void U64_accepts_only_the_canonical_decimal_text(string text, bool ok) => Assert.Equal(ok, EvidenceScalars.TryParseU64(text, out _));

    [Theory]
    [InlineData("0", true)]
    [InlineData("9223372036854775807", true)]
    [InlineData("9223372036854775808", false)]
    [InlineData("01", false)]
    [InlineData("-1", false)]
    public void D64_accepts_only_the_canonical_non_negative_text(string text, bool ok) => Assert.Equal(ok, EvidenceScalars.TryParseD64(text, out _));

    [Fact]
    public void Gv_a23_duplicate_key_bom_bad_utf8_and_lone_surrogates_are_refused_before_canonicalization()
    {
        Assert.Equal("Json.duplicate_key", Refused("{\"a\":1,\"a\":1}").Cause);
        Assert.Equal("/x/a", Refused("{\"x\":{\"a\":1,\"a\":2}}").Pointer);
        Assert.Equal("Json.bom", Refused([0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}']).Cause);
        Assert.Equal("Json.utf8_invalid", Refused([(byte)'"', 0xC3, 0x28, (byte)'"']).Cause);
        Assert.Equal("Json.utf8_invalid", Refused([(byte)'"', 0xED, 0xA0, 0x80, (byte)'"']).Cause);   // a raw (CESU) surrogate
        Assert.Equal("Json.lone_surrogate", Refused("\"\\ud800\"").Cause);
        Assert.Equal("Json.lone_surrogate", Refused("\"\\udc00\"").Cause);
        Assert.Equal("Json.lone_surrogate", Refused("\"\\ud800\\u0041\"").Cause);
        Assert.Equal("Json.trailing_content", Refused("{} {}").Cause);
        Assert.Equal("Json.trailing_content", Refused("1 2").Cause);
        Assert.All(new[] { "[1,]", "{\"a\":1,}", "NaN", "'a'", "[01]", "[1.]", "[.5]", "[-]", "{a:1}", "\"\t\"" }, j => Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(j).Code));
    }

    [Fact]
    public void Equivalent_escapes_read_as_the_same_string_and_jcs_writes_them_one_way()
    {
        var a = (JString)StrictJson.Parse("\"\\u00e9\\/\\u0041\""u8);
        var b = (JString)StrictJson.Parse(Encoding.UTF8.GetBytes("\"é/A\""));
        Assert.Equal(a.Value, b.Value);
        Assert.Equal("\"é/A\"", Jcs.Text(a));
        Assert.Equal("\"\\u0001\\n\\\"\\\\\"", Jcs.Text(J.S("\u0001\n\"\\")));
        // a valid pair survives as a pair (no NFC/NFD change, no trim)
        Assert.Equal("\"😀 \"", Jcs.Text(StrictJson.Parse("\"\\ud83d\\ude00 \""u8)));
    }

    [Fact]
    public void Keys_are_sorted_by_utf16_code_units_not_by_code_points_or_culture()
    {
        // U+FB33 (BMP, one unit 0xFB33) sorts AFTER U+1F600 (surrogate 0xD83D...) in UTF-16 order
        var o = J.O(("\uFB33", J.N(1)), ("\U0001F600", J.N(2)), ("a", J.N(3)), ("B", J.N(4)));
        Assert.Equal("{\"B\":4,\"a\":3,\"\U0001F600\":2,\"\uFB33\":1}", Jcs.Text(o));
    }

    [Fact]
    public void Numbers_keep_their_token_text_and_jcs_refuses_an_integer_beyond_two_to_the_53()
    {
        var n = (JNumber)((JArray)StrictJson.Parse("[1.0,9007199254740992,-0]"u8)).Items[0];
        Assert.Equal("1.0", n.Raw);
        Assert.Equal("1", Jcs.Text(n));
        Assert.Throws<ArgumentOutOfRangeException>(() => Jcs.Text(new JNumber("9007199254740992")));
        Assert.Equal("9007199254740991", Jcs.Text(new JNumber("9007199254740991")));
    }
}
