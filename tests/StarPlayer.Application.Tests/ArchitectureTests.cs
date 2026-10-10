using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Xml.Linq;

namespace StarPlayer.Application.Tests;

/// <summary>
/// Enforces the ROADMAP §1.2 dependency table against the real project files, compiled assemblies
/// (including the Godot client) and client sources. Needs no Godot installation.
/// </summary>
public class ArchitectureTests
{
    internal static readonly string RepositoryRoot = FindRepositoryRoot();
    private static string Root => RepositoryRoot;

    // project -> project references it may have (ROADMAP §1.2). The client must not reach Domain or
    // Simulation directly: it talks to Application contracts and Infrastructure only inside Bootstrap.
    private static readonly Dictionary<string, string[]> AllowedProjectReferences = new()
    {
        ["src/StarPlayer.Domain/StarPlayer.Domain.csproj"] = [],
        ["src/StarPlayer.Simulation/StarPlayer.Simulation.csproj"] = ["StarPlayer.Domain"],
        ["src/StarPlayer.Application/StarPlayer.Application.csproj"] = ["StarPlayer.Domain", "StarPlayer.Simulation"],
        ["src/StarPlayer.Infrastructure/StarPlayer.Infrastructure.csproj"] = ["StarPlayer.Application", "StarPlayer.Domain"],
        ["game/StarPlayer.Client.csproj"] = ["StarPlayer.Application", "StarPlayer.Infrastructure"],
    };

    // project -> NuGet packages it may have. Anything absent from this table may have none.
    private static readonly Dictionary<string, string[]> AllowedPackages = new()
    {
        ["src/StarPlayer.Infrastructure/StarPlayer.Infrastructure.csproj"] = ["Microsoft.Data.Sqlite"],
    };

    private static readonly string[] ForbiddenInCoreAssemblies =
        ["Godot", "Microsoft.Data", "SQLitePCL", "System.Data", "System.Net", "StarPlayer.Infrastructure", "StarPlayer.Client"];

    // Assemblies the Godot client must never reference, even transitively-visible ones: using a Domain or
    // Simulation type (even via `var`, without naming its namespace) makes the compiler record the reference.
    private static readonly string[] ForbiddenInClientAssembly = ["StarPlayer.Domain", "StarPlayer.Simulation"];

    [Fact]
    public void Project_references_follow_the_layer_table()
    {
        foreach (var (project, allowed) in AllowedProjectReferences)
        {
            var actual = ProjectReferenceNames(project);
            Assert.True(actual.All(allowed.Contains), $"{project} references {string.Join(", ", actual.Except(allowed))}, which the layer table forbids.");
        }
    }

    [Fact]
    public void Client_project_has_no_direct_Domain_or_Simulation_reference()
    {
        var references = ProjectReferenceNames("game/StarPlayer.Client.csproj");

        Assert.DoesNotContain("StarPlayer.Domain", references);
        Assert.DoesNotContain("StarPlayer.Simulation", references);
        Assert.Contains("StarPlayer.Application", references);
    }

    [Fact]
    public void Core_projects_use_the_plain_SDK_and_only_whitelisted_packages()
    {
        foreach (var project in AllowedProjectReferences.Keys)
        {
            var document = XDocument.Load(Path.Combine(Root, project));
            var sdk = document.Root!.Attribute("Sdk")!.Value;
            var packages = document.Descendants("PackageReference").Select(e => e.Attribute("Include")!.Value).ToArray();
            var allowed = AllowedPackages.GetValueOrDefault(project, []);

            Assert.True(packages.All(allowed.Contains), $"{project} has unexpected packages: {string.Join(", ", packages.Except(allowed))}");
            if (project.StartsWith("src/", StringComparison.Ordinal))
                Assert.Equal("Microsoft.NET.Sdk", sdk);
        }
    }

