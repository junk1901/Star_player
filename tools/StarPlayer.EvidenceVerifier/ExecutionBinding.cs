using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using StarPlayer.SimHarness.Evidence;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.EvidenceVerifier;

/// <summary>
/// G4-T3/T4/T5/T8: what the official verifier process binds before it reads any evidence, and keeps bound until it exits.
/// <list type="bullet">
/// <item>T8 start environment: no variable whose name starts with an OWNER-listed prefix (in addition to E2-1, which is unchanged).</item>
/// <item>Host: the process image is the pinned <c>dotnet.exe</c> (mode A, an apphost is refused) and every execution pin (hostfxr, hostpolicy, runtime, SDK VSTest, …) and the git executable have their pinned bytes.</item>
/// <item>T3: the running verifier DLL is the Build Provenance VERIFIER_BINARY at its relative path and exactly one approved <c>verifiers[]</c> entry; the receipt names that entry.</item>
/// <item>T4: V-1 the verifier folder's SimHarness copy has the SIMHARNESS_BINARY bytes and is the SimHarness assembly actually in use; V-2 every loaded assembly and module is a pinned file (path and held
/// bytes), StarPlayer assemblies only from the verifier folder, one assembly per name, all in the default load context, no assembly loaded from bytes. No path, Windows directory or runtime root is
/// trusted by location (G4-01): an exception set for OS / shared-runtime modules is an OWNER decision that does not exist yet, so without pins the binding refuses (Fail-Closed).</item>
/// <item>T5: every pinned file is opened once without write or delete sharing, hashed from that handle and held, so it cannot be replaced while the process runs.</item>
/// </list>
/// An in-process check cannot recover a process into which hostile code was loaded before it ran (F-20): the G5 ACLs remain the prevention, this is the binding and the detection.
/// </summary>
public sealed class ExecutionBinding : IDisposable
{
    private readonly List<FileStream> _held = [];
    private readonly Dictionary<string, string> _pinned = new(StringComparer.OrdinalIgnoreCase);    // full path → SHA-256 of the held bytes
    private string? _violation;
    private bool _bound;

    private ExecutionBinding(InstallConfig config) => Config = config;

    public InstallConfig Config { get; }

    /// <summary>The approval record's <c>verifiers[]</c> entry of the running verifier binary (after <see cref="BindBuild"/>).</summary>
    internal JObject? VerifierEntry { get; private set; }

    internal string VerifierDirectory { get; private set; } = "";

    private static EvidenceException Fail(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, cause, "", "execution_binding", message);

    /// <summary>The OWNER-listed prefixes present in <paramref name="variables"/> (case-insensitive), ordinal order.</summary>
    public static IReadOnlyList<string> StartupViolations(IDictionary variables, IReadOnlyList<string> prefixes) =>
        variables.Keys.Cast<string>().Where(k => prefixes.Any(p => k.StartsWith(p, StringComparison.OrdinalIgnoreCase))).Order(StringComparer.Ordinal).ToList();

