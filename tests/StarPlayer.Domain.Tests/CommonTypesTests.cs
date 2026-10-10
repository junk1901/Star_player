using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Tests;

public class CommonTypesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" p1")]
    [InlineData("p1 ")]
    public void Id_rejects_blank_or_non_canonical_text(string? text)
    {
        var result = PlayerId.Create(text);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("PlayerId.", result.Error!.Value.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Ids_with_same_text_are_equal_and_different_id_types_do_not_mix()
    {
        var a = PlayerId.Create("p-001").Value;
        var b = PlayerId.Create("p-001").Value;

        Assert.Equal(a, b);
        Assert.Equal("p-001", a.ToString());
        Assert.True(TeamId.Create("t-001").IsSuccess);
        Assert.True(CareerId.Create("c-001").IsSuccess);
        Assert.Equal("TeamId.empty", TeamId.Create("").Error!.Value.Code);
        Assert.Equal("CareerId.empty", CareerId.Create("").Error!.Value.Code);
    }

    [Fact]
    public void WorldRevision_starts_at_zero_and_only_moves_forward()
    {
        var revision = WorldRevision.Initial;

        Assert.Equal(0, revision.Value);
        Assert.Equal(1, revision.Next().Value);
        Assert.Equal(2, revision.Next().Next().Value);
        Assert.Equal(WorldRevision.Initial, WorldRevision.Create(0).Value);
        Assert.Equal("WorldRevision.negative", WorldRevision.Create(-1).Error!.Value.Code);
    }

    [Fact]
    public void WorldRevision_overflow_is_an_error_not_a_wraparound()
    {
        var max = WorldRevision.Create(long.MaxValue).Value;

        Assert.Throws<OverflowException>(() => max.Next());
    }

    [Fact]
    public void Result_success_carries_value_and_failure_carries_error()
    {
        var ok = Result<int>.Success(7);
        var failed = Result<int>.Failure(new Error("x.failed", "실패"));

        Assert.True(ok.IsSuccess);
        Assert.Equal(7, ok.Value);
        Assert.False(failed.IsSuccess);
        Assert.Equal("x.failed", failed.Error!.Value.Code);
        Assert.True(Result.Success().IsSuccess);
        Assert.False(Result.Failure(new Error("y", "실패")).IsSuccess);
    }
}
