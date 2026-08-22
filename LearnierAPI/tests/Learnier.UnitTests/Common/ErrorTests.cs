using Learnier.Application.Common.Results;
using Shouldly;

namespace Learnier.UnitTests.Common;

public sealed class ErrorTests
{
    [Fact]
    public void Create_CarriesCodeAndParameters()
    {
        var error = Error.Conflict("booking.session_full", ("capacity", 12));

        error.Code.ShouldBe("booking.session_full");
        error.Type.ShouldBe(ErrorType.Conflict);
        error.Parameters["capacity"].ShouldBe(12);
    }

    [Fact]
    public void Create_WithoutParameters_YieldsEmptyParameters()
    {
        var error = Error.NotFound("course.not_found");

        error.Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void Create_WithBlankCode_Throws()
    {
        // Bos kod, ceviri asamasinda sessizce bos metne donusurdu;
        // hatanin olustugu yerde patlamasi tercih edilir.
        Should.Throw<ArgumentException>(() => Error.Validation("  "));
    }

    /// <summary>
    /// Lokalizasyon kuralinin bekcisi: <see cref="Error"/> uzerinde kullaniciya
    /// gosterilecek metin tasiyan bir uye bulunmamali. Boyle bir uye eklenirse
    /// ceviri Application katmanina sizmaya baslar.
    /// </summary>
    [Fact]
    public void Error_HasNoUserFacingTextMember()
    {
        var textMembers = typeof(Error)
            .GetProperties()
            .Where(p => p.Name is "Message" or "Description" or "Text")
            .ToList();

        textMembers.ShouldBeEmpty(
            "Error yalnizca kod ve parametre tasimali; metin WebApi katmaninda uretilir.");
    }
}
