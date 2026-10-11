using System.Security.Cryptography;
using System.Text;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C1: every legacy event of the Phase 2 corpus still has the ef1 receipt it had before the ef2 contract existed. The digest below was computed
/// on the unchanged Phase 3-B2 commit (ddbc999) and is a literal on purpose: it must not be regenerated from the current code.
/// </summary>
public class Ef1CorpusPinTests
{
    private const string DigestAtPhase3B2 = "417a91f06b4e588e8b691321008e1288cd77d10ca76608005591d248e42cc375";

    private static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    [Fact]
    public void Every_receipt_of_the_hundred_phase_2_games_is_byte_identical_to_the_one_before_ef2_existed()
    {
        var root = Root();
        var loaded = new ScriptedGameSource(File.ReadAllText(Path.Combine(root, "content", "rules", "game_rules_profiles.json")),
            File.ReadAllText(Path.Combine(root, "tests", "Fixtures", "phase2_scripted_games_100.json"))).Load();
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);

        var lines = new StringBuilder();
        var count = 0;
        foreach (var game in loaded.Value)
            foreach (var e in game.Events)
            {
                var r = EventFingerprint.Receipt(e);
                Assert.Equal("ef1", r.FingerprintVersion);
                lines.Append(game.Id).Append('|').Append(r.EventId).Append('|').Append(r.Kind).Append('|').Append(r.FingerprintVersion).Append('|').Append(r.Fingerprint).Append('\n');
                count++;
            }

        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lines.ToString()))).ToLowerInvariant();
        Assert.Equal(30974, count);
        Assert.Equal(DigestAtPhase3B2, digest);
    }

    private const string WriterDigestAtPhase3B2 = "e08400165a69feed281eab5e763dad833dcccdb122820465c53a24d006f19ffe";

    [Fact]
    public void The_legacy_event_writer_produces_the_same_bytes_for_the_hundred_phase_2_games_as_before_event_format_2_existed()
    {
        var root = Root();
        var loaded = new ScriptedGameSource(File.ReadAllText(Path.Combine(root, "content", "rules", "game_rules_profiles.json")),
            File.ReadAllText(Path.Combine(root, "tests", "Fixtures", "phase2_scripted_games_100.json"))).Load();
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);

        var all = new StringBuilder();
        foreach (var game in loaded.Value)
            all.Append(game.Id).Append('\n').Append(ScriptedGameJson.WriteEvents(game.Events)).Append('\n');

        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(all.ToString()))).ToLowerInvariant();
        Assert.Equal(WriterDigestAtPhase3B2, digest);
    }
}
