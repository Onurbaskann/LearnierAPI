using FluentValidation;
using Learnier.Domain.Identity;

namespace Learnier.Application.Features.Organizations.Commands.CreateOrganization;

/// <param name="Slug">URL ve kiraci anahtari olarak kullanilacak kisa ad.</param>
/// <param name="TimeZoneId">IANA saat dilimi, ornegin <c>Europe/Istanbul</c>.</param>
/// <param name="DefaultCurrency">ISO 4217 kodu, ornegin <c>TRY</c>.</param>
public sealed record CreateOrganizationCommand(
    string Name,
    string Slug,
    OrganizationType OrganizationType,
    string TimeZoneId,
    string DefaultCurrency);

/// <param name="MembershipId">Kurucunun bu kurumdaki uyeligi; sahip rolu atanmis olur.</param>
public sealed record CreateOrganizationResult(Guid OrganizationId, string Slug, Guid MembershipId);

internal sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithErrorCode("organization.name_required")
            .MaximumLength(200).WithErrorCode("organization.name_too_long");

        RuleFor(c => c.Slug)
            .NotEmpty().WithErrorCode("organization.slug_required")
            .MaximumLength(100).WithErrorCode("organization.slug_too_long")
            // URL'de tasindigi icin yalnizca kucuk harf, rakam ve tek tire.
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$").WithErrorCode("organization.slug_invalid");

        RuleFor(c => c.OrganizationType)
            .IsInEnum().WithErrorCode("organization.type_invalid");

        RuleFor(c => c.TimeZoneId)
            .NotEmpty().WithErrorCode("organization.timezone_required")
            // Gecersiz saat dilimi kaydedilirse oturum saatleri sonradan
            // cozulemez hale gelir; kayit aninda dogrulanir.
            .Must(BeAKnownTimeZone).WithErrorCode("organization.timezone_invalid");

        RuleFor(c => c.DefaultCurrency)
            .NotEmpty().WithErrorCode("organization.currency_required")
            .Length(3).WithErrorCode("organization.currency_invalid");
    }

    private static bool BeAKnownTimeZone(string timeZoneId)
        => !string.IsNullOrWhiteSpace(timeZoneId)
           && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);
}
