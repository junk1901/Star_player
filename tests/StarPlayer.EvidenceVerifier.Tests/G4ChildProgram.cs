using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.AccessControl;
using System.Security.Principal;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>The test classes that set the static <c>Certification.VariablesSeam</c> / <c>CaptureSeam</c> run one at a time (a parallel class would reset them mid-test).</summary>
[CollectionDefinition(Name)]
public sealed class CertificationSeams
{
    public const string Name = "Certification static seams";
}

/// <summary>TEST_ONLY values shared by the G4 tests and their isolated child processes (none of them is an OWNER value).</summary>
internal static class G4Fixtures
{
    public static string Sha(string path) => Archive.Sha(File.ReadAllBytes(path));

    public static string BuildRoot { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    public static string VerifierDll => typeof(ExecutionBinding).Assembly.Location;

    public static string Bin => Path.GetDirectoryName(VerifierDll)!;

    /// <summary>A Build Provenance naming every file of this test folder (the running verifier as VERIFIER_BINARY) and the real SimHarness build output.</summary>
    public static JObject Provenance(string? simHarnessSha = null, string? verifierRel = null, Func<string, bool>? skip = null, IEnumerable<JObject>? extra = null)
    {
        var rows = new List<JNode>();
        foreach (var file in Directory.EnumerateFiles(Bin, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(BuildRoot, file).Replace('\\', '/');
            if (skip?.Invoke(rel) == true)
                continue;
            var role = string.Equals(file, VerifierDll, StringComparison.OrdinalIgnoreCase) ? "VERIFIER_BINARY" : file.EndsWith(".json", StringComparison.Ordinal) ? "RUNTIME_CONFIG" : "DEPENDENCY";
            var bytes = File.ReadAllBytes(file);
            rows.Add(J.O(("role", J.S(role)), ("relative_path", J.S(role == "VERIFIER_BINARY" ? verifierRel ?? rel : rel)), ("raw_sha256", J.S(Archive.Sha(bytes))), ("byte_length", J.N(bytes.Length))));
        }

        var sim = File.ReadAllBytes(Path.Combine(BuildRoot, "tools", "StarPlayer.SimHarness", "bin", "Release", "net8.0", "StarPlayer.SimHarness.dll"));
        rows.Add(J.O(("role", J.S("SIMHARNESS_BINARY")), ("relative_path", J.S("tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll")),
            ("raw_sha256", J.S(simHarnessSha ?? Archive.Sha(sim))), ("byte_length", J.N(sim.Length))));
        rows.AddRange(extra ?? []);
        return J.O(("artifacts", J.A(rows)));
    }

    public static IReadOnlyList<JNode> Verifiers(string sha) => [J.O(("id", J.S("verifier-test")), ("version", J.S("1")), ("source_git_sha", J.S("0000000000000000000000000000000000000000")), ("binary_sha256", J.S(sha)))];

    public static InstallConfig Config(string root, IReadOnlyList<ToolPin> pins, IReadOnlyList<string> dynamicNames, IReadOnlyList<string>? prefixes = null, string? dotnetSha = null)
    {
        var host = Environment.ProcessPath!;
        return new InstallConfig(Path.Combine(root, "anchor.json"), BuildRoot, Path.Combine(root, "archive"), Path.Combine(root, "work"), new ToolPin(host, dotnetSha ?? Sha(host)), new ToolPin(host, Sha(host)),
            pins, prefixes ?? ["CORECLR_"], dynamicNames, [], [], "");
    }

    /// <summary>The <c>dotnet exec</c> arguments that run this test assembly's own <see cref="G4ChildProgram.Main"/>.</summary>
    public static IReadOnlyList<string> Exec(params string[] args)
    {
        var dll = typeof(G4Fixtures).Assembly.Location;
        var stem = Path.Combine(Bin, Path.GetFileNameWithoutExtension(dll));
        return ["exec", "--runtimeconfig", stem + ".runtimeconfig.json", "--depsfile", stem + ".deps.json", dll, .. args];
    }

    public static string Dotnet => TestRunner.FromRuntime(BuildRoot).Dotnet;

    /// <summary>Runs one isolated child scenario; its exit code and output.</summary>
    public static (int Exit, string Output) Child(params string[] args)
    {
        var start = new ProcessStartInfo(Dotnet)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = BuildRoot,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var a in Exec(args))
            start.ArgumentList.Add(a);
        using var p = Process.Start(start)!;
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(120_000))
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException("child");
        }

        return (p.ExitCode, output.Result + error.Result);
    }
}

