using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ashare.V2.Api.Tests;

public class HealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_endpoint_returns_200_with_envelope()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(body));

        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("operation", out _),
            "Response must be an OperationEnvelope with 'operation' property");
        Assert.True(doc.RootElement.TryGetProperty("data", out _),
            "Response must have a 'data' property");
    }

    [Fact]
    public async Task Health_envelope_data_contains_status_healthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("status", out var status));
        Assert.Equal("healthy", status.GetString());
    }

    [Fact]
    public async Task Home_view_returns_envelope()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/home/view");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("operation", out _),
            "/home/view must return OperationEnvelope");
    }
}
