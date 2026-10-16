using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-B): the player-input-set CONTENT VERSION 3. It adds the catcher's GameCalling and the pitcher's Control to the v2 entries and has its own identity prefix; the content version 1
/// and 2 identities are untouched. Expected identities are pinned literals computed by an independent Python implementation of the documented text format, and by a second implementation written in this
/// file; nothing here asks the production digest what it should be.
/// </summary>
public class GameCallingInputSetTests
{
    // sheets (p-a: fielding 10, speed 20, arm 30, framing 70, game calling 80, control 35), (p-b: 40, 50, 60, no catcher ratings, control 65) and (p-c: a pitcher with no hitter ratings, control 90)
    // computed by the independent Python implementation of the documented text
    private const string GoldenV3 = "da455d6ee5279a6df3dd91d0126792068c21a15a2184c705019f3828342f31fc";
    private const string GoldenV3GameCalling81 = "a2e7c9b4d560c888edba83ba7e9b3eee5da79bc5c6dd81a1d066d52a6615772b";
    private const string GoldenV3Control36 = "f0776f5004c14828e70c336117fbddb5632a5e656d63a6d16c5b7edea4813892";
    private const string GoldenV1 = "b266907778bf7c670f317920178dc38184e82c4a26c7d3e223dd70093ee3fd61";
    private const string GoldenV2 = "62bf50d2660c72de922aadee1ae989b790f0b2898c7cd5f1695662929d7aefda";

    private static readonly PlayerId A = PlayerId.Create("p-a").Value;
    private static readonly PlayerId B = PlayerId.Create("p-b").Value;
    private static readonly PlayerId C = PlayerId.Create("p-c").Value;

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static IEnumerable<PlayerSheet> Sheets(int callingA = 80, int controlA = 35, int framingA = 70) =>
    [
        Gc.Sheet(A, new Gc.Spec(controlA, (framingA, callingA), 10, 20, 30)),
        Gc.Sheet(B, new Gc.Spec(65, null, 40, 50, 60)),
        Gc.Sheet(C, new Gc.Spec(90, null, Hitter: false)),
    ];

    // ---------------------------------------------------------------- the v1 and v2 identities cannot move

    [Fact]
    public void Content_versions_1_and_2_keep_their_exact_identity_and_never_see_game_calling_or_control()
    {
        var v1 = Ok(PlayerInputSet.Create(Sheets()));
        var v2 = Ok(PlayerInputSet.CreateForFraming(Sheets()));

        Assert.Equal(GoldenV1, v1.Id);
        Assert.Equal(GoldenV2, v2.Id);
        Assert.Equal(GoldenV1, Sha("player-input-set-v1\np-a|fielding=10|speed=20|arm=30\np-b|fielding=40|speed=50|arm=60\np-c|fielding=-|speed=-|arm=-"));
        Assert.Equal(GoldenV2, Sha("player-input-set-v2\np-a|fielding=10|speed=20|arm=30|framing=70\np-b|fielding=40|speed=50|arm=60|framing=-\np-c|fielding=-|speed=-|arm=-|framing=-"));
        Assert.Equal((PlayerInputSet.ContentV1, PlayerInputSet.ContentV2), (v1.ContentVersion, v2.ContentVersion));
        // a v1 / v2 set can never hold a value its identity ignores
        foreach (var set in new[] { v1, v2 })
            Assert.All(set.Entries, e => Assert.Equal((null, null), (e.GameCalling, e.PitcherControl)));
        // ... so changing only those ratings does not move a v1 / v2 identity
        Assert.Equal(GoldenV1, Ok(PlayerInputSet.Create(Sheets(callingA: 1, controlA: 99))).Id);
        Assert.Equal(GoldenV2, Ok(PlayerInputSet.CreateForFraming(Sheets(callingA: 1, controlA: 99))).Id);
    }

    [Fact]
    public void The_existing_factories_never_create_content_version_3_implicitly()
    {
        Assert.Equal(PlayerInputSet.ContentV1, Ok(PlayerInputSet.Create(Sheets())).ContentVersion);
        Assert.Equal(PlayerInputSet.ContentV2, Ok(PlayerInputSet.CreateForFraming(Sheets())).ContentVersion);
        Assert.Equal(PlayerInputSet.ContentV3, Ok(PlayerInputSet.CreateForGameCalling(Sheets())).ContentVersion);
        Assert.Equal("player-input-set-v3", PlayerInputSet.ContentV3);
        Assert.Equal(3, new[] { PlayerInputSet.ContentV1, PlayerInputSet.ContentV2, PlayerInputSet.ContentV3 }.Distinct().Count());
    }

    // ---------------------------------------------------------------- content version 3

