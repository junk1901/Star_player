using System.Diagnostics;
using System.Text;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// F-C2-A Part B (FC-D10 dirty policy): a pure parser of <c>git status --porcelain=v1 -z --untracked-files=all --ignored</c>, a classifier that excepts only the three protected user files at their exact
/// paths, and a fail-closed adapter. Build output is never trusted by its path alone: with no build-provenance proof yet it makes the check NOT_READY, not clean.
/// </summary>
public class GitIntegrityTests
{
    private const string Sha = "97045829d5836ff6dec8b40109c84d016d92c340";

    private static string Z(params string[] records) => string.Concat(records.Select(r => r + "\0"));

    private static GitIntegrityReport Evaluate(string porcelain) => GitIntegrityPolicy.Evaluate(H.Ok(GitStatusParser.Parse(porcelain)));

    // ---------------------------------------------------------------- pure parser

    [Fact]
    public void The_parser_reads_every_status_form_including_both_paths_of_a_rename()
    {
        var entries = H.Ok(GitStatusParser.Parse(Z(" M src/A.cs", "M  src/B.cs", "A  new.cs", " D gone.cs", "R  to name.cs", "from name.cs", "C  copy.cs", "orig.cs", "?? 한글 폴더/파일 이름.md", "!! tools/X/bin/x.dll", "UU conflict.cs", "MM both.cs")));

        Assert.Equal(10, entries.Count);
        Assert.Equal(new GitStatusEntry(' ', 'M', "src/A.cs", null), entries[0]);
        Assert.Equal(new GitStatusEntry('M', ' ', "src/B.cs", null), entries[1]);
        Assert.Equal(new GitStatusEntry('R', ' ', "to name.cs", "from name.cs"), entries[4]);           // -z: the new path, then the original, each NUL-terminated
        Assert.Equal(new GitStatusEntry('C', ' ', "copy.cs", "orig.cs"), entries[5]);
        Assert.Equal("한글 폴더/파일 이름.md", entries[6].Path);
        Assert.True(entries[6].IsUntracked);
        Assert.True(entries[7].IsIgnored);
        Assert.Empty(H.Ok(GitStatusParser.Parse("")));                                                  // clean
    }

    [Theory]
    [InlineData("?? a.md")]                     // no NUL terminator: truncated output
    [InlineData("?? a.md\0garbage")]            // trailing bytes after the last record
    [InlineData("?a.md\0")]                     // too short / no separator
    [InlineData("??_a.md\0")]                   // the third byte must be a space
    [InlineData("?? \0")]                       // empty path
    [InlineData("XY a.md\0")]                   // unknown status letters
    [InlineData("?M a.md\0")]                   // '?' only as "??"
    [InlineData("!  a.md\0")]                   // '!' only as "!!"
    [InlineData("R  only-new.cs\0")]            // a rename without its original path
    [InlineData("?? /abs/a.md\0")]              // not repository-relative
    [InlineData("?? ../up.md\0")]               // escapes the repository
    [InlineData("?? a/../../b.md\0")]
    public void Malformed_porcelain_output_is_an_error_not_a_clean_tree(string text)
    {
        var parsed = GitStatusParser.Parse(text);
        Assert.False(parsed.IsSuccess);
        Assert.Equal("Git.status_malformed", parsed.Error!.Value.Code);
    }

    // ---------------------------------------------------------------- policy

    [Fact]
    public void Only_the_three_protected_files_at_their_exact_paths_are_excepted()
    {
        Assert.Equal(GitIntegrityState.Clean, Evaluate("").State);
        Assert.Equal(GitIntegrityState.Clean, Evaluate(Z("?? TASK.md", "?? docs/2023recordRule.pdf", "?? docs/2026-official-baseball-rules.pdf")).State);
        Assert.Equal(new[] { "TASK.md", "docs/2023recordRule.pdf", "docs/2026-official-baseball-rules.pdf" }, GitIntegrityPolicy.ProtectedPaths.ToArray());

        foreach (var lookalike in new[] { "task.md", "TASK.md.bak", "docs/TASK.md", "src/TASK.md", "TASK.MD", "docs/2023recordRule.pdf.tmp", "docs/2023RecordRule.pdf", "Docs/2023recordRule.pdf",
                     "docs/sub/2026-official-baseball-rules.pdf", " TASK.md", "TASK.md/" })
        {
            var report = Evaluate(Z("?? " + lookalike));
            Assert.True(report.State == GitIntegrityState.Dirty, lookalike);
            Assert.Equal("Git.dirty", report.Code);
        }
    }

