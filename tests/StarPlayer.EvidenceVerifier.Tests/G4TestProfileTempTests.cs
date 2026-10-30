using System.Security.AccessControl;
using System.Security.Principal;
using StarPlayer.SimHarness.Evidence;
using static StarPlayer.EvidenceVerifier.Tests.G4Fixtures;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// G4-TEMP (B-stage trace POTENTIAL_DECISION_DEPENDENCY, OWNER C-stage A): the child <c>dotnet test</c> gets TEMP/TMP = a NEW folder pinned inside its own results directory (and, in an official run,
/// written only by the TCB or this account), never the parent's TEMP. Preparing that folder fails closed before any child starts. All values are TEST_ONLY.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class G4TestProfileTempTests : IDisposable
{
    private const string Project = "StarPlayer.Domain.Tests";
    private static readonly string[] Cases = [Project + "|" + Project + ".X.Y"];
    private static readonly SecurityIdentifier Self = WindowsIdentity.GetCurrent().User!, Everyone = new(WellKnownSidType.WorldSid, null);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "simharness-tests", "g4-temp-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static EvidenceError Refused(Action a) => Assert.Throws<EvidenceException>(a).Error;

    private static TestRunner Fake(string scenario, params string[] args) => TestRunner.FromRuntime(BuildRoot) with { LeadingArguments = Exec(["g4-fake-dotnet", scenario, .. args]) };

    private string Temp(string name) => Directory.CreateDirectory(Path.Combine(_root, $"{name}-{Guid.NewGuid():N}")).FullName;

    /// <summary>A directory whose DACL no longer inherits and names only this account, SYSTEM and Administrators (what G5 asks of R-WORK).</summary>
    private string Protected(string name)
    {
        var d = new DirectoryInfo(Temp(name));
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[] { Self, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        d.SetAccessControl(acl);
        return d.FullName;
    }

    private static void Grant(string directory, SecurityIdentifier sid, FileSystemRights rights, AccessControlType type = AccessControlType.Allow)
    {
        var info = new DirectoryInfo(directory);
        var acl = info.GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(sid, rights, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, type));
        info.SetAccessControl(acl);
    }

    private static string? Writers(string directory) => WorkTree.WriterProblem(new DirectoryInfo(directory).GetAccessControl().GetSecurityDescriptorBinaryForm());

    [Fact]
    public void The_child_temp_and_tmp_are_a_new_protected_folder_inside_each_runs_results_directory_and_never_the_parents_writable_temp()
    {
        var parentTemp = Temp("parent-temp");
        Grant(parentTemp, Everyone, FileSystemRights.Modify);
        Assert.NotNull(Writers(parentTemp));                                                       // precondition: the parent's TEMP is writable by another account
        var work = Protected("work");

        var (exit, output) = Child(new Dictionary<string, string> { ["TEMP"] = parentTemp, ["TMP"] = parentTemp }, "g4-temp-isolation", work, "g4-planted-postprocess");

        Assert.True(exit == 0, output);
        Assert.Equal(2, output.Split('\n').Count(l => l.Trim() == $"result={Cases[0]}:Passed"));  // the PASS result and exit code are unchanged
        Assert.Contains($"parent_temp={parentTemp}\\", output);
        Assert.True(Directory.Exists(Path.Combine(parentTemp, "g4-planted-postprocess", "artifacts")));
        var runs = Directory.GetDirectories(work);
        Assert.Equal(2, runs.Length);
        foreach (var results in runs)
        {
            var temp = Path.Combine(results, "temp");
            var seen = File.ReadAllLines(Path.Combine(results, "temp-env.txt")).ToDictionary(l => l[..l.IndexOf('=')], l => l[(l.IndexOf('=') + 1)..]);
            Assert.Equal(temp, seen["TEMP"], ignoreCase: true);
            Assert.Equal(temp, seen["TMP"], ignoreCase: true);
            Assert.Equal(temp + "\\", seen["GetTempPath"], ignoreCase: true);
            Assert.Equal("False", seen["planted_visible"]);                                         // the planted post-processing folder is not under the child's TEMP
            Assert.Equal("True", seen["roundtrip"]);                                                // ordinary temporary files still work there
            Assert.Empty(Directory.EnumerateFileSystemEntries(temp));
            Assert.Null(Writers(temp));
        }

        Assert.NotEqual(Path.Combine(runs[0], "temp"), Path.Combine(runs[1], "temp"));              // two runs never share a temporary folder
    }

    public static TheoryData<string> Sabotage => ["existing-folder", "junction", "no-add-subdirectory", "inherited-writer"];

    [Theory]
    [MemberData(nameof(Sabotage))]
    public void Preparing_the_child_temp_folder_fails_closed_before_any_child_starts(string sabotage)
    {
        var outside = Temp("outside");
        string? results = null;
        var runner = Fake("temp-env", "none") with
        {
            RequireProtectedWorkTree = sabotage == "inherited-writer",
            BeforeChild = (_, r) =>
            {
                results = r;
                var temp = Path.Combine(r, "temp");
                switch (sabotage)
                {
                    case "existing-folder":                                                        // somebody made the name first: it is never reused
                        Directory.CreateDirectory(temp);
                        break;
                    case "junction":                                                               // the name is a link to a folder outside the results tree
                        using (var mk = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{temp}\" \"{outside}\"") { UseShellExecute = false, CreateNoWindow = true })!)
                            mk.WaitForExit();
                        Assert.True(File.GetAttributes(temp).HasFlag(FileAttributes.ReparsePoint));
                        break;
                    case "no-add-subdirectory":                                                    // the folder cannot be created at all
                        Grant(r, Self, FileSystemRights.AppendData, AccessControlType.Deny);
                        break;
                    case "inherited-writer":                                                       // the results folder passed its check; what is created below it would let another account write
                        Grant(r, Everyone, FileSystemRights.Write);
                        break;
                }
            },
        };
        var work = sabotage == "inherited-writer" ? Protected("work") : Temp("work");
        try
        {
            var cause = Refused(() => TestProfiles.Run(Project, "x.dll", Cases, work, runner)).Cause;

            Assert.Equal(sabotage == "inherited-writer" ? "TestProfile.results_acl" : "TestProfile.results_directory", cause);
            Assert.NotNull(results);
            Assert.False(File.Exists(Path.Combine(results!, "temp-env.txt")), "a child started");    // the scripted child writes this first: no child ran
            Assert.Empty(Directory.GetFiles(results!, "*.trx", SearchOption.TopDirectoryOnly));
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));                            // nothing reached the link target
        }
        finally
        {
            var temp = results is null ? null : Path.Combine(results, "temp");
            if (temp is not null && Directory.Exists(temp) && File.GetAttributes(temp).HasFlag(FileAttributes.ReparsePoint))
                Directory.Delete(temp);                                                              // the junction itself, not its target
            if (results is not null && sabotage == "no-add-subdirectory")
            {
                var info = new DirectoryInfo(results);
                var acl = info.GetAccessControl();
                acl.RemoveAccessRule(new FileSystemAccessRule(Self, FileSystemRights.AppendData, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny));
                info.SetAccessControl(acl);
            }
        }
    }

    [Fact]
    public void A_trx_the_child_drops_into_its_temp_folder_is_a_second_trx_and_refused()
    {
        Assert.Equal("TestProfile.trx", Refused(() => TestProfiles.Run(Project, "x.dll", Cases, Temp("work"), Fake("trx-in-temp"))).Cause);
    }
}
