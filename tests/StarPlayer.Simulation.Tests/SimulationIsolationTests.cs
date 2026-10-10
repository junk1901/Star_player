using System.Reflection;

namespace StarPlayer.Simulation.Tests;

// Phase 0 has no simulation behavior (ROADMAP Phase 0 Non-Goals). The only contract to prove now
// is that the assembly exists, loads without Godot, and depends on Domain alone.
public class SimulationIsolationTests
{
    [Fact]
    public void Simulation_assembly_loads_and_references_no_project_assembly_except_Domain()
    {
        var assembly = Assembly.Load("StarPlayer.Simulation");

        var projectReferences = assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith("StarPlayer.", StringComparison.Ordinal))
            .ToArray();

        // The compiler drops unused references, so "empty" is valid today; a future Application/Infrastructure reference is not.
        Assert.All(projectReferences, name => Assert.Equal("StarPlayer.Domain", name));
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), name => name.Name!.StartsWith("Godot", StringComparison.Ordinal));
    }
}
