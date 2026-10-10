using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using static StarPlayer.Domain.Tests.TestData;

namespace StarPlayer.Domain.Tests;

public class ValueObjectTests
{
    // ---- ids ----

    [Fact]
    public void Every_id_type_validates_through_the_same_rules_and_keeps_its_own_code_prefix()
    {
        Assert.Equal("ClubId.empty", ClubId.Create("").Error!.Value.Code);
        Assert.Equal("StadiumId.not_canonical", StadiumId.Create(" s1").Error!.Value.Code);
        Assert.Equal("ParkFactorProfileId.invalid_character", ParkFactorProfileId.Create("a b").Error!.Value.Code);
        Assert.Equal("AmateurTeamId.too_long", AmateurTeamId.Create(new string('x', 65)).Error!.Value.Code);
        Assert.Equal("RuleProfileId.invalid_character", RuleProfileId.Create("규칙").Error!.Value.Code);
        Assert.Equal("PlayerId.invalid_character", PlayerId.Create("p/1").Error!.Value.Code);
        Assert.True(PlayerId.Create(new string('x', 64)).IsSuccess);
        Assert.True(PlayerId.Create("player_2032_0841").IsSuccess);
    }

    [Fact]
    public void Same_text_in_different_id_types_is_not_interchangeable_but_equal_inside_one_type()
    {
        Assert.Equal(P("x-1"), P("x-1"));
        Assert.NotEqual(P("x-1"), P("x-2"));
        Assert.Equal("x-1", ClubId.Create("x-1").Value.ToString());
        // default(PlayerId) is "no id": it never equals a created id.
        Assert.NotEqual(default, P("x-1"));
    }

    // ---- skill scale ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(100)]
    public void Skill_accepts_the_whole_inclusive_0_to_100_scale(int value) => Assert.Equal(value, SkillValue.Create(value).Value.Value);

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Skill_rejects_below_0_and_above_100(int value)
    {
        var result = SkillValue.Create(value, "컨택");

        Assert.False(result.IsSuccess);
        Assert.Equal("SkillValue.out_of_range", result.Error!.Value.Code);
    }

    [Fact]
    public void Cheat_scale_is_still_the_0_to_100_skill_scale_not_a_creation_budget()
    {
        // Cheat creation lifts the recommended-point limit, never the 0-100 scale.
        var allMax = new HitterRatings(S(100), S(100), S(100), S(100), S(100), S(100));

        Assert.All(Ratings.Create(allMax, null, null).Value.Skills(), s => Assert.Equal(100, s.Value.Value));
        Assert.False(SkillValue.Create(150).IsSuccess);
    }

    // ---- ratings shape ----

    [Fact]
    public void Ratings_kind_is_derived_from_the_rating_sets_that_exist()
    {
        Assert.Equal(PlayerKind.Hitter, Ratings.Create(Hitter(), null, null).Value.Kind);
        Assert.Equal(PlayerKind.Pitcher, Ratings.Create(null, Pitcher(), null).Value.Kind);
        Assert.Equal(PlayerKind.TwoWay, Ratings.Create(Hitter(), Pitcher(), null).Value.Kind);
    }

    [Fact]
    public void Ratings_need_a_role_and_catcher_tools_need_hitter_ratings()
    {
        Assert.Equal("Ratings.no_role", Ratings.Create(null, null, null).Error!.Value.Code);
        Assert.Equal("Ratings.catcher_without_hitter", Ratings.Create(null, Pitcher(), Catcher(50)).Error!.Value.Code);
    }

    [Fact]
    public void Hitter_has_six_pitcher_four_and_catcher_four_skills_in_fixed_order()
    {
        var all = Ratings.Create(Hitter(10), Pitcher(20), Catcher(30)).Value.Skills();

        Assert.Equal(
            [SkillKey.Contact, SkillKey.Power, SkillKey.Eye, SkillKey.Speed, SkillKey.Fielding, SkillKey.Arm,
             SkillKey.Stuff, SkillKey.Control, SkillKey.Velocity, SkillKey.Endurance,
             SkillKey.ReceivingFraming, SkillKey.Blocking, SkillKey.PopTime, SkillKey.GameCalling],
            all.Select(s => s.Key));
    }

    [Fact]
    public void Repertoire_needs_a_pitch_and_rejects_duplicates()
    {
        Assert.Equal("PitchRepertoire.empty", PitchRepertoire.Create([]).Error!.Value.Code);
        Assert.Equal("PitchRepertoire.duplicate",
            PitchRepertoire.Create([new PitchRating(PitchType.Slider, S(40)), new PitchRating(PitchType.Slider, S(50))]).Error!.Value.Code);
        Assert.Equal([PitchType.Fastball, PitchType.Curve],
            PitchRepertoire.Create([new PitchRating(PitchType.Curve, S(40)), new PitchRating(PitchType.Fastball, S(50))]).Value.Pitches.Select(p => p.Type));
    }

    [Fact]
    public void Pop_time_skill_and_observed_pop_time_are_different_units_and_types()
    {
        // Higher-is-better 0-100 skill vs lower-is-faster milliseconds.
        var skill = new CatcherRatings(S(50), S(50), S(95), S(50));
        Assert.Equal(95, skill.PopTime.Value);
        Assert.Equal(1950, PopTimeObservation.FromMilliseconds(1950).Value.Milliseconds);
        Assert.Equal("PopTimeObservation.out_of_range", PopTimeObservation.FromMilliseconds(95).Error!.Value.Code);
        Assert.NotEqual(typeof(SkillValue), typeof(PopTimeObservation));
    }

    // ---- age ----

