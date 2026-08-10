using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching.Commands.AddAvailability;

/// <param name="StartLocalTime">Egitmenin kendi saat diliminde baslangic.</param>
/// <param name="ValidUntil">Bos ise aralik suresiz gecerlidir.</param>
public sealed record AddAvailabilityCommand(
    Guid ProfileId,
    DayOfWeek DayOfWeek,
    TimeOnly StartLocalTime,
    TimeOnly EndLocalTime,
    DateOnly ValidFrom,
    DateOnly? ValidUntil);

public sealed record AddAvailabilityResult(Guid AvailabilityId);

internal sealed class AddAvailabilityValidator : AbstractValidator<AddAvailabilityCommand>
{
    public AddAvailabilityValidator()
    {
        RuleFor(c => c.DayOfWeek)
            .IsInEnum().WithErrorCode("teaching.day_of_week_invalid");

        RuleFor(c => c.EndLocalTime)
            .GreaterThan(c => c.StartLocalTime)
            .WithErrorCode("teaching.availability_time_range_invalid");

        RuleFor(c => c.ValidUntil)
            .GreaterThanOrEqualTo(c => c.ValidFrom)
            .WithErrorCode("teaching.availability_date_range_invalid")
            .When(c => c.ValidUntil is not null);
    }
}

/// <summary>
/// Egitmene haftalik uygunluk araligi ekler.
/// </summary>
/// <remarks>
/// Kaynak dokumanin 6. bolumu geregi recurrence motoru yok: haftalik aralik ve
/// tarihli istisna, ihtiyacin buyuk cogunlugunu cok daha ucuza karsiliyor.
/// </remarks>
public sealed class AddAvailabilityHandler(
    IInstructorRepository instructors,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AddAvailabilityResult>> Handle(
        AddAvailabilityCommand command,
        bool canManageInstructors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        var profile = await instructors.FindWithDetailsAsync(command.ProfileId, cancellationToken);

        if (profile is null)
        {
            return TeachingErrors.ProfileNotFound;
        }

        if (InstructorAccess.Check(profile, currentTenant, canManageInstructors) is { } denied)
        {
            return denied;
        }

        // Cakisma kontrolu: ayni gunde, gecerlilik pencereleri kesisen ve saat
        // araliklari ust uste binen ikinci bir kayit olusursa slot uretiminde
        // ayni saat iki kez uretilir ve egitmen ayni anda iki oturuma atanabilirdi.
        var overlaps = await instructors.HasOverlappingAvailabilityAsync(
            profile.Id,
            command.DayOfWeek,
            command.StartLocalTime,
            command.EndLocalTime,
            command.ValidFrom,
            command.ValidUntil,
            excludeAvailabilityId: null,
            cancellationToken);

        if (overlaps)
        {
            return TeachingErrors.AvailabilityOverlaps;
        }

        var availability = profile.AddAvailability(
            command.DayOfWeek,
            command.StartLocalTime,
            command.EndLocalTime,
            command.ValidFrom,
            command.ValidUntil);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddAvailabilityResult(availability.Id);
    }
}
