namespace SDPP.BuildingBlocks.Application;

/// <summary>Outcome of an application use case, avoiding exceptions for expected business failures.</summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    protected Result(bool isSuccess, string? error, string? errorCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string error, string errorCode = "BUSINESS_RULE_VIOLATION") => new(false, error, errorCode);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error, string errorCode = "BUSINESS_RULE_VIOLATION") =>
        Result<T>.Failure(error, errorCode);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, string? error, string? errorCode)
        : base(isSuccess, error, errorCode) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede acceder al valor de un Result fallido.");

    public static Result<T> Success(T value) => new(true, value, null, null);
    public new static Result<T> Failure(string error, string errorCode = "BUSINESS_RULE_VIOLATION") =>
        new(false, default, error, errorCode);
}
