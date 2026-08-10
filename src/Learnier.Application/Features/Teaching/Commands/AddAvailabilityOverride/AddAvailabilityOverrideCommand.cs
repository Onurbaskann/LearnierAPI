using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Teaching;

namespace Learnier.Application.Features.Teaching.Commands.AddAvailabilityOverride;

/// <param name="StartLocalTime">
/// Saatler bos birakilirsa istisna gun boyunu kapsar; birlikte verilirse yalnizca
/// o araligi etkiler.
/// </param>
public sealed record AddAvailabilityOverrideCommand(
    Guid ProfileId,
    DateOnly OverrideDate,
    AvailabilityOverrideType OverrideType,
    TimeOnly? StartLocalTime = null,
    TimeOnly? EndLocalTime = null,
    string? Reason = null);

public sealed record AddAvailabilityOverrideResult(Guid OverrideId);

internal sealed class AddAvailabilityOverrideValidator
    : AbstractValidator<AddAvailabilityOverrideCommand>
{
    public AddAvailabilityOverrideValidator()
    {
        RuleFor(c => c.OverrideType)
            .IsInEnum().WithErrorCode("teaching.override_type_invalid");

        // Saatler ya birlikte verilir ya da ikisi de bos kalir.
        RuleFor(c => c.EndLocalTime)
            .NotNull().WithErrorCode("teaching.override_times_incomplete")
            .When(c => c.StartLocalTime is not null);

        RuleFor(c => c.StartLocalTime)
            .NotNull().WithErrorCode("teaching.override_times_incomplete")
            .When(c => c.EndLocalTime is not null);

        RuleFor(c => c.EndLocalTime)
            .GreaterThan(c => c.StartLocalTime)
            .WithErrorCode("teaching.override_time_range_invalid")
            .When(c => c.StartLocalTime is not null && c.EndLocalTime is not null);

        RuleFor(c => c.Reason)
            .MaximumLength(500).WithErrorCode("teaching.override_reason_too_long");
    }
}

/// <summary>
/// Belirli bir tarihte haftalik uygunlugu degistiren istisna ekler: izin veya ek mesai.
/// </summary>
public sealed class AddAvailabilityOverrideHandler(
    IInstructorRepository instructors,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AddAvailabilityOverrideResult>> Handle(
        AddAvailabilityOverrideCommand command,
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

        var availabilityOverride = InstructorAvailabilityOverride.Create(
            profile.Id,
            command.OverrideDate,
            command.OverrideType,
            command.StartLocalTime,
            command.EndLocalTime,
            command.Reason);

        instructors.AddOverride(availabilityOverride);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddAvailabilityOverrideResult(availabilityOverride.Id);
    }
}
