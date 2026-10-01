using System.Net;
using AilabTemplate.Api.Health;
using Microsoft.Extensions.DependencyInjection;

namespace AilabTemplate.Api.Tests;

public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Healthz_Returns200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/healthz", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readyz_Returns200_AfterStartup()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/readyz", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readyz_Returns503_WhileNotReady_ButHealthzStays200()
    {
        using var client = factory.CreateClient();
        var readiness = factory.Services.GetRequiredService<ReadinessState>();

        readiness.MarkNotReady();
        try
        {
            using var ready = await client.GetAsync(new Uri("/readyz", UriKind.Relative));
            using var live = await client.GetAsync(new Uri("/healthz", UriKind.Relative));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        }
        finally
        {
            readiness.MarkReady();
        }
    }
}
