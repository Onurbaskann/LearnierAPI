using Learnier.Domain.Common;
using Shouldly;

namespace Learnier.UnitTests.Common;

public sealed class EntityTests
{
    private sealed class TestEntity : Entity;

    private sealed record TestOccurred : DomainEvent;

    private sealed class TestAggregate : AggregateRoot
    {
        public void DoSomething() => RaiseDomainEvent(new TestOccurred());
    }

    [Fact]
    public void Id_IsUuidVersion7()
    {
        var entity = new TestEntity();

        // UUID surum numarasi 7. baytin ust yarisinda tutulur.
        var version = (entity.Id.ToByteArray(bigEndian: true)[6] & 0xF0) >> 4;

        version.ShouldBe(7, "index yerelligi icin v4 degil v7 kullanilmali.");
    }

    [Fact]
    public void Id_IsTimeOrdered()
    {
        var first = new TestEntity().Id;
        Thread.Sleep(2);
        var second = new TestEntity().Id;

        var firstTimestamp = first.ToByteArray(bigEndian: true).AsSpan(0, 6);
        var secondTimestamp = second.ToByteArray(bigEndian: true).AsSpan(0, 6);

        secondTimestamp.SequenceCompareTo(firstTimestamp).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Equals_ComparesById()
    {
        var entity = new TestEntity();
        var other = new TestEntity();

        entity.Equals(other).ShouldBeFalse();
        entity.Equals(entity).ShouldBeTrue();
    }

    [Fact]
    public void AggregateRoot_CollectsAndClearsDomainEvents()
    {
        var aggregate = new TestAggregate();

        aggregate.DoSomething();
        aggregate.DomainEvents.Count.ShouldBe(1);

        aggregate.ClearDomainEvents();
        aggregate.DomainEvents.ShouldBeEmpty();
    }
}
