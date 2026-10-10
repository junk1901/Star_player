using StarPlayer.Application.Ports;
using StarPlayer.Application.Queries;
using StarPlayer.Application.Sessions;
using StarPlayer.Domain.Common;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Application.Tests;

public class PlayerCardAndSelfCheckTests
{
    private sealed class FailingSource : IPlayerWorldSource
    {
        public Result<PlayerWorld> Load() => throw new InvalidOperationException("디스크 오류");
    }

    private static PlayerInfoService Service(bool cheat = false) => new(new FixturePlayerWorldSource(), new PlayerSessionOptions(CheatMode: cheat));

    [Fact]
    public void Self_check_passes_all_seven_steps_on_the_fictional_pack_in_normal_and_cheat_sessions()
    {
        foreach (var cheat in new[] { false, true })
        {
            var report = PlayerInfoSelfCheck.Run(Service(cheat));

            Assert.True(report.Passed, string.Join("; ", report.Steps.Where(s => !s.Passed).Select(s => $"{s.Name}: {s.Detail}")));
            Assert.Equal(7, report.Steps.Count);
        }
    }

    [Fact]
    public void Self_check_reports_a_failed_load_instead_of_throwing()
    {
        var service = new PlayerInfoService(new FailingSource());

        var report = PlayerInfoSelfCheck.Run(service);

        Assert.False(report.Passed);
        Assert.Single(report.Steps);
        Assert.Contains("디스크 오류", service.LoadError);
    }

    [Fact]
    public void Card_text_uses_plain_line_feeds_and_always_carries_the_hidden_notice()
    {
        var service = Service();
        foreach (var item in service.ListPlayers())
        {
            var card = PlayerCardFormatter.Format(service.GetPlayer(new PlayerQuery(item.PlayerId)).View!);

            Assert.DoesNotContain('\r', card);
            Assert.Contains(PlayerCardFormatter.HiddenNotice, card);
            Assert.Contains(item.DisplayName, card);
            Assert.Empty(HiddenDataGuard.FindLeaks(card));
        }
    }

    [Fact]
    public void Own_card_shows_bare_numbers_and_other_cards_show_grades_with_ranges()
    {
        var service = Service();
        var own = PlayerCardFormatter.Format(service.GetPlayer(new PlayerQuery("p-0001")).View!);
        var other = PlayerCardFormatter.Format(service.GetPlayer(new PlayerQuery("p-0005")).View!);

        Assert.Contains("컨택 : 58", own);
        Assert.Contains("[내 선수 · 정확한 현재 능력치]", own);
        Assert.DoesNotContain("신뢰도", own);
        Assert.Contains("[다른 선수 · 관측 등급과 범위]", other);
        Assert.Contains("신뢰도", other);
        Assert.DoesNotContain("컨택 : 55", other); //박민수(p-0005)'s real contact must not appear as a number
        Assert.Matches(@"컨택 : [SABCDEF] \(\d+~\d+\)", other);
    }

    [Fact]
    public void Card_separates_base_ability_state_and_proficiency_sections()
    {
        var card = PlayerCardFormatter.Format(Service().GetPlayer(new PlayerQuery("p-0006")).View!);

        var ability = card.IndexOf("■ 기본 능력", StringComparison.Ordinal);
        var state = card.IndexOf("■ 현재 상태", StringComparison.Ordinal);
        var proficiency = card.IndexOf("■ 포지션과 숙련도", StringComparison.Ordinal);

        Assert.True(0 < ability && ability < state && state < proficiency);
        Assert.Contains("[포수 능력]", card);
        Assert.Contains("포수 : ", card[proficiency..]);
    }
}
