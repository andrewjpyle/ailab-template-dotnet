using AilabTemplate.Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AilabTemplate.Api.Tests;

/// <summary>Hosts the real app in-memory with a known token.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Token = "test-token-not-a-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting(AilabOptions.ApiTokenVariable, Token)
               .UseSetting(AilabOptions.MaxTextLengthVariable, "200");
}

/// <summary>Hosts the app with no token configured, to prove the protected surface fails closed.</summary>
public sealed class NoTokenApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting(AilabOptions.ApiTokenVariable, string.Empty);
}
