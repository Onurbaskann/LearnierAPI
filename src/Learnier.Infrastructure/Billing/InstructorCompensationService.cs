using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Billing;

internal sealed class InstructorCompensationService(
    AppDbContext context,
    ICurrentTenant currentTenant,
    IClock clock) : IInstructorCompensationService
{
    public async Task<Result> RegisterLateCancellationAsync(
        Guid instructorProfileId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var state = await context.InstructorPenaltyStates
            .SingleOrDefaultAsync(
                item => item.InstructorProfileId == instructorProfileId,
                cancellationToken);

        if (state is null)
        {
            state = InstructorPenaltyState.Create(instructorProfileId);
            context.InstructorPenaltyStates.Add(state);
        }

        state.RegisterLateCancellation(sessionId, clock.UtcNow);
        return Result.Success();
    }

    public async Task<Result> CreateEarningsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await context.LessonSessions
            .Where(item => item.Id == sessionId)
            .Select(item => new
            {
                item.Id,
                item.OrganizationId,
                item.StartsAt,
                item.EndsAt,
                item.Course.SubjectId,
                InstructorIds = item.Instructors.Select(instructor => instructor.InstructorProfileId)
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Error.NotFound("scheduling.session_not_found");
        }

        var durationMinutes = (int)(session.EndsAt - session.StartsAt).TotalMinutes;
        var configuredRate = await context.InstructorCompensationRates
            .Where(rate => rate.SubjectId == session.SubjectId
                           && rate.LessonDurationMinutes == durationMinutes
                           && rate.IsActive)
            .Select(rate => new { rate.Amount, rate.Currency })
            .SingleOrDefaultAsync(cancellationToken);

        if (configuredRate is null)
        {
            return Error.Conflict(
                "compensation.rate_not_configured",
                ("duration", durationMinutes));
        }

        foreach (var instructorId in session.InstructorIds.Order())
        {
            // Farkli derslerin ayni anda tamamlanmasi halinde tek penalty'nin iki
            // kazanca birden uygulanmasini engeller.
            await context.InstructorProfiles
                .FromSqlInterpolated(
                    $"SELECT * FROM instructor_profiles WHERE id = {instructorId} FOR UPDATE")
                .IgnoreQueryFilters()
                .SingleAsync(cancellationToken);

            if (await context.InstructorEarnings.AnyAsync(
                    earning => earning.SessionId == sessionId
                               && earning.InstructorProfileId == instructorId,
                    cancellationToken))
            {
                continue;
            }

            var penaltyState = await context.InstructorPenaltyStates.SingleOrDefaultAsync(
                state => state.InstructorProfileId == instructorId,
                cancellationToken);
            var penaltyPercentage = penaltyState is { Level: > 0 }
                ? await ResolvePenaltyPercentageAsync(penaltyState.Level, cancellationToken)
                : 0m;

            context.InstructorEarnings.Add(InstructorEarning.Create(
                sessionId,
                instructorId,
                session.SubjectId,
                durationMinutes,
                configuredRate.Amount,
                penaltyPercentage,
                configuredRate.Currency,
                clock.UtcNow));

            // Penalty, bir sonraki ders gerçekten tamamlandığında tek seferde kapanır.
            penaltyState?.Clear();
        }

        return Result.Success();
    }

    public async Task<Result<Guid>> ConfigureRateAsync(
        Guid subjectId,
        int lessonDurationMinutes,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return Error.Forbidden("tenant.organization_required");
        }

        if (!await context.Subjects.AnyAsync(subject => subject.Id == subjectId, cancellationToken))
        {
            return Error.NotFound("catalog.subject_not_found");
        }

        var rate = await context.InstructorCompensationRates.SingleOrDefaultAsync(
            item => item.SubjectId == subjectId
                    && item.LessonDurationMinutes == lessonDurationMinutes,
            cancellationToken);

        if (rate is null)
        {
            rate = InstructorCompensationRate.Create(
                organizationId, subjectId, lessonDurationMinutes, amount, currency);
            context.InstructorCompensationRates.Add(rate);
        }
        else
        {
            rate.Update(amount, currency);
        }

        return rate.Id;
    }

    public async Task<Result> ConfigurePenaltyStepsAsync(
        IReadOnlyList<decimal> percentages,
        CancellationToken cancellationToken)
    {
        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return Error.Forbidden("tenant.organization_required");
        }

        var existing = await context.InstructorPenaltySteps
            .OrderBy(step => step.Level)
            .ToListAsync(cancellationToken);

        for (var index = 0; index < percentages.Count; index++)
        {
            var level = index + 1;
            var step = existing.FirstOrDefault(item => item.Level == level);
            if (step is null)
            {
                context.InstructorPenaltySteps.Add(
                    InstructorPenaltyStep.Create(organizationId, level, percentages[index]));
            }
            else
            {
                step.Update(percentages[index]);
            }
        }

        if (existing.Any(step => step.Level > percentages.Count))
        {
            context.InstructorPenaltySteps.RemoveRange(
                existing.Where(step => step.Level > percentages.Count));
        }

        return Result.Success();
    }

    private async Task<decimal> ResolvePenaltyPercentageAsync(
        int level,
        CancellationToken cancellationToken)
    {
        var percentage = await context.InstructorPenaltySteps
            .Where(step => step.Level <= level)
            .OrderByDescending(step => step.Level)
            .Select(step => (decimal?)step.Percentage)
            .FirstOrDefaultAsync(cancellationToken);

        if (percentage is not null)
        {
            return percentage.Value;
        }

        // Kurum ayar yapana kadar ürünün kararlaştırılan başlangıç basamakları.
        return level switch
        {
            1 => 10m,
            2 => 15m,
            3 => 20m,
            _ => 25m
        };
    }
}
