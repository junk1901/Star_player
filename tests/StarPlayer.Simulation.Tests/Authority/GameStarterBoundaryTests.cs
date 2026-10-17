using System.Reflection;
using System.Text.RegularExpressions;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.Randomness;

namespace StarPlayer.Simulation.Tests.Authority;

/// <summary>
/// Phase 3-F F-A: <see cref="GameStarter"/> is the one public way to begin a game's authority. These pins keep it a way to begin a NEW game only: nothing outside Simulation can supply a state, a
/// restored history or an aggregate, and no other public member of the assembly hands out a cursor or an aggregate.
/// </summary>
public class GameStarterBoundaryTests
{
    [Fact]
    public void It_is_a_static_class_with_exactly_one_public_method_that_takes_a_game_start_and_a_seed()
    {
        var type = typeof(GameStarter);
        Assert.True(type.IsAbstract && type.IsSealed && type.IsPublic);
        var begin = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("Begin", begin.Name);
        Assert.Equal([typeof(GameStart), typeof(ulong)], begin.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.Equal(typeof(StarPlayer.Domain.Common.Result<GameCursor>), begin.ReturnType);
    }

    [Fact]
    public void No_other_public_member_of_the_assembly_creates_a_cursor_or_an_aggregate()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var makers = typeof(GameCursor).Assembly.GetExportedTypes()
            .SelectMany(t => t.GetMethods(all).Where(m => !m.IsSpecialName).Select(m => (Type: t, Method: m)))
            .Where(x => Contains(x.Method.ReturnType, typeof(GameCursor)) || Contains(x.Method.ReturnType, typeof(AuthoritativeGame)))
            .Select(x => $"{x.Type.Name}.{x.Method.Name}")
            .ToArray();

        Assert.Equal(["GameStarter.Begin"], makers);
        Assert.Empty(typeof(GameCursor).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    private static bool Contains(Type type, Type wanted) => type == wanted || (type.IsGenericType && type.GetGenericArguments().Any(a => Contains(a, wanted)));

    [Fact]
    public void The_source_builds_the_state_with_the_reducer_and_never_restores_or_takes_a_state()
    {
        var path = Path.Combine(RngFixture.Root, "src", "StarPlayer.Simulation", "Baseball", "GameStarter.cs");
        var code = Regex.Replace(Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\r\n]*", "");

        Assert.Contains("GameReducer.Start(", code, StringComparison.Ordinal);
        Assert.Contains("AuthoritativeGame.StartNew(", code, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "Restore", "GameState? ", "GameState ", "Publish", "CommitGate", "NextBounded", ".Stream(", "System.Random", "DateTime" })
            Assert.DoesNotContain(forbidden, code);
    }
}
