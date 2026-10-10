using System.Reflection;
using Godot;
using StarPlayer.Application.Diagnostics;
using StarPlayer.Infrastructure.Sqlite;

namespace StarPlayer.Client.Bootstrap;

public sealed record AppServices(BuildInfo Build, DiagnosticsService Diagnostics);

/// <summary>The only place that knows concrete Infrastructure types (ROADMAP §1.2).</summary>
public static class CompositionRoot
{
    public static AppServices Build()
    {
        var assemblyVersion = typeof(CompositionRoot).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var godotVersion = Engine.GetVersionInfo()["string"].AsString();
        var build = new BuildInfo("Star Player", $"{assemblyVersion} · Godot {godotVersion} · .NET {System.Environment.Version}");

        return new AppServices(build, new DiagnosticsService(new SqliteStorageDiagnostics()));
    }
}
