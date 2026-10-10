using StarPlayer.Application.Diagnostics;

namespace StarPlayer.Application.Ports;

/// <summary>
/// Phase 0 probe of the storage provider using a throwaway test schema. It is not a save format.
/// Implementations report each failed check as a failed step instead of throwing.
/// </summary>
public interface IStorageDiagnostics
{
    IReadOnlyList<DiagnosticStep> RunChecks();
}
