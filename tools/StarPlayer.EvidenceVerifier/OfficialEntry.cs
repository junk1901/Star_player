using System.Collections;
using System.Runtime.Versioning;
using System.Text;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier;

/// <summary>Tests only: replacements for the Windows checks and the registry connection of <see cref="OfficialEntry"/> (production passes none).</summary>
internal sealed record OfficialSeams(
    string? ConfigPath = null, Func<string, string?>? Acl = null, Func<bool>? Elevated = null, IDictionary? Variables = null, string? ProcessPath = null,
    Func<TrustAnchorV1, IEvidenceRegistry>? Connect = null);

/// <summary>
/// G4-T1 (F-13): the official certification entry point. In this order, and nothing is issued unless every step passes (Fail-Closed):
/// <list type="number">
/// <item>E2-1 (G4-03): the DOTNET_ / COMPlus_ check and the initial capture C0, before anything else is read; that C0 is the one C1 and C2 are compared with;</item>
/// <item>the OWNER installation settings from their one fixed location (§16 T1 — no anchor, root or pin comes from the command line);</item>
/// <item>the G4 process binding before any trust input is read (start environment, pinned dotnet host, execution pins);</item>
/// <item>the trust anchor from the settings (T1/T2) and the authenticated registry pipe (T3);</item>
/// <item><see cref="Certification.Open"/> with the binding (E2-1..E2-5, T3/T4 binding of the build);</item>
/// <item>the requested family or Phase certification through the existing <see cref="Certification"/> API only.</item>
/// </list>
/// The command line carries only evidence identifiers and the submitted artifact references, which are verified, never trusted.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class OfficialEntry
{
    public const int Issued = 0, Refused = 1, Usage = 2;

    private static readonly string[] Families = ["fc3-cal", "fc3-int", "fc3-dom"];

    private static readonly Dictionary<string, string[]> Commands = new(StringComparer.Ordinal)
    {
        ["certify-family"] = ["family", "approval-record-id", "approval-record-sha256", "authorization-id", "document"],
        ["certify-phase"] = ["approval-record-id", "approval-record-sha256", "authorization-id", "calibration", "integration", "dominance", "regression-proof"],
    };

    public static bool Handles(string command) => Commands.ContainsKey(command);

    /// <summary>Exactly the command's options, each once with a non-empty value; null and a message otherwise.</summary>
    internal static (Dictionary<string, string>? Options, string? Error) Parse(string command, IReadOnlyList<string> args)
    {
        var allowed = Commands[command];
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i += 2)
        {
            var name = args[i].StartsWith("--", StringComparison.Ordinal) ? args[i][2..] : "";
            if (!allowed.Contains(name, StringComparer.Ordinal))
                return (null, $"알 수 없는 인자 '{args[i]}'입니다(앵커·root·Pin은 설치 설정에서만 읽습니다).");
            if (i + 1 >= args.Count || args[i + 1].Length == 0)
                return (null, $"'{args[i]}'에 값이 없습니다.");
            if (!options.TryAdd(name, args[i + 1]))
                return (null, $"'{args[i]}'가 두 번 있습니다.");
        }

        if (allowed.FirstOrDefault(a => !options.ContainsKey(a)) is { } missing)
            return (null, $"'--{missing}'가 필요합니다.");
        if (options.TryGetValue("family", out var f) && !Families.Contains(f, StringComparer.Ordinal))
            return (null, $"family '{f}'는 fc3-cal|fc3-int|fc3-dom가 아닙니다.");
        return (options, null);
    }

    /// <summary>A submitted artifact reference (strict JSON, §2.5 ArtifactRef shape). It is only the claim the verifier then checks against the archive.</summary>
    private static JObject Reference(string json) => ApprovalFormats.Read(EvidenceShapes.ArtifactRef, Encoding.UTF8.GetBytes(json));

    public static int Run(string command, IReadOnlyList<string> args, TextWriter output, TextWriter error, OfficialSeams? seams = null)
    {
        var (o, usage) = Parse(command, args);
        if (o is null)
        {
            error.WriteLine($"usage: {usage}");
            return Usage;
        }

        seams ??= new OfficialSeams();
        try
        {
            var c0 = Certification.InitialCapture(seams.Variables);
            var config = InstallConfigReader.Read(seams.ConfigPath ?? InstallConfigReader.DefaultPath, seams.Acl, seams.Elevated);
            using var binding = ExecutionBinding.Establish(config, seams.Variables, seams.ProcessPath);
            var anchor = TrustAnchorReader.Read(config.AnchorPath, config.BuildRoot, config.ArchiveRoot, seams.Acl, seams.Elevated);
            var registry = (seams.Connect ?? (a => RegistryPipeClient.Connect(a)))(anchor);
            try
            {
                var session = Certification.Open(anchor, registry, o["approval-record-id"], o["approval-record-sha256"], o["authorization-id"], config.BuildRoot, config.ArchiveRoot, binding, c0);
                IssuedReceipt receipt;
                if (command == "certify-family")
                {
                    receipt = Certification.IssueFamily(session, Certification.VerifyFamily(session, o["family"], Reference(o["document"])));
                }
                else
                {
                    var families = new[] { "calibration", "integration", "dominance" }.Select((k, i) => Certification.VerifyFamily(session, Families[i], Reference(o[k]))).ToList();
                    var children = families.Select(v => Certification.IssueFamily(session, v)).ToList();
                    var run = Certification.RunTestProfile(session, config.BuildRoot, config.WorkRoot);
                    var proof = Certification.VerifyRegressionProof(session, run, Reference(o["regression-proof"]));
                    receipt = Certification.IssuePhase(session, families[0], families[1], families[2], children, proof);
                }

                output.WriteLine($"receipt_id={receipt.ReceiptId} raw_sha256={receipt.RawSha256} state={receipt.State}");
                return Issued;
            }
            finally
            {
                (registry as IDisposable)?.Dispose();
            }
        }
        catch (EvidenceException e)
        {
            error.WriteLine($"{e.Error.Code} {e.Error.Cause}: {e.Error.Message}");
            return Refused;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            error.WriteLine($"{EvidenceCodes.ApprovalMismatch} Execution.io: {e.Message}");
            return Refused;
        }
    }
}
