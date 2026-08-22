using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Teaching;

namespace Learnier.Application.Features.Teaching.Commands.CreateInstructorProfile;

/// <param name="MembershipId">Profilin baglanacagi uyelik.</param>
/// <param name="TimeZoneId">Uygunluk saatlerinin yorumlanacagi saat dilimi.</param>
public sealed record CreateInstructorProfileCommand(
    Guid MembershipId,
    string TimeZoneId,
    string? Bio = null,
    decimal? DefaultHourlyRate = null,
    string? DefaultHourlyRateCurrency = null);

public sealed record CreateInstructorProfileResult(Guid ProfileId, InstructorStatus Status);

internal sealed class CreateInstructorProfileValidator
    : AbstractValidator<CreateInstructorProfileCommand>
{
    public CreateInstructorProfileValidator()
    {
        RuleFor(c => c.MembershipId)
            .NotEmpty().WithErrorCode("organization.membership_required");

        RuleFor(c => c.TimeZoneId)
            .NotEmpty().WithErrorCode("teaching.timezone_required")
            .Must(BeAKnownTimeZone).WithErrorCode("teaching.timezone_invalid");

        RuleFor(c => c.Bio)
            .MaximumLength(4000).WithErrorCode("teaching.bio_too_long");

        RuleFor(c => c.DefaultHourlyRate)
            .GreaterThanOrEqualTo(0).WithErrorCode("teaching.hourly_rate_invalid")
            .When(c => c.DefaultHourlyRate is not null);

        // Tutar ile para birimi ayrilmaz: biri varsa digeri de zorunlu.
        RuleFor(c => c.DefaultHourlyRateCurrency)
            .NotEmpty().WithErrorCode("teaching.currency_required")
            .Length(3).WithErrorCode("teaching.currency_invalid")
            .When(c => c.DefaultHourlyRate is not null);

        RuleFor(c => c.DefaultHourlyRate)
            .NotNull().WithErrorCode("teaching.hourly_rate_required")
            .When(c => !string.IsNullOrWhiteSpace(c.DefaultHourlyRateCurrency));
    }

    private static bool BeAKnownTimeZone(string? timeZoneId)
        => !string.IsNullOrWhiteSpace(timeZoneId)
           && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);
}

/// <summary>
/// Bir uyelik icin egitmen profili acar.
/// </summary>
/// <remarks>
/// Profil <c>Pending</c> baslar; ders verebilmesi icin ayrica aktiflestirilmelidir.
/// Bu ayrim, basvuran ile onaylanan egitmeni ayirmaya yarar.
/// </remarks>
public sealed class CreateInstructorProfileHandler(
    IInstructorRepository instructors,
    IMembershipRepository memberships,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CreateInstructorProfileResult>> Handle(
        CreateInstructorProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        // Uyelik sorgusu kiraci filtresine tabi: baska kurumun uyeligine
        // profil acilamaz, kimligi bilinse bile bulunamaz.
        var membership = await memberships.FindWithRolesAsync(command.MembershipId, cancellationToken);

        if (membership is null)
        {
            return TeachingErrors.MembershipNotFound;
        }

        var existing = await instructors.FindByMembershipAsync(membership.Id, cancellationToken);

        if (existing is not null)
        {
            return TeachingErrors.ProfileAlreadyExists;
        }

        var profile = InstructorProfile.Create(membership.Id, command.TimeZoneId, command.Bio);

        if (command.DefaultHourlyRate is not null)
        {
            profile.SetHourlyRate(command.DefaultHourlyRate, command.DefaultHourlyRateCurrency);
        }

        instructors.AddProfile(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateInstructorProfileResult(profile.Id, profile.Status);
    }
}
