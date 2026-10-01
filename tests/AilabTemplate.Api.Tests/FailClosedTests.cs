using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AilabTemplate.Api.Tests;

/// <summary>With no token configured the protected surface must refuse everyone, never allow everyone.</summary>
public sealed class FailClosedTests(NoTokenApiFactory factory) : IClassFixture<NoTokenApiFactory>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("anything")]
    public async Task Score_WhenTokenUnset_Returns503ForEveryCaller(string? presented)
    {
        using var client = factory.CreateClient();
        if (presented is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", presented);
        }

        using var response = await client.PostAsJsonAsync(new Uri("/v1/score", UriKind.Relative), new { text = "great" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    public async Task HealthEndpoints_StayUnauthenticated_WhenTokenUnset(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
