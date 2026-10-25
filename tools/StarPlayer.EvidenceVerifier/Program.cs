namespace StarPlayer.EvidenceVerifier;

/// <summary>
/// B-1 evidence verifier entry point. Only the environment pre-check is wired as a command; a full official verification needs an owner-registered trust anchor, registry and approval record, none of
/// which exist (Official Execution NOT_READY), so no command here can certify anything.
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

        Console.Error.WriteLine("usage: StarPlayer.EvidenceVerifier environment");
        Console.Error.WriteLine("       (공식 검증은 OWNER 등록 앵커·Registry·승인 기록이 없어 실행할 수 없습니다: Official Execution NOT_READY)");
        return 2;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