    [Theory]
    [InlineData("2000-05-17", "2020-05-16", 19)] // the day before the birthday
    [InlineData("2000-05-17", "2020-05-17", 20)] // on the birthday
    [InlineData("2000-05-17", "2020-05-18", 20)]
    [InlineData("2000-05-17", "2000-05-17", 0)] // day of birth
    [InlineData("2000-12-31", "2001-12-30", 0)]
    [InlineData("2000-12-31", "2001-12-31", 1)]
    [InlineData("2000-01-01", "2030-12-31", 30)]
    [InlineData("2000-02-29", "2023-02-28", 22)] // leap-day birthday falls on Mar 1 in common years
    [InlineData("2000-02-29", "2023-03-01", 23)]
    [InlineData("2000-02-29", "2024-02-28", 23)]
    [InlineData("2000-02-29", "2024-02-29", 24)] // real leap day
    public void Age_is_completed_years_with_exact_boundaries(string birth, string on, int expected)
        => Assert.Equal(expected, AgeCalculator.AgeOn(D(birth), D(on)).Value);

    [Fact]
    public void Age_before_birth_is_an_error()
    {
        var result = AgeCalculator.AgeOn(D("2000-05-17"), D("2000-05-16"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Age.before_birth", result.Error!.Value.Code);
    }

    // ---- innings as outs ----

    [Fact]
    public void One_hundred_twelve_outs_display_as_thirty_seven_and_one()
    {
        Assert.Equal("37.1", InningsOuts.FromOuts(112).Value.ToString());
        Assert.Equal(112, InningsOuts.Parse("37.1").Value.Outs);
        Assert.Equal(InningsOuts.FromOuts(112).Value, InningsOuts.FromParts(37, 1).Value);
    }

    [Theory]
    [InlineData(0, "0.0")]
    [InlineData(1, "0.1")]
    [InlineData(2, "0.2")]
    [InlineData(3, "1.0")]
    [InlineData(111, "37.0")]
    [InlineData(113, "37.2")]
    public void Outs_display_round_trips_through_parse(long outs, string text)
    {
        Assert.Equal(text, InningsOuts.FromOuts(outs).Value.ToString());
        Assert.Equal(outs, InningsOuts.Parse(text).Value.Outs);
    }

    [Theory]
    [InlineData("37.3")]
    [InlineData("37.10")]
    [InlineData("37.")]
    [InlineData(".1")]
    [InlineData("-1.0")]
    [InlineData("1.1.1")]
    [InlineData("abc")]
    [InlineData("")]
    public void Innings_text_with_a_decimal_fraction_or_bad_shape_is_rejected(string text)
        => Assert.Equal("InningsOuts.invalid_text", InningsOuts.Parse(text).Error!.Value.Code);

    [Fact]
    public void Innings_are_summed_as_outs_not_as_decimals()
    {
        // 6.2 + 0.1 is 7.0 innings, not 6.3.
        var sum = InningsOuts.Parse("6.2").Value.Add(InningsOuts.Parse("0.1").Value).Value;

        Assert.Equal("7.0", sum.ToString());
        Assert.False(InningsOuts.FromOuts(-1).IsSuccess);
        Assert.False(InningsOuts.FromParts(1, 3).IsSuccess);
        Assert.False(InningsOuts.FromOuts(long.MaxValue).Value.Add(InningsOuts.FromOuts(1).Value).IsSuccess);
    }

    // ---- money ----

    [Fact]
    public void Money_arithmetic_is_checked_integer_won()
    {
        var a = Money.FromWon(1_000_000_000);

        Assert.Equal(1_500_000_000, a.Add(Money.FromWon(500_000_000)).Value.Won);
        Assert.Equal(-1, Money.Zero.Subtract(Money.FromWon(1)).Value.Won);
        Assert.Equal("Money.overflow", Money.FromWon(long.MaxValue).Add(Money.FromWon(1)).Error!.Value.Code);
        Assert.Equal("Money.overflow", Money.FromWon(long.MinValue).Subtract(Money.FromWon(1)).Error!.Value.Code);
        Assert.Equal("Money.overflow", Money.FromWon(long.MaxValue).Multiply(2).Error!.Value.Code);
        Assert.Equal("1,000,000,000원", a.ToString());
    }

    [Theory]
    [InlineData(1005, 1, 10, MoneyRounding.TowardZero, 100)] // 100.5
    [InlineData(1005, 1, 10, MoneyRounding.HalfAwayFromZero, 101)]
    [InlineData(-1005, 1, 10, MoneyRounding.TowardZero, -100)]
    [InlineData(-1005, 1, 10, MoneyRounding.HalfAwayFromZero, -101)]
    [InlineData(1004, 1, 10, MoneyRounding.HalfAwayFromZero, 100)]
    [InlineData(1000, 15, 100, MoneyRounding.HalfAwayFromZero, 150)]
    public void Money_scaling_uses_the_stated_rounding_rule(long won, long numerator, long denominator, MoneyRounding rounding, long expected)
        => Assert.Equal(expected, Money.FromWon(won).Scale(numerator, denominator, rounding).Value.Won);

    [Fact]
    public void Money_scaling_is_exact_for_huge_values_and_rejects_bad_input()
    {
        Assert.Equal(long.MaxValue / 2, Money.FromWon(long.MaxValue - 1).Scale(1, 2, MoneyRounding.TowardZero).Value.Won);
        Assert.Equal("Money.zero_denominator", Money.FromWon(1).Scale(1, 0, MoneyRounding.TowardZero).Error!.Value.Code);
        Assert.Equal("Money.overflow", Money.FromWon(long.MaxValue).Scale(2, 1, MoneyRounding.TowardZero).Error!.Value.Code);
    }
}
