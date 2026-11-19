using System.Security.Claims;
using TNWatch.Domain.Assignments;

namespace TNWatch.WebUI.Auth;

/// <summary>Maps the logged-in principal (OIDC claims) to the domain's <see cref="UserRef"/>.</summary>
public static class CurrentUser
{
    public static UserRef From(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var userName = principal.FindFirst("preferred_username")?.Value ?? principal.Identity?.Name ?? "unbekannt";
        var displayName = principal.FindFirst("given_name")?.Value ?? principal.FindFirst("name")?.Value ?? userName;
        return new UserRef(userName, displayName);
    }

    public static bool IsManager(ClaimsPrincipal principal) =>
        principal?.IsInRole(Roles.Betriebsleiter) ?? false;
}
