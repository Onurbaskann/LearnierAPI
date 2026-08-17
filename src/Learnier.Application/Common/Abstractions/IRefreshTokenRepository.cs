using Learnier.Domain.Identity;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Yenileme tokeni okuma ve yazma islemleri.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Ozete gore token kaydini bulur.
    /// </summary>
    /// <remarks>
    /// Arama ham token ile degil ozetle yapilir: veritabaninda yalnizca ozet saklanir.
    /// Donen varlik izlenir, cagiran taraf uzerinde iptal isaretleyebilir.
    /// </remarks>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> FindActiveByUserIdAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    void Add(RefreshToken token);
}

/// <summary>
/// Yenileme tokeni uretimi ve ozetlenmesi.
/// </summary>
public interface IRefreshTokenFactory
{
    /// <summary>
    /// Yeni bir token uretir.
    /// </summary>
    /// <remarks>
    /// Omur bilgisi de burada uretilir: erisim tokeninin omru gibi bu da bir
    /// yapilandirma karari ve ikisi ayni yerde tutuluyor.
    /// </remarks>
    NewRefreshToken Create();

    string Hash(string rawToken);
}

/// <param name="RawToken">Ham token. Yalnizca bir kez, istemciye donerken gorunur.</param>
/// <param name="TokenHash">Veritabaninda saklanacak ozet.</param>
public sealed record NewRefreshToken(
    string RawToken,
    string TokenHash,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
