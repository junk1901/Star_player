namespace StarPlayer.Application.Diagnostics;

/// <summary>One named check of the Phase 0 environment probe.</summary>
public sealed record DiagnosticStep(string Name, bool Passed, string Detail);

public sealed record DiagnosticReport(IReadOnlyList<DiagnosticStep> Steps)
{
    /// <summary>An empty report is a failure: nothing was proven.</summary>
    public bool Passed => Steps.Count > 0 && Steps.All(step => step.Passed);
}
