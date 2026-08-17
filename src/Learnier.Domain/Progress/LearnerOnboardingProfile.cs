using Learnier.Domain.Catalog;
using Learnier.Domain.Common;

namespace Learnier.Domain.Progress;

public enum LearningGoal
{
    Career,
    SchoolSupport,
    ExamPreparation,
    Travel,
    ConversationPractice,
    Hobby
}

public enum SelfAssessment
{
    StartingFromScratch,
    BasicUnderstanding,
    SimpleCommunication,
    Comfortable,
    Advanced
}

public enum LessonFocus
{
    Conversation,
    DailyUse,
    Professional,
    Exam,
    StructuredFoundation
}

public enum InstructorPreference
{
    Motivating,
    Patient,
    Structured,
    Conversational,
    NoPreference
}

/// <summary>Öğrencinin bir ders alanındaki ihtiyaç analizi ve başlangıç önerisi.</summary>
public sealed class LearnerOnboardingProfile : Entity, IAuditableEntity, ITenantScoped
{
    private LearnerOnboardingProfile()
    {
        DifficultyAreas = [];
        AvailabilityPreferences = [];
    }

    public Guid OrganizationId { get; private set; }
    public Guid LearnerUserId { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid? EstimatedLevelId { get; private set; }
    public LearningGoal LearningGoal { get; private set; }
    public SelfAssessment SelfAssessment { get; private set; }
    public LessonFocus LessonFocus { get; private set; }
    public InstructorPreference InstructorPreference { get; private set; }
    public string[] DifficultyAreas { get; private set; }
    public string[] AvailabilityPreferences { get; private set; }
    public int WeeklyLessonGoal { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }
    public Subject Subject { get; private set; } = null!;
    public Level? EstimatedLevel { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static LearnerOnboardingProfile Create(
        Guid organizationId,
        Guid learnerUserId,
        Guid subjectId,
        Guid? estimatedLevelId,
        LearningGoal learningGoal,
        SelfAssessment selfAssessment,
        LessonFocus lessonFocus,
        InstructorPreference instructorPreference,
        IEnumerable<string> difficultyAreas,
        int weeklyLessonGoal,
        IEnumerable<string> availabilityPreferences,
        DateTimeOffset completedAt)
    {
        var profile = new LearnerOnboardingProfile
        {
            OrganizationId = organizationId,
            LearnerUserId = learnerUserId,
            SubjectId = subjectId
        };
        profile.Update(
            estimatedLevelId,
            learningGoal,
            selfAssessment,
            lessonFocus,
            instructorPreference,
            difficultyAreas,
            weeklyLessonGoal,
            availabilityPreferences,
            completedAt);
        return profile;
    }

    public void Update(
        Guid? estimatedLevelId,
        LearningGoal learningGoal,
        SelfAssessment selfAssessment,
        LessonFocus lessonFocus,
        InstructorPreference instructorPreference,
        IEnumerable<string> difficultyAreas,
        int weeklyLessonGoal,
        IEnumerable<string> availabilityPreferences,
        DateTimeOffset completedAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(weeklyLessonGoal, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weeklyLessonGoal, 7);

        EstimatedLevelId = estimatedLevelId;
        LearningGoal = learningGoal;
        SelfAssessment = selfAssessment;
        LessonFocus = lessonFocus;
        InstructorPreference = instructorPreference;
        DifficultyAreas = Normalize(difficultyAreas);
        WeeklyLessonGoal = weeklyLessonGoal;
        AvailabilityPreferences = Normalize(availabilityPreferences);
        CompletedAt = completedAt;
    }

    private static string[] Normalize(IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
