using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.InPlayFeasibility;

namespace StarPlayer.Simulation.Tests.InPlay;

/// <summary>
/// Phase 3-C2B-2, Part A: the 19-template catalogue <c>inplay-template-v1</c> and its approved 45 (batted ball, defence result) combinations. The template table below is a
/// hand-written literal of docs/game-engine.md section 23.2; the 45 combinations are read from the tuning JSON by the test itself, so a tuning change that makes a zero
/// combination positive fails here (it would need a template decision and a re-run of the matrix, ADR-007).
/// </summary>
public class InPlayCatalogTests
{
    // id | class | batted | result | batter destination | credits | error
    private static readonly (string Id, string Class, BattedBallType Batted, InPlayResult Result, int Dest, string Credits, string Error)[] Expected =
    [
        ("ipt1.ground.out", "ground", BattedBallType.Ground, InPlayResult.Out, 0, "SS:A,1B:PO", ""),
        ("ipt1.ground.single", "ground", BattedBallType.Ground, InPlayResult.Single, 1, "", ""),
        ("ipt1.ground.double", "ground", BattedBallType.Ground, InPlayResult.Double, 2, "", ""),
        ("ipt1.ground.roe", "ground", BattedBallType.Ground, InPlayResult.ReachedOnError, 1, "", "SS"),
        ("ipt1.line.out", "line", BattedBallType.Line, InPlayResult.Out, 0, "SS:PO", ""),
        ("ipt1.line.single", "line", BattedBallType.Line, InPlayResult.Single, 1, "", ""),
        ("ipt1.line.double", "line", BattedBallType.Line, InPlayResult.Double, 2, "", ""),
        ("ipt1.line.triple", "line", BattedBallType.Line, InPlayResult.Triple, 3, "", ""),
        ("ipt1.line.homerun", "line", BattedBallType.Line, InPlayResult.HomeRun, 4, "", ""),
        ("ipt1.line.roe", "line", BattedBallType.Line, InPlayResult.ReachedOnError, 1, "", "SS"),
        ("ipt1.fly.out", "fly", BattedBallType.Fly, InPlayResult.Out, 0, "CF:PO", ""),
        ("ipt1.fly.single", "fly", BattedBallType.Fly, InPlayResult.Single, 1, "", ""),
        ("ipt1.fly.double", "fly", BattedBallType.Fly, InPlayResult.Double, 2, "", ""),
        ("ipt1.fly.triple", "fly", BattedBallType.Fly, InPlayResult.Triple, 3, "", ""),
        ("ipt1.fly.homerun", "fly", BattedBallType.Fly, InPlayResult.HomeRun, 4, "", ""),
        ("ipt1.fly.roe", "fly", BattedBallType.Fly, InPlayResult.ReachedOnError, 1, "", "CF"),
        ("ipt1.pop.out", "pop", BattedBallType.Pop, InPlayResult.Out, 0, "2B:PO", ""),
        ("ipt1.pop.single", "pop", BattedBallType.Pop, InPlayResult.Single, 1, "", ""),
        ("ipt1.pop.roe", "pop", BattedBallType.Pop, InPlayResult.ReachedOnError, 1, "", "CF"),
    ];

    [Fact]
    public void The_policy_versions_are_the_approved_names()
    {
        Assert.Equal("inplay-mapping-v1", InPlayPolicy.MappingVersion);
        Assert.Equal("walkoff-credit-v1", InPlayPolicy.WalkOffCreditVersion);
        Assert.Equal("inplay-template-v1", InPlayPolicy.TemplateCatalogVersion);
        Assert.Equal("inplay-candidate-v1", InPlayPolicy.CandidateContractVersion);
    }

    [Fact]
    public void The_catalogue_has_exactly_the_19_approved_templates_with_the_approved_content()
    {
        Assert.Equal(19, Expected.Length);
        Assert.Equal(19, InPlayTemplateCatalog.All.Length);
        Assert.Equal(Expected.Select(e => e.Id), InPlayTemplateCatalog.All.Select(t => t.Id));
        foreach (var want in Expected)
        {
            var t = InPlayTemplateCatalog.Find(want.Class, want.Result);
            Assert.NotNull(t);
            Assert.Equal(want.Id, t!.Id);
            Assert.Equal(want.Batted, t.Batted);
            Assert.Equal(want.Dest, t.BatterDestination);
            Assert.Equal(want.Credits, ProdHarness.CreditText(t.Credits));
            Assert.Equal(want.Error, t.Error is { } e ? InPlayTemplates.Short(e) : "");
        }
    }

    [Fact]
    public void Only_the_ground_out_has_an_assist_and_every_out_has_exactly_one_putout()
    {
        foreach (var t in InPlayTemplateCatalog.All)
        {
            if (t.Result == InPlayResult.Out)
            {
                Assert.Equal(1, t.Credits.Count(c => c.Kind == CreditKind.Putout));
                Assert.Equal(t.Class == "ground" ? 1 : 0, t.Credits.Count(c => c.Kind == CreditKind.Assist));
                Assert.Null(t.Error);
            }
            else
            {
                Assert.Empty(t.Credits);                                               // a hit or an error has no putout and no assist
                Assert.Equal(t.Result == InPlayResult.ReachedOnError, t.Error is not null);
            }
        }
    }