    [Fact]
    public void A_protected_path_is_excepted_only_while_it_is_untracked()
    {
        Assert.Equal(GitIntegrityState.Dirty, Evaluate(Z(" M TASK.md")).State);              // tracked and modified
        Assert.Equal(GitIntegrityState.Dirty, Evaluate(Z("A  TASK.md")).State);              // staged
        Assert.Equal(GitIntegrityState.Dirty, Evaluate(Z("!! TASK.md")).State);              // ignored
        Assert.Equal(GitIntegrityState.Dirty, Evaluate(Z("R  TASK.md", "src/Old.cs")).State); // renamed onto the protected name
    }

    [Theory]
    [InlineData(" M src/StarPlayer.Domain/Common/Result.cs")]       // tracked change
    [InlineData("M  src/StarPlayer.Domain/Common/Result.cs")]       // staged change
    [InlineData("D  content/tuning/virtual_tuning_v4.json")]
    [InlineData("?? tools/StarPlayer.SimHarness/Evil.cs")]          // untracked source
    [InlineData("?? tools/New/New.csproj")]                         // untracked project
    [InlineData("?? content/seeds/unapproved.json")]                // unapproved input
    [InlineData("!! src/StarPlayer.Domain/Hidden.cs")]              // ignored source
    [InlineData("!! secrets/key.json")]
    [InlineData("!! build/simharness/old-run/manifest.json")]       // old run output is not approved build output
    [InlineData("!! build/export/StarPlayer.exe")]
    [InlineData("!! bin/x.dll")]                                    // a bin folder that is not a project's
    [InlineData("!! src/bin/x.dll")]
    [InlineData("?? tools/StarPlayer.SimHarness/bin/x.dll")]        // build-output path but untracked, not ignored
    public void Changes_and_unapproved_files_are_rejected(string record)
    {
        var report = Evaluate(Z(record));
        Assert.Equal(GitIntegrityState.Dirty, report.State);
        Assert.Equal("Git.dirty", report.Code);
        Assert.Single(report.Rejected);
    }

    [Fact]
    public void Ignored_project_build_output_is_not_trusted_by_its_path_and_keeps_the_check_not_ready()
    {
        var report = Evaluate(Z("?? TASK.md", "!! tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll", "!! src/StarPlayer.Domain/obj/Release/net8.0/StarPlayer.Domain.AssemblyInfo.cs"));

        Assert.Equal(GitIntegrityState.BuildOutputUnverified, report.State);   // never Clean: the provenance proof (build at the approved SHA) is not implemented in F-C2-A
        Assert.Equal("Git.build_output_unverified", report.Code);
        Assert.Equal(2, report.BuildOutput.Count);
        Assert.Empty(report.Rejected);

        // one unapproved file among build output makes it Dirty, which outranks NOT_READY
        Assert.Equal(GitIntegrityState.Dirty, Evaluate(Z("!! tools/StarPlayer.SimHarness/bin/a.dll", "?? stray.txt")).State);
    }

    // ---------------------------------------------------------------- adapter (fail-closed)

    private static Func<string[], GitCommandResult> Fake(string root, string head, string status, int statusExit = 0) => args => args[0] switch
    {
        "rev-parse" when args[1] == "--show-toplevel" => new(0, Encoding.UTF8.GetBytes(root + "\n"), ""),
        "rev-parse" => new(0, Encoding.UTF8.GetBytes(head + "\n"), ""),
        "status" => new(statusExit, Encoding.UTF8.GetBytes(status), statusExit == 0 ? "" : "fatal: boom"),
        _ => new(128, [], "unexpected"),
    };

    [Fact]
    public void The_adapter_asks_git_for_nul_separated_status_with_every_untracked_and_ignored_file()
    {
        var seen = new List<string>();
        var report = GitIntegrity.Check("C:/repo", args =>
        {
            seen.Add(string.Join(' ', args));
            return Fake("C:/repo", Sha, "")(args);
        });

        Assert.Equal(GitIntegrityState.Clean, report.State);
        Assert.Equal(Sha, report.HeadSha);
        Assert.Contains("status --porcelain=v1 -z --untracked-files=all --ignored", seen);
    }

