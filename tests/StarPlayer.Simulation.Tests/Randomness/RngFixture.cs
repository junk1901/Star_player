using System.Globalization;
using System.Text.Json;

namespace StarPlayer.Simulation.Tests.Randomness;

// Golden vectors come from tests/Fixtures/generator/generate_rng_golden.py: an independent Python implementation of the PCG reference that
// first proves itself against the PCG author's published demo output. They are never produced by the C# code under test.
internal static class RngFixture
{
    public static readonly string Root = FindRoot();

    private static readonly Lazy<JsonElement> Doc = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase3a_rng_golden.json"))).RootElement);

    public static JsonElement Golden => Doc.Value;

    public static ulong Hex64(JsonElement e) => ulong.Parse(e.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    public static uint Hex32(JsonElement e) => Convert.ToUInt32(e.GetString()!.Replace("0x", ""), 16);

    private static string FindRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "StarPlayer.sln")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("저장소 루트(StarPlayer.sln)를 찾을 수 없습니다.");
    }
}