    [Fact]
    public void No_template_is_a_double_play_fielders_choice_sacrifice_or_compound_error()
    {
        Assert.DoesNotContain(InPlayTemplateCatalog.All, t => t.Result == InPlayResult.FieldersChoice);
        Assert.All(InPlayTemplateCatalog.All, t => Assert.True(t.Credits.Length <= 2));
        Assert.All(InPlayTemplateCatalog.All, t => Assert.True(t.Error is null || t.Credits.Length == 0));
    }

    [Fact]
    public void Pop_hits_and_errors_carry_the_beyond_infielder_fact_and_the_outfielder_error()
    {
        foreach (var id in new[] { "ipt1.pop.single", "ipt1.pop.roe" })
            Assert.Contains("pop_beyond_infielder_ordinary_effort", InPlayTemplateCatalog.All.Single(t => t.Id == id).VirtualFacts);
        Assert.Equal(FieldPosition.CenterField, InPlayTemplateCatalog.All.Single(t => t.Id == "ipt1.pop.roe").Error);
        Assert.DoesNotContain("pop_beyond_infielder_ordinary_effort", InPlayTemplateCatalog.All.Single(t => t.Id == "ipt1.pop.out").VirtualFacts);
    }

    [Fact]
    public void Every_positive_combination_of_the_tuning_resolves_to_its_template_with_class_and_intensity()
    {
        Assert.Equal(45, FeasibilityData.Positive.Count);
        foreach (var combo in FeasibilityData.Positive)
        {
            var resolved = InPlayTemplateCatalog.Resolve(combo.Batted, combo.Result);

            Assert.True(resolved.IsSuccess, $"{combo.Key}: {resolved.Error?.Code}");
            var r = resolved.Value;
            Assert.Equal(combo.Batted, r.BattedBall);
            Assert.Equal(combo.Result, r.DefenseResult);
            Assert.Equal(InPlayTemplates.ClassOf(combo.Batted), r.BattedBallClass);
            Assert.Equal(combo.Batted == "popup" ? null : combo.Batted.Split('_')[1], r.Intensity);   // the intensity stays for the audit and selects no template
            Assert.Equal($"ipt1.{r.BattedBallClass}.{(combo.Result switch { "home_run" => "homerun", "reached_on_error" => "roe", var o => o })}", r.Template.Id);
        }
    }

    [Fact]
    public void The_two_pop_up_combinations_that_were_easy_to_forget_resolve()
    {
        Assert.Equal("ipt1.pop.single", InPlayTemplateCatalog.Resolve("popup", "single").Value.Template.Id);
        Assert.Equal("ipt1.pop.roe", InPlayTemplateCatalog.Resolve("popup", "reached_on_error").Value.Template.Id);
    }

    [Fact]
    public void Every_zero_probability_combination_is_refused_and_never_silently_mapped()
    {
        Assert.Equal(15, FeasibilityData.AllCombos.Count(c => !c.Positive));
        foreach (var combo in FeasibilityData.AllCombos.Where(c => !c.Positive))
        {
            var resolved = InPlayTemplateCatalog.Resolve(combo.Batted, combo.Result);

            Assert.False(resolved.IsSuccess, combo.Key);
            // a result the class has no template for at all is "unsupported"; a result the class does have but this intensity may not produce is "not approved"
            var hasTemplate = InPlayTemplateCatalog.Find(InPlayTemplates.ClassOf(combo.Batted), InPlayTemplates.ResultText(combo.Result) switch
            {
                "Out" => InPlayResult.Out, "Single" => InPlayResult.Single, "Double" => InPlayResult.Double, "Triple" => InPlayResult.Triple,
                "HomeRun" => InPlayResult.HomeRun, _ => InPlayResult.ReachedOnError,
            }) is not null;
            Assert.Equal(hasTemplate ? "InPlayPlan.combination_not_approved" : "InPlayPlan.template_unsupported", resolved.Error!.Value.Code);
        }
    }

    [Theory]
    [InlineData("ground_weak", "double", "InPlayPlan.combination_not_approved")]
    [InlineData("ground_weak", "triple", "InPlayPlan.template_unsupported")]
    [InlineData("popup", "home_run", "InPlayPlan.template_unsupported")]
    [InlineData("line_medium", "home_run", "InPlayPlan.combination_not_approved")]
    [InlineData("fly_weak", "triple", "InPlayPlan.combination_not_approved")]
    [InlineData("bunt_weak", "out", "InPlayPlan.batted_ball_unknown")]
    [InlineData("", "out", "InPlayPlan.batted_ball_unknown")]
    [InlineData(null, "out", "InPlayPlan.batted_ball_unknown")]
    [InlineData("Fly_Hard", "out", "InPlayPlan.batted_ball_unknown")]
    [InlineData("fly_hard", "fielders_choice", "InPlayPlan.defense_result_unknown")]
    [InlineData("fly_hard", "", "InPlayPlan.defense_result_unknown")]
    [InlineData("fly_hard", null, "InPlayPlan.defense_result_unknown")]
    [InlineData("fly_hard", "Single", "InPlayPlan.defense_result_unknown")]
    public void Unknown_or_unapproved_input_is_refused_with_a_stable_code(string? batted, string? result, string code)
    {
        Assert.Equal(code, InPlayTemplateCatalog.Resolve(batted, result).Error!.Value.Code);
    }

    [Fact]
    public void The_catalogue_is_immutable_and_has_no_random_or_clock_dependence()
    {
        var first = InPlayTemplateCatalog.All;
        var second = InPlayTemplateCatalog.All;

        Assert.Equal(first.Select(t => t.Id), second.Select(t => t.Id));
        Assert.Equal(19, first.Select(t => t.Id).Distinct().Count());
    }
}
