using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Scheduling.Commands.CancelSession;
using Learnier.Domain.Scheduling;
using Learnier.Domain.Teaching;
using NSubstitute;
using Shouldly;

namespace Learnier.UnitTests.Features;

public sealed class CancelSessionHandlerTests
{
    [Fact]
    public async Task Instructor_CannotCancelLessonAtOneHourBoundary()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddHours(1));

        var result = await setup.Handler.Handle(
            new CancelSessionCommand(setup.Session.Id, "Program değişikliği", true),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("scheduling.instructor_cancellation_deadline_passed");
        setup.Session.Status.ShouldBe(LessonSessionStatus.Scheduled);
    }

    [Fact]
    public async Task Instructor_CanCancelOwnLessonMoreThanOneHourBeforeStart()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddHours(1).AddMinutes(1));

        var result = await setup.Handler.Handle(
            new CancelSessionCommand(setup.Session.Id, "Program değişikliği", true),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        setup.Session.Status.ShouldBe(LessonSessionStatus.Cancelled);
        setup.Session.CancellationReason.ShouldBe("Program değişikliği");
    }

    [Fact]
    public async Task Instructor_CannotCancelAnotherInstructorsLesson()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddDays(1), assignOwnedInstructor: false);

        var result = await setup.Handler.Handle(
            new CancelSessionCommand(setup.Session.Id, null, true),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("scheduling.session_not_owned");
    }

    private static HandlerSetup CreateHandler(
        DateTimeOffset now,
        DateTimeOffset startsAt,
        bool assignOwnedInstructor = true)
    {
        var membershipId = Guid.NewGuid();
        var profile = InstructorProfile.Create(membershipId, "Europe/Istanbul");
        var session = LessonSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionType.Private,
            startsAt,
            startsAt.AddMinutes(50),
            1,
            1);
        session.AssignInstructor(
            assignOwnedInstructor ? profile.Id : Guid.NewGuid(),
            SessionInstructorRole.Lead);

        var scheduling = Substitute.For<ISchedulingRepository>();
        scheduling.FindSessionForUpdateAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);
        scheduling.FindSessionAsync(session.Id, true, Arg.Any<CancellationToken>())
            .Returns(session);
        scheduling.ListActiveBookingsAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns([]);

        var instructors = Substitute.For<IInstructorRepository>();
        instructors.FindByMembershipAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns(profile);

        var tenant = Substitute.For<ICurrentTenant>();
        tenant.HasTenant.Returns(true);
        tenant.MembershipId.Returns(membershipId);

        var transaction = Substitute.For<ITransaction>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(transaction);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);

        var handler = new CancelSessionHandler(
            scheduling,
            instructors,
            Substitute.For<IBookingEntitlementPolicy>(),
            tenant,
            unitOfWork,
            clock);

        return new HandlerSetup(handler, session);
    }

    private sealed record HandlerSetup(CancelSessionHandler Handler, LessonSession Session);
}
