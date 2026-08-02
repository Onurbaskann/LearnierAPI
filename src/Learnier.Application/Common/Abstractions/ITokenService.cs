namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Erisim tokeni uretimi.
/// </summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(Guid userId, string email);
}

/// <summary>
/// Uretilmis erisim tokeni.
/// </summary>
/// <param name="Value">Imzalanmis JWT.</param>
/// <param name="ExpiresAt">Gecerlilik bitisi (UTC).</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
