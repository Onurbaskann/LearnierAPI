using System.Diagnostics.CodeAnalysis;

namespace Learnier.Application.Common.Results;

/// <summary>
/// Deger dondurmeyen bir islemin sonucu.
/// </summary>
/// <remarks>
/// Is kurali ihlalleri exception ile degil bu tiple tasinir. Exception yalnizca
/// gercekten beklenmedik durumlar icin ayrilir; "kontenjan dolu" beklenen bir sonuctur,
/// istisna degildir.
/// </remarks>
public class Result
{
    protected Result(Error? error)
    {
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public Error? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Basarili oldugunda <typeparamref name="TValue"/> donduren bir islemin sonucu.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(TValue? value, Error? error)
        : base(error)
    {
        _value = value;
    }

    /// <summary>
    /// Basari degeri. Yalnizca <see cref="Result.IsSuccess"/> dogruyken okunmali.
    /// </summary>
    /// <exception cref="InvalidOperationException">Sonuc basarisizken erisilirse.</exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Basarisiz bir sonucun degeri okunamaz.");

    public static Result<TValue> Success(TValue value) => new(value, null);

    public static new Result<TValue> Failure(Error error) => new(default, error);

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure(error);
}
