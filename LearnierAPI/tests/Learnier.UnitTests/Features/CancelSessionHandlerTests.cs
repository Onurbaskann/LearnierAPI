using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Application.Features.Scheduling.Commands.CancelSession;
using Learnier.Domain.Scheduling;
using Learnier.Domain.Teaching;
using NSubstitute;
using Shouldly;

namespace Learnier.UnitTests.Features;

public sealed class CancelSessionHandlerTests
{
    [Fact]
    public async Task Instructor_CanCancelWithinFourHours_AndReceivesPenalty()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddHours(1));

        var result = await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand("Program değişikliği"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        setup.Session.Status.ShouldBe(LessonSessionStatus.Cancelled);
        await setup.Compensation.Received(1).RegisterLateCancellationAsync(
            Arg.Any<Guid>(), setup.Session.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Instructor_CanCancelMoreThanFourHoursBeforeStart_WithoutPenalty()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddHours(4).AddMinutes(1));

        var result = await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand("Program değişikliği"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        setup.Session.Status.ShouldBe(LessonSessionStatus.Cancelled);
        await setup.Compensation.DidNotReceive().RegisterLateCancellationAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        setup.Session.CancellationReason.ShouldBe("Program değişikliği");
    }

    [Fact]
    public async Task Instructor_CancelsExactlyAtPolicyDeadline_WithoutPenalty()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddHours(6));
        setup.Session.ApplyCancellationPolicy(60, 360, 2);

        var result = await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand(null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await setup.Compensation.DidNotReceive().RegisterLateCancellationAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Instructor_CancelsAfterPolicyDeadline_AndReceivesPenalty()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddHours(6).AddTicks(-1));
        setup.Session.ApplyCancellationPolicy(60, 360, 2);

        var result = await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand(null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await setup.Compensation.Received(1).RegisterLateCancellationAsync(
            Arg.Any<Guid>(), setup.Session.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Instructor_CannotCancelAnotherInstructorsLesson()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddDays(1), assignOwnedInstructor: false);

        var result = await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand(null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("scheduling.session_not_owned");
    }

    [Fact]
    public async Task Instructor_ClosingSessionWithoutBookings_ReceivesNoPenalty()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddMinutes(30), withBooking: false);

        var result = await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand(null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PenaltyApplied.ShouldBeFalse();
        result.Value.CancelledBookingCount.ShouldBe(0);
        await setup.Compensation.DidNotReceive().RegisterLateCancellationAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Instructor_CannotCancelAfterSessionStarted()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddTicks(-1));

        var result = await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand(null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("scheduling.instructor_cancellation_deadline_passed");
    }

    [Fact]
    public async Task Instructor_CancellingAlreadyCancelledSession_CreatesNoSecondPenalty()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddHours(1));

        await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand(null),
            TestContext.Current.CancellationToken);
        var second = await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand(null),
            TestContext.Current.CancellationToken);

        second.IsSuccess.ShouldBeTrue();
        second.Value.PenaltyApplied.ShouldBeFalse();
        await setup.Compensation.Received(1).RegisterLateCancellationAsync(
            Arg.Any<Guid>(), setup.Session.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Instructor_LateCancellation_ReportsPenaltyPercentage()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var setup = CreateHandler(now, now.AddHours(1));

        var result = await setup.Handler.Handle(
            setup.Session.Id,
            isInstructorInitiated: true,
            new CancelSessionCommand(null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PenaltyApplied.ShouldBeTrue();
        result.Value.PenaltyPercentage.ShouldBe(15m);
        result.Value.StudentCreditsRefunded.ShouldBe(1);
    }

    private static HandlerSetup CreateHandler(
        DateTimeOffset now,
        DateTimeOffset startsAt,
        bool assignOwnedInstructor = true,
        bool withBooking = true)
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

        var booking = session.Book(
            Guid.NewGuid(),
            Guid.NewGuid(),
            BookingAccessSource.Credit,
            now.AddDays(-1),
            reservedSeatCount: 0);

        var scheduling = Substitute.For<ISchedulingRepository>();
        scheduling.FindSessionForUpdateAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);
        scheduling.FindSessionAsync(session.Id, true, Arg.Any<CancellationToken>())
            .Returns(session);
        scheduling.ListActiveBookingsAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(withBooking ? [booking] : []);
        scheduling.LockInstructorAsync(profile.Id, Arg.Any<CancellationToken>())
            .Returns(true);

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

        var compensation = Substitute.For<IInstructorCompensationService>();
        compensation.RegisterLateCancellationAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new LateCancellationOutcome(true, 15m)));

        var entitlements = Substitute.For<IBookingEntitlementPolicy>();
        entitlements.ReleaseAsync(
                Arg.Any<SessionBooking>(), true, Arg.Any<CancellationToken>())
            .Returns(Result.Success(true));
        var meetings = Substitute.For<IMeetingRepository>();

        var handler = new CancelSessionHandler(
            scheduling,
            instructors,
            entitlements,
            compensation,
            meetings,
            tenant,
            unitOfWork,
            clock);

        return new HandlerSetup(handler, session, compensation);
    }

    private sealed record HandlerSetup(
        CancelSessionHandler Handler,
        LessonSession Session,
        IInstructorCompensationService Compensation);
}
