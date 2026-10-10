using StarPlayer.Application.Ports;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Baseball.Game;

namespace StarPlayer.Application.Projections;

/// <summary>
/// Compares a finalized game with expected results that were written down independently of the engine. Every difference is
/// reported by name; an empty result means all numbers match. Stat keys are the lower-cased <see cref="StatKey"/> names.
/// </summary>
internal static class ExpectedComparison
{
    public static IReadOnlyList<string> Compare(FinalizedGame game, ExpectedGame expected)
    {
        var problems = new List<string>();
        void Same<T>(string name, T actual, T wanted)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, wanted))
                problems.Add($"{name}: 실제 {actual} ≠ 기대 {wanted}");
        }

        void SameList(string name, IReadOnlyList<int> actual, IReadOnlyList<int> wanted)
        {
            if (!actual.SequenceEqual(wanted))
                problems.Add($"{name}: 실제 [{string.Join(",", actual)}] ≠ 기대 [{string.Join(",", wanted)}]");
        }

        var box = game.BoxScore;
        Same("결과", SnakeCase.From(game.Outcome.ToString()), expected.Outcome);
        Same("원정 득점", box.Away.Runs, expected.AwayRuns);
        Same("홈 득점", box.Home.Runs, expected.HomeRuns);
        SameList("원정 이닝별 득점", box.AwayLine, expected.AwayLine);
        SameList("홈 이닝별 득점", box.HomeLine, expected.HomeLine);
        Same("원정 안타", box.Away.Hits, expected.AwayHits);
        Same("홈 안타", box.Home.Hits, expected.HomeHits);
        Same("원정 실책", box.Away.Errors, expected.AwayErrors);
        Same("홈 실책", box.Home.Errors, expected.HomeErrors);
        Same("원정 잔루", box.Away.LeftOnBase, expected.AwayLeftOnBase);
        Same("홈 잔루", box.Home.LeftOnBase, expected.HomeLeftOnBase);

        // player lines: compare every non-zero key in both directions (battery keys belong to pair records, compared below)
        var actualPlayers = box.Players.GroupBy(p => p.Player.Value).ToDictionary(
            g => g.Key,
            g => g.SelectMany(p => p.Stats).Where(kv => !kv.Key.ToString().StartsWith("Battery", StringComparison.Ordinal))
                .GroupBy(kv => kv.Key.ToString().ToLowerInvariant()).ToDictionary(k => k.Key, k => k.Sum(kv => kv.Value)));
        foreach (var id in actualPlayers.Keys.Union(expected.Players.Keys).Order(StringComparer.Ordinal))
        {
            var a = actualPlayers.GetValueOrDefault(id) ?? [];
            var w = expected.Players.TryGetValue(id, out var wanted) ? wanted : new Dictionary<string, int>();
            foreach (var key in a.Keys.Union(w.Keys).Order(StringComparer.Ordinal))
                Same($"선수 {id} {key}", a.GetValueOrDefault(key), w.GetValueOrDefault(key));
        }

        var catchers = box.CatcherExposures.ToDictionary(c => c.Catcher.Value);
        foreach (var id in catchers.Keys.Union(expected.Catchers.Keys).Order(StringComparer.Ordinal))
        {
            catchers.TryGetValue(id, out var c);
            var w = expected.Catchers.TryGetValue(id, out var wanted) ? wanted : new Dictionary<string, int>();
            Same($"포수 {id} 받은 공", c?.PitchesReceived ?? 0, w.GetValueOrDefault("pitches"));
            Same($"포수 {id} 아웃", c?.OutsWhileCatching ?? 0, w.GetValueOrDefault("outs"));
            Same($"포수 {id} 도루 허용", c?.StolenBasesAllowed ?? 0, w.GetValueOrDefault("sba"));
            Same($"포수 {id} 도루 저지", c?.CaughtStealing ?? 0, w.GetValueOrDefault("cs"));
            Same($"포수 {id} 포일", c?.PassedBalls ?? 0, w.GetValueOrDefault("pb"));
        }

        var batteries = box.BatteryExposures.ToDictionary(b => $"{b.Catcher.Value}/{b.Pitcher.Value}");
        foreach (var id in batteries.Keys.Union(expected.Batteries.Keys).Order(StringComparer.Ordinal))
        {
            batteries.TryGetValue(id, out var b);
            var w = expected.Batteries.TryGetValue(id, out var wanted) ? wanted : new Dictionary<string, int>();
            Same($"배터리 {id} 투구", b?.Pitches ?? 0, w.GetValueOrDefault("pitches"));
            Same($"배터리 {id} 아웃", b?.Outs ?? 0, w.GetValueOrDefault("outs"));
            Same($"배터리 {id} 상대 타자", b?.BattersFaced ?? 0, w.GetValueOrDefault("bf"));
        }

        var decisions = box.Decisions.ToDictionary(d => SnakeCase.From(d.Kind.ToString()), d => d.Pitcher.Value);
        foreach (var kind in decisions.Keys.Union(expected.Decisions.Keys).Order(StringComparer.Ordinal))
            Same($"투수 결정 {kind}", decisions.GetValueOrDefault(kind) ?? "-", expected.Decisions.GetValueOrDefault(kind) ?? "-");

        var appearances = box.Appearances.Select(a => $"{SnakeCase.From(a.Team.ToString())}:{a.Player.Value}:{(a.InBattingOrder ? 1 : 0)}:{(a.Pitched ? 1 : 0)}").Order(StringComparer.Ordinal).ToList();
        if (!appearances.SequenceEqual(expected.Appearances.Order(StringComparer.Ordinal)))
            problems.Add($"경기 참가 목록이 다름: 실제 {string.Join(" ", appearances)} / 기대 {string.Join(" ", expected.Appearances)}");
        return problems;
    }
}
