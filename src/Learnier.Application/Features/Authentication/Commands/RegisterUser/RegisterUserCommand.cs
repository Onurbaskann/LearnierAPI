namespace Learnier.Application.Features.Authentication.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName);

/// <summary>
/// Kayit sonucu.
/// </summary>
/// <remarks>
/// Token dondurulmez: hesap henuz dogrulanmamis oldugu icin giris yapamaz.
/// <see cref="VerificationRequired"/> her zaman dogru; alan, ileride davetle
/// gelen ve dogrulama gerektirmeyen kayitlar eklendiginde anlam kazanacak.
/// </remarks>
public sealed record RegisterUserResult(Guid UserId, string Email, bool VerificationRequired);
