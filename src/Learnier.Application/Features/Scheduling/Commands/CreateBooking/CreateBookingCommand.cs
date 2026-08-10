using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CreateBooking;

/// <param name="LearnerUserId">
/// Bos birakilirsa istegi yapan kullanici adina rezervasyon yapilir. Dolu ise
/// cagiricinin baskasi adina islem yapma yetkisi olmalidir.
/// </param>
public sealed record CreateBookingCommand(Guid SessionId, Guid? LearnerUserId = null);

public sealed record CreateBookingResult(
    Guid BookingId,
    BookingStatus Status,
    BookingAccessSource AccessSource);

internal sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(c => c.SessionId)
            .NotEmpty().WithErrorCode("scheduling.session_required");
    }
}
