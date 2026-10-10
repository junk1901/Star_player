namespace StarPlayer.Domain.Common;

/// <summary>How a percentage/ratio result is rounded to whole KRW. No floating point is ever involved.</summary>
public enum MoneyRounding
{
    TowardZero,
    HalfAwayFromZero,
}

/// <summary>Signed 64-bit KRW amount (ROADMAP §3.2). Arithmetic is checked: overflow is an error, never a wraparound.</summary>
public readonly record struct Money : IComparable<Money>
{
    public long Won { get; private init; }

    public static Money Zero => default;

    public static Money FromWon(long won) => new() { Won = won };

    public Result<Money> Add(Money other)
    {
        try { return Result<Money>.Success(FromWon(checked(Won + other.Won))); }
        catch (OverflowException) { return Overflow(); }
    }

    public Result<Money> Subtract(Money other)
    {
        try { return Result<Money>.Success(FromWon(checked(Won - other.Won))); }
        catch (OverflowException) { return Overflow(); }
    }

    public Result<Money> Multiply(long factor)
    {
        try { return Result<Money>.Success(FromWon(checked(Won * factor))); }
        catch (OverflowException) { return Overflow(); }
    }

    /// <summary>Computes <c>Won * numerator / denominator</c> exactly in integers with an explicit rounding rule.</summary>
    public Result<Money> Scale(long numerator, long denominator, MoneyRounding rounding)
    {
        if (denominator == 0)
            return Result<Money>.Failure(new Error("Money.zero_denominator", "비율의 분모는 0일 수 없습니다."));

        var product = (Int128)Won * numerator;
        var quotient = product / denominator;
        var remainder = product % denominator;
        if (rounding == MoneyRounding.HalfAwayFromZero && Int128.Abs(remainder) * 2 >= Int128.Abs((Int128)denominator))
            quotient += (product < 0) == (denominator < 0) ? 1 : -1;

        return quotient < long.MinValue || quotient > long.MaxValue ? Overflow() : Result<Money>.Success(FromWon((long)quotient));
    }

    public int CompareTo(Money other) => Won.CompareTo(other.Won);

    public override string ToString() => $"{Won.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}원";

    private static Result<Money> Overflow() => Result<Money>.Failure(new Error("Money.overflow", "금액 계산이 허용 범위를 넘었습니다."));
}
