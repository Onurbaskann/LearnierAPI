using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.AssignSessionInstructor;

public sealed record AssignSessionInstructorCommand(
    Guid SessionId,
    Guid InstructorProfileId,
    SessionInstructorRole Role);

internal sealed class AssignSessionInstructorValidator
    : AbstractValidator<AssignSessionInstructorCommand>
{
    public AssignSessionInstructorValidator()
    {
        RuleFor(c => c.InstructorProfileId)
            .NotEmpty().WithErrorCode("scheduling.instructor_required");

        RuleFor(c => c.Role)
            .IsInEnum().WithErrorCode("scheduling.instructor_role_invalid");
    }
}

/// <summary>
/// Oturuma egitmen atar.
/// </summary>
/// <remarks>
/// <para>
/// Ayni egitmenin ayni saat araliginda iki oturuma atanmasi engellenir. Kaynak
/// dokumanin 13. bolumu bunu normal unique index'in cozemedigi bir durum olarak
/// isaret ediyor: cakisma esitlik degil aralik kesisimi sorusudur.
/// </para>
/// <para>
/// Kontrol islem icinde yapiliyor. PostgreSQL'in <c>EXCLUDE</c> kisiti daha guclu
/// bir garanti verirdi; ileride yogunluk artarsa eklenmeli. Su an atama seyrek ve
/// yonetici denetiminde yapilan bir islem.
/// </para>
/// </remarks>
public sealed class AssignSessionInstructorHandler(
    ISchedulingRepository scheduling,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        AssignSessionInstructorCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return Result.Failure(SchedulingErrors.OrganizationContextRequired);
        }

        var session = await scheduling.FindSessionAsync(
            command.SessionId, includeInstructors: true, cancellationToken);

        if (session is null)
        {
            return Result.Failure(SchedulingErrors.SessionNotFound);
        }

        if (!await scheduling.InstructorExistsAsync(command.InstructorProfileId, cancellationToken))
        {
            return Result.Failure(SchedulingErrors.InstructorNotFound);
        }

        // Zaten atanmissa cakisma kontrolu anlamsiz; AssignInstructor da tekrar eklemez.
        var alreadyAssigned = session.Instructors
            .Any(i => i.InstructorProfileId == command.InstructorProfileId);

        if (!alreadyAssigned)
        {
            var busy = await scheduling.HasInstructorConflictAsync(
                command.InstructorProfileId,
                session.StartsAt,
                session.EndsAt,
                excludeSessionId: session.Id,
                cancellationToken);

            if (busy)
            {
                return Result.Failure(SchedulingErrors.InstructorBusy);
            }
        }

        session.AssignInstructor(command.InstructorProfileId, command.Role);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
