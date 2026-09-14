using System.Net;
using Xunit;

namespace TodoApp.IntegrationTests;

public class SecurityHeadersIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Response_Contains_All_Standard_Security_Headers()
    {
        // ACT: Make any request to the API
        var response = await _client.GetAsync("/api/Auth/login");

        // ASSERT: Check headers are present in response
        var headers = response.Headers;

        Assert.True(headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", headers.GetValues("X-Content-Type-Options").First());

        Assert.True(headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", headers.GetValues("X-Frame-Options").First());

        Assert.True(headers.Contains("X-XSS-Protection"));
        Assert.Equal("1; mode=block", headers.GetValues("X-XSS-Protection").First());

        Assert.True(headers.Contains("Referrer-Policy"));
        Assert.Equal("no-referrer", headers.GetValues("Referrer-Policy").First());

        Assert.True(headers.Contains("Content-Security-Policy"));
        Assert.Equal("default-src 'self'", headers.GetValues("Content-Security-Policy").First());
    }
}

