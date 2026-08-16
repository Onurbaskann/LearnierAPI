using Learnier.Domain.Identity;

namespace Learnier.Application.Common.Abstractions;

public interface IEmailVerificationTokenRepository
{
    /// <summary>
    /// Ozete gore token kaydini bulur. Donen varlik izlenir: cagiran taraf
    /// uzerinde tuketilmis isaretleyebilir.
    /// </summary>
    Task<EmailVerificationToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(EmailVerificationToken token);
}

public interface IEmailVerificationTokenFactory
{
    NewEmailVerificationToken Create();

    string Hash(string rawToken);
}

/// <param name="RawToken">Ham token. Yalnizca kullaniciya gonderilen e-postada bulunur.</param>
public sealed record NewEmailVerificationToken(
    string RawToken,
    string TokenHash,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