/// <summary>
/// The entry point of this test assembly when it is run as its own process (<c>dotnet exec</c>), never by the test host. It isolates what cannot be undone inside the shared test host (G4-01: loads from
/// bytes, other load contexts, unpinned native modules) and plays a scripted <c>dotnet test</c> (G4-02 / G4-04: exit codes and TRX files). Exit codes: 0 no violation, 3 a violation (printed),
/// 4 the binding refused (cause printed), 2 usage.
/// </summary>
public static class G4ChildProgram
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        return Dispatch(args);
    }

    private static int Dispatch(string[] args) => args switch
    {
        ["g4-binding", var scenario] => Binding(scenario),
        ["g4-fake-dotnet", var scenario, .. var rest] => FakeDotnet(scenario, rest),
        _ => 2,
    };

    private static IReadOnlyList<ToolPin> LoadedPins()
    {
        var bin = G4Fixtures.Bin.TrimEnd('\\') + "\\";
        var host = Environment.ProcessPath!;
        using var self = Process.GetCurrentProcess();
        return AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.Location.Length > 0).Select(a => a.Location)
            .Concat(self.Modules.Cast<ProcessModule>().Select(m => m.FileName))
            .Select(Path.GetFullPath).Where(p => !p.StartsWith(bin, StringComparison.OrdinalIgnoreCase) && !string.Equals(p, host, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).Select(p => new ToolPin(p, G4Fixtures.Sha(p))).ToList();
    }

    private static int Binding(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "simharness-tests", "g4-child-" + Guid.NewGuid().ToString("N"));
        var build = G4Fixtures.Provenance();
        var verifiers = G4Fixtures.Verifiers(G4Fixtures.Sha(G4Fixtures.VerifierDll));
        try
        {
            // stand-in OWNER pins: what this process has loaded. The binding code loads more on first use, so the snapshot is retaken until one binding passes (bounded).
            ExecutionBinding? b = null;
            for (var attempt = 0; b is null; attempt++)
            {
                var dynamicNames = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.IsDynamic).Select(a => a.GetName().Name!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                var candidate = ExecutionBinding.Establish(G4Fixtures.Config(root, LoadedPins(), dynamicNames), new Hashtable());
                try
                {
                    candidate.BindBuild(build, verifiers, G4Fixtures.BuildRoot);
                    b = candidate;
                }
                catch (EvidenceException e) when (e.Error.Cause == "Execution.unpinned_load" && attempt < 8)
                {
                    candidate.Dispose();
                }
            }

            using var bound = b;
            var bin = G4Fixtures.Bin;
            switch (scenario)
            {
                case "baseline":
                    break;
                case "windows-module":
                    NativeLibrary.Load(NotLoaded(Environment.SystemDirectory, "msimg32.dll", "winmm.dll", "dbghelp.dll", "wtsapi32.dll"));
                    break;
                case "runtime-module":
                    NativeLibrary.Load(NotLoaded(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "Microsoft.DiaSymReader.Native.amd64.dll", "mscordbi.dll", "mscordaccore.dll"));
                    break;
                case "byte-load":
                    Assembly.Load(File.ReadAllBytes(Path.Combine(bin, "xunit.assert.dll")));
                    break;
                case "alc":
                    new AssemblyLoadContext("g4-other").LoadFromAssemblyPath(Path.Combine(bin, "xunit.assert.dll"));
                    break;
                case "duplicate":
                    new AssemblyLoadContext("g4-duplicate").LoadFromAssemblyPath(Path.Combine(bin, "StarPlayer.SimHarness.dll"));
                    break;
                default:
                    return 2;
            }

            var violation = b.Inspect();
            Console.WriteLine(violation ?? "inspect=ok");
            return violation is null ? 0 : 3;
        }
        catch (EvidenceException e)
        {
            Console.WriteLine($"{e.Error.Cause}: {e.Error.Message}");
            return 4;
        }
    }

    private static string NotLoaded(string directory, params string[] names)
    {
        using var self = Process.GetCurrentProcess();
        var loaded = self.Modules.Cast<ProcessModule>().Select(m => Path.GetFileName(m.FileName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return names.Select(n => Path.Combine(directory, n)).First(p => File.Exists(p) && !loaded.Contains(Path.GetFileName(p)));
    }

    /// <summary>Plays <c>dotnet test &lt;dll&gt; --filter … --logger trx;LogFileName=… --results-directory …</c> with a scripted exit code and TRX.</summary>
    private static int FakeDotnet(string scenario, string[] rest)
    {
        string Option(string name) => rest[Array.IndexOf(rest, name) + 1];
        var results = Option("--results-directory");
        var trx = Path.Combine(results, Option("--logger").Split("LogFileName=")[1]);
        var methods = Option("--filter").Split('|').Select(f => f["FullyQualifiedName=".Length..]).ToList();
        void Write(string path, string outcome) => File.WriteAllText(path,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>"
            + string.Concat(methods.Select(m => $"<UnitTestResult testName=\"{m}\" outcome=\"{outcome}\" />")) + "</Results></TestRun>");
        switch (scenario)
        {
            case "pass": Write(trx, "Passed"); return 0;
            case "fail": Write(trx, "Failed"); return 1;
            case "exit1-passed": Write(trx, "Passed"); return 1;
            case "exit0-failed": Write(trx, "Failed"); return 0;
            case "exit1-none": return 1;
            case "exit0-none": return 0;
            case "exit3-passed": Write(trx, "Passed"); return 3;
            case "two-trx": Write(trx, "Passed"); Write(Path.Combine(results, "other.trx"), "Passed"); return 0;
            case "sleep": Thread.Sleep(60_000); return 0;
            case "trx-everyone":
                Write(trx, "Passed");
                var info = new FileInfo(trx);
                var acl = info.GetAccessControl();
                acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Write, AccessControlType.Allow));
                info.SetAccessControl(acl);
                return 0;
            default: return 2;
        }
    }
}
