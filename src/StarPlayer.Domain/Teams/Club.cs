using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Teams;

/// <summary>Stable franchise identity (C06) with presentation names. Renaming never changes <see cref="Id"/>.</summary>
public sealed record Club
{
    public ClubId Id { get; }
    public string Name { get; }
    public string ShortName { get; }

    private Club(ClubId id, string name, string shortName)
    {
        Id = id;
        Name = name;
        ShortName = shortName;
    }

    public static Result<Club> Create(ClubId id, string? name, string? shortName)
    {
        if (id == default)
            return Result<Club>.Failure(new Error("Club.id_missing", "구단 ID가 없습니다."));
        if (!Valid(name, 40) || !Valid(shortName, 12))
            return Result<Club>.Failure(new Error("Club.name_invalid", "구단 이름은 앞뒤 공백 없이 1~40자, 약칭은 1~12자여야 합니다."));
        return Result<Club>.Success(new Club(id, name!, shortName!));
    }

    public Result<Club> Rename(string? name, string? shortName) => Create(Id, name, shortName);

    private static bool Valid(string? text, int max) =>
        !string.IsNullOrWhiteSpace(text) && text == text.Trim() && text.Length <= max && !text.Any(char.IsControl);
}
