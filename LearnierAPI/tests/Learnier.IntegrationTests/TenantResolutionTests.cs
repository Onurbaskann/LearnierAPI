using System.Net;
using System.Net.Http.Headers;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Tenant cozumlemesinin kapali varsayilan davrandigini dogrular.
/// </summary>
public sealed class TenantResolutionTests(LearnierApiFactory factory)
    : IClassFixture<LearnierApiFactory>
{
    private const string OrganizationHeader = "X-Organization-Id";

    private static readonly Uri HealthEndpoint = new("/health", UriKind.Relative);

    [Fact]
    public async Task WithoutOrganizationHeader_RequestPasses()
    {
        // Giris ve kayit gibi endpoint'ler organizasyon kapsami disindadir.
        using var client = factory.CreateClient();

        var response = await client.GetAsync(HealthEndpoint, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithOrganizationHeader_ButUnauthenticated_IsRejected()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(OrganizationHeader, Guid.CreateVersion7().ToString());

        var response = await client.GetAsync(HealthEndpoint, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WithMalformedOrganizationId_IsRejected()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(OrganizationHeader, "bu-bir-guid-degil");

        var response = await client.GetAsync(HealthEndpoint, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WithInvalidToken_IsRejected()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "gecersiz.token.degeri");
        client.DefaultRequestHeaders.Add(OrganizationHeader, Guid.CreateVersion7().ToString());

        var response = await client.GetAsync(HealthEndpoint, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