    [Fact]
    public void Content_version_3_has_its_own_identity_covering_game_calling_and_control_with_a_fixed_null_token()
    {
        var set = Ok(PlayerInputSet.CreateForGameCalling(Sheets()));

        Assert.Equal(GoldenV3, set.Id);
        Assert.Equal(GoldenV3, Sha("player-input-set-v3\np-a|fielding=10|speed=20|arm=30|framing=70|game_calling=80|control=35\np-b|fielding=40|speed=50|arm=60|framing=-|game_calling=-|control=65\np-c|fielding=-|speed=-|arm=-|framing=-|game_calling=-|control=90"));
        Assert.Equal(PlayerInputSet.ContentV3, set.ContentVersion);
        Assert.Equal((70, 80, 35), (set.Find(A)!.ReceivingFraming, set.Find(A)!.GameCalling, set.Find(A)!.PitcherControl));
        Assert.Equal(((int?)null, (int?)null, (int?)65), (set.Find(B)!.ReceivingFraming, set.Find(B)!.GameCalling, set.Find(B)!.PitcherControl));      // a role rating the player does not have stays null
        Assert.Equal((null, null, null, null, 90), (set.Find(C)!.Fielding, set.Find(C)!.Speed, set.Find(C)!.Arm, set.Find(C)!.ReceivingFraming, set.Find(C)!.PitcherControl));
        Assert.Null(set.Find(C)!.GameCalling);
        // the same sheets are a different identity in every content version
        Assert.Equal(3, new[] { GoldenV1, GoldenV2, GoldenV3 }.Distinct().Count());
        Assert.Equal(GoldenV3, set.Id);
    }

