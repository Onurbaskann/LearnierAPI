using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CloseInstructorSlot;

public sealed record CloseInstructorSlotCommand(Guid SessionId);

/// <summary>Egitmenin kendisine ait, henuz rezerve edilmemis slotu kapatir.</summary>
public sealed class CloseInstructorSlotHandler(
    ISchedulingRepository scheduling,
    IInstructorRepository instructors,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> Handle(
        CloseInstructorSlotCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.MembershipId is not { } membershipId)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        var profile = await instructors.FindByMembershipAsync(membershipId, cancellationToken);
        if (profile is null)
        {
            return SchedulingErrors.InstructorNotFound;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var session = await scheduling.FindSessionForUpdateAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return SchedulingErrors.SessionNotFound;
        }

        session = await scheduling.FindSessionAsync(command.SessionId, true, cancellationToken);
        if (session is null)
        {
            return SchedulingErrors.SessionNotFound;
        }

        if (session.SessionType is not SessionType.Private
            || session.Instructors.All(item => item.InstructorProfileId != profile.Id))
        {
            return SchedulingErrors.SlotNotOwned;
        }

        if (session.Status is LessonSessionStatus.Cancelled or LessonSessionStatus.Completed
            || session.StartsAt <= clock.UtcNow)
        {
            return SchedulingErrors.SessionNotCancellable;
        }

        if ((await scheduling.ListActiveBookingsAsync(session.Id, cancellationToken)).Count > 0)
        {
            return SchedulingErrors.SlotHasBooking;
        }

        session.Cancel("Egitmen tarafindan kapatildi.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
