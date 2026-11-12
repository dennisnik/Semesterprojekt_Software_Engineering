using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TNWatch.IntegrationTests;

/// <summary>End-to-end: simulator → pipeline → PostgreSQL → REST API (Development auth, MQTT disabled).</summary>
[Collection(PostgresGroup.Name)]
public sealed class ApiTests(PostgresFixture db) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await db.ResetAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:TnWatch", db.ConnectionString)
            .UseSetting("Mqtt:Enabled", "false")
            .UseSetting("Simulator:StartEnabled", "false")
            .UseSetting("Auth:Mode", "Development"));
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Simulator_toggle_produces_fleet_visible_via_api()
    {
        var client = _factory.CreateClient();
        Assert.Equal("Healthy", await client.GetStringAsync("/health"));

        var on = await client.PostAsync("/api/simulator/on", null);
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);

        JsonElement summary = default;
        for (var i = 0; i < 50; i++)
        {
            summary = await client.GetFromJsonAsync<JsonElement>("/api/fleet/summary");
            if (summary.GetProperty("total").GetInt32() == 7)
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.Equal(7, summary.GetProperty("total").GetInt32());
        Assert.Equal(1, summary.GetProperty("offline").GetInt32());

        var node = await client.GetFromJsonAsync<JsonElement>("/api/nodes/SIM-002");
        Assert.Equal("Normal", node.GetProperty("status").GetString());

        var csv = await client.GetStringAsync("/api/nodes/SIM-002/export.csv");
        Assert.StartsWith("node_id,measured_at_utc", csv, StringComparison.Ordinal);

        await client.PostAsync("/api/simulator/off", null);
        var nodes = await client.GetFromJsonAsync<JsonElement>("/api/nodes");
        Assert.Equal(0, nodes.GetArrayLength()); // simulated nodes hidden while simulator is off
    }

    [Theory]
    [InlineData("/api/nodes/bad%20id")]
    [InlineData("/api/nodes/SIM-001/measurements?from=2026-01-01T00:00:00Z&to=2025-01-01T00:00:00Z")]
    [InlineData("/api/nodes/SIM-001/measurements?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/nodes/SIM-001/measurements?from=not-a-date")]
    public async Task Invalid_parameters_are_rejected(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_node_returns_404()
        => Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("/api/nodes/NOPE-1")).StatusCode);
}
