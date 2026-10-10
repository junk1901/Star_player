using System.Reflection;
using StarPlayer.Application.Ports;
using StarPlayer.Domain.Common;

namespace StarPlayer.Infrastructure.Fixtures;

/// <summary>
/// Supplies the Phase 1 world from JSON text. The default instance reads the fictional two-club pack that is embedded in
/// this assembly (so the exported game needs no loose content folder). This is a test fixture source, not the Phase 4
/// datapack importer: it never writes and creates no save. It is <b>internal</b>: the raw world (including hidden
/// development data) is reachable only by the trusted composition code in this assembly; clients get
/// <see cref="PlayerInfoComposition"/> and the safe query service it returns.
/// </summary>
internal sealed class FixturePlayerWorldSource : IPlayerWorldSource
{
    public const string EmbeddedPackName = "phase1_two_club_pack.json";

    private readonly Func<string> _readJson;

    public FixturePlayerWorldSource() : this(ReadEmbeddedPack)
    {
    }

    public FixturePlayerWorldSource(string json) : this(() => json)
    {
    }

    private FixturePlayerWorldSource(Func<string> readJson) => _readJson = readJson;

    public Result<PlayerWorld> Load()
    {
        try
        {
            return WorldJson.Read(_readJson());
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            return Result<PlayerWorld>.Failure(new Error("Fixture.unreadable", $"가상 선수 데이터를 읽을 수 없습니다: {exception.Message}"));
        }
    }

    public static string ReadEmbeddedPack()
    {
        var assembly = typeof(FixturePlayerWorldSource).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedPackName)
            ?? throw new InvalidOperationException($"내장 데이터 '{EmbeddedPackName}'이(가) 어셈블리에 없습니다.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
