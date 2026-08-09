namespace Learnier.Application.Features.Authentication.Commands.RefreshAccessToken;

/// <param name="RefreshToken">Girista veya onceki yenilemede alinan ham token.</param>
public sealed record RefreshAccessTokenCommand(string RefreshToken);

/// <summary>
/// Yenileme sonucu. Hem erisim hem yenileme tokeni yenilenir.
/// </summary>
public sealed record RefreshAccessTokenResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken);
