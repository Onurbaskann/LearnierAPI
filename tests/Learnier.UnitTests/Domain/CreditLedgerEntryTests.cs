using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;
using Shouldly;

namespace Learnier.UnitTests.Domain;

public sealed class CreditLedgerEntryTests
{
    private readonly Guid _subscriptionId = Guid.NewGuid();
    private readonly Guid _learnerId = Guid.NewGuid();
    private readonly Guid _bookingId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void GrantAndReserve_ShouldChangeAvailableBalance()
    {
        var grant = CreditLedgerEntry.Grant(
            _subscriptionId, _learnerId, SessionType.Private, 12, _now);
        var reserve = CreditLedgerEntry.Reserve(
            _subscriptionId, _learnerId, SessionType.Private, _bookingId, _now);

        grant.Quantity.ShouldBe(12);
        grant.TransactionType.ShouldBe(CreditTransactionType.PeriodGrant);
        reserve.Quantity.ShouldBe(-1);
        reserve.TransactionType.ShouldBe(CreditTransactionType.Reserve);
        (grant.Quantity + reserve.Quantity).ShouldBe(11);
    }

    [Fact]
    public void Consume_ShouldBeZeroQuantityAuditMovement()
    {
        var consume = CreditLedgerEntry.Consume(
            _subscriptionId, _learnerId, SessionType.Private, _bookingId, _now);

        consume.Quantity.ShouldBe(0);
        consume.TransactionType.ShouldBe(CreditTransactionType.Consume);
        consume.BookingId.ShouldBe(_bookingId);
    }

    [Fact]
    public void Refund_ShouldReverseReservation()
    {
        var reserve = CreditLedgerEntry.Reserve(
            _subscriptionId, _learnerId, SessionType.Private, _bookingId, _now);
        var refund = CreditLedgerEntry.Refund(
            _subscriptionId, _learnerId, SessionType.Private, _bookingId, _now);

        refund.Quantity.ShouldBe(1);
        refund.TransactionType.ShouldBe(CreditTransactionType.Refund);
        (reserve.Quantity + refund.Quantity).ShouldBe(0);
    }

    [Fact]
    public void Expire_ShouldReduceBalance()
    {
        var expire = CreditLedgerEntry.Expire(
            _subscriptionId, _learnerId, SessionType.Private, 3, _now);

        expire.Quantity.ShouldBe(-3);
        expire.TransactionType.ShouldBe(CreditTransactionType.Expire);
    }
}
