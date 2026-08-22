using Learnier.Application.Common.Results;
using Shouldly;

namespace Learnier.UnitTests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_ExposesValue()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Failure_CarriesError()
    {
        Result<int> result = Error.NotFound("course.not_found");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("course.not_found");
    }

    [Fact]
    public void Failure_ReadingValue_Throws()
    {
        // Sessizce default deger dondurmek, hatanin fark edilmeden
        // akisin devam etmesine yol acardi.
        Result<int> result = Error.Conflict("booking.session_full");

        Should.Throw<InvalidOperationException>(() => result.Value);
    }
}
