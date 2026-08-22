using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Scheduling;

/// <summary>Birebir rezervasyonlari ders paketi ve kredi defteriyle yetkilendirir.</summary>
internal sealed class SubscriptionCreditEntitlementPolicy(
    AppDbContext context,
    IClock clock) : IBookingEntitlementPolicy
{
    public async Task<Result<BookingGrant>> AuthorizeAsync(
        Guid learnerUserId,
        LessonSession session,
        int? lessonDurationMinutes,
        CancellationToken cancellationToken)
    {
        var subjectId = await context.Courses
            .Where(course => course.Id == session.CourseId)
            .Select(course => (Guid?)course.SubjectId)
            .SingleOrDefaultAsync(cancellationToken);

        if (subjectId is null)
        {
            return Error.NotFound("booking.course_not_found");
        }

        var now = clock.UtcNow;

        if (session.SessionType is not SessionType.Private)
        {
            var subscriptionId = await (
                    from subscription in context.Subscriptions
                    join price in context.PlanPrices on subscription.PlanPriceId equals price.Id
                    join plan in context.SubscriptionPlans on price.PlanId equals plan.Id
                    join access in context.PlanSubjectAccess on plan.Id equals access.PlanId
                    where subscription.SubscriberUserId == learnerUserId
                          && subscription.Status == SubscriptionStatus.Active
                          && subscription.CurrentPeriodStart <= now
                          && subscription.CurrentPeriodEnd > now
                          && plan.Status == PlanStatus.Active
                          && access.SubjectId == subjectId.Value
                    orderby subscription.CurrentPeriodEnd, subscription.Id
                    select (Guid?)subscription.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return subscriptionId is { } id
                ? new BookingGrant(BookingAccessSource.Subscription, id)
                : Error.Forbidden("booking.lesson_package_required");
        }

        var durationMinutes = lessonDurationMinutes
            ?? (int)(session.EndsAt - session.StartsAt).TotalMinutes;

        var candidateIds = await (
                from subscription in context.Subscriptions
                join price in context.PlanPrices on subscription.PlanPriceId equals price.Id
                join plan in context.SubscriptionPlans on price.PlanId equals plan.Id
                join access in context.PlanSubjectAccess on plan.Id equals access.PlanId
                where subscription.SubscriberUserId == learnerUserId
                      && subscription.Status == SubscriptionStatus.Active
                      && subscription.CurrentPeriodStart <= now
                      && subscription.CurrentPeriodEnd > now
                      && plan.Status == PlanStatus.Active
                      && plan.MonthlyLessonCredits != null
                      && plan.LessonDurationMinutes == durationMinutes
                      && access.SubjectId == subjectId.Value
                orderby subscription.CurrentPeriodEnd, subscription.Id
                select subscription.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (candidateIds.Count is 0)
        {
            return Error.Forbidden(
                "booking.compatible_package_not_found",
                ("duration", durationMinutes));
        }

        // Farkli oturumlara ayni anda gelen isteklerin ayni son krediyi iki kez
        // harcamasini engellemek icin aday abonelikler kararli sirayla kilitlenir.
        foreach (var subscriptionId in candidateIds)
        {
            await context.Subscriptions
                .FromSqlInterpolated(
                    $"SELECT * FROM subscriptions WHERE id = {subscriptionId} FOR UPDATE")
                .IgnoreQueryFilters()
                .SingleAsync(cancellationToken);

            var period = await CurrentCreditPeriodAsync(
                subscriptionId,
                learnerUserId,
                cancellationToken);

            if (period is not null
                && await AvailableCreditsAsync(
                    subscriptionId,
                    learnerUserId,
                    period,
                    cancellationToken) > 0)
            {
                return new BookingGrant(BookingAccessSource.Credit, subscriptionId);
            }
        }

        return Error.Conflict("booking.credit_exhausted");
    }

    public async Task<Result<Guid?>> ReserveAsync(
        SessionBooking booking,
        CancellationToken cancellationToken)
    {
        if (booking.AccessSource is not BookingAccessSource.Credit)
        {
            return Result.Success<Guid?>(null);
        }

        if (booking.SubscriptionId is not { } subscriptionId)
        {
            return Error.Conflict("booking.subscription_missing");
        }

        var existingId = await context.CreditLedger
            .Where(entry => entry.BookingId == booking.Id
                            && entry.TransactionType == CreditTransactionType.Reserve)
            .Select(entry => (Guid?)entry.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (existingId is not null)
        {
            return existingId;
        }

        var period = await CurrentCreditPeriodAsync(
            subscriptionId,
            booking.LearnerUserId,
            cancellationToken);

        if (period is null
            || await AvailableCreditsAsync(
                subscriptionId,
                booking.LearnerUserId,
                period,
                cancellationToken) <= 0)
        {
            return Error.Conflict("booking.credit_exhausted");
        }

        var sessionType = await GetSessionTypeAsync(booking.SessionId, cancellationToken);
        if (sessionType is null)
        {
            return Error.NotFound("booking.session_not_found");
        }

        var entry = CreditLedgerEntry.Reserve(
            subscriptionId,
            booking.LearnerUserId,
            sessionType.Value,
            booking.Id,
            clock.UtcNow,
            periodStart: period.Start);

        context.CreditLedger.Add(entry);
        return entry.Id;
    }

    public async Task<Result> ConsumeAsync(
        SessionBooking booking,
        CancellationToken cancellationToken)
    {
        if (booking.AccessSource is not BookingAccessSource.Credit)
        {
            return Result.Success();
        }

        if (booking.SubscriptionId is not { } subscriptionId
            || booking.CreditLedgerEntryId is null)
        {
            return Error.Conflict("booking.credit_reservation_missing");
        }

        var reserve = await context.CreditLedger
            .Where(entry => entry.BookingId == booking.Id
                            && entry.TransactionType == CreditTransactionType.Reserve)
            .Select(entry => new { entry.PeriodStart })
            .SingleOrDefaultAsync(cancellationToken);

        if (reserve is null)
        {
            return Error.Conflict("booking.credit_reservation_missing");
        }

        var alreadyConsumed = await context.CreditLedger.AnyAsync(
            entry => entry.BookingId == booking.Id
                     && entry.TransactionType == CreditTransactionType.Consume,
            cancellationToken);

        if (alreadyConsumed)
        {
            return Result.Success();
        }

        var sessionType = await GetSessionTypeAsync(booking.SessionId, cancellationToken);
        if (sessionType is null)
        {
            return Error.NotFound("booking.session_not_found");
        }

        context.CreditLedger.Add(CreditLedgerEntry.Consume(
            subscriptionId,
            booking.LearnerUserId,
            sessionType.Value,
            booking.Id,
            clock.UtcNow,
            reserve.PeriodStart));

        return Result.Success();
    }

    public async Task<Result<bool>> ReleaseAsync(
        SessionBooking booking,
        bool refundable,
        CancellationToken cancellationToken)
    {
        if (!refundable || booking.AccessSource is not BookingAccessSource.Credit)
        {
            return false;
        }

        if (booking.SubscriptionId is not { } subscriptionId
            || booking.CreditLedgerEntryId is null)
        {
            return Error.Conflict("booking.credit_reservation_missing");
        }

        var reserve = await context.CreditLedger
            .Where(entry => entry.BookingId == booking.Id
                            && entry.TransactionType == CreditTransactionType.Reserve)
            .Select(entry => new { entry.PeriodStart })
            .SingleOrDefaultAsync(cancellationToken);

        if (reserve is null)
        {
            return Error.Conflict("booking.credit_reservation_missing");
        }

        var alreadyRefunded = await context.CreditLedger.AnyAsync(
            entry => entry.BookingId == booking.Id
                     && entry.TransactionType == CreditTransactionType.Refund,
            cancellationToken);

        if (alreadyRefunded)
        {
            return false;
        }

        var sessionType = await GetSessionTypeAsync(booking.SessionId, cancellationToken);
        if (sessionType is null)
        {
            return Error.NotFound("booking.session_not_found");
        }

        context.CreditLedger.Add(CreditLedgerEntry.Refund(
            subscriptionId,
            booking.LearnerUserId,
            sessionType.Value,
            booking.Id,
            clock.UtcNow,
            periodStart: reserve.PeriodStart));

        return true;
    }

    private async Task<int> AvailableCreditsAsync(
        Guid subscriptionId,
        Guid learnerUserId,
        CreditPeriod period,
        CancellationToken cancellationToken)
        => await context.CreditLedger
            .Where(entry => entry.SubscriptionId == subscriptionId
                            && entry.LearnerUserId == learnerUserId
                            && entry.SessionType == SessionType.Private
                            && (entry.PeriodStart == period.Start
                                || (period.IsLegacy && entry.PeriodStart == null)))
            .SumAsync(entry => (int?)entry.Quantity, cancellationToken) ?? 0;

    private async Task<CreditPeriod?> CurrentCreditPeriodAsync(
        Guid subscriptionId,
        Guid learnerUserId,
        CancellationToken cancellationToken)
    {
        var grant = await context.CreditLedger
            .Where(entry => entry.SubscriptionId == subscriptionId
                            && entry.LearnerUserId == learnerUserId
                            && entry.SessionType == SessionType.Private
                            && entry.TransactionType == CreditTransactionType.PeriodGrant
                            && entry.PeriodStart <= clock.UtcNow
                            && (entry.ExpiresAt == null || entry.ExpiresAt > clock.UtcNow))
            .OrderByDescending(entry => entry.PeriodStart ?? entry.CreatedAt)
            .Select(entry => new { entry.PeriodStart, entry.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        return grant is null
            ? null
            : new CreditPeriod(grant.PeriodStart ?? grant.CreatedAt, grant.PeriodStart is null);
    }

    private async Task<SessionType?> GetSessionTypeAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
        => await context.LessonSessions
            .Where(session => session.Id == sessionId)
            .Select(session => (SessionType?)session.SessionType)
            .SingleOrDefaultAsync(cancellationToken);

    private sealed record CreditPeriod(DateTimeOffset Start, bool IsLegacy);
}
