using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier;

/// <summary>One capture of the calculation environment (§18.4 E2-1): everything the environment profile pins, plus the digest of the environment block.</summary>
public sealed record EnvironmentSnapshot(
    string EnvironmentDigest, string OsVersion, string OsArchitecture, string ProcessArchitecture, string FrameworkDescription, string NetCoreAppVersion, string RuntimeIdentifier,
    string UcrtFileVersion, string UcrtSha256, IReadOnlyDictionary<string, string> RuntimeFiles)
{
    public string Text() => string.Join('\n', new[] { $"env_digest={EnvironmentDigest}", $"os={OsVersion} {OsArchitecture}", $"process={ProcessArchitecture}", $"framework={FrameworkDescription}",
        $"netcore_app={NetCoreAppVersion}", $"rid={RuntimeIdentifier}", $"ucrtbase={UcrtFileVersion} {UcrtSha256}" }.Concat(RuntimeFiles.Select(f => $"{f.Key}={f.Value}")));
}

/// <summary>
/// §18 (S01 §5 + PATCH-04/05, S03 E2): environment capture C0/C1/C2, the static check of E2-1 (no variable whose name starts with <c>DOTNET_</c> or <c>COMPlus_</c>, case-insensitively), the exact profile comparison
/// of E2-4 (a pure function of a snapshot and a profile, E5) and the bit Golden of E2-5 (36 <c>UpperQuantile(0.05/j)</c> bits and the <c>dom-witness-fixture-v1</c> digest). The Dominance formulas are not
/// changed; only their binary64 results are recorded.
/// </summary>
public static class ExecutionEnvironment
{
    public static readonly string[] ForbiddenPrefixes = ["COMPlus_", "DOTNET_"];

