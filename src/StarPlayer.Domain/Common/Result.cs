namespace StarPlayer.Domain.Common;

/// <summary>Stable machine code plus a Korean player/developer-facing message.</summary>
public readonly record struct Error(string Code, string Message);

/// <summary>Outcome of an operation with no value. Expected failures are values, not exceptions.</summary>
public readonly record struct Result
{
    public Error? Error { get; private init; }

    public bool IsSuccess => Error is null;

    public static Result Success() => default;

    public static Result Failure(Error error) => new() { Error = error };
}

/// <summary>Outcome of an operation that yields <typeparamref name="T"/> on success.</summary>
public readonly record struct Result<T>
{
    private T? Payload { get; init; }

    public Error? Error { get; private init; }

    public bool IsSuccess => Error is null;

    public T Value => IsSuccess
        ? Payload!
        : throw new InvalidOperationException($"Result failed ({Error!.Value.Code}); it has no value.");

    public static Result<T> Success(T value) => new() { Payload = value };

    public static Result<T> Failure(Error error) => new() { Error = error };
}
