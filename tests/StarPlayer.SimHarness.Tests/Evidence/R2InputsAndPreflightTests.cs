using System.Text.Json;
using System.Xml.Linq;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>B1 T11 R2-A (the consumed inputs are exactly the embedded data files and their repository originals) and R2-B (an official execution records its READY preflight before its first unit).</summary>
public sealed class R2InputsAndPreflightTests(DominanceOfficialWorld w) : IClassFixture<DominanceOfficialWorld>
{
    private static string Repo { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    [Fact]
    public void R2A_the_input_list_is_exactly_the_project_s_embedded_resources_and_each_resource_is_its_repository_file_byte_for_byte()
    {
        var project = XDocument.Load(Path.Combine(Repo, "tools", "StarPlayer.SimHarness", "StarPlayer.SimHarness.csproj"));
        var embedded = project.Descendants("EmbeddedResource").Select(e => ((string)e.Attribute("LogicalName")!, Path.GetFullPath(Path.Combine(Repo, "tools", "StarPlayer.SimHarness", (string)e.Attribute("Include")!))))
            .OrderBy(x => x.Item1, StringComparer.Ordinal).ToList();
        Assert.Equal(embedded, HarnessData.Inputs.Select(i => (i.Name, Path.GetFullPath(Path.Combine([Repo, .. i.RepositoryPath.Split('/')])))).OrderBy(x => x.Item1, StringComparer.Ordinal));
        Assert.Equal(HarnessData.Inputs.Select(i => i.Name).Order(StringComparer.Ordinal), typeof(HarnessData).Assembly.GetManifestResourceNames().Order(StringComparer.Ordinal));   // no other embedded input
        foreach (var (name, path) in HarnessData.Inputs)
            Assert.Equal(File.ReadAllBytes(Path.Combine([Repo, .. path.Split('/')])), HarnessData.ResourceBytes(name));                                                  // the run reads the build input's bytes
    }

    [Fact]
    public void R2B_an_official_execution_records_its_ready_family_and_unit_preflight_before_its_first_unit()
    {
        var report = DominancePreflight.Run(new DominanceOfficialRequest(w.Root, "dom-r2b", null), w.Approvals, DominanceOfficialWorld.Git());
        Assert.True(report.Ready);
        var stopped = DominanceOfficial.Execute(report, "dom-r2b", w.Approvals, w.Root, integrity: _ => new Error("Test.before_first_cell", "x"));   // aborted before any cell runs
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, 0), (stopped.Termination, stopped.Cells.Count));
        var output = report.OutputDirectory!;
        Assert.Empty(Directory.GetDirectories(Path.Combine(output, "cells")));                                                                       // no game ran
        Assert.Equal(JsonSerializer.Serialize(DominanceOfficial.PreflightDocument(report, "dom-r2b"), RunReport.Json), File.ReadAllText(Path.Combine(output, OfficialOutput.PreflightFile)));
        var units = Directory.GetFiles(Path.Combine(output, OfficialOutput.PreflightDirectory)).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(DominanceMatrix.Cells.Select(c => OfficialOutput.UnitPreflightName(c.Id)).Order(StringComparer.Ordinal), units);
        foreach (var c in report.Cells)
            Assert.Equal(JsonSerializer.Serialize(OfficialOutput.Result(c.Report), RunReport.Json),
                File.ReadAllText(Path.Combine(output, OfficialOutput.PreflightDirectory, OfficialOutput.UnitPreflightName(c.Pinned.Cell.Id))));
    }
}
