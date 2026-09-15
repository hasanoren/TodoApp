using System.Net;
using Xunit;

namespace TodoApp.IntegrationTests;

public class CorsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Request_From_AllowedOrigin_Returns_Cors_Headers()
    {
        // ARRANGE: Request with Origin: http://localhost:3000
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/Auth/login");
        request.Headers.Add("Origin", "http://localhost:3000");

        // ACT
        var response = await _client.SendAsync(request);

        // ASSERT: Response contains Access-Control-Allow-Origin and Allow-Credentials
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin").First());

        Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").First());
    }

    [Fact]
    public async Task PreflightOptions_From_AllowedOrigin_Returns_Success_And_Cors_Headers()
    {
        // ARRANGE: OPTIONS preflight request
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/todoitems");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        // ACT
        var response = await _client.SendAsync(request);

        // ASSERT: 200 or 204
        Assert.True(response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.OK);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task Request_From_UnauthorizedOrigin_Does_Not_Return_Cors_Origin_Header()
    {
        // ARRANGE: Request with untrusted origin
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/Auth/login");
        request.Headers.Add("Origin", "http://evil-attacker.com");

        // ACT
        var response = await _client.SendAsync(request);

        // ASSERT: Access-Control-Allow-Origin should NOT be returned for unauthorized origin
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}

