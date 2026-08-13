using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Compensation;

public sealed record ConfigureCompensationRateCommand(
    Guid SubjectId,
    int LessonDurationMinutes,
    decimal Amount,
    string Currency);

public sealed record ConfigureCompensationRateResult(Guid RateId);

internal sealed class ConfigureCompensationRateValidator
    : AbstractValidator<ConfigureCompensationRateCommand>
{
    public ConfigureCompensationRateValidator()
    {
        RuleFor(command => command.SubjectId)
            .NotEmpty().WithErrorCode("compensation.subject_required");
        RuleFor(command => command.LessonDurationMinutes)
            .Must(duration => duration is 30 or 50)
            .WithErrorCode("compensation.duration_invalid");
        RuleFor(command => command.Amount)
            .GreaterThanOrEqualTo(0).WithErrorCode("compensation.amount_invalid");
        RuleFor(command => command.Currency)
            .NotEmpty().WithErrorCode("compensation.currency_required")
            .Length(3).WithErrorCode("compensation.currency_invalid");
    }
}

public sealed class ConfigureCompensationRateHandler(
    IInstructorCompensationService compensation,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<ConfigureCompensationRateResult>> Handle(
        ConfigureCompensationRateCommand command,
        CancellationToken cancellationToken)
    {
        var configured = await compensation.ConfigureRateAsync(
            command.SubjectId,
            command.LessonDurationMinutes,
            command.Amount,
            command.Currency,
            cancellationToken);
        if (configured.IsFailure)
        {
            return configured.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ConfigureCompensationRateResult(configured.Value);
    }
}

public sealed record ConfigurePenaltyStepsCommand(IReadOnlyList<decimal> Percentages);

internal sealed class ConfigurePenaltyStepsValidator
    : AbstractValidator<ConfigurePenaltyStepsCommand>
{
    public ConfigurePenaltyStepsValidator()
    {
        RuleFor(command => command.Percentages)
            .NotEmpty().WithErrorCode("compensation.penalty_steps_required")
            .Must(percentages => percentages.Count <= 20)
            .WithErrorCode("compensation.penalty_steps_too_many")
            .Must(percentages => percentages.All(value => value is >= 0 and <= 100))
            .WithErrorCode("compensation.penalty_percentage_invalid")
            .Must(IsStrictlyIncreasing)
            .WithErrorCode("compensation.penalty_steps_not_increasing");
    }

    private static bool IsStrictlyIncreasing(IReadOnlyList<decimal> percentages)
        => percentages.Zip(percentages.Skip(1), (left, right) => right > left).All(result => result);
}

public sealed class ConfigurePenaltyStepsHandler(
    IInstructorCompensationService compensation,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        ConfigurePenaltyStepsCommand command,
        CancellationToken cancellationToken)
    {
        var configured = await compensation.ConfigurePenaltyStepsAsync(
            command.Percentages,
            cancellationToken);
        if (configured.IsFailure)
        {
            return configured.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
