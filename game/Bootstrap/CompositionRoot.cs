using System.Reflection;
using Godot;
using StarPlayer.Application.Diagnostics;
using StarPlayer.Application.Projections;
using StarPlayer.Application.Queries;
using StarPlayer.Application.Sessions;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Infrastructure.Sqlite;

namespace StarPlayer.Client.Bootstrap;

public sealed record AppServices(BuildInfo Build, DiagnosticsService Diagnostics, PlayerInfoService Players, GameDiagnosticsService Games);

/// <summary>The only place that knows concrete Infrastructure types (ROADMAP §1.2).</summary>
public static class CompositionRoot
{
    public static AppServices Build(LaunchOptions options)
    {
        var assemblyVersion = typeof(CompositionRoot).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var godotVersion = Engine.GetVersionInfo()["string"].AsString();
        var build = new BuildInfo("Star Player", $"{assemblyVersion} · Godot {godotVersion} · .NET {System.Environment.Version}");

        // Cheat mode is a session setting decided here, never by a screen. "My player" comes from the trusted fixture binding, not from any option.
        var players = PlayerInfoComposition.CreateFictionalPhase1(new PlayerSessionOptions(CheatMode: options.CheatMode));
        return new AppServices(build, new DiagnosticsService(new SqliteStorageDiagnostics()), players, GameDiagnosticsComposition.CreateFictionalPhase2());
    }
}