    /// <summary>Opens <paramref name="path"/> once (read, share read only: no writer, no delete, no rename), hashes those bytes and keeps the handle. A reparse point is refused.</summary>
    internal static (FileStream Handle, string Sha, long Length) Hold(string path)
    {
        var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw Fail("Execution.reparse_point", $"'{path}'는 Reparse Point입니다.");
            var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s)).ToLowerInvariant();
            return (s, sha, s.Length);
        }
        catch
        {
            s.Dispose();
            throw;
        }
    }

    private void Pin(string path, string expectedSha, string what)
    {
        var (h, sha, _) = Hold(path);
        _held.Add(h);
        if (sha != expectedSha)
            throw Fail("Execution.pin_mismatch", $"{what} '{path}'의 SHA-256이 승인 Pin과 다릅니다.");
        _pinned[path] = sha;
    }

    /// <summary>
    /// Before the anchor is read: T8 start environment, the pinned host image and every execution pin. <paramref name="processPath"/> is a seam for tests (production: <see cref="Environment.ProcessPath"/>).
    /// </summary>
    public static ExecutionBinding Establish(InstallConfig config, IDictionary? variables = null, string? processPath = null)
    {
        if (StartupViolations(variables ?? Environment.GetEnvironmentVariables(), config.StartupForbiddenPrefixes) is { Count: > 0 } bad)
            throw EvidenceCodes.Fail(EvidenceCodes.EnvironmentMismatch, "Environment.startup_policy", "", "environment", $"승인 시작 환경에 없는 변수가 있습니다: {string.Join(", ", bad)} (G4-T8).");
        var host = processPath ?? Environment.ProcessPath ?? "";
        if (!string.Equals(host, config.Dotnet.Path, StringComparison.OrdinalIgnoreCase))
            throw Fail("Execution.host_unpinned", $"프로세스 이미지 '{host}'가 승인 dotnet '{config.Dotnet.Path}'가 아닙니다(Apphost·다른 dotnet 거부).");
        var b = new ExecutionBinding(config);
        try
        {
            b.Pin(config.Dotnet.Path, config.Dotnet.Sha256, "dotnet");
            b.Pin(config.Git.Path, config.Git.Sha256, "git");
            foreach (var p in config.ExecutionPins)
                b.Pin(p.Path, p.Sha256, "실행 Pin");
            return b;
        }
        catch
        {
            b.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Holds every file under <paramref name="directory"/>: each must be a Build Provenance artifact of one of <paramref name="roles"/> at its relative path with its bytes, or an OWNER-listed unpinned output
    /// file (A9 coverage maps, PDBs, …). Anything else present is refused (an unpinned adapter or host cannot run unseen), and every artifact of those roles the provenance places under the folder must be
    /// present (a declared dependency missing from disk is refused, Codex NOTE). Returns the held handles with their full path and SHA.
    /// </summary>
    internal static IReadOnlyList<(string Path, string Sha, FileStream Handle)> HoldTree(string directory, JObject build, string buildRoot, IReadOnlyList<string> unpinned, params string[] roles)
    {
        var artifacts = Items(build["artifacts"]).Select(Obj).Where(x => roles.Contains(Text(x["role"]))).ToDictionary(x => Text(x["relative_path"]), StringComparer.Ordinal);
        var root = buildRoot.TrimEnd('\\') + "\\";
        var held = new List<(string, string, FileStream)>();
        try
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw Fail("Execution.reparse_point", $"'{directory}'는 Reparse Point입니다.");
            foreach (var d in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories))
                if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0)
                    throw Fail("Execution.reparse_point", $"'{d}'는 Reparse Point입니다.");
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw Fail("Execution.outside_build_root", $"'{file}'가 build_root 밖에 있습니다.");
                var rel = file[root.Length..].Replace('\\', '/');
                if (unpinned.Contains(rel, StringComparer.Ordinal))
                    continue;
                if (!artifacts.TryGetValue(rel, out var a))
                    throw Fail("Execution.unpinned_file", $"'{rel}'가 Build Provenance({string.Join("|", roles)})에 없습니다.");
                var (h, sha, length) = Hold(file);
                held.Add((file, sha, h));
                if (sha != Text(a["raw_sha256"]) || length != Long(a["byte_length"]))
                    throw Fail("Execution.artifact_changed", $"'{rel}'의 바이트가 Build Provenance와 다릅니다.");
            }

            var prefix = Path.GetRelativePath(buildRoot, directory).Replace('\\', '/') + "/";
            var seen = held.Select(h => h.Item1[root.Length..].Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
            if (artifacts.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).FirstOrDefault(k => !seen.Contains(k)) is { } missing)
                throw Fail("Execution.artifact_missing", $"Build Provenance의 '{missing}'가 디스크에 없습니다.");
            return held;
        }
        catch
        {
            foreach (var (_, _, h) in held)
                h.Dispose();
            throw;
        }
    }

    /// <summary>
    /// After the trust bootstrap (E2-3), before E2-4: T3 (the running verifier binary), T4 V-1 (the SimHarness copy), the verifier folder held whole (T5) and V-2 (every loaded file pinned); later loads are
    /// watched and a violation poisons the binding (<see cref="Inspect"/> is asked again before a receipt is stored).
    /// </summary>
    /// <param name="simHarnessInUse">Tests only: the SimHarness assembly this process uses (production: the one that defines the evidence code).</param>
    internal void BindBuild(JObject build, IReadOnlyList<JNode> verifiers, string buildRoot, Assembly? simHarnessInUse = null)
    {
        if (_bound)
            throw Fail("Execution.rebind", "실행 결속은 한 번만 합니다.");
        _bound = true;
        if (!string.Equals(buildRoot, Config.BuildRoot, StringComparison.OrdinalIgnoreCase))
            throw Fail("Execution.build_root", $"검증 build root '{buildRoot}'가 설치 설정 build_root가 아닙니다.");
        JObject Single(string role) => Items(build["artifacts"]).Select(Obj).Where(x => Text(x["role"]) == role).ToList() is [var one]
            ? one : throw Fail("Execution.role_count", $"Build Provenance에 {role}가 정확히 하나가 아닙니다.");

        var location = typeof(ExecutionBinding).Assembly.Location;
        var verifier = Single("VERIFIER_BINARY");
        var expected = Path.Combine([buildRoot, .. Text(verifier["relative_path"]).Split('/')]);
        if (location.Length == 0 || !string.Equals(Path.GetFullPath(location), expected, StringComparison.OrdinalIgnoreCase))
            throw Fail("Execution.verifier_location", $"실행 중인 검증기 '{location}'가 VERIFIER_BINARY '{expected}'가 아닙니다.");
        VerifierDirectory = Path.GetDirectoryName(expected)!;

        var simHarness = Single("SIMHARNESS_BINARY");
        var copyRel = Path.GetRelativePath(buildRoot, Path.Combine(VerifierDirectory, "StarPlayer.SimHarness.dll")).Replace('\\', '/');
        if (Items(build["artifacts"]).Select(Obj).Where(x => Text(x["role"]) == "DEPENDENCY" && Text(x["relative_path"]) == copyRel).ToList() is not [var copy]
            || Text(copy["raw_sha256"]) != Text(simHarness["raw_sha256"]) || Long(copy["byte_length"]) != Long(simHarness["byte_length"]))
            throw Fail("Execution.simharness_copy", $"검증기 폴더의 SimHarness 사본 '{copyRel}'가 SIMHARNESS_BINARY와 같은 바이트의 DEPENDENCY가 아닙니다(V-1).");
        var copyPath = Path.Combine([buildRoot, .. copyRel.Split('/')]);
        var used = simHarnessInUse ?? typeof(Archive).Assembly;
        if (used.IsDynamic || used.Location.Length == 0 || !string.Equals(Path.GetFullPath(used.Location), copyPath, StringComparison.OrdinalIgnoreCase)
            || AssemblyLoadContext.GetLoadContext(used) != AssemblyLoadContext.Default)
            throw Fail("Execution.simharness_load", $"실제 사용 중인 SimHarness '{used.Location}'가 검증기 폴더 사본 '{copyPath}'(기본 로드 컨텍스트)가 아닙니다.");

        foreach (var (path, sha, handle) in HoldTree(VerifierDirectory, build, buildRoot, Config.UnpinnedOutputFiles, "VERIFIER_BINARY", "DEPENDENCY", "RUNTIME_CONFIG"))
        {
            _held.Add(handle);
            _pinned[path] = sha;
        }

        var running = _pinned[expected];
        if (verifiers.Select(Obj).Where(v => Text(v["binary_sha256"]) == running).ToList() is not [var entry])
            throw Fail("Execution.verifier_entry", "실행 중인 검증기의 SHA-256과 같은 승인 기록 verifiers[] 항목이 정확히 하나가 아닙니다(F-07).");
        VerifierEntry = entry;

        AppDomain.CurrentDomain.AssemblyLoad += OnLoad;
        if (Inspect() is { } bad)
            throw Fail("Execution.unpinned_load", bad);
    }

    private void OnLoad(object? sender, AssemblyLoadEventArgs e) => _violation ??= Managed(e.LoadedAssembly);

    /// <summary>
    /// One loaded assembly. A dynamic (Reflection.Emit) assembly is allowed only in the default context and only under a name the OWNER listed (<c>allowed_dynamic_assemblies</c>, empty by default): it can
    /// be created only by code already running, so it is no route for unpinned code from disk, but it is not trusted by default either. Everything else needs a file location that is a pinned path; a
    /// StarPlayer assembly must come from the verifier folder; another load context is refused.
    /// </summary>
    private string? Managed(Assembly a)
    {
        var name = a.GetName().Name ?? "";
        if (AssemblyLoadContext.GetLoadContext(a) is { } alc && alc != AssemblyLoadContext.Default)
            return $"어셈블리 '{a.FullName}'가 기본이 아닌 로드 컨텍스트 '{alc.Name}'에 있습니다.";
        if (a.IsDynamic)
            return Config.AllowedDynamicAssemblies.Contains(name, StringComparer.Ordinal) ? null : $"승인 목록에 없는 동적 어셈블리 '{name}'가 있습니다(OWNER 미결정 예외).";
        if (a.Location.Length == 0)
            return $"파일 위치가 없는 어셈블리 '{a.FullName}'가 로드되었습니다(바이트 로드 거부).";
        var path = Path.GetFullPath(a.Location);
        if (name.StartsWith("StarPlayer.", StringComparison.Ordinal) && VerifierDirectory.Length > 0
            && !path.StartsWith(VerifierDirectory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            return $"StarPlayer 어셈블리 '{path}'가 검증기 폴더 밖에서 로드되었습니다.";
        return Loaded(path, "어셈블리");
    }

    private string? Loaded(string path, string what) =>
        _pinned.ContainsKey(path) ? File.Exists(path) ? null : $"Pin된 {what} '{path}'가 사라졌습니다." : $"Pin되지 않은 {what} '{path}'가 로드되었습니다.";

    /// <summary>
    /// V-2: the first violation among the loaded assemblies (one per name across all load contexts, then each as <see cref="Managed"/>) and the process modules (each a pinned path), or a watched later
    /// load, or null. A module loaded and unloaded between two checks is not seen (sampling, not a lock; the prevention is the G5 ACL).
    /// </summary>
    public string? Inspect()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        if (assemblies.Where(a => !a.IsDynamic).GroupBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } twice)
            return $"이름이 같은 어셈블리 '{twice.Key}'가 {twice.Count()}번 로드되었습니다.";
        if (_violation is { } v)
            return v;
        foreach (var a in assemblies)
            if (Managed(a) is { } bad)
                return bad;
        using var self = Process.GetCurrentProcess();
        foreach (ProcessModule m in self.Modules)
            if (Loaded(Path.GetFullPath(m.FileName), "모듈") is { } bad)
                return bad;
        return null;
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.AssemblyLoad -= OnLoad;
        foreach (var h in _held)
            h.Dispose();
        _held.Clear();
    }
}
