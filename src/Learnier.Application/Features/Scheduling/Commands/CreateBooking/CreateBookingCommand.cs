using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CreateBooking;

/// <param name="LearnerUserId">
/// Bos birakilirsa istegi yapan kullanici adina rezervasyon yapilir. Dolu ise
/// cagiricinin baskasi adina islem yapma yetkisi olmalidir.
/// </param>
/// <remarks>
/// Oturum kimligi komutta degil handler parametresinde tasinir: rotadan geliyor.
/// Komut yalnizca govdeden geleni tutarsa action parametresi olarak baglanabilir
/// ve <c>ValidationFilter</c> kurallari calistirabilir.
/// </remarks>
public sealed record CreateBookingCommand(
    Guid? LearnerUserId = null,
    int? LessonDurationMinutes = null);

public sealed record CreateBookingResult(
    Guid BookingId,
    BookingStatus Status,
    BookingAccessSource AccessSource);

internal sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(c => c.LessonDurationMinutes)
            .Must(duration => duration is null or 30 or 50)
            .WithErrorCode("scheduling.lesson_duration_invalid");
    }
}