    [Fact]
    public void Every_git_failure_is_fail_closed()
    {
        Assert.Equal("Git.query_failed", GitIntegrity.Check("C:/repo", Fake("C:/repo", Sha, "", statusExit: 128)).Code);
        Assert.Equal("Git.query_failed", GitIntegrity.Check("C:/repo", _ => new GitCommandResult(-1, [], "git not found")).Code);
        Assert.Equal("Git.query_failed", GitIntegrity.Check("C:/repo", _ => throw new InvalidOperationException("boom")).Code);
        Assert.Equal("Git.head_invalid", GitIntegrity.Check("C:/repo", Fake("C:/repo", "not-a-sha", "")).Code);
        Assert.Equal("Git.head_invalid", GitIntegrity.Check("C:/repo", Fake("C:/repo", Sha.ToUpperInvariant(), "")).Code);
        Assert.Equal("Git.root_mismatch", GitIntegrity.Check("C:/repo", Fake("C:/other", Sha, "")).Code);
        Assert.Equal("Git.root_mismatch", GitIntegrity.Check("C:/repo", Fake("C:/repo/sub", Sha, "")).Code);
        Assert.Equal("Git.status_malformed", GitIntegrity.Check("C:/repo", Fake("C:/repo", Sha, "?? a.md")).Code);
        Assert.Equal("Git.output_not_utf8", GitIntegrity.Check("C:/repo", a => a[0] == "status" ? new GitCommandResult(0, [0x3F, 0x3F, 0x20, 0xFF, 0x00], "") : Fake("C:/repo", Sha, "")(a)).Code);
        foreach (var code in new[] { "Git.query_failed", "Git.head_invalid", "Git.root_mismatch", "Git.status_malformed", "Git.output_not_utf8" })
            Assert.False(GitIntegrityState.Clean.ToString() == code);
        Assert.Equal(GitIntegrityState.Error, GitIntegrity.Check("C:/repo", Fake("C:/other", Sha, "")).State);
    }

    // ---------------------------------------------------------------- a real temporary repository

    private static void Git(string dir, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "core.autocrlf=false", "-c", "commit.gpgsign=false" }.Concat(args))
            start.ArgumentList.Add(a);
        using var p = Process.Start(start)!;
        p.StandardOutput.ReadToEnd();
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        Assert.True(p.ExitCode == 0, $"git {string.Join(' ', args)}: {err}");
    }

    [Fact]
    public void A_real_temporary_repository_is_classified_through_the_real_git_adapter()
    {
        var dir = Path.Combine(Path.GetTempPath(), "simharness-tests", "git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Git(dir, "init", "-q");
            File.WriteAllText(Path.Combine(dir, ".gitignore"), "**/bin/\n");
            File.WriteAllText(Path.Combine(dir, "tracked.txt"), "a");
            Git(dir, "add", ".");
            Git(dir, "commit", "-q", "-m", "init");
            Directory.CreateDirectory(Path.Combine(dir, "docs"));
            File.WriteAllText(Path.Combine(dir, "TASK.md"), "x");
            File.WriteAllText(Path.Combine(dir, "docs", "2023recordRule.pdf"), "x");
            File.WriteAllText(Path.Combine(dir, "docs", "2026-official-baseball-rules.pdf"), "x");

            var clean = GitIntegrity.Check(dir);
            Assert.True(clean.State == GitIntegrityState.Clean, $"{clean.Code}: {clean.Message}");
            Assert.Matches("^[0-9a-f]{40}$", clean.HeadSha!);

            Directory.CreateDirectory(Path.Combine(dir, "공백 있는 폴더"));
            File.WriteAllText(Path.Combine(dir, "공백 있는 폴더", "새 파일.cs"), "x");
            var dirty = GitIntegrity.Check(dir);
            Assert.Equal(GitIntegrityState.Dirty, dirty.State);
            Assert.Equal("공백 있는 폴더/새 파일.cs", Assert.Single(dirty.Rejected).Path);

            File.Delete(Path.Combine(dir, "공백 있는 폴더", "새 파일.cs"));
            Directory.CreateDirectory(Path.Combine(dir, "tools", "P", "bin"));
            File.WriteAllText(Path.Combine(dir, "tools", "P", "bin", "p.dll"), "x");
            Assert.Equal(GitIntegrityState.BuildOutputUnverified, GitIntegrity.Check(dir).State);

            File.WriteAllText(Path.Combine(dir, "tracked.txt"), "changed");
            Assert.Equal(GitIntegrityState.Dirty, GitIntegrity.Check(dir).State);

            Assert.Equal("Git.root_mismatch", GitIntegrity.Check(Path.Combine(dir, "docs")).Code);   // a subdirectory is not the repository root
        }
        finally
        {
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }
    }
}
