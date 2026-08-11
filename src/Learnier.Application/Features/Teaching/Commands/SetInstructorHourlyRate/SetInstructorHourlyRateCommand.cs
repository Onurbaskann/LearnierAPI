using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching.Commands.SetInstructorHourlyRate;

public sealed record SetInstructorHourlyRateCommand(
    Guid ProfileId,
    decimal? HourlyRate,
    string? Currency);

internal sealed class SetInstructorHourlyRateValidator
    : AbstractValidator<SetInstructorHourlyRateCommand>
{
    public SetInstructorHourlyRateValidator()
    {
        RuleFor(c => c.HourlyRate)
            .GreaterThanOrEqualTo(0).WithErrorCode("teaching.hourly_rate_invalid")
            .When(c => c.HourlyRate is not null);

        RuleFor(c => c.Currency)
            .NotEmpty().WithErrorCode("teaching.currency_required")
            .Length(3).WithErrorCode("teaching.currency_invalid")
            .When(c => c.HourlyRate is not null);

        RuleFor(c => c.HourlyRate)
            .NotNull().WithErrorCode("teaching.hourly_rate_required")
            .When(c => !string.IsNullOrWhiteSpace(c.Currency));
    }
}

/// <summary>Egitmenin varsayilan saatlik ucretini belirler veya temizler.</summary>
public sealed class SetInstructorHourlyRateHandler(
    IInstructorRepository instructors,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        SetInstructorHourlyRateCommand command,
        bool canManageInstructors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return Result.Failure(TeachingErrors.OrganizationContextRequired);
        }

        var profile = await instructors.FindWithDetailsAsync(command.ProfileId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(TeachingErrors.ProfileNotFound);
        }

        if (InstructorAccess.Check(profile, currentTenant, canManageInstructors) is { } denied)
        {
            return Result.Failure(denied);
        }

        profile.SetHourlyRate(command.HourlyRate, command.Currency);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
