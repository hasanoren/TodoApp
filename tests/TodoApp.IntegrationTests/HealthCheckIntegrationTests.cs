using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TodoApp.IntegrationTests;

public class HealthCheckIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthCheckIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_Liveness_ReturnsOkAndHealthy()
    {
        // ACT
        var response = await _client.GetAsync("/health");

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", content.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetHealthReady_Readiness_ReturnsOkAndDatabaseHealthy()
    {
        // ACT
        var response = await _client.GetAsync("/health/ready");

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", content.GetProperty("status").GetString());

        var entries = content.GetProperty("entries");
        Assert.True(entries.TryGetProperty("database", out var dbEntry));
        Assert.Equal("Healthy", dbEntry.GetProperty("status").GetString());
        Assert.Equal("Veritabanı bağlantısı başarılı.", dbEntry.GetProperty("description").GetString());
    }
}

