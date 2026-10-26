namespace StarPlayer.EvidenceVerifier;

/// <summary>
/// B-1 evidence verifier entry point: the environment pre-check and the G4 official certification commands (<see cref="OfficialEntry"/>). The certification commands need the OWNER installation settings,
/// trust anchor, registry and approval record, none of which exist (Official Execution NOT_READY), so today they always refuse before anything is read from the evidence.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args is ["environment"])
        {
            var c0 = ExecutionEnvironment.Capture();
            Console.WriteLine(c0.Text());
            return ExecutionEnvironment.ForbiddenVariables(Environment.GetEnvironmentVariables()) is { Count: > 0 } bad
                ? Fail($"Evidence.environment_mismatch: 금지 접두사 변수 {string.Join(", ", bad)}")
                : 0;
        }

        if (OperatingSystem.IsWindows() && args is [var command, .. var rest] && OfficialEntry.Handles(command))
            return OfficialEntry.Run(command, rest, Console.Out, Console.Error);

        Console.Error.WriteLine("usage: StarPlayer.EvidenceVerifier environment");
        Console.Error.WriteLine("       StarPlayer.EvidenceVerifier certify-family --family fc3-cal|fc3-int|fc3-dom --approval-record-id ID --approval-record-sha256 H --authorization-id ID --document ARTIFACT_REF_JSON");
        Console.Error.WriteLine("       StarPlayer.EvidenceVerifier certify-phase --approval-record-id ID --approval-record-sha256 H --authorization-id ID --calibration REF --integration REF --dominance REF --regression-proof REF");
        Console.Error.WriteLine("       (앵커·root·Pin은 %ProgramData%\\StarPlayer\\EvidenceVerifier\\install-config.json에서만 읽습니다. 현재 OWNER 설정·앵커·Registry가 없어 공식 인증은 거부됩니다: Official Execution NOT_READY)");
        return 2;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
