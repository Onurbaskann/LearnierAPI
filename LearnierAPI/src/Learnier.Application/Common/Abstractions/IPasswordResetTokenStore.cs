namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Parola sifirlama tokenlarini kisa sureli ve tek kullanimlik saklar.
/// </summary>
public interface IPasswordResetTokenStore
{
    NewPasswordResetToken Issue(Guid userId);

    Guid? Consume(string rawToken);
}

public sealed record NewPasswordResetToken(string RawToken, DateTimeOffset ExpiresAt);