    [Fact]
    public void Changing_game_calling_or_control_or_framing_changes_the_v3_identity_and_each_value_belongs_to_its_player()
    {
        Assert.Equal(GoldenV3GameCalling81, Ok(PlayerInputSet.CreateForGameCalling(Sheets(callingA: 81))).Id);       // pinned: the game-calling token is in the text
        Assert.Equal(GoldenV3Control36, Ok(PlayerInputSet.CreateForGameCalling(Sheets(controlA: 36))).Id);           // pinned: the control token is in the text
        var ids = new[] { Sheets(), Sheets(callingA: 81), Sheets(controlA: 36), Sheets(framingA: 71) }.Select(s => Ok(PlayerInputSet.CreateForGameCalling(s)).Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        // 0 / 50 / 100 and "no rating" are four different identities for the same slot
        PlayerInputSet WithCatcher((int, int)? catcher) => Ok(PlayerInputSet.CreateForGameCalling([Gc.Sheet(A, new Gc.Spec(50, catcher))]));
        var calling = new[] { 0, 50, 100 }.Select(c => WithCatcher((50, c)).Id).Append(WithCatcher(null).Id).ToList();
        Assert.Equal(4, calling.Distinct().Count());
        // the rating belongs to ITS player
        Assert.NotEqual(Ok(PlayerInputSet.CreateForGameCalling([Gc.Sheet(A, new Gc.Spec(40, (50, 50))), Gc.Sheet(B, new Gc.Spec(60, (50, 50)))])).Id,
            Ok(PlayerInputSet.CreateForGameCalling([Gc.Sheet(A, new Gc.Spec(60, (50, 50))), Gc.Sheet(B, new Gc.Spec(40, (50, 50)))])).Id);
    }

    [Fact]
    public void The_v3_identity_is_deterministic_order_free_and_sensitive_to_membership()
    {
        var sheets = Sheets().ToList();
        Assert.Equal(Ok(PlayerInputSet.CreateForGameCalling(sheets)).Id, Ok(PlayerInputSet.CreateForGameCalling(sheets)).Id);
        Assert.Equal(GoldenV3, Ok(PlayerInputSet.CreateForGameCalling(Enumerable.Reverse(sheets))).Id);               // the order of the sheets is irrelevant (PlayerId ordinal order)
        Assert.Equal(GoldenV3, Ok(PlayerInputSet.CreateForGameCalling([sheets[2], sheets[0], sheets[1]])).Id);
        Assert.NotEqual(GoldenV3, Ok(PlayerInputSet.CreateForGameCalling(sheets.Take(2))).Id);                         // membership
        Assert.Equal(["p-a", "p-b", "p-c"], Ok(PlayerInputSet.CreateForGameCalling(Enumerable.Reverse(sheets))).Entries.Select(e => e.Player.Value));
    }

    [Fact]
    public void The_v3_text_uses_invariant_digits_and_ordinal_player_order_whatever_the_culture()
    {
        var upper = PlayerId.Create("p-B").Value;
        var lower = PlayerId.Create("p-a").Value;
        var set = Ok(PlayerInputSet.CreateForGameCalling([Gc.Sheet(lower, new Gc.Spec(7, (8, 9))), Gc.Sheet(upper, new Gc.Spec(70, (80, 90)))]));
        var expected = Sha("player-input-set-v3\np-B|fielding=50|speed=50|arm=50|framing=80|game_calling=90|control=70\np-a|fielding=50|speed=50|arm=50|framing=8|game_calling=9|control=7");
        Assert.Equal(expected, set.Id);                                                                                  // 'B' (0x42) sorts before 'a' (0x61) in ordinal order
        var previous = CultureInfo();
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");           // different digit shapes / calendar
            Assert.Equal(expected, Ok(PlayerInputSet.CreateForGameCalling([Gc.Sheet(lower, new Gc.Spec(7, (8, 9))), Gc.Sheet(upper, new Gc.Spec(70, (80, 90)))])).Id);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    private static System.Globalization.CultureInfo CultureInfo() => System.Globalization.CultureInfo.CurrentCulture;

    [Fact]
    public void Content_version_3_refuses_a_missing_list_a_missing_sheet_and_a_repeated_player()
    {
        Assert.Equal("PlayerInputSet.sheets_missing", Code(PlayerInputSet.CreateForGameCalling(null)));
        Assert.Equal("PlayerInputSet.sheet_missing", Code(PlayerInputSet.CreateForGameCalling([Gc.Sheet(A, new Gc.Spec()), null])));
        Assert.Equal("PlayerInputSet.duplicate_player", Code(PlayerInputSet.CreateForGameCalling([Gc.Sheet(A, new Gc.Spec(1)), Gc.Sheet(A, new Gc.Spec(2))])));
        Assert.True(PlayerInputSet.CreateForGameCalling([]).IsSuccess);
    }

    [Fact]
    public void A_v3_set_is_a_value_copy_of_plain_integers_and_keeps_no_sheet_delegate_or_mutable_collection()
    {
        var source = new List<PlayerSheet?>(Sheets());
        var set = Ok(PlayerInputSet.CreateForGameCalling(source));
        var (id, entries) = (set.Id, set.Entries);

        source.Clear();
        source.Add(Gc.Sheet(A, new Gc.Spec(1, (1, 1))));

        Assert.Equal(id, set.Id);
        Assert.Equal(3, set.Count);
        Assert.Equal(entries, set.Entries);
        Assert.Equal(typeof(ImmutableArray<PlayerRunFieldInput>), set.Entries.GetType());
        // the published entry type holds only a PlayerId and plain nullable integers: no sheet, no reference to a mutable object
        var types = typeof(PlayerRunFieldInput).GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.Name != "EqualityContract").Select(p => p.PropertyType).Distinct().ToList();
        Assert.Equal(new[] { typeof(PlayerId), typeof(int?) }.OrderBy(t => t.FullName), types.OrderBy(t => t.FullName));
        var fields = typeof(PlayerInputSet).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(f => f.FieldType).ToList();
        Assert.DoesNotContain(fields, t => t == typeof(PlayerSheet) || typeof(System.Delegate).IsAssignableFrom(t));
        Assert.Contains("player-input-set-v3", set.ToString());
    }

    // ---------------------------------------------------------------- the required-input policy per model and mode

    [Fact]
    public void The_catcher_input_is_required_by_model_and_mode_exactly_as_the_policy_table_says()
    {
        var (v1, v2, v3, v4) = (Gx.Tuning1.Model, Gx.Tuning2.Model, Gx.Tuning3.Model, Gx.Tuning4.Model);
        var automated = PitchBranchFixture.Automated;
        var manual = PitchBranchFixture.NonAutomated;

        // (model, rules) -> (framing required, game calling required, any catcher input, pitcher control pinned)
        foreach (var (model, rules, framing, calling, any, pitcher) in new (ProbabilityModel, GameRuleSnapshot, bool, bool, bool, bool)[]
        {
            (v1, automated, false, false, false, false), (v1, manual, false, false, false, false),
            (v2, automated, false, false, false, false), (v2, manual, false, false, false, false),
            (v3, automated, false, false, false, false), (v3, manual, true, false, true, false),
            (v4, automated, false, true, true, true), (v4, manual, true, true, true, true),
        })
        {
            var label = $"{model.Version} {rules.CalledPitchMode}";
            Assert.Equal(framing, FrozenCatcherInput.FramingRequired(model, rules));
            Assert.Equal(calling, FrozenCatcherInput.GameCallingRequired(model));
            Assert.Equal(any, FrozenCatcherInput.Required(model, rules));
            Assert.Equal(pitcher, FrozenPitcherInput.Required(model));
            Assert.True(label.Length > 0);
        }

        Assert.False(FrozenCatcherInput.Required(null, manual));
        Assert.False(FrozenPitcherInput.Required(null));
        Assert.False(FrozenCatcherInput.FramingRequired(v3, null));          // a missing rules snapshot is never read as non_automated
    }

    [Fact]
    public void The_catcher_reads_only_the_ratings_the_policy_requires_and_the_content_version_the_model_reads()
    {
        var automated = Gc.Game(PitchBranchFixture.Automated, calling: 77, framing: 66, approach: false);
        var manual = Gc.Game(PitchBranchFixture.NonAutomated, calling: 77, framing: 66, approach: false);
        var stateA = Aggregate(automated.Driver, 1).State;
        var stateM = Aggregate(manual.Driver, 1).State;

        var a = Ok(FrozenCatcherInput.Build(stateA, automated.Set, Gx.Tuning4.Model, "t"));
        Assert.Equal((automated.Catcher, 77, null), (a.Catcher, a.GameCalling, a.ReceivingFraming));                     // automated: Game Calling only; the set's framing is never read
        var m = Ok(FrozenCatcherInput.Build(stateM, manual.Set, Gx.Tuning4.Model, "t"));
        Assert.Equal((manual.Catcher, 77, 66), (m.Catcher, m.GameCalling, m.ReceivingFraming));                          // non_automated: both

        // the content version the model reads: v4 needs 3 (a v2 set carries no game calling), v3 needs exactly 2
        Assert.Equal("t.catcher_input_format", Code(FrozenCatcherInput.Build(stateM, Fr.SetV2(_ => 50), Gx.Tuning4.Model, "t")));
        Assert.Equal("t.catcher_input_format", Code(FrozenCatcherInput.Build(stateM, Fr.SetV1(_ => 50), Gx.Tuning4.Model, "t")));
        Assert.Equal("t.catcher_input_format", Code(FrozenCatcherInput.Build(stateM, manual.Set, Gx.Tuning3.Model, "t")));       // a v3-content set is not read by model v3
        Assert.Equal("t.catcher_input_unexpected", Code(FrozenCatcherInput.Build(stateA, automated.Set, Gx.Tuning3.Model, "t")));  // nothing to read: model v3 + automated
        Assert.Equal("t.catcher_input_unexpected", Code(FrozenCatcherInput.Build(stateA, automated.Set, Gx.Tuning2.Model, "t")));
    }

    [Fact]
    public void A_missing_or_invalid_required_rating_is_a_typed_error_and_is_never_replaced_by_fifty()
    {
        var rules = PitchBranchFixture.NonAutomated;
        var probe = Fr.AtStart(rules, null).State;
        var (pitcher, catcher) = (Gc.PitcherOf(probe), Gc.CatcherOf(probe));
        PlayerInputSet SetWith(Func<PlayerId, Gc.Spec> spec) => Gc.SetV3(spec);
        var state = Aggregate(Gc.Game(rules, approach: false).Driver, 1).State;

        // the catcher has no catcher ratings at all: non_automated needs Framing first, automated needs Game Calling
        var noCatcherRatings = SetWith(id => new Gc.Spec(50, id == catcher ? null : (50, 50)));
        Assert.Equal("t.catcher_ratings_missing", Code(FrozenCatcherInput.Build(state, noCatcherRatings, Gx.Tuning4.Model, "t")));
        var automatedState = Aggregate(Gc.Game(PitchBranchFixture.Automated, approach: false).Driver, 1).State;
        Assert.Equal("t.catcher_game_calling_missing", Code(FrozenCatcherInput.Build(automatedState, noCatcherRatings, Gx.Tuning4.Model, "t")));
        // the catcher is not in the set at all
        var withoutCatcher = Ok(PlayerInputSet.CreateForGameCalling(Gc.Sheets(id => new Gc.Spec(50, (50, 50))).Where(s => s.Id != catcher)));
        Assert.Equal("t.catcher_input_missing", Code(FrozenCatcherInput.Build(state, withoutCatcher, Gx.Tuning4.Model, "t")));
        // the pitcher: not in the set, or without pitching ratings
        var withoutPitcher = Ok(PlayerInputSet.CreateForGameCalling(Gc.Sheets(id => new Gc.Spec(50, (50, 50))).Where(s => s.Id != pitcher)));
        Assert.Equal("t.pitcher_input_missing", Code(FrozenPitcherInput.Build(state, withoutPitcher, "t")));
        var noPitching = SetWith(id => new Gc.Spec(id == pitcher ? null : 50, (50, 50)));
        Assert.Equal("t.pitcher_ratings_missing", Code(FrozenPitcherInput.Build(state, noPitching, "t")));
        // the pitcher input only reads content version 3
        Assert.Equal("t.pitcher_input_format", Code(FrozenPitcherInput.Build(state, Fr.SetV2(_ => 50), "t")));
        Assert.Equal("t.pitcher_input_format", Code(FrozenPitcherInput.Build(state, Fr.SetV1(_ => 50), "t")));
    }
}
