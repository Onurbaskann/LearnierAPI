using System.Net;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Tarayici istemcileri icin CORS politikasinin yalnizca yapilandirilan
/// origin'lere izin verdigini dogrular.
/// </summary>
public sealed class CorsTests(LearnierApiFactory factory)
    : IClassFixture<LearnierApiFactory>
{
    private const string AllowedOrigin = "http://localhost:8082";
    private const string DisallowedOrigin = "https://example.com";

    private static readonly Uri HealthEndpoint = new("/health", UriKind.Relative);

    [Fact]
    public async Task Preflight_FromAllowedOrigin_IsAccepted()
    {
        using var client = factory.CreateClient();
        using var request = CreatePreflightRequest(AllowedOrigin);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldContain(AllowedOrigin);
        response.Headers.GetValues("Access-Control-Allow-Methods").ShouldContain("GET");
    }

    [Fact]
    public async Task Preflight_FromDisallowedOrigin_HasNoCorsPermission()
    {
        using var client = factory.CreateClient();
        using var request = CreatePreflightRequest(DisallowedOrigin);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, HealthEndpoint);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }
}
