using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Compensation;

public sealed record WaiveInstructorPenaltyCommand(Guid InstructorProfileId, string Reason);

internal sealed class WaiveInstructorPenaltyValidator
    : AbstractValidator<WaiveInstructorPenaltyCommand>
{
    public WaiveInstructorPenaltyValidator()
    {
        RuleFor(command => command.InstructorProfileId)
            .NotEmpty().WithErrorCode("compensation.instructor_required");
        RuleFor(command => command.Reason)
            .NotEmpty().WithErrorCode("compensation.waiver_reason_required")
            .MaximumLength(500).WithErrorCode("compensation.waiver_reason_too_long");
    }
}

public sealed class GetInstructorPenaltyHistoryHandler(
    IInstructorCompensationService compensation)
{
    public Task<Result<InstructorPenaltyHistory>> Handle(
        Guid instructorProfileId,
        CancellationToken cancellationToken)
        => compensation.GetPenaltyHistoryAsync(instructorProfileId, cancellationToken);
}

public sealed class WaiveInstructorPenaltyHandler(
    IInstructorCompensationService compensation,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        WaiveInstructorPenaltyCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var result = await compensation.WaivePenaltyAsync(
            command.InstructorProfileId,
            command.Reason,
            cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
