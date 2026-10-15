using System.Reflection;
using System.Runtime.CompilerServices;
using StarPlayer.Application.Diagnostics;
using StarPlayer.Application.Ports;
using StarPlayer.Application.Projections;
using StarPlayer.Application.Queries;
using StarPlayer.Application.Sessions;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Infrastructure.Sqlite;

namespace StarPlayer.Application.Tests;

/// <summary>
/// The Godot client is ONE assembly in which Bootstrap and presenters live side by side, so a namespace rule alone cannot
/// stop a presenter from calling what Bootstrap may call. The boundary is therefore structural: raw world data, the world
/// port, the viewer context and the service constructor are <c>internal</c> and visible only to Infrastructure and tests;
/// the only things the client assembly can name are the safe query service, its DTOs and two Infrastructure entry points.
/// These tests pin that, and add compiled-IL and source scans as defense in depth (they do not replace the structure).
/// </summary>
public class TrustedBoundaryTests
{
    private static readonly Assembly ApplicationAssembly = typeof(PlayerInfoService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(PlayerInfoComposition).Assembly;
    private static readonly Assembly DomainAssembly = typeof(StarPlayer.Domain.Common.PlayerId).Assembly;

    private static readonly string[] DangerousNames =
        ["PlayerWorld", "IPlayerWorldSource", "ViewerContext", "DevelopmentProfile", "PlayerSheet", "FixturePlayerWorldSource", "WorldJson",
         "IScriptedGameSource", "ScriptedGame", "ExpectedGame", "ScriptedGameSource", "ScriptedGameJson", "GameRulesJson", "GameReducer", "GameState",
         "WorldFormat", "BatterZoneProfiles", "BatterZoneProfileEntry", "BatterZoneTendency", "WorldV2Dto"];

    // ---- public surface: reflection ----

    private static IEnumerable<Type> TypesIn(Type type)
    {
        if (type.IsByRef || type.IsArray || type.IsPointer)
        {
            foreach (var inner in TypesIn(type.GetElementType()!))
                yield return inner;
            yield break;
        }

        yield return type;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
            foreach (var inner in TypesIn(argument))
                yield return inner;
    }

    /// <summary>Every type reachable through the public (and protected) signatures of the assembly's exported types.</summary>
    private static HashSet<Type> PublicSurface(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var found = new HashSet<Type>();
        foreach (var exported in assembly.GetExportedTypes())
        {
            foreach (var t in TypesIn(exported)) found.Add(t);
            if (exported.BaseType is { } baseType) foreach (var t in TypesIn(baseType)) found.Add(t);
            foreach (var @interface in exported.GetInterfaces()) foreach (var t in TypesIn(@interface)) found.Add(t);
            foreach (var constructor in exported.GetConstructors(all).Where(c => c.IsPublic || c.IsFamily))
                foreach (var parameter in constructor.GetParameters()) foreach (var t in TypesIn(parameter.ParameterType)) found.Add(t);
            foreach (var method in exported.GetMethods(all).Where(m => m.IsPublic || m.IsFamily))
            {
                foreach (var t in TypesIn(method.ReturnType)) found.Add(t);
                foreach (var parameter in method.GetParameters()) foreach (var t in TypesIn(parameter.ParameterType)) found.Add(t);
            }

            foreach (var property in exported.GetProperties(all).Where(p => (p.GetMethod?.IsPublic ?? false) || (p.SetMethod?.IsPublic ?? false)))
                foreach (var t in TypesIn(property.PropertyType)) found.Add(t);
            foreach (var field in exported.GetFields(all).Where(f => f.IsPublic || f.IsFamily))
                foreach (var t in TypesIn(field.FieldType)) found.Add(t);
        }

        return found;
    }

    [Fact]
    public void Application_public_api_exposes_no_domain_type_raw_world_or_hidden_profile()
    {
        var surface = PublicSurface(ApplicationAssembly);

        Assert.NotEmpty(surface);
        Assert.DoesNotContain(surface, t => t.Assembly == DomainAssembly);
        Assert.DoesNotContain(surface, t => DangerousNames.Contains(t.Name));
        Assert.DoesNotContain(surface, t => t.Namespace?.StartsWith("StarPlayer.Domain", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Infrastructure_public_api_exposes_only_the_composition_entry_points_and_nothing_raw()
    {
        var exported = InfrastructureAssembly.GetExportedTypes().Select(t => t.Name).Order().ToArray();

        Assert.Equal(["GameDiagnosticsComposition", "PlayerInfoComposition", "SqliteStorageDiagnostics"], exported);
        var surface = PublicSurface(InfrastructureAssembly);
        Assert.DoesNotContain(surface, t => t.Assembly == DomainAssembly);
        Assert.DoesNotContain(surface, t => DangerousNames.Contains(t.Name));
        // The raw source, JSON reader and DTOs exist but are not visible outside the trusted boundary.
        foreach (var name in new[] { "FixturePlayerWorldSource", "WorldJson", "WorldDto", "DevelopmentDto", "ScriptedGameSource", "ScriptedGameJson", "GameRulesJson", "ScriptedGameDto", "EventDto" })
        {
            var type = InfrastructureAssembly.GetTypes().Single(t => t.Name == name);
            Assert.False(type.IsVisible, $"{name} must not be public");
        }
    }

    [Fact]
    public void Application_ports_expose_only_the_storage_probe_publicly_and_the_world_port_is_internal()
    {
        var publicPorts = ApplicationAssembly.GetExportedTypes().Where(t => t.Namespace == "StarPlayer.Application.Ports").Select(t => t.Name).ToArray();

        Assert.Equal(["IStorageDiagnostics"], publicPorts);
        Assert.False(typeof(IPlayerWorldSource).IsVisible);
        Assert.False(typeof(PlayerWorld).IsVisible);
        Assert.False(typeof(ViewerContext).IsVisible);
        Assert.False(typeof(IScriptedGameSource).IsVisible);
        Assert.False(typeof(ScriptedGame).IsVisible);
        Assert.False(typeof(ExpectedGame).IsVisible);
    }

    [Fact]
    public void InternalsVisibleTo_lists_pin_the_trusted_boundary_and_never_include_the_client()
    {
        static string[] Friends(Assembly assembly) =>
            assembly.GetCustomAttributes<InternalsVisibleToAttribute>().Select(a => a.AssemblyName).Order().ToArray();

        Assert.Equal(["StarPlayer.Application.Tests", "StarPlayer.Infrastructure", "StarPlayer.Infrastructure.Tests"], Friends(ApplicationAssembly));
        Assert.Equal(["StarPlayer.Application.Tests", "StarPlayer.Infrastructure.Tests"], Friends(InfrastructureAssembly));
        Assert.Equal(["StarPlayer.Domain.Tests"], Friends(DomainAssembly));
        Assert.DoesNotContain(new[] { Friends(ApplicationAssembly), Friends(InfrastructureAssembly), Friends(DomainAssembly) }.SelectMany(x => x), n => n.Contains("Client", StringComparison.Ordinal));
    }

    // ---- constructors, factories and the identity path ----

    [Fact]
    public void Only_the_composition_entry_point_can_produce_a_query_service_and_it_takes_no_identity()
    {
        Assert.Empty(typeof(PlayerInfoService).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        var factories = new[] { ApplicationAssembly, InfrastructureAssembly }
            .SelectMany(a => a.GetExportedTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.ReturnType == typeof(PlayerInfoService))
            .ToList();

        var factory = Assert.Single(factories);
        Assert.Equal("PlayerInfoComposition.CreateFictionalPhase1", $"{factory.DeclaringType!.Name}.{factory.Name}");
        var parameter = Assert.Single(factory.GetParameters());
        Assert.Equal(typeof(PlayerSessionOptions), parameter.ParameterType);
        Assert.Equal(["CheatMode"], typeof(PlayerSessionOptions).GetProperties().Where(p => p.Name != "EqualityContract").Select(p => p.Name));
    }

    [Fact]
    public void No_public_member_anywhere_accepts_a_player_id_as_the_session_identity()
    {
        var offenders = new[] { ApplicationAssembly, InfrastructureAssembly }
            .SelectMany(a => a.GetExportedTypes())
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .OfType<MethodBase>()
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(StarPlayer.Domain.Common.PlayerId) || p.Name?.Contains("controlled", StringComparison.OrdinalIgnoreCase) == true))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Public_query_service_methods_speak_only_primitives_and_public_dtos()
    {
        var methods = typeof(PlayerInfoService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotEmpty(methods);
        var dtoNamespace = typeof(PlayerPublicView).Namespace;
        foreach (var method in methods.Where(m => !m.IsSpecialName))
        foreach (var type in method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType).SelectMany(TypesIn))
            Assert.True(type.IsPrimitive || type == typeof(string) || type == typeof(void) || type.Namespace == dtoNamespace || type.Namespace == "System.Collections.Generic",
                $"{method.Name} exposes {type.FullName}");
    }

    [Fact]
    public void The_game_diagnostics_service_has_no_public_constructor_and_only_one_public_way_to_get_it()
    {
        Assert.Empty(typeof(GameDiagnosticsService).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        var factories = new[] { ApplicationAssembly, InfrastructureAssembly }
            .SelectMany(a => a.GetExportedTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.ReturnType == typeof(GameDiagnosticsService))
            .ToList();

        var factory = Assert.Single(factories);
        Assert.Equal("GameDiagnosticsComposition.CreateFictionalPhase2", $"{factory.DeclaringType!.Name}.{factory.Name}");
        Assert.Empty(factory.GetParameters());
    }

    [Fact]
    public void The_game_diagnostics_service_speaks_only_primitives_and_public_projection_dtos_and_runs_no_command_but_next_outcome()
    {
        var methods = typeof(GameDiagnosticsService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).ToList();

        Assert.Equal(["Current", "ListGames", "Next", "RunToEnd", "Select"], methods.Select(m => m.Name).Order().ToArray());
        var dtoNamespace = typeof(GameView).Namespace;
        foreach (var method in methods)
        foreach (var type in method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType).SelectMany(TypesIn))
            Assert.True(type.IsPrimitive || type == typeof(string) || type == typeof(void) || type.Namespace == dtoNamespace || type.Namespace == "System.Collections.Generic",
                $"{method.Name} exposes {type.FullName}");
    }

    // ---- the compiled Godot client ----

    private static string ClientDll()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        return Path.Combine(ArchitectureTests.RepositoryRoot, "game", ".godot", "mono", "temp", "bin", configuration, "StarPlayer.Client.dll");
    }

    [Fact]
    public void Compiled_client_touches_infrastructure_only_from_the_composition_root_and_names_only_the_two_entry_points()
    {
        var client = ClientDll();
        Assert.True(File.Exists(client), $"Compiled client not found at {client}");

        var users = IlScan.TypesReferencing(client, "StarPlayer.Infrastructure");
        var named = IlScan.NamedTypesFrom(client, "StarPlayer.Infrastructure");

        Assert.Equal(["StarPlayer.Client.Bootstrap.CompositionRoot"], users.Order().ToArray());
        Assert.Subset(new HashSet<string>
        {
            "StarPlayer.Infrastructure.Fixtures.PlayerInfoComposition", "StarPlayer.Infrastructure.Fixtures.GameDiagnosticsComposition",
            "StarPlayer.Infrastructure.Sqlite.SqliteStorageDiagnostics",
        }, named);
        Assert.Empty(IlScan.TypesReferencing(client, "StarPlayer.Domain"));
        Assert.Empty(IlScan.TypesReferencing(client, "StarPlayer.Simulation"));
    }

    [Fact]
    public void Client_sources_never_name_raw_world_hidden_profile_or_authority_types()
    {
        foreach (var file in ArchitectureTests.SourceFilesUnder(Path.Combine(ArchitectureTests.RepositoryRoot, "game")))
        {
            var found = SourceScan.Find(File.ReadAllText(file), SourceScan.RawWorldRules);
            Assert.True(found.Count == 0, $"{Path.GetRelativePath(ArchitectureTests.RepositoryRoot, file)} names {string.Join(", ", found)}");
        }
    }

    [Fact]
    public void Only_the_composition_root_source_names_infrastructure_in_the_client()
    {
        var offenders = ArchitectureTests.SourceFilesUnder(Path.Combine(ArchitectureTests.RepositoryRoot, "game"))
            .Where(file => SourceScan.Find(File.ReadAllText(file), SourceScan.PresenterRules).Contains("StarPlayer.Infrastructure"))
            .Select(file => Path.GetRelativePath(ArchitectureTests.RepositoryRoot, file).Replace('\\', '/'))
            .ToList();

        Assert.Equal(["game/Bootstrap/CompositionRoot.cs"], offenders);
    }

    // ---- the detectors must detect ----

    private static class InfrastructureToucher
    {
        public static object Touch() => new SqliteStorageDiagnostics();
    }

    private static class InfrastructureFreeType
    {
        public static int Add(int a, int b) => a + b;
    }

    [Fact]
    public void IL_scanner_detects_a_real_infrastructure_call_including_in_nested_types_and_ignores_clean_types()
    {
        var self = typeof(TrustedBoundaryTests).Assembly.Location;

        var users = IlScan.TypesReferencing(self, "StarPlayer.Infrastructure");

        // Nested types count for their top-level owner, so the probe shows up as this test class.
        Assert.Contains(typeof(TrustedBoundaryTests).FullName!, users);
        Assert.DoesNotContain("StarPlayer.Application.Tests.SourceScan", users);
        Assert.Contains("StarPlayer.Infrastructure.Sqlite.SqliteStorageDiagnostics", IlScan.NamedTypesFrom(self, "StarPlayer.Infrastructure"));
        Assert.Equal(5, InfrastructureFreeType.Add(2, 3));
        Assert.NotNull(InfrastructureToucher.Touch());
    }

    [Fact]
    public void Raw_world_source_rules_flag_the_dangerous_names_and_ignore_comments_and_text()
    {
        Assert.Equal(["PlayerWorld"], SourceScan.Find("var w = new PlayerWorld();", SourceScan.RawWorldRules));
        Assert.Equal(["FixturePlayerWorldSource"], SourceScan.Find("using var s = new FixturePlayerWorldSource().Load();", SourceScan.RawWorldRules));
        Assert.Equal(["DevelopmentProfile"], SourceScan.Find("DevelopmentProfile p = null;", SourceScan.RawWorldRules));
        Assert.Contains("WorldJson", SourceScan.Find("WorldJson.Read(text);", SourceScan.RawWorldRules));
        Assert.Contains("ViewerContext", SourceScan.Find("new ViewerContext(id, true)", SourceScan.RawWorldRules));
        Assert.Empty(SourceScan.Find("// PlayerWorld is internal\nvar text = \"DevelopmentProfile\";", SourceScan.RawWorldRules));
        Assert.Empty(SourceScan.Find("var service = PlayerInfoComposition.CreateFictionalPhase1();", SourceScan.RawWorldRules));
    }

    [Fact]
    public void Raw_world_source_rules_also_flag_the_hidden_batter_tendency_types_and_their_one_preparation_door()
    {
        Assert.Equal(["BatterZoneProfiles"], SourceScan.Find("BatterZoneProfiles profiles = null;", SourceScan.RawWorldRules));
        Assert.Equal(["BatterZoneProfileEntry"], SourceScan.Find("var e = new BatterZoneProfileEntry(id, t);", SourceScan.RawWorldRules));
        Assert.Equal(["BatterZoneTendency"], SourceScan.Find("BatterZoneTendency t = BatterZoneTendency.Neutral;", SourceScan.RawWorldRules));
        Assert.Equal(["ZoneReferenceDistribution"], SourceScan.Find("var r = ZoneReferenceDistribution.UniformV1;", SourceScan.RawWorldRules));
        Assert.Equal(["WorldFormat"], SourceScan.Find("WorldFormat f = null;", SourceScan.RawWorldRules));
        Assert.Equal(["PrepareFromProfiles"], SourceScan.Find("PitchPreparation.PrepareFromProfiles(a, b);", SourceScan.RawWorldRules));
        // comments and string text are not code, and a longer identifier is not the type
        Assert.Empty(SourceScan.Find("// BatterZoneProfiles stays in the world\nvar text = \"BatterZoneTendency\";", SourceScan.RawWorldRules));
        Assert.Empty(SourceScan.Find("var x = MyBatterZoneProfilesView.Show();", SourceScan.RawWorldRules));
    }

    [Fact]
    public void The_v2_world_dto_is_internal_like_every_other_raw_dto()
    {
        Assert.False(InfrastructureAssembly.GetTypes().Single(t => t.Name == "WorldV2Dto").IsVisible);
        Assert.False(InfrastructureAssembly.GetTypes().Single(t => t.Name == "BatterZoneTendencyDto").IsVisible);
        Assert.False(typeof(WorldFormat).IsVisible);
    }

    [Fact]
    public void Surface_walker_would_catch_a_leaking_signature()
    {
        // Mutation check of the reflection walker itself: a type exposing a Domain value through a generic list is found.
        var leaky = typeof(LeakyForDetector);
        var types = TypesIn(leaky.GetMethod(nameof(LeakyForDetector.Get))!.ReturnType).ToList();

        Assert.Contains(types, t => t.Assembly == DomainAssembly);
    }

    private sealed class LeakyForDetector
    {
        public IReadOnlyList<StarPlayer.Domain.Players.DevelopmentProfile> Get() => [];
    }

    // ---- public query path cannot reach raw data (behavioural) ----

    [Fact]
    public void Public_composition_returns_views_only_and_never_a_raw_object_graph()
    {
        var service = PlayerInfoComposition.CreateFictionalPhase1(new PlayerSessionOptions(CheatMode: true));
        var everything = new List<object> { service.GetSummary()!, service.ListPlayers() };
        everything.AddRange(service.ListPlayers().Select(p => (object)service.GetPlayer(new PlayerQuery(p.PlayerId)).View!));

        var graph = new HashSet<Type>();
        void Walk(object? value, int depth)
        {
            if (value is null || depth > 6) return;
            var type = value.GetType();
            graph.Add(type);
            if (value is string || type.IsPrimitive) return;
            if (value is System.Collections.IEnumerable items) { foreach (var item in items) Walk(item, depth + 1); return; }
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
                Walk(property.GetValue(value), depth + 1);
        }

        foreach (var item in everything) Walk(item, 0);

        Assert.DoesNotContain(graph, t => t.Assembly == DomainAssembly);
        Assert.DoesNotContain(graph, t => DangerousNames.Contains(t.Name));
        Assert.Contains(typeof(PlayerPublicView), graph);
        // The diagnostics DTOs are the only other public Application data a client sees.
        Assert.DoesNotContain(graph, t => t == typeof(DiagnosticReport));
    }
}
