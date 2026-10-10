using System.Globalization;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball;

/// <summary>
/// Pitching innings stored as integer outs (ROADMAP §3.2): 112 outs display as <c>37.1</c>; the digit after the dot
/// is outs (0-2), never a decimal fraction of an inning.
/// </summary>
public readonly record struct InningsOuts : IComparable<InningsOuts>
{
    public long Outs { get; private init; }

    public static InningsOuts Zero => default;

    public static Result<InningsOuts> FromOuts(long outs) =>
        outs < 0
            ? Result<InningsOuts>.Failure(new Error("InningsOuts.negative", "아웃 수는 0 이상이어야 합니다."))
            : Result<InningsOuts>.Success(new InningsOuts { Outs = outs });

    public static Result<InningsOuts> FromParts(long fullInnings, int extraOuts)
    {
        if (fullInnings < 0 || extraOuts is < 0 or > 2)
            return Result<InningsOuts>.Failure(new Error("InningsOuts.invalid_parts", "이닝은 0 이상, 추가 아웃은 0~2여야 합니다."));
        try { return FromOuts(checked(fullInnings * 3 + extraOuts)); }
        catch (OverflowException) { return Overflow(); }
    }

    /// <summary>Parses the display form, e.g. <c>37.1</c>. Only <c>.0</c>, <c>.1</c>, <c>.2</c> are legal.</summary>
    public static Result<InningsOuts> Parse(string? text)
    {
        var parts = (text ?? "").Split('.');
        if (parts.Length > 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var innings)
            || (parts.Length == 2 && !(parts[1].Length == 1 && parts[1][0] is >= '0' and <= '2')))
            return Result<InningsOuts>.Failure(new Error("InningsOuts.invalid_text", "이닝 표기는 '37.1'처럼 소수점 아래가 0~2여야 합니다."));
        return FromParts(innings, parts.Length == 2 ? parts[1][0] - '0' : 0);
    }

    public Result<InningsOuts> Add(InningsOuts other)
    {
        try { return FromOuts(checked(Outs + other.Outs)); }
        catch (OverflowException) { return Overflow(); }
    }

    public int CompareTo(InningsOuts other) => Outs.CompareTo(other.Outs);

    public override string ToString() => $"{Outs / 3}.{Outs % 3}";

    private static Result<InningsOuts> Overflow() => Result<InningsOuts>.Failure(new Error("InningsOuts.overflow", "이닝 계산이 허용 범위를 넘었습니다."));
}
