// =============================================================================
// FILE: Results/Result.cs
// PURPOSE: Railway-oriented programming for explicit error handling
// =============================================================================

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Company.Persistence.Abstractions.Results;

/// <summary>
/// Represents an operation outcome that can succeed with a value or fail with an error.
/// </summary>
[DebuggerDisplay("{IsSuccess ? \"Success\" : \"Failure: \" + Error?.Code}")]
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly Error? _error;

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Cannot access Value on failed Result: {_error}");
    public Error? Error => _error;

    private Result(T value) { _value = value; _error = null; IsSuccess = true; }
    private Result(Error error) { _value = default; _error = error ?? throw new ArgumentNullException(nameof(error)); IsSuccess = false; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);

    public TResult Match<TResult>(Func<T, TResult> success, Func<Error, TResult> failure) =>
        IsSuccess ? success(_value!) : failure(_error!);

    public void Switch(Action<T> success, Action<Error> failure)
    {
        if (IsSuccess) success(_value!);
        else failure(_error!);
    }

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
        IsSuccess ? Result<TNew>.Success(mapper(_value!)) : Result<TNew>.Failure(_error!);

    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
        IsSuccess ? binder(_value!) : Result<TNew>.Failure(_error!);

    public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> binder) =>
        IsSuccess ? await binder(_value!).ConfigureAwait(false) : Result<TNew>.Failure(_error!);

    public T GetValueOrDefault(T defaultValue) => IsSuccess ? _value! : defaultValue;
    public T GetValueOrDefault(Func<Error, T> factory) => IsSuccess ? _value! : factory(_error!);

    public Result<T> Ensure(Func<T, bool> predicate, Error error) =>
        IsFailure ? this : predicate(_value!) ? this : error;

    public bool Equals(Result<T> other) =>
        IsSuccess == other.IsSuccess &&
        EqualityComparer<T?>.Default.Equals(_value, other._value) &&
        Equals(_error, other._error);

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(IsSuccess, _value, _error);
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({_error})";
}

/// <summary>
/// Static factory methods for creating Result instances.
/// </summary>
public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
    public static Result<Unit> Success() => Result<Unit>.Success(Unit.Value);
}
