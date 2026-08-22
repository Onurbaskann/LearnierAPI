using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Compensation;

public sealed record CancellationPolicyDto(
    int StudentRefundCutoffMinutes,
    int InstructorPenaltyCutoffMinutes,
    int Version);

public sealed record ConfigureCancellationPolicyCommand(
    int StudentRefundCutoffMinutes,
    int InstructorPenaltyCutoffMinutes);

internal sealed class ConfigureCancellationPolicyValidator
    : AbstractValidator<ConfigureCancellationPolicyCommand>
{
    public ConfigureCancellationPolicyValidator()
    {
        RuleFor(command => command.StudentRefundCutoffMinutes)
            .InclusiveBetween(0, 10_080)
            .WithErrorCode("compensation.student_cancellation_cutoff_invalid");
        RuleFor(command => command.InstructorPenaltyCutoffMinutes)
            .InclusiveBetween(0, 10_080)
            .WithErrorCode("compensation.instructor_cancellation_cutoff_invalid");
    }
}

public sealed class GetCancellationPolicyHandler(ICancellationPolicyService cancellationPolicies)
{
    public async Task<Result<CancellationPolicyDto>> Handle(CancellationToken cancellationToken)
    {
        var result = await cancellationPolicies.GetCurrentAsync(cancellationToken);
        return result.IsFailure
            ? result.Error
            : ToDto(result.Value);
    }

    private static CancellationPolicyDto ToDto(CancellationPolicySnapshot policy)
        => new(
            policy.StudentRefundCutoffMinutes,
            policy.InstructorPenaltyCutoffMinutes,
            policy.Version);
}

public sealed class ConfigureCancellationPolicyHandler(
    ICancellationPolicyService cancellationPolicies,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CancellationPolicyDto>> Handle(
        ConfigureCancellationPolicyCommand command,
        CancellationToken cancellationToken)
    {
        var result = await cancellationPolicies.ConfigureAsync(
            command.StudentRefundCutoffMinutes,
            command.InstructorPenaltyCutoffMinutes,
            cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CancellationPolicyDto(
            result.Value.StudentRefundCutoffMinutes,
            result.Value.InstructorPenaltyCutoffMinutes,
            result.Value.Version);
    }
}
