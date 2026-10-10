namespace StarPlayer.Application.Tests;

/// <summary>
/// Simulation must be deterministic and headless (ROADMAP §6.1 RNG rules): no wall clock, shared/ambient
/// randomness, generated ids, environment, files, network or thread scheduling. The detector is tested
/// both ways: every forbidden construct is found, ordinary code is not flagged.
/// </summary>
public class SimulationDeterminismTests
{
    private static readonly string Root = ArchitectureTests.RepositoryRoot;

    [Theory]
    [InlineData("var r = new Random();", "new Random")]
    [InlineData("var r = new Random ();", "new Random")]
    [InlineData("var r = new System.Random(42);", "new Random")]
    [InlineData("Random r = new();", "target-typed new Random")]
    [InlineData("var n = Random.Shared.Next();", "Random.Shared")]
    [InlineData("var n = System.Random . Shared.Next();", "Random.Shared")]
    [InlineData("var b = RandomNumberGenerator.GetBytes(4);", "RandomNumberGenerator")]
    [InlineData("var t = DateTime.Now;", "DateTime clock")]
    [InlineData("var t = DateTime.UtcNow;", "DateTime clock")]
    [InlineData("var t = DateTime.Today;", "DateTime clock")]
    [InlineData("var t = System.DateTime.UtcNow;", "DateTime clock")]
    [InlineData("var t = global::System.DateTimeOffset.UtcNow;", "DateTime clock")]
    [InlineData("var t = DateTime\n    .Now;", "DateTime clock")]
    [InlineData("var id = Guid.NewGuid();", "Guid.NewGuid")]
    [InlineData("var ms = Environment.TickCount64;", "Environment")]
    [InlineData("var w = Stopwatch.StartNew();", "Stopwatch/TimeProvider")]
    [InlineData("var t = TimeProvider.System.GetUtcNow();", "Stopwatch/TimeProvider")]
    [InlineData("var text = File.ReadAllText(path);", "File static")]
    [InlineData("var text = System.IO.File.ReadAllText(path);", "System.IO")]
    [InlineData("using System.IO;", "System.IO")]
    [InlineData("var names = Directory.GetFiles(dir);", "File static")]
    [InlineData("using var reader = new StreamReader(path);", "file stream types")]
    [InlineData("var p = Path.GetTempPath();", "Path temp")]
    [InlineData("using var http = new HttpClient();", "System.Net")]
    [InlineData("Parallel.For(0, 10, i => { });", "thread scheduling")]
    [InlineData("await Task.Run(() => 1);", "thread scheduling")]
    [InlineData("var h = key.GetHashCode();", "randomized hash")]
    [InlineData("var h = HashCode.Combine(a, b);", "randomized hash")]
    // Evasions of a naive text search:
    [InlineData("var s = $\"stamp {DateTime.UtcNow}\";", "DateTime clock")]
    [InlineData("var url = \"http://x\"; var t = DateTime.Now;", "DateTime clock")]
    [InlineData("/* harmless */ var t = DateTime.Now;", "DateTime clock")]
    [InlineData("var c = '\"'; var t = DateTime.Now;", "DateTime clock")]
    public void Detector_finds_each_forbidden_construct(string source, string expectedRule)
    {
        Assert.Contains(expectedRule, SourceScan.Find(source, SourceScan.SimulationRules));
    }

    [Theory]
    [InlineData("// DateTime.Now and new Random() are forbidden here")]
    [InlineData("/* File.ReadAllText(path); Guid.NewGuid(); */ var x = 1;")]
    [InlineData("var message = \"DateTime.UtcNow, Random.Shared, File.Exists\";")]
    [InlineData("var path = @\"C:\\data\\DateTime.Now\\Guid.NewGuid()\";")]
    [InlineData("var raw = \"\"\"Environment.TickCount64\"\"\";")]
    [InlineData("var now = clock.Now; var today = calendar.Today;")]
    [InlineData("var rng = new SeededRandom(seed); var n = rng.Next();")]
    [InlineData("var n = context.Random.Next(); var s = streams.Random.Shared;")]
    [InlineData("var d = DateTime.MinValue; var span = TimeSpan.FromSeconds(1); var day = DateOnly.FromDayNumber(5);")]
    [InlineData("var id = Guid.Empty; var parsed = Guid.Parse(text);")]
    [InlineData("var e = GameEnvironment.Create(); var p = player.File.Name; var f = Profile.Files;")]
    [InlineData("public override int GetHashCode() => 17;")]
    [InlineData("var path = Path.Combine(a, b); var name = Path.GetFileName(p);")]
    [InlineData("var t = Task.CompletedTask; var v = await Task.FromResult(1);")]
    [InlineData("var ordered = items.OrderBy(i => i.Id).ToArray();")]
    public void Detector_does_not_flag_ordinary_deterministic_code(string source)
    {
        Assert.True(SourceScan.Find(source, SourceScan.SimulationRules).Count == 0,
            $"false positive on: {source} -> {string.Join(", ", SourceScan.Find(source, SourceScan.SimulationRules))}");
    }

    [Fact]
    public void Simulation_sources_contain_no_forbidden_construct()
    {
        var folder = Path.Combine(Root, "src", "StarPlayer.Simulation");
        foreach (var file in ArchitectureTests.SourceFilesUnder(folder))
        {
            var found = SourceScan.Find(File.ReadAllText(file), SourceScan.SimulationRules);
            Assert.True(found.Count == 0, $"{Path.GetRelativePath(Root, file)} uses {string.Join(", ", found)}");
        }
    }
}
