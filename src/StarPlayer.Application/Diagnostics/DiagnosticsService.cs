using StarPlayer.Application.Ports;

namespace StarPlayer.Application.Diagnostics;

/// <summary>
/// Phase 0 use case: run the storage probe and return a public read model. Presenters only ever see
/// <see cref="DiagnosticReport"/>; an unexpected port failure becomes a failed step, never an exception
/// or a Domain type.
/// </summary>
public sealed class DiagnosticsService(IStorageDiagnostics storage)
{
    public const string StorageStepName = "저장소 진단 실행";

    public DiagnosticReport Run()
    {
        try
        {
            return new DiagnosticReport(storage.RunChecks());
        }
        catch (Exception exception)
        {
            return new DiagnosticReport([new DiagnosticStep(StorageStepName, false, $"저장소 진단을 실행하지 못했습니다: {exception.Message}")]);
        }
    }
}