    public static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>E2-1 digest: upper-cased names, <c>NAME=VALUE\n</c> in ordinal name order, UTF-8, SHA-256.</summary>
    public static string EnvironmentDigest(IDictionary variables)
    {
        var lines = variables.Cast<DictionaryEntry>().Select(e => (Name: ((string)e.Key).ToUpperInvariant(), Value: (string?)e.Value ?? "")).OrderBy(e => e.Name, StringComparer.Ordinal)
            .Select(e => $"{e.Name}={e.Value}\n");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(lines)))).ToLowerInvariant();
    }

    public static IReadOnlyList<string> ForbiddenVariables(IDictionary variables) =>
        variables.Keys.Cast<string>().Where(k => ForbiddenPrefixes.Any(p => k.StartsWith(p, StringComparison.OrdinalIgnoreCase))).Order(StringComparer.Ordinal).ToList();

    public static EnvironmentSnapshot Capture()
    {
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var ucrt = Path.Combine(Environment.SystemDirectory, "ucrtbase.dll");
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "System.Private.CoreLib.dll", "clrjit.dll", "coreclr.dll" })
            files[name] = File.Exists(Path.Combine(runtime, name)) ? Sha(Path.Combine(runtime, name)) : "missing";
        return new EnvironmentSnapshot(
            EnvironmentDigest(Environment.GetEnvironmentVariables()), Environment.OSVersion.Version.ToString(), RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(), RuntimeInformation.FrameworkDescription, Environment.Version.ToString(), RuntimeInformation.RuntimeIdentifier,
            File.Exists(ucrt) ? FileVersionInfo.GetVersionInfo(ucrt).FileVersion ?? "" : "missing", File.Exists(ucrt) ? Sha(ucrt) : "missing", files);
    }

    /// <summary>E2-4 / E5: the first field in which a snapshot differs from an environment profile (§18.2), or null. Never switches to another profile.</summary>
    public static string? Mismatch(EnvironmentSnapshot c, JObject profile)
    {
        string P(string path) => Schema.Text(path.Split('/').Aggregate((JNode)profile, (n, k) => ((JObject)n)[k]));
        var expected = new (string Name, string Want, string Got)[]
        {
            ("os.version", P("os/version"), c.OsVersion), ("os.architecture", P("os/architecture"), c.OsArchitecture), ("process_architecture", P("process_architecture"), c.ProcessArchitecture),
            ("runtime.framework_description", P("runtime/framework_description"), c.FrameworkDescription), ("runtime.netcore_app_version", P("runtime/netcore_app_version"), c.NetCoreAppVersion),
            ("runtime.runtime_identifier", P("runtime/runtime_identifier"), c.RuntimeIdentifier), ("native_math.file_version", P("native_math/file_version"), c.UcrtFileVersion),
            ("native_math.raw_sha256", P("native_math/raw_sha256"), c.UcrtSha256),
        };
        if (expected.FirstOrDefault(e => e.Want != e.Got) is { Name: not null } bad)
            return $"{bad.Name}: 프로필 '{bad.Want}' ≠ 실측 '{bad.Got}'";
        // every required file is compared (B1-T11-05): the profile's rows are looked up by the required names, never iterated as submitted
        var rows = Schema.Items(profile["runtime_files"]).Select(Schema.Obj).ToList();
        if (!rows.Select(f => Schema.Text(f["name"])).SequenceEqual(ApprovalFormats.RuntimeFileNames, StringComparer.Ordinal))
            return "runtime_files: 정확히 세 파일(ordinal 순서)이 아닙니다";
        foreach (var name in ApprovalFormats.RuntimeFileNames)
            if (c.RuntimeFiles.GetValueOrDefault(name) is not { } got || got != Schema.Text(rows.Single(f => Schema.Text(f["name"]) == name)["raw_sha256"]))
                return $"runtime_files.{name}: SHA가 프로필과 다릅니다";
        return null;
    }

    /// <summary>E2-5: the 36 bits of <c>UpperQuantile(0.05/j)</c>, j = 1..36.</summary>
    public static IReadOnlyList<string> UpperQuantileBits() =>
        Enumerable.Range(1, 36).Select(j => EvidenceScalars.Bits64(DominanceStatistics.UpperQuantile(DominanceStatistics.Alpha / j))).ToList();

    /// <summary>
    /// E2-5 <c>dom-witness-fixture-v1</c> (§18.2): cell c = 1..48 gets <c>CellSample(20, 80+c, ⌊(80+c)²/20⌋+40+c)</c>; the 240 witnesses in their generated order, every binary64 as BITS64 (Holm critical
    /// null after the stop), digest <c>sha256(ASCII("STARPLAYER-ENV-WITNESS-V1") || 0x0A || JCS(array))</c>. Interpretation (recorded in the B1 report): difference, standard_error, lower_bound and
    /// non_superiority_z are all written as BITS64.
    /// </summary>
    public static string WitnessDigest()
    {
        var samples = DominanceMatrix.Cells.ToDictionary(c => c.Id, c => new CellSample(20, 80 + c.Number, (80L + c.Number) * (80 + c.Number) / 20 + 40 + c.Number), StringComparer.Ordinal);
        var (batter, pitcher) = DominanceStatistics.Evaluate(samples);
        var rows = new[] { batter, pitcher }.SelectMany(side => side.Strategies.SelectMany(s => s.Witnesses.Select(w => (JNode)J.O(
            ("side", J.S(side.Side.ToString())), ("strategy", J.S(s.Strategy)), ("rival", J.S(w.Rival)), ("column", J.S(w.Column)),
            ("difference", J.S(EvidenceScalars.Bits64(w.Difference))), ("standard_error", J.S(EvidenceScalars.Bits64(w.StandardError))), ("lower_bound", J.S(EvidenceScalars.Bits64(w.LowerBound))),
            ("non_superiority_z", J.S(EvidenceScalars.Bits64(w.NonSuperiorityZ))), ("holm_critical", w.HolmCritical is { } h ? J.S(EvidenceScalars.Bits64(h)) : JNode.Null),
            ("holm_rejected", J.B(w.HolmRejected)))))).ToList();
        if (rows.Count != 240)
            throw new InvalidOperationException($"Witness는 240개여야 합니다(받은 수 {rows.Count}).");
        return EvidenceHash.APlus("STARPLAYER-ENV-WITNESS-V1", Jcs.Bytes(J.A(rows)));
    }

    /// <summary>E2-5 against a profile's math_golden: the first differing value, or null.</summary>
    public static string? GoldenMismatch(JObject profile)
    {
        var golden = (JObject)profile["math_golden"];
        var bits = UpperQuantileBits();
        var rows = Schema.Items(golden["upper_quantile"]).Select(Schema.Obj).ToList();
        if (rows.Count != 36 || rows.Select((e, i) => Schema.Long(e["divisor"]) != i + 1).Any(x => x))
            return "upper_quantile: divisor가 정확히 1..36 순서가 아닙니다";
        for (var j = 1; j <= 36; j++)                                                                    // all 36 divisors, by position (B1-T11-05)
            if (bits[j - 1] != Schema.Text(rows[j - 1]["bits"]))
                return $"upper_quantile j={j}";
        return WitnessDigest() == Schema.Text(golden["witness_digest"]) ? null : "witness_digest";
    }
}