    [Fact]
    public void Test_projects_only_build_the_client_never_reference_its_assembly()
    {
        // Application.Tests builds the client (ReferenceOutputAssembly=false) so the compiled-reference
        // check below always inspects a fresh assembly. No test project may compile against the client.
        foreach (var project in Directory.GetFiles(Path.Combine(Root, "tests"), "*.csproj", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(project);
            Assert.DoesNotContain("Godot", File.ReadAllText(project));
            foreach (var reference in document.Descendants("ProjectReference").Where(e => e.Attribute("Include")!.Value.Contains("StarPlayer.Client")))
            {
                Assert.EndsWith("StarPlayer.Application.Tests.csproj", project);
                Assert.Equal("false", reference.Attribute("ReferenceOutputAssembly")?.Value);
            }
        }
    }

    [Fact]
    public void Client_project_compiles_only_its_own_folder()
    {
        var path = Path.Combine(Root, "game/StarPlayer.Client.csproj");

        Assert.Empty(XDocument.Load(path).Descendants("Compile"));
        Assert.DoesNotContain("tests", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("StarPlayer.Domain")]
    [InlineData("StarPlayer.Simulation")]
    [InlineData("StarPlayer.Application")]
    public void Compiled_core_assemblies_do_not_reference_engine_sql_http_or_infrastructure(string assemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(name => name.Name!).ToArray();

        var forbidden = ForbiddenReferences(references, ForbiddenInCoreAssemblies);
        Assert.True(forbidden.Count == 0, $"{assemblyName} references {string.Join(", ", forbidden)}.");
        if (assemblyName == "StarPlayer.Domain")
            Assert.DoesNotContain("System.IO.FileSystem", references);
    }

    [Fact]
    public void Compiled_client_assembly_references_neither_Domain_nor_Simulation()
    {
        var client = ClientAssemblyPath();
        Assert.True(File.Exists(client), $"Compiled client not found at {client}; Application.Tests should have built it.");

        var references = AssemblyReferenceNames(client);

        Assert.Contains("StarPlayer.Application", references);
        var forbidden = ForbiddenReferences(references, ForbiddenInClientAssembly);
        Assert.True(forbidden.Count == 0, $"StarPlayer.Client.dll references {string.Join(", ", forbidden)}; presenters must use Application contracts only.");
    }

    [Fact]
    public void Client_sources_never_name_Domain_or_Simulation()
    {
        foreach (var file in SourceFilesUnder(Path.Combine(Root, "game")))
        {
            var found = SourceScan.Find(File.ReadAllText(file), SourceScan.PresenterRules).Intersect(["StarPlayer.Domain", "StarPlayer.Simulation"]).ToList();
            Assert.True(found.Count == 0, $"{Path.GetRelativePath(Root, file)} uses {string.Join(", ", found)}");
        }
    }

    [Fact]
    public void Presenters_and_components_reach_the_core_only_through_Application_contracts()
    {
        foreach (var file in SourceFilesUnder(Path.Combine(Root, "game/UI")).Concat(SourceFilesUnder(Path.Combine(Root, "game/Components"))))
        {
            var found = SourceScan.Find(File.ReadAllText(file), SourceScan.PresenterRules);
            Assert.True(found.Count == 0, $"{Path.GetRelativePath(Root, file)} touches {string.Join(", ", found)}");
        }
    }

    [Fact]
    public void Only_Bootstrap_binds_infrastructure_in_the_client()
    {
        var offenders = SourceFilesUnder(Path.Combine(Root, "game"))
            .Where(file => SourceScan.Find(File.ReadAllText(file), SourceScan.PresenterRules).Contains("StarPlayer.Infrastructure"))
            .Select(file => Path.GetRelativePath(Root, file).Replace('\\', '/'))
            .Where(path => !path.StartsWith("game/Bootstrap/", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    // --- The detectors must actually detect (otherwise the tests above prove nothing). ---

    [Fact]
    public void Detectors_flag_forbidden_references_and_presenter_tokens()
    {
        Assert.Equal(["Godot.Core"], ForbiddenReferences(["System.Runtime", "Godot.Core"], ForbiddenInCoreAssemblies));
        Assert.Equal(["StarPlayer.Domain"], ForbiddenReferences(["StarPlayer.Application", "StarPlayer.Domain"], ForbiddenInClientAssembly));

        Assert.Equal(["Microsoft.Data", "SqliteConnection"],
            SourceScan.Find("using Microsoft.Data.Sqlite; var c = new SqliteConnection();", SourceScan.PresenterRules));
        Assert.Equal(["StarPlayer.Domain"], SourceScan.Find("using StarPlayer.Domain.Common;", SourceScan.PresenterRules));
        Assert.Equal(["StarPlayer.Domain"], SourceScan.Find("var id = new StarPlayer . Domain.Common.WorldRevision();", SourceScan.PresenterRules));
        Assert.Empty(SourceScan.Find("using StarPlayer.Application.Diagnostics; // StarPlayer.Domain is forbidden", SourceScan.PresenterRules));
    }

    [Fact]
    public void Assembly_reader_reads_real_metadata()
    {
        var thisAssembly = Assembly.GetExecutingAssembly().Location;

        Assert.Contains("StarPlayer.Application", AssemblyReferenceNames(thisAssembly));
    }

    // --- helpers ---

    private static string ClientAssemblyPath()
    {
        // Output of the same configuration this test run was built with (bin/<Configuration>/net8.0/).
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        return Path.Combine(Root, "game", ".godot", "mono", "temp", "bin", configuration, "StarPlayer.Client.dll");
    }

    private static string[] AssemblyReferenceNames(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        return reader.AssemblyReferences.Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name)).ToArray();
    }

    private static string[] ProjectReferenceNames(string project) =>
        XDocument.Load(Path.Combine(Root, project)).Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToArray();

    private static List<string> ForbiddenReferences(IEnumerable<string> references, string[] forbiddenPrefixes) =>
        references.Where(r => forbiddenPrefixes.Any(p => r.StartsWith(p, StringComparison.Ordinal))).ToList();

    internal static IEnumerable<string> SourceFilesUnder(string folder)
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{separator}obj{separator}")
                            && !file.Contains($"{separator}bin{separator}")
                            && !file.Contains($"{separator}.godot{separator}"))
            : [];
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "StarPlayer.sln")))
                return directory.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }
}
