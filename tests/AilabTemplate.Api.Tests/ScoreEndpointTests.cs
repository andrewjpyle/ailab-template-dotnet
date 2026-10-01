using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AilabTemplate.Api.Tests;

public sealed class ScoreEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Uri ScoreUri = new("/v1/score", UriKind.Relative);

    private HttpClient AuthorizedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Token);
        return client;
    }

    [Fact]
    public async Task Score_WithoutToken_Returns401WithChallenge()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(ScoreUri, new { text = "great" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Theory]
    [InlineData("Bearer wrong-token")]
    [InlineData("Basic dGVzdDp0ZXN0")]
    [InlineData("Bearer")]
    [InlineData("test-token-not-a-secret")]
    public async Task Score_WithBadAuthorization_Returns401(string header)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, ScoreUri) { Content = JsonContent.Create(new { text = "great" }) };
        request.Headers.TryAddWithoutValidation("Authorization", header);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Score_WithToken_Returns200AndScore()
    {
        using var client = AuthorizedClient();

        using var response = await client.PostAsJsonAsync(ScoreUri, new { text = "Setup was easy and support was great." });
        var body = await response.Content.ReadFromJsonAsync<ScoreResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("positive", body.Label);
        Assert.Equal(1.0, body.Score);
        Assert.Equal(["easy", "great"], body.Matches);
        Assert.Equal("keyword-baseline-v1", body.Model);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Score_BlankText_Returns400(string text)
    {
        using var client = AuthorizedClient();

        using var response = await client.PostAsJsonAsync(ScoreUri, new { text });
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", body?.Error);
    }

    [Fact]
    public async Task Score_TextOverConfiguredLimit_Returns400()
    {
        using var client = AuthorizedClient();

        using var response = await client.PostAsJsonAsync(ScoreUri, new { text = new string('a', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Score_MalformedJson_Returns400()
    {
        using var client = AuthorizedClient();
        using var content = new StringContent("{not json", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(ScoreUri, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
