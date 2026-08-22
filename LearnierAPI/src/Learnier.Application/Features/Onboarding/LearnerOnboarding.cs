using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Progress;

namespace Learnier.Application.Features.Onboarding;

public sealed record SaveLearnerOnboardingCommand(
    Guid SubjectId,
    LearningGoal LearningGoal,
    SelfAssessment SelfAssessment,
    IReadOnlyList<string> DifficultyAreas,
    LessonFocus LessonFocus,
    InstructorPreference InstructorPreference,
    int WeeklyLessonGoal,
    IReadOnlyList<string> AvailabilityPreferences);

public sealed record LearnerOnboardingResult(
    Guid Id,
    Guid SubjectId,
    string SubjectName,
    Guid? EstimatedLevelId,
    string? EstimatedLevelCode,
    LearningGoal LearningGoal,
    SelfAssessment SelfAssessment,
    IReadOnlyList<string> DifficultyAreas,
    LessonFocus LessonFocus,
    InstructorPreference InstructorPreference,
    int WeeklyLessonGoal,
    IReadOnlyList<string> AvailabilityPreferences,
    DateTimeOffset CompletedAt);

internal sealed class SaveLearnerOnboardingValidator
    : AbstractValidator<SaveLearnerOnboardingCommand>
{
    private static readonly HashSet<string> DifficultyCodes =
    ["Speaking", "Listening", "SentenceBuilding", "Vocabulary", "Confidence", "ReadingWriting"];

    private static readonly HashSet<string> AvailabilityCodes =
    ["WeekdayDaytime", "WeekdayEvening", "Weekend", "Flexible"];

    public SaveLearnerOnboardingValidator()
    {
        RuleFor(command => command.SubjectId).NotEmpty().WithErrorCode("onboarding.subject_required");
        RuleFor(command => command.LearningGoal).IsInEnum().WithErrorCode("onboarding.goal_invalid");
        RuleFor(command => command.SelfAssessment).IsInEnum().WithErrorCode("onboarding.level_invalid");
        RuleFor(command => command.LessonFocus).IsInEnum().WithErrorCode("onboarding.focus_invalid");
        RuleFor(command => command.InstructorPreference).IsInEnum().WithErrorCode("onboarding.instructor_preference_invalid");
        RuleFor(command => command.WeeklyLessonGoal).InclusiveBetween(1, 7)
            .WithErrorCode("onboarding.weekly_goal_invalid");
        RuleFor(command => command.DifficultyAreas)
            .NotEmpty().WithErrorCode("onboarding.difficulty_required")
            .Must(values => values.All(DifficultyCodes.Contains))
            .WithErrorCode("onboarding.difficulty_invalid");
        RuleFor(command => command.AvailabilityPreferences)
            .NotEmpty().WithErrorCode("onboarding.availability_required")
            .Must(values => values.All(AvailabilityCodes.Contains))
            .WithErrorCode("onboarding.availability_invalid");
    }
}

public sealed class SaveLearnerOnboardingHandler(
    ILearnerOnboardingRepository repository,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<LearnerOnboardingResult>> Handle(
        SaveLearnerOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentTenant.OrganizationId is not { } organizationId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var subject = await repository.FindSubjectAsync(command.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Error.NotFound("onboarding.subject_not_found");
        }

        var levels = await repository.ListLevelsAsync(subject.Id, cancellationToken);
        var estimatedLevel = levels.Count == 0
            ? null
            : levels[Math.Min((int)command.SelfAssessment, levels.Count - 1)];

        var profile = await repository.FindAsync(userId, subject.Id, cancellationToken);
        if (profile is null)
        {
            profile = LearnerOnboardingProfile.Create(
                organizationId, userId, subject.Id, estimatedLevel?.Id,
                command.LearningGoal, command.SelfAssessment, command.LessonFocus,
                command.InstructorPreference, command.DifficultyAreas,
                command.WeeklyLessonGoal, command.AvailabilityPreferences, clock.UtcNow);
            repository.Add(profile);
        }
        else
        {
            profile.Update(
                estimatedLevel?.Id, command.LearningGoal, command.SelfAssessment,
                command.LessonFocus, command.InstructorPreference, command.DifficultyAreas,
                command.WeeklyLessonGoal, command.AvailabilityPreferences, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(profile, subject.Name, estimatedLevel?.Code);
    }

    internal static LearnerOnboardingResult ToResult(
        LearnerOnboardingProfile profile,
        string subjectName,
        string? estimatedLevelCode)
        => new(
            profile.Id, profile.SubjectId, subjectName, profile.EstimatedLevelId,
            estimatedLevelCode, profile.LearningGoal, profile.SelfAssessment,
            profile.DifficultyAreas, profile.LessonFocus, profile.InstructorPreference,
            profile.WeeklyLessonGoal, profile.AvailabilityPreferences, profile.CompletedAt);
}

public sealed class GetMyLearnerOnboardingHandler(
    ILearnerOnboardingRepository repository,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<LearnerOnboardingResult>>> Handle(
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var profiles = await repository.ListAsync(userId, cancellationToken);
        return profiles.Select(profile => SaveLearnerOnboardingHandler.ToResult(
            profile, profile.Subject.Name, profile.EstimatedLevel?.Code)).ToList();
    }
}
