using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>One record of <c>git status --porcelain=v1 -z</c>: index status, work-tree status, the repository-relative path and, for a rename or copy, the original path.</summary>
public sealed record GitStatusEntry(char Index, char WorkTree, string Path, string? OriginalPath)
{
    public bool IsUntracked => Index == '?';

    public bool IsIgnored => Index == '!';
}

/// <summary>
/// Pure parser of <c>git status --porcelain=v1 -z</c> output (already decoded as UTF-8). Each record is <c>XY PATH</c> terminated by NUL; a rename or copy (<c>R</c> / <c>C</c>) is followed by its
/// original path as a second NUL-terminated field. With <c>-z</c> git neither quotes nor escapes a path, so spaces, Korean text and other characters are taken verbatim; anything that does not
/// have exactly this shape (truncation, trailing bytes, unknown letters, an empty, absolute or escaping path) is <c>Git.status_malformed</c>, never a clean tree.
/// </summary>
public static class GitStatusParser
{
    private const string Letters = " MTADRCU";

    public static Result<IReadOnlyList<GitStatusEntry>> Parse(string text)
    {
        static Result<IReadOnlyList<GitStatusEntry>> Bad(string why) => Result<IReadOnlyList<GitStatusEntry>>.Failure(new Error("Git.status_malformed", $"git status 출력 형식이 올바르지 않습니다: {why}"));
        var entries = new List<GitStatusEntry>();
        if (text.Length == 0)
            return Result<IReadOnlyList<GitStatusEntry>>.Success(entries);
        if (text[^1] != '\0')
            return Bad("마지막 기록이 NUL로 끝나지 않습니다(잘린 출력).");
        var fields = text[..^1].Split('\0');
        for (var i = 0; i < fields.Length; i++)
        {
            var record = fields[i];
            if (record.Length < 4 || record[2] != ' ')
                return Bad($"기록 '{record}'은(는) 'XY PATH' 형식이 아닙니다.");
            char x = record[0], y = record[1];
            var pair = (x is '?' or '!' || y is '?' or '!') ? (x == y ? null : "'?'와 '!'는 '??'·'!!'로만 쓰입니다.") : (Letters.Contains(x) && Letters.Contains(y) ? null : $"알 수 없는 상태 '{x}{y}'.");
            if (pair is not null)
                return Bad(pair);
            string? original = null;
            if (x is 'R' or 'C' || y is 'R' or 'C')
            {
                if (++i >= fields.Length)
                    return Bad($"이름 변경/복사 '{record}'의 원래 경로가 없습니다.");
                original = fields[i];
            }

            foreach (var path in new[] { record[3..], original })
                if (path is not null && PathProblem(path) is { } problem)
                    return Bad($"경로 '{path}': {problem}");
            entries.Add(new GitStatusEntry(x, y, record[3..], original));
        }

        return Result<IReadOnlyList<GitStatusEntry>>.Success(entries);
    }

    private static string? PathProblem(string path) =>
        path.Length == 0 ? "비어 있습니다."
        : path[0] == '/' || (path.Length > 1 && path[1] == ':') ? "저장소 기준 상대 경로가 아닙니다."
        : path.Split('/').Any(s => s == "..") ? "저장소 밖을 가리킵니다."
        : null;
}

public enum GitIntegrityState { Clean, BuildOutputUnverified, Dirty, Error }

/// <summary>The verdict on a working tree. Only <see cref="GitIntegrityState.Clean"/> may start an official run; every other state carries a <c>Git.*</c> code.</summary>
public sealed record GitIntegrityReport(GitIntegrityState State, string? Code, string Message, string? HeadSha, IReadOnlyList<GitStatusEntry> Rejected, IReadOnlyList<GitStatusEntry> BuildOutput);

/// <summary>
/// FC-D10 dirty policy. Excepted: exactly the three protected user files, at their exact paths, while untracked. Build output (an ignored file under a project's <c>bin/</c> or <c>obj/</c> in
/// <c>src/</c>, <c>tools/</c> or <c>tests/</c>) is the only other candidate exception, and F-C2-A cannot yet bind it to the approved source SHA (no build-provenance proof), so it makes the state
/// <see cref="GitIntegrityState.BuildOutputUnverified"/> (NOT_READY), never <see cref="GitIntegrityState.Clean"/>. Everything else is rejected: a tracked or staged change, any other untracked file,
/// any other ignored file (old run output under <c>build/</c> included).
/// </summary>
public static class GitIntegrityPolicy
{
    public static IReadOnlyList<string> ProtectedPaths { get; } = ["TASK.md", "docs/2023recordRule.pdf", "docs/2026-official-baseball-rules.pdf"];

    private static readonly Regex BuildOutputPath = new("^(src|tools|tests)/[^/]+/(bin|obj)/.+$", RegexOptions.CultureInvariant);

