using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using TNWatch.Domain.Battery;
using TNWatch.Domain.Diagnostics;
using TNWatch.Domain.Export;
using TNWatch.Domain.Fleet;
using TNWatch.TelemetryIngestion.Mqtt;
using TNWatch.WebUI.Auth;

namespace TNWatch.WebUI.Api;

public sealed record DiagnosticsDto(bool MqttConnected, bool SimulatorRunning, LatencySnapshot Ingestion);

/// <summary>REST API (ADR-02). All endpoints require authentication; parameters are typed and range-checked (NFA08).</summary>
public static class FleetApi
{
    private const int MaxHistoryDays = 90;
    private const int MaxChartPoints = 2000;

    public static IEndpointRouteBuilder MapFleetApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").WithTags("TN-Watch");

        api.MapGet("/nodes", async (IFleetQueries q, ISimulatorControl sim, CancellationToken ct) =>
            TypedResults.Ok(await q.GetNodesAsync(sim.IsRunning, ct)));

        api.MapGet("/nodes/{id}", async Task<Results<Ok<NodeSnapshot>, NotFound, BadRequest<string>>> (string id, IFleetQueries q, CancellationToken ct) =>
        {
            if (!IsValidId(id))
            {
                return TypedResults.BadRequest("Invalid node id.");
            }

            return await q.GetNodeAsync(id, ct) is { } node ? TypedResults.Ok(node) : TypedResults.NotFound();
        });

        api.MapGet("/nodes/{id}/measurements", async Task<Results<Ok<IReadOnlyList<MeasurementPoint>>, BadRequest<string>>> (
            string id, DateTimeOffset? from, DateTimeOffset? to, int? maxPoints, IFleetQueries q, TimeProvider time, CancellationToken ct) =>
        {
            if (!TryRange(id, from, to, time, out var since, out var until, out var error))
            {
                return TypedResults.BadRequest(error);
            }

            var points = await q.GetMeasurementsAsync(id, since, until, ct);
            return TypedResults.Ok(MeasurementDownsampler.Downsample(points, Math.Clamp(maxPoints ?? MaxChartPoints, 10, MaxChartPoints)));
        });

        api.MapGet("/nodes/{id}/export.csv", async Task<Results<FileContentHttpResult, BadRequest<string>>> (
            string id, DateTimeOffset? from, DateTimeOffset? to, IFleetQueries q, TimeProvider time, CancellationToken ct) =>
        {
            if (!TryRange(id, from, to, time, out var since, out var until, out var error))
            {
                return TypedResults.BadRequest(error);
            }

            var csv = MeasurementCsv.Build(id, await q.GetMeasurementsAsync(id, since, until, ct));
            var fileName = $"{id}_{since:yyyyMMdd}-{until:yyyyMMdd}.csv";
            return TypedResults.File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        });

        api.MapGet("/nodes/{id}/status-changes", async Task<Results<Ok<IReadOnlyList<StatusChange>>, BadRequest<string>>> (
            string id, int? limit, IFleetQueries q, CancellationToken ct) =>
            IsValidId(id)
                ? TypedResults.Ok(await q.GetStatusChangesAsync(id, Math.Clamp(limit ?? 50, 1, 500), ct))
                : TypedResults.BadRequest("Invalid node id."));

        api.MapGet("/nodes/{id}/battery-events", async Task<Results<Ok<IReadOnlyList<BatteryEvent>>, BadRequest<string>>> (
            string id, int? limit, IFleetQueries q, CancellationToken ct) =>
            IsValidId(id)
                ? TypedResults.Ok(await q.GetBatteryEventsAsync(id, Math.Clamp(limit ?? 50, 1, 500), ct))
                : TypedResults.BadRequest("Invalid node id."));

        api.MapGet("/fleet/summary", async (IFleetQueries q, ISimulatorControl sim, CancellationToken ct) =>
            TypedResults.Ok(await q.GetSummaryAsync(sim.IsRunning, ct)));

        api.MapPost("/simulator/{state:regex(^(on|off)$)}", async (string state, ISimulatorControl sim) =>
        {
            await sim.SetRunningAsync(state == "on");
            return TypedResults.Ok(new { running = sim.IsRunning });
        }).RequireAuthorization(AuthSetup.ManagerPolicy);

        api.MapGet("/diagnostics", (MqttIngestionWorker mqtt, ISimulatorControl sim, IngestionStats stats) =>
            TypedResults.Ok(new DiagnosticsDto(mqtt.IsConnected, sim.IsRunning, stats.Snapshot())))
            .RequireAuthorization(AuthSetup.ManagerPolicy);

        return endpoints;
    }

    /// <summary>Node IDs: 1–64 chars of letters, digits, '-', '_' or '.'.</summary>
    internal static bool IsValidId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    internal static bool TryRange(string id, DateTimeOffset? from, DateTimeOffset? to, TimeProvider time,
        out DateTimeOffset since, out DateTimeOffset until, out string error)
    {
        until = to ?? time.GetUtcNow();
        since = from ?? until.AddDays(-1);
        error = string.Empty;

        if (!IsValidId(id))
        {
            error = "Invalid node id.";
        }
        else if (since > until)
        {
            error = "'from' must be before 'to'.";
        }
        else if (until - since > TimeSpan.FromDays(MaxHistoryDays))
        {
            error = $"Range must not exceed {MaxHistoryDays} days.";
        }

        return error.Length == 0;
    }
}
