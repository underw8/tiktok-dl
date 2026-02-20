namespace TikTokDl.Core.Domain.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorMessage { get; }
    
    protected Result(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }
    
    public static Result Success() => new(true);
    public static Result Failure(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Represents the result of an operation that can succeed with a value or fail
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }
    
    private Result(bool isSuccess, T? value = default, string? errorMessage = null) 
        : base(isSuccess, errorMessage)
    {
        Value = value;
    }
    
    public static Result<T> Success(T value) => new(true, value);
    public static new Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
    
    /// <summary>
    /// Implicit conversion from T to Result&lt;T&gt;
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);
}
