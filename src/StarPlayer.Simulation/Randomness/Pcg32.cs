using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Randomness;

/// <summary>
/// PCG32 XSH-RR 64/32 (M. E. O'Neill, pcg-c-basic), version <see cref="RngVersions.Algorithm"/>. No <c>System.Random</c>, clock, hash code or
/// shared global state is involved. Not thread-safe: one serialized writer owns an instance.
/// <para>
/// State step: <c>state = state * 6364136223846793005 + inc</c> (unsigned 64-bit, wrapping). Output of the OLD state:
/// <c>xorshifted = (uint)(((old &gt;&gt; 18) ^ old) &gt;&gt; 27)</c>, <c>rot = (uint)(old &gt;&gt; 59)</c>, result = <c>xorshifted</c> rotated right by <c>rot</c>.
/// Seeding (<c>pcg32_srandom_r</c>): <c>state = 0; inc = (initseq &lt;&lt; 1) | 1;</c> step; <c>state += initstate;</c> step. The increment is always odd.
/// Bounded integers (<c>pcg32_boundedrand_r</c>): <c>threshold = (2^32 - bound) % bound</c>; draw until the output is <c>&gt;= threshold</c>; return <c>output % bound</c>.
/// Every rejected output is a consumed draw; a bound of 1 still consumes one draw (as the reference does).
/// </para>
/// </summary>
public sealed class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private ulong _increment;

    public Pcg32(ulong initState, ulong initSeq)
    {
        _state = 0;
        _increment = unchecked((initSeq << 1) | 1UL);
        Step();
        _state = unchecked(_state + initState);
        Step();
    }

    /// <summary>Raw 32-bit outputs consumed since seeding (rejected bounded outputs included; the two seeding steps are not draws).</summary>
    public long Draws { get; private set; }

    public ulong State => _state;

    public ulong Increment => _increment;

    /// <summary>
    /// Next raw output. At <c>Draws == long.MaxValue</c> it throws <see cref="InvalidOperationException"/> BEFORE the state is touched, so the counter
    /// can never wrap and the generator is unchanged by the refusal.
    /// </summary>
    public uint NextUInt32()
    {
        if (Draws == long.MaxValue)
            throw DrawLimitReached();
        ulong old = Step();
        Draws++;
        uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << ((32 - rot) & 31));
    }

    /// <summary>
    /// Unbiased integer in <c>[0, bound)</c>. <paramref name="bound"/> 0 is a caller bug and throws. If the draw limit is reached while rejected
    /// outputs are being skipped, the generator (state and <see cref="Draws"/>) is restored to what it was before the call and the call throws.
    /// </summary>
    public uint NextBounded(uint bound)
    {
        if (bound == 0)
            throw new ArgumentOutOfRangeException(nameof(bound), "bound는 1 이상이어야 합니다.");
        uint threshold = unchecked(0u - bound) % bound;
        ulong startState = _state;
        long startDraws = Draws;
        while (true)
        {
            if (Draws == long.MaxValue)
            {
                _state = startState;
                Draws = startDraws;
                throw DrawLimitReached();
            }
            uint r = NextUInt32();
            if (r >= threshold)
                return r % bound;
        }
    }

    /// <summary>
    /// Jump ahead by <paramref name="delta"/> state steps in O(log delta) (pcg_advance); <see cref="Draws"/> grows by <paramref name="delta"/>.
    /// A delta above <c>long.MaxValue</c> is an invalid argument; a delta larger than the remaining draw allowance throws
    /// <see cref="InvalidOperationException"/>. Both are checked before anything changes.
    /// </summary>
    public void Advance(ulong delta)
    {
        if (delta > long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(delta), "소비 횟수는 long.MaxValue 이하여야 합니다.");
        if (delta > (ulong)(long.MaxValue - Draws))
            throw DrawLimitReached();
        ulong accMult = 1, accPlus = 0, curMult = Multiplier, curPlus = _increment;
        for (ulong d = delta; d > 0; d >>= 1)
        {
            if ((d & 1) != 0)
            {
                accMult = unchecked(accMult * curMult);
                accPlus = unchecked(accPlus * curMult + curPlus);
            }
            curPlus = unchecked((curMult + 1) * curPlus);
            curMult = unchecked(curMult * curMult);
        }
        _state = unchecked(accMult * _state + accPlus);
        Draws += (long)delta;
    }

    /// <summary>Rebuilds a generator from stored fields. Rejects an even increment and a negative draw count.</summary>
    public static Result<Pcg32> FromState(ulong state, ulong increment, long draws)
    {
        if ((increment & 1UL) == 0)
            return Result<Pcg32>.Failure(new Error("Rng.increment_even", "난수 증분(increment)은 홀수여야 합니다."));
        if (draws < 0)
            return Result<Pcg32>.Failure(new Error("Rng.draws_negative", "난수 소비 횟수는 0 이상이어야 합니다."));
        return Result<Pcg32>.Success(new Pcg32(state, increment, draws, rawState: true));
    }

    internal Pcg32 Clone() => new(_state, _increment, Draws, rawState: true);

    internal void CopyFrom(Pcg32 other)
    {
        _state = other._state;
        _increment = other._increment;
        Draws = other.Draws;
    }

    private Pcg32(ulong state, ulong increment, long draws, bool rawState)
    {
        _state = state;
        _increment = increment;
        Draws = draws;
    }

    private static InvalidOperationException DrawLimitReached() => new("난수 소비 횟수가 한도(long.MaxValue)에 도달했습니다.");

    private ulong Step()
    {
        ulong old = _state;
        _state = unchecked(old * Multiplier + _increment);
        return old;
    }
}
