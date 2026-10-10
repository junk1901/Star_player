using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Players;

/// <summary>
/// Everything about one player that a visibility policy may be asked to project: identity, base ratings, transient state,
/// proficiency and status. The hidden <see cref="DevelopmentProfile"/> is intentionally NOT a member, so exclusion from
/// public views holds by construction. Jersey numbers and club contracts are separate owners as well.
/// </summary>
public sealed record PlayerSheet
{
    public Player Player { get; }
    public Ratings Ratings { get; }
    public PlayerState State { get; }
    public ProficiencyProfile Proficiency { get; }
    public PlayerStatus Status { get; }

    private PlayerSheet(Player player, Ratings ratings, PlayerState state, ProficiencyProfile proficiency, PlayerStatus status)
    {
        Player = player;
        Ratings = ratings;
        State = state;
        Proficiency = proficiency;
        Status = status;
    }

    public PlayerId Id => Player.Id;

    public PlayerKind Kind => Ratings.Kind;

    public static Result<PlayerSheet> Create(Player player, Ratings ratings, PlayerState state, ProficiencyProfile proficiency, PlayerStatus status)
    {
        var preferred = proficiency.PreferredPosition;
        if (preferred == FieldPosition.Pitcher && ratings.Pitcher is null)
            return Result<PlayerSheet>.Failure(new Error("PlayerSheet.pitcher_without_pitching", $"{player.Id} 선수의 선호 포지션이 투수인데 투수 능력치가 없습니다."));
        if (preferred != FieldPosition.Pitcher && ratings.Hitter is null)
            return Result<PlayerSheet>.Failure(new Error("PlayerSheet.fielder_without_hitting", $"{player.Id} 선수의 선호 포지션이 야수/지명타자인데 타자 능력치가 없습니다."));
        return Result<PlayerSheet>.Success(new PlayerSheet(player, ratings, state, proficiency, status));
    }
}
