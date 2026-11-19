using System.Security.Claims;
using TNWatch.Domain.Assignments;
using TNWatch.WebUI.Auth;

namespace TNWatch.WebUI.Api;

/// <summary>Request body for POST /api/nodes/{id}/assignment. Without <see cref="UserName"/> the caller claims the node.</summary>
public sealed record AssignRequest(string? UserName);

/// <summary>REST endpoints for F11 (claim / assign / release). Outcome → HTTP: 409 taken, 403 not allowed, 404 unknown.</summary>
public static class AssignmentApi
{
    public static IEndpointRouteBuilder MapAssignmentApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").WithTags("Zuständigkeit (F11)");

        api.MapGet("/assignments", async (IAssignmentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(ct)));

        api.MapGet("/users/assignable", async (IUserDirectory users, CancellationToken ct) =>
            TypedResults.Ok(await users.GetAssignableUsersAsync(ct)))
            .RequireAuthorization(AuthSetup.ManagerPolicy);

        api.MapPost("/nodes/{id}/assignment", async (string id, AssignRequest? body, ClaimsPrincipal principal,
            IAssignmentService service, IUserDirectory users, CancellationToken ct) =>
        {
            if (!FleetApi.IsValidId(id))
            {
                return Results.BadRequest("Invalid node id.");
            }

            var actor = CurrentUser.From(principal);
            var isManager = CurrentUser.IsManager(principal);
            if (string.IsNullOrWhiteSpace(body?.UserName) || string.Equals(body.UserName, actor.UserName, StringComparison.OrdinalIgnoreCase))
            {
                return ToHttp(await service.ClaimAsync(id, actor, isManager, ct));
            }

            var target = (await users.GetAssignableUsersAsync(ct))
                .FirstOrDefault(u => string.Equals(u.UserName, body.UserName, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                return Results.BadRequest($"Unknown user '{body.UserName}'.");
            }

            return ToHttp(await service.AssignAsync(id, target, actor, isManager, ct));
        });

        api.MapDelete("/nodes/{id}/assignment", async (string id, ClaimsPrincipal principal, IAssignmentService service, CancellationToken ct) =>
            FleetApi.IsValidId(id)
                ? ToHttp(await service.ReleaseAsync(id, CurrentUser.From(principal), CurrentUser.IsManager(principal), ct))
                : Results.BadRequest("Invalid node id."));

        return endpoints;
    }

    internal static IResult ToHttp(AssignmentResult result) => result.Outcome switch
    {
        AssignmentOutcome.Ok or AssignmentOutcome.Unchanged => Results.Ok(result.Current),
        AssignmentOutcome.AlreadyAssigned => Results.Conflict(result.Current),
        AssignmentOutcome.NotAllowed => Results.StatusCode(StatusCodes.Status403Forbidden),
        _ => Results.NotFound(),
    };
}
