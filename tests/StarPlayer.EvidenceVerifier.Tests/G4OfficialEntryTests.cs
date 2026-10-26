using System.Collections;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// G4 official entry point (B-4 OWNER decision draft §4, T1-T8): the installation settings, the process binding (start environment, host, pins, running verifier, SimHarness copy, loaded files), the held
/// folders, the child runner and results directory, and the CLI order. Each guard has a passing case and a counterexample that must refuse. All values are TEST_ONLY; none is an OWNER value.
/// </summary>
public sealed class G4OfficialEntryTests(TestOnlyWorld w) : IClassFixture<TestOnlyWorld>
{
    private static EvidenceError Refused(Action a) => Assert.Throws<EvidenceException>(a).Error;

    private static string Sha(string path) => Archive.Sha(File.ReadAllBytes(path));

    private static readonly Func<string, string?> AclOk = _ => null;

    private static readonly Func<bool> NotElevated = () => false;

    private static string BuildRoot => TestOnlyWorld.RepositoryRoot;

    private static string VerifierDll => typeof(ExecutionBinding).Assembly.Location;

    private static string DotnetRoot => Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));

    private static string Host => Environment.ProcessPath!;

    private string Temp(string name)
    {
        var d = Path.Combine(w.Root, $"g4-{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(d);
        return d;
    }

    /// <summary>A config for this test process: its host as "dotnet", a scratch file as "git", roots under the world.</summary>
    private InstallConfig Config(IReadOnlyList<string>? prefixes = null, IReadOnlyList<string>? allowed = null, string? dotnetSha = null)
    {
        var git = Path.Combine(Temp("git"), "git.exe");
        File.WriteAllBytes(git, [1, 2, 3]);
        return new InstallConfig(Path.Combine(w.Root, "anchor.json"), BuildRoot, Path.Combine(w.Root, "archive"), Path.Combine(w.Root, "work"), new ToolPin(Host, dotnetSha ?? Sha(Host)),
            new ToolPin(git, Sha(git)), [], prefixes ?? ["CORECLR_"], allowed ?? [DotnetRoot], [], [], "");
    }

    /// <summary>A Build Provenance naming every file of this test folder (the running verifier as VERIFIER_BINARY) and the real SimHarness build output.</summary>
    private static JObject Provenance(string? simHarnessSha = null, string? verifierRel = null, Func<string, bool>? skip = null)
    {
        var rows = new List<JNode>();
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(VerifierDll)!, "*", SearchOption.AllDirectories))
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
        return J.O(("artifacts", J.A(rows)));
    }

    private static IReadOnlyList<JNode> Verifiers(string sha) => [J.O(("id", J.S("verifier-test")), ("version", J.S("1")), ("source_git_sha", J.S(TestOnlyWorld.Head)), ("binary_sha256", J.S(sha)))];

    // ---------------------------------------------------------------- T1 installation settings

    private string WriteConfig(Func<JObject, JObject>? edit = null)
    {
        var dir = Temp("config");
        var o = J.O(("format", J.S(InstallConfigReader.Format)), ("anchor_path", J.S(Path.Combine(dir, "anchor.json"))), ("build_root", J.S(BuildRoot)),
            ("archive_root", J.S(Path.Combine(dir, "archive"))), ("work_root", J.S(Path.Combine(dir, "work"))), ("dotnet", J.O(("path", J.S(Host)), ("raw_sha256", J.S(Sha(Host))))),
            ("git", J.O(("path", J.S(Host)), ("raw_sha256", J.S(Sha(Host))))), ("execution_pins", J.A()), ("startup_forbidden_prefixes", J.A(J.S("CORECLR_"), J.S("VSTEST_"))),
            ("allowed_module_roots", J.A(J.S(DotnetRoot))), ("unpinned_output_files", J.A()));
        var path = Path.Combine(dir, "install-config.json");
        File.WriteAllBytes(path, Jcs.Bytes(edit?.Invoke(o) ?? o));
        return path;
    }

    [Fact]
    public void A_well_formed_installation_config_is_read_once_with_canonical_roots_and_its_bytes_hashed()
    {
        var path = WriteConfig();
        var c = InstallConfigReader.Read(path, AclOk, NotElevated);
        Assert.Equal(BuildRoot, c.BuildRoot);
        Assert.Equal(["CORECLR_", "VSTEST_"], c.StartupForbiddenPrefixes);
        Assert.Equal(Sha(path), c.RawSha256);
    }

    [Fact]
    public void A_missing_elevated_writable_unknown_field_unsorted_or_root_internal_config_is_refused()
    {
        Assert.Equal("Trust.install_config_missing", Refused(() => InstallConfigReader.Read(Path.Combine(w.Root, "none.json"), AclOk, NotElevated)).Cause);
        Assert.Equal("Trust.install_config_untrusted", Refused(() => InstallConfigReader.Read(WriteConfig(), AclOk, () => true)).Cause);
        Assert.Equal("Trust.install_config_untrusted", Refused(() => InstallConfigReader.Read(WriteConfig(), _ => "Users: write", NotElevated)).Cause);
        Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(() => InstallConfigReader.Read(WriteConfig(o => (JObject)JEditMembers.With(o, "extra", J.S("x"))), AclOk, NotElevated)).Code);
        Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(() => InstallConfigReader.Read(WriteConfig(o => (JObject)JEditMembers.With(o, "startup_forbidden_prefixes", J.A(J.S("VSTEST_"), J.S("CORECLR_")))), AclOk, NotElevated)).Code);
        Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(() => InstallConfigReader.Read(WriteConfig(o => (JObject)JEditMembers.With(o, "build_root", J.S("relative\\root"))), AclOk, NotElevated)).Code);
        Assert.Equal("Trust.install_config_untrusted", Refused(() => InstallConfigReader.Read(WriteConfig(o => (JObject)JEditMembers.With(o, "anchor_path", J.S(Path.Combine(BuildRoot, "anchor.json")))), AclOk, NotElevated)).Cause);
    }

    // ---------------------------------------------------------------- T8 start environment, host and pins

    [Fact]
    public void The_binding_holds_the_pinned_host_and_tools_so_they_cannot_be_rewritten_until_it_is_disposed()
    {
        var c = Config();
        using (var b = ExecutionBinding.Establish(c, new Hashtable()))
            Assert.Throws<IOException>(() => File.WriteAllBytes(c.Git.Path, [9]));
        File.WriteAllBytes(c.Git.Path, [9]);                                                              // released after the process binding ends
    }

    [Fact]
    public void A_forbidden_start_variable_another_host_or_a_changed_pin_refuses_the_binding()
    {
        Assert.Equal(("Evidence.environment_mismatch", "Environment.startup_policy"),
            (Refused(() => ExecutionBinding.Establish(Config(), new Hashtable { ["CoreClr_Enable_Profiling"] = "1" })).Code, Refused(() => ExecutionBinding.Establish(Config(), new Hashtable { ["CORECLR_PROFILER_PATH"] = "x" })).Cause));
        Assert.Equal("Execution.host_unpinned", Refused(() => ExecutionBinding.Establish(Config(), new Hashtable(), processPath: Path.Combine(BuildRoot, "tools", "StarPlayer.EvidenceVerifier", "bin", "Release", "net8.0", "StarPlayer.EvidenceVerifier.exe"))).Cause);
        Assert.Equal("Execution.pin_mismatch", Refused(() => ExecutionBinding.Establish(Config(dotnetSha: new string('0', 64)), new Hashtable())).Cause);
    }

    // ---------------------------------------------------------------- T3 / T4 / T5 running verifier, SimHarness copy, loaded files

    [Fact]
    public void The_running_verifier_its_folder_and_every_loaded_file_bind_to_the_provenance_and_the_matching_verifier_entry()
    {
        using var b = ExecutionBinding.Establish(Config(), new Hashtable());
        b.BindBuild(Provenance(), Verifiers(Sha(VerifierDll)), BuildRoot);
        Assert.Equal(Sha(VerifierDll), Schema.Text(b.VerifierEntry!["binary_sha256"]));
        Assert.Null(b.Inspect());
    }

    [Fact]
    public void A_moved_verifier_a_foreign_simharness_copy_no_matching_entry_an_unpinned_folder_file_or_an_unpinned_load_refuses()
    {
        string Bind(JObject build, string? verifierSha = null, InstallConfig? config = null)
        {
            using var b = ExecutionBinding.Establish(config ?? Config(), new Hashtable());
            return Refused(() => b.BindBuild(build, Verifiers(verifierSha ?? Sha(VerifierDll)), BuildRoot)).Cause;
        }

        Assert.Equal("Execution.verifier_location", Bind(Provenance(verifierRel: "tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll")));
        Assert.Equal("Execution.simharness_copy", Bind(Provenance(simHarnessSha: new string('a', 64))));
        Assert.Equal("Execution.verifier_entry", Bind(Provenance(), verifierSha: new string('b', 64)));
        Assert.Equal("Execution.unpinned_file", Bind(Provenance(skip: rel => rel.EndsWith("/testhost.exe", StringComparison.Ordinal))));
        Assert.Equal("Execution.unpinned_load", Bind(Provenance(), config: Config(allowed: [])));     // the shared framework is neither pinned nor an allowed root
    }

    [Fact]
    public void A_held_folder_refuses_an_extra_or_changed_file_accepts_a_listed_unpinned_output_and_blocks_writes_while_held()
    {
        var root = Temp("tree");
        var dir = Path.Combine(root, "out");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "a.dll"), [1]);
        File.WriteAllBytes(Path.Combine(dir, "a.pdb"), [2]);
        var build = J.O(("artifacts", J.A(J.O(("role", J.S("DEPENDENCY")), ("relative_path", J.S("out/a.dll")), ("raw_sha256", J.S(Archive.Sha([1]))), ("byte_length", J.N(1))))));
        Assert.Equal("Execution.unpinned_file", Refused(() => ExecutionBinding.HoldTree(dir, build, root, [], "DEPENDENCY")).Cause);
        var held = ExecutionBinding.HoldTree(dir, build, root, ["out/a.pdb"], "DEPENDENCY");
        Assert.Throws<IOException>(() => File.WriteAllBytes(Path.Combine(dir, "a.dll"), [7]));
        foreach (var h in held)
            h.Handle.Dispose();
        File.WriteAllBytes(Path.Combine(dir, "a.dll"), [7]);
        Assert.Equal("Execution.artifact_changed", Refused(() => ExecutionBinding.HoldTree(dir, build, root, ["out/a.pdb"], "DEPENDENCY")).Cause);
    }

    [Fact]
    public void A_session_opened_with_a_binding_refuses_a_verifier_that_is_not_the_provenance_verifier_binary()
    {
        var trust = new TestOnlyTrust(w);
        var registry = w.Own(trust.Open(RegistryRole.VerifierService, TestOnlyTrust.Verifier));
        var production = w.ProducerFor(trust);
        var config = Config() with { BuildRoot = w.Root, ArchiveRoot = production.ArchiveRoot };
        using var b = ExecutionBinding.Establish(config, new Hashtable());
        Certification.VariablesSeam = () => new Hashtable();
        try
        {
            Assert.Equal("Execution.verifier_location",
                Refused(() => Certification.Open(trust.Anchor, registry, trust.Approval.Id, trust.Approval.RawSha256, "auth-test-1", w.Root, production.ArchiveRoot, b)).Cause);
            Assert.Equal("Execution.archive_root",
                Refused(() => Certification.Open(trust.Anchor, registry, trust.Approval.Id, trust.Approval.RawSha256, "auth-test-1", w.Root, Path.Combine(w.Root, "elsewhere"), b)).Cause);
        }
        finally
        {
            Certification.VariablesSeam = null;
        }
    }

    // ---------------------------------------------------------------- T2 / T7 child runner and results directory

    [Fact]
    public void The_child_runner_is_an_absolute_dotnet_in_the_build_root_and_a_name_or_a_run_without_its_one_trx_is_refused()
    {
        var runner = TestRunner.FromRuntime(BuildRoot);
        Assert.True(Path.IsPathFullyQualified(runner.Dotnet) && File.Exists(runner.Dotnet));
        Assert.Equal("dotnet.exe", Path.GetFileName(runner.Dotnet), ignoreCase: true);
        string[] cases = ["StarPlayer.Domain.Tests|StarPlayer.Domain.Tests.X.Y"];
        Assert.Equal("TestProfile.runner", Refused(() => TestProfiles.Run("StarPlayer.Domain.Tests", "x.dll", cases, Temp("work"), runner with { Dotnet = "dotnet" })).Cause);
        var where = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");     // starts, writes no TRX
        Assert.Equal("TestProfile.trx", Refused(() => TestProfiles.Run("StarPlayer.Domain.Tests", "x.dll", cases, Temp("work"), runner with { Dotnet = where })).Cause);
    }

    [Fact]
    public void A_real_child_test_run_passes_while_its_whole_folder_is_held_against_replacement()
    {
        const string project = "StarPlayer.Domain.Tests";
        const string caseId = project + "|" + project + ".GameRulesCalledPitchModeTests.The_legacy_content_hash_is_the_value_computed_before_the_pitch_zone_fields_existed";
        var assembly = Path.Combine(BuildRoot, "tests", project, "bin", "Release", "net8.0", project + ".dll");
        var dir = Path.GetDirectoryName(assembly)!;
        var rows = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Select(f =>
        {
            var bytes = File.ReadAllBytes(f);
            return (JNode)J.O(("role", J.S(f == assembly ? "TEST_ASSEMBLY" : "DEPENDENCY")), ("relative_path", J.S(Path.GetRelativePath(BuildRoot, f).Replace('\\', '/'))),
                ("raw_sha256", J.S(Archive.Sha(bytes))), ("byte_length", J.N(bytes.Length)));
        }).ToList();
        var held = ExecutionBinding.HoldTree(dir, J.O(("artifacts", J.A(rows))), BuildRoot, [], "TEST_ASSEMBLY", "DEPENDENCY");
        try
        {
            var results = TestProfiles.Run(project, assembly, [caseId], Temp("work"), TestRunner.FromRuntime(BuildRoot));
            Assert.Equal([new TestCaseResult(caseId, "Passed")], results);
            Assert.Throws<IOException>(() => File.OpenWrite(assembly).Dispose());
        }
        finally
        {
            foreach (var h in held)
                h.Handle.Dispose();
        }
    }

    // ---------------------------------------------------------------- T1 CLI order

    [Fact]
    public void The_cli_takes_no_anchor_root_or_pin_and_needs_every_evidence_option_exactly_once()
    {
        string?[] Error(params string[] a) => [OfficialEntry.Parse("certify-family", a).Error];
        string[] ok = ["--family", "fc3-cal", "--approval-record-id", "a", "--approval-record-sha256", "h", "--authorization-id", "x", "--document", "{}"];
        Assert.Null(OfficialEntry.Parse("certify-family", ok).Error);
        Assert.NotNull(Error([.. ok, "--anchor", "C:\\a.json"])[0]);
        Assert.NotNull(Error([.. ok, "--family", "fc3-int"])[0]);
        Assert.NotNull(Error(ok[..^2])[0]);
        Assert.NotNull(Error(["--family", "fc3-x", .. ok[2..]])[0]);
        var e = new StringWriter();
        Assert.Equal(OfficialEntry.Usage, OfficialEntry.Run("certify-phase", ["--calibration", "{}"], TextWriter.Null, e));
    }

    [Fact]
    public void The_cli_refuses_in_order_config_then_start_environment_then_anchor_and_never_reaches_the_registry()
    {
        string[] args = ["--family", "fc3-cal", "--approval-record-id", "a", "--approval-record-sha256", new string('0', 64), "--authorization-id", "x", "--document", "{}"];
        IEvidenceRegistry Never(TrustAnchorV1 _) => throw new InvalidOperationException("registry reached");
        string Run(OfficialSeams s)
        {
            var e = new StringWriter();
            Assert.Equal(OfficialEntry.Refused, OfficialEntry.Run("certify-family", args, TextWriter.Null, e, s with { Acl = AclOk, Elevated = NotElevated, Connect = Never }));
            return e.ToString();
        }

        Assert.Contains("Trust.install_config_missing", Run(new OfficialSeams(ConfigPath: Path.Combine(w.Root, "absent.json"), Variables: new Hashtable())));
        var config = WriteConfig();
        Assert.Contains("Environment.startup_policy", Run(new OfficialSeams(ConfigPath: config, Variables: new Hashtable { ["VSTEST_HOST_DEBUG"] = "1" })));
        Assert.Contains("Trust.anchor_missing", Run(new OfficialSeams(ConfigPath: config, Variables: new Hashtable())));
    }
}
