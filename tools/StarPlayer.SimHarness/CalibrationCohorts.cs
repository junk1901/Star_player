using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using static StarPlayer.SimHarness.NeutralCohort;

namespace StarPlayer.SimHarness;

/// <summary>
/// The two F-C2-B calibration cohorts of ADR-008 (FC-D1, FC-D2; proposal §3.3 / §4). Both are two mirror clubs (<c>club-sim-a</c> / <c>club-sim-h</c>, same structure, other ids), nine right-handed
/// batters in the <c>neutral-v1</c> designated-hitter lineup shape, and a seven-man starting rotation of 3 RHP (slots 0-2) : 4 LHP (slots 3-6) plus one unused reliever, so a game is wholly
/// same-hand (rotation slot 0-2) or opposite-hand (3-6). These are the F-C2-B instances; their <see cref="CohortInputs.InputsHash"/> is frozen only in the Final Execution Freeze.
/// </summary>
public static class CalibrationCohorts
{
    public const string NeutralMixedId = "neutral-mixed-v1";
    public const string VirtualLeagueId = "virtual-league-v1";

    /// <summary>FC-D2.1: the only rating value set of <c>virtual-league-v1</c> (mean 50, symmetric about 50).</summary>
    public static readonly int[] VirtualLeagueValues = [35, 40, 45, 50, 50, 50, 55, 60, 65];

    /// <summary>Proposal §4.1 cyclic shifts per hitter ability (Contact, Power, Eye, Speed, Fielding, Arm): batter i has value <c>v[(i + shift) mod 9]</c>.</summary>
    public static readonly int[] VirtualLeagueShifts = [0, 2, 4, 1, 3, 5];

    /// <summary>Proposal §4.2 starters (Stuff, Control, Velocity) for rotation slots 0-6; each handedness group averages 50.</summary>
    public static readonly (int Stuff, int Control, int Velocity)[] VirtualLeagueStarters =
        [(40, 50, 60), (50, 60, 40), (60, 40, 50), (35, 45, 55), (45, 55, 65), (55, 65, 35), (65, 35, 45)];

    public const int RotationSize = 7;
    public const int RightHandedStarters = 3;

    /// <summary>FC-D1: every rating 50, all batters bat right, rotation <c>sp7-3r4l-v1</c>; one bench hitter and one reliever per team (neither is used: the harness has no substitution).</summary>
    public static CohortInputs NeutralMixed() => Build(NeutralMixedId, CohortKind.Neutral, "nm", "sp7-3r4l-v1", false, true,
        _ => new HitterRatings(S(), S(), S(), S(), S(), S()), _ => (50, 50, 50),
        "모든 능력치 50, 전원 우타, 선발 7명 로테이션 우완 3 : 좌완 4(중립 혼합 코호트)");

    /// <summary>FC-D2: value set 35..65 with cyclic per-ability shifts, starter and batting-order rotation <c>sp7-3r4l-bo9-v1</c> (63-game cycle); catcher tools, reliever and endurance 50.</summary>
    public static CohortInputs VirtualLeague() => Build(VirtualLeagueId, CohortKind.VirtualLeague, "vl", "sp7-3r4l-bo9-v1", true, false,
        i =>
        {
            int V(int ability) => VirtualLeagueValues[(i + VirtualLeagueShifts[ability]) % VirtualLeagueValues.Length];
            return new HitterRatings(S(V(0)), S(V(1)), S(V(2)), S(V(3)), S(V(4)), S(V(5)));
        },
        slot => VirtualLeagueStarters[slot],
        "가상 리그 값 35~65 순환 배치, 미러 팀, 선발 7명(우완 3 : 좌완 4)·타순 로테이션(가상 리그 코호트)");

    private static CohortInputs Build(string id, CohortKind kind, string prefix, string rotationId, bool rotateBattingOrder, bool benchHitter, Func<int, HitterRatings> hitter,
        Func<int, (int Stuff, int Control, int Velocity)> starter, string description)
    {
        var sheets = new List<PlayerSheet>();
        (TeamSetup Team, ImmutableArray<PlayerId> Starters) Team(string label, string club)
        {
            var lineup = new List<LineupSlot>();
            for (var i = 0; i < LineupPositions.Length; i++)
            {
                var pid = Pid($"{prefix}-{label}-b{i + 1}");
                var catcher = LineupPositions[i] == FieldPosition.Catcher ? new CatcherRatings(S(), S(), S(), S()) : null;
                sheets.Add(MakeSheet(pid, club, $"가상 {label} 타자 {i + 1}", ThrowingHand.Right, hitter(i), null, catcher));
                lineup.Add(new LineupSlot(i + 1, pid, LineupPositions[i]));
            }

            var starters = new List<PlayerId>();
            for (var slot = 0; slot < RotationSize; slot++)
            {
                var pid = Pid($"{prefix}-{label}-sp{slot + 1}");
                var (stuff, control, velocity) = starter(slot);
                sheets.Add(MakeSheet(pid, club, $"가상 {label} 선발 {slot + 1}", slot < RightHandedStarters ? ThrowingHand.Right : ThrowingHand.Left, null, Pitcher(stuff, control, velocity), null));
                starters.Add(pid);
            }

            var reliever = Pid($"{prefix}-{label}-rp");
            sheets.Add(MakeSheet(reliever, club, $"가상 {label} 구원", ThrowingHand.Right, null, Pitcher(50, 50, 50), null));
            var bench = starters.Skip(1).ToList();
            if (benchHitter)
            {
                var bh = Pid($"{prefix}-{label}-bh");
                sheets.Add(MakeSheet(bh, club, $"가상 {label} 벤치 타자", ThrowingHand.Right, new HitterRatings(S(), S(), S(), S(), S(), S()), null, null));
                bench.Add(bh);
            }

            bench.Add(reliever);
            return (new TeamSetup(lineup, starters[0], bench), [.. starters]);
        }

        var away = Team("a", "club-sim-a");
        var home = Team("h", "club-sim-h");
        return Must(CohortInputs.Create(id, kind, description, away.Team, home.Team, sheets, new CohortRotation(rotationId, away.Starters, home.Starters, rotateBattingOrder)));
    }
}
