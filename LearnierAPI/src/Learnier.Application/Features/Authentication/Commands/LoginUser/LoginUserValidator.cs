using FluentValidation;

namespace Learnier.Application.Features.Authentication.Commands.LoginUser;

/// <summary>
/// Giris isteginin bicimsel dogrulamasi.
/// </summary>
/// <remarks>
/// Kurallar mesaj degil kod tasir (<c>WithErrorCode</c>); ceviri WebApi katmaninda
/// yapilir. Burada yalnizca <b>bicim</b> dogrulanir - parolanin dogrulugu gibi is
/// kurallari handler'a aittir.
/// </remarks>
internal sealed class LoginUserValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().WithErrorCode("auth.email_required")
            .EmailAddress().WithErrorCode("auth.email_invalid");

        // Uzunluk alt siniri bilerek yok: mevcut bir hesabin parolasi kurallar
        // degistiginde kisa kalmis olabilir ve girisi engellenmemeli.
        RuleFor(c => c.Password)
            .NotEmpty().WithErrorCode("auth.password_required");
    }
}
