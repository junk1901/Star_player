namespace StarPlayer.Application.Diagnostics;

/// <summary>Static build facts shown on the shell. Supplied by the composition root.</summary>
public sealed record BuildInfo(string ProductName, string Version);
