namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-F F-A: the headless harness (tools/StarPlayer.SimHarness) cannot see the Infrastructure readers of the two content files it is pinned to, so it has its own small reader. These literals are
/// asserted here, on the production readers, and again in the harness tests on the harness' reader: if either reader ever disagreed with the other about a single value, one of the two tests fails.
/// </summary>
public class SimHarnessInputPinTests
{
    internal const string NonAutomatedHash = "2d3f7557750c65d920ee79ec0ff57121ae8b8990f13368ae4daa7520492dce2d";
    internal const string AutomatedHash = "7ad9776729b4a71d60af352d620df561d02f699e036fd6f4a4870b82635f4fa7";

    [Fact]
    public void The_production_readers_give_the_content_hashes_the_harness_reader_is_pinned_to()
    {
        Assert.Equal(NonAutomatedHash, PitchBranchFixture.NonAutomated.ContentHash);
        Assert.Equal(AutomatedHash, PitchBranchFixture.Automated.ContentHash);
        Assert.Equal(TuningV4Tests.GoldenHashV4, Gx.Tuning4.ContentHash);
    }
}
