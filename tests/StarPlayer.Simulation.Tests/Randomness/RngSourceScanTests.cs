using System.Text.RegularExpressions;

namespace StarPlayer.Simulation.Tests.Randomness;

// ROADMAP §6.1: never System.Random, wall time, randomized hashing, thread scheduling or a shared global generator in simulation code.
public class RngSourceScanTests
{
    private static readonly string[] Forbidden =
    [
        @"\bSystem\.Random\b", @"\bnew\s+Random\s*\(", @"\bRandom\.Shared\b", @"\bRandomNumberGenerator\b",
        @"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b", @"\bStopwatch\b", @"\bEnvironment\.TickCount",
        @"\.GetHashCode\s*\(", @"\bHashCode\.Combine\b", @"\bGuid\.NewGuid\b", @"\bThread(Static|Local)?\b", @"\bTask\.Run\b",
        @"\bstatic\s+(?!readonly\b|class\b|extern\b)[A-Za-z<>\[\],\s?]+\s+_?[A-Za-z]+\s*(=|;)",
    ];

    [Fact]
    public void Randomness_sources_use_no_nondeterministic_input_or_mutable_static_state()
    {
        string dir = Path.Combine(RngFixture.Root, "src", "StarPlayer.Simulation", "Randomness");
        string[] files = Directory.GetFiles(dir, "*.cs");
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            // Strip comments so documentation may name the forbidden things.
            string code = Regex.Replace(Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\r\n]*", "");
            foreach (string pattern in Forbidden)
                Assert.False(Regex.IsMatch(code, pattern), $"{Path.GetFileName(file)} matches forbidden pattern {pattern}");
        }
    }

    [Fact]
    public void Simulation_assembly_still_depends_on_domain_only()
    {
        var names = typeof(StarPlayer.Simulation.Randomness.Pcg32).Assembly.GetReferencedAssemblies()
            .Select(n => n.Name!).Where(n => n.StartsWith("StarPlayer.", StringComparison.Ordinal));
        Assert.All(names, n => Assert.Equal("StarPlayer.Domain", n));
    }
}