    public static GitIntegrityReport Evaluate(IReadOnlyList<GitStatusEntry> entries, string? headSha = null)
    {
        var rejected = new List<GitStatusEntry>();
        var build = new List<GitStatusEntry>();
        foreach (var e in entries)
        {
            if (e.IsUntracked && ProtectedPaths.Contains(e.Path, StringComparer.Ordinal))
                continue;
            (e.IsIgnored && BuildOutputPath.IsMatch(e.Path) ? build : rejected).Add(e);
        }

        if (rejected.Count > 0)
            return new(GitIntegrityState.Dirty, "Git.dirty", $"승인되지 않은 작업 트리 상태 {rejected.Count}건: {string.Join(", ", rejected.Take(5).Select(r => $"'{r.Index}{r.WorkTree} {r.Path}'"))}{(rejected.Count > 5 ? " …" : "")}", headSha, rejected, build);
        if (build.Count > 0)
            return new(GitIntegrityState.BuildOutputUnverified, "Git.build_output_unverified", $"빌드 산출물 {build.Count}건이 승인 소스 SHA에 결속되었다는 증거가 없습니다(F-C2-A 미구현, NOT_READY).", headSha, rejected, build);
        return new(GitIntegrityState.Clean, null, "보호 파일 외 변경 없음", headSha, rejected, build);
    }
}

/// <summary>The raw result of one git invocation (<see cref="ExitCode"/> -1 when git could not be started).</summary>
public sealed record GitCommandResult(int ExitCode, byte[] Stdout, string Stderr);

/// <summary>
/// The git adapter: asks the real repository for its root, HEAD and NUL-separated status, and hands the bytes to the pure parser and policy. Fail-closed: a git that cannot start or exits nonzero,
/// output that is not strict UTF-8, a HEAD that is not a lowercase hex object id, or a root other than the expected one is <see cref="GitIntegrityState.Error"/>. It never modifies the repository
/// (<c>GIT_OPTIONAL_LOCKS=0</c> stops status from refreshing the index).
/// </summary>
public static class GitIntegrity
{
    public const string StatusArguments = "status --porcelain=v1 -z --untracked-files=all --ignored";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static GitIntegrityReport Check(string expectedRoot, Func<string[], GitCommandResult>? git = null)
    {
        git ??= args => Run(expectedRoot, args);
        GitIntegrityReport Error(string code, string message, string? head = null) => new(GitIntegrityState.Error, code, message, head, [], []);
        try
        {
            var root = Query(git, ["rev-parse", "--show-toplevel"]);
            if (root.Error is not null)
                return Error(root.Error.Value.Code, root.Error.Value.Message);
            if (!SamePath(root.Text!.TrimEnd('\n', '\r'), expectedRoot))
                return Error("Git.root_mismatch", $"저장소 루트가 다릅니다: 기대 '{expectedRoot}', git '{root.Text.Trim()}'.");
            var head = Query(git, ["rev-parse", "HEAD"]);
            if (head.Error is not null)
                return Error(head.Error.Value.Code, head.Error.Value.Message);
            var sha = head.Text!.TrimEnd('\n', '\r');
            if (!Regex.IsMatch(sha, "^([0-9a-f]{40}|[0-9a-f]{64})$", RegexOptions.CultureInvariant))
                return Error("Git.head_invalid", $"HEAD가 커밋 해시가 아닙니다: '{sha}'.");
            var status = Query(git, StatusArguments.Split(' '));
            if (status.Error is not null)
                return Error(status.Error.Value.Code, status.Error.Value.Message, sha);
            var entries = GitStatusParser.Parse(status.Text!);
            return entries.IsSuccess ? GitIntegrityPolicy.Evaluate(entries.Value, sha) : Error(entries.Error!.Value.Code, entries.Error.Value.Message, sha);
        }
        catch (Exception e)
        {
            return Error("Git.query_failed", $"git 조회에 실패했습니다: {e.Message}");
        }
    }

    private static (string? Text, Error? Error) Query(Func<string[], GitCommandResult> git, string[] args)
    {
        var r = git(args);
        if (r.ExitCode != 0)
            return (null, new Error("Git.query_failed", $"git {string.Join(' ', args)} 실패(exit {r.ExitCode}): {r.Stderr.Trim()}"));
        try
        {
            return (StrictUtf8.GetString(r.Stdout), null);
        }
        catch (DecoderFallbackException)
        {
            return (null, new Error("Git.output_not_utf8", $"git {string.Join(' ', args)} 출력이 UTF-8이 아닙니다."));
        }
    }

    private static bool SamePath(string a, string b)
    {
        static string N(string p) => Path.GetFullPath(p).Replace('\\', '/').TrimEnd('/');
        return string.Equals(N(a), N(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public static GitCommandResult Run(string workingDirectory, string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (var a in args)
            start.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(start);
            if (p is null)
                return new GitCommandResult(-1, [], "git을 시작할 수 없습니다.");
            var error = p.StandardError.ReadToEndAsync();
            using var output = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(output);
            p.WaitForExit();
            return new GitCommandResult(p.ExitCode, output.ToArray(), error.Result);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new GitCommandResult(-1, [], e.Message);
        }
    }
}
