using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Hata metinlerinin istegin diline gore uretildigini uctan uca dogrular.
/// </summary>
/// <remarks>
/// Bu test lokalizasyon zincirinin tamamini kapsar: hata kodu -> kaynak dosya ->
/// Accept-Language ile secilen kultur -> ProblemDetails.detail.
/// Zincirin herhangi bir halkasi kopar ve metin sabitlenirse test kirilir.
/// </remarks>
public sealed class LocalizationTests(LearnierApiFactory factory)
    : IClassFixture<LearnierApiFactory>
{
    private static readonly Uri HealthEndpoint = new("/health", UriKind.Relative);

    [Theory]
    [InlineData("tr", "kimlik doğrulaması")]
    [InlineData("en", "Authentication is required")]
    public async Task ErrorDetail_IsLocalizedPerRequestLanguage(string language, string expectedFragment)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", language);
        client.DefaultRequestHeaders.Add("X-Organization-Id", Guid.CreateVersion7().ToString());

        var response = await client.GetAsync(HealthEndpoint, TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var detail = problem.GetProperty("detail").GetString();

        detail.ShouldNotBeNull();
        detail.ShouldContain(expectedFragment, Case.Insensitive);
    }

    [Fact]
    public async Task ErrorResponse_CarriesStableErrorCode()
    {
        // Istemci metne degil koda gore dal ayirabilmeli; metin dile gore degisir, kod degismez.
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Organization-Id", Guid.CreateVersion7().ToString());

        var response = await client.GetAsync(HealthEndpoint, TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        problem.GetProperty("errorCode").GetString().ShouldBe("common.unauthorized");
    }
}
