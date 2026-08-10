using FluentValidation;

namespace Learnier.Application.Features.Authentication.Commands.RegisterUser;

/// <summary>
/// Kayit isteginin bicimsel dogrulamasi.
/// </summary>
/// <remarks>
/// Giristen farkli olarak burada parola icin alt sinir var: yeni bir parola
/// belirlenirken kural uygulanabilir, mevcut bir hesaba giriste ise uygulanamaz.
/// </remarks>
internal sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().WithErrorCode("auth.email_required")
            .EmailAddress().WithErrorCode("auth.email_invalid")
            .MaximumLength(320).WithErrorCode("auth.email_too_long");

        RuleFor(c => c.Password)
            .NotEmpty().WithErrorCode("auth.password_required")
            .MinimumLength(8).WithErrorCode("auth.password_too_short")
            // Ust sinir parola ozetleyicisini asiri buyuk girdilerle mesgul
            // etmemek icin; hizmet disi birakma denemelerine karsi.
            .MaximumLength(128).WithErrorCode("auth.password_too_long");

        RuleFor(c => c.FirstName)
            .NotEmpty().WithErrorCode("auth.first_name_required")
            .MaximumLength(100).WithErrorCode("auth.first_name_too_long");

        RuleFor(c => c.LastName)
            .NotEmpty().WithErrorCode("auth.last_name_required")
            .MaximumLength(100).WithErrorCode("auth.last_name_too_long");
    }
}
