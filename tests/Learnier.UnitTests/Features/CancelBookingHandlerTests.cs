using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Application.Features.Scheduling.Commands.CancelBooking;
using Learnier.Domain.Scheduling;
using NSubstitute;
using Shouldly;

namespace Learnier.UnitTests.Features;

public sealed class CancelBookingHandlerTests
{
    [Fact]
    public async Task Learner_CancelsExactlyAtDeadline_AndCreditIsRefunded()
    {
        var setup = CreateHandler(TimeSpan.FromHours(1));

        var result = await setup.Handler.Handle(
            new CancelBookingCommand(setup.Booking.Id),
            false,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Refunded.ShouldBeTrue();
        await setup.Entitlements.Received(1).ReleaseAsync(
            setup.Booking, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Learner_CancelsInsideDeadline_AndCreditIsConsumed()
    {
        var setup = CreateHandler(TimeSpan.FromHours(1).Subtract(TimeSpan.FromTicks(1)));

        var result = await setup.Handler.Handle(
            new CancelBookingCommand(setup.Booking.Id),
            false,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Refunded.ShouldBeFalse();
        await setup.Entitlements.Received(1).ReleaseAsync(
            setup.Booking, false, Arg.Any<CancellationToken>());
    }

    private static HandlerSetup CreateHandler(TimeSpan timeUntilStart)
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var learnerId = Guid.NewGuid();
        var session = LessonSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), SessionType.Private,
            now.Add(timeUntilStart), now.Add(timeUntilStart).AddMinutes(50), 1, 1);
        session.ApplyCancellationPolicy(60, 240, 1);
        var booking = session.Book(
            learnerId, learnerId, BookingAccessSource.Subscription, now.AddDays(-1), 0);

        var scheduling = Substitute.For<ISchedulingRepository>();
        scheduling.FindBookingAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        scheduling.FindSessionForUpdateAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        scheduling.FindNextWaitlistedAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns((SessionBooking?)null);

        var entitlements = Substitute.For<IBookingEntitlementPolicy>();
        entitlements.ReleaseAsync(booking, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<bool>.Success(call.ArgAt<bool>(1)));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(learnerId);
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.HasTenant.Returns(true);

        var transaction = Substitute.For<ITransaction>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);

        return new HandlerSetup(
            new CancelBookingHandler(
                scheduling, entitlements, currentUser, tenant, unitOfWork, clock),
            booking,
            entitlements);
    }

    private sealed record HandlerSetup(
        CancelBookingHandler Handler,
        SessionBooking Booking,
        IBookingEntitlementPolicy Entitlements);
}
