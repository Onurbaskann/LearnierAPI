using Learnier.Application.Common.Abstractions;

namespace Learnier.Application.Features.Authentication.Commands.LoginUser;

/// <param name="Email">Kullanicinin e-postasi. Karsilastirma buyuk/kucuk harf duyarsizdir.</param>
public sealed record LoginUserCommand(string Email, string Password);

/// <summary>
/// Basarili girisin sonucu.
/// </summary>
/// <remarks>
/// <see cref="Memberships"/> bilerek liste: bir kullanici birden fazla kurumda uye
/// olabilir ve rolu her kurumda farklidir. Istemci aktif kurumu buradan secip
/// sonraki isteklerde <c>X-Organization-Id</c> basligiyla tasir.
/// </remarks>
/// <param name="RefreshToken">
/// Erisim tokeni suresi dolunca yenilemek icin kullanilir. Ham deger yalnizca burada
/// gorunur; veritabaninda yalnizca ozeti saklanir.
/// </param>
public sealed record LoginUserResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    AuthenticatedUser User,
    IReadOnlyList<UserMembership> Memberships);

public sealed record AuthenticatedUser(Guid Id, string Email, string FirstName, string LastName);
