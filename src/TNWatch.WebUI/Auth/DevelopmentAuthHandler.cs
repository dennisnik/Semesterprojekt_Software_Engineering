using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TNWatch.WebUI.Auth;

/// <summary>
/// Development-only authentication: every request is the configured demo user.
/// Lets the UI run without Keycloak (e.g. <c>dotnet run</c>). Refused outside the Development environment.
/// </summary>
public sealed class DevelopmentAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AuthOptions> authOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = authOptions.Value.DevelopmentUser;
        var identity = new ClaimsIdentity(
            [
                new Claim("preferred_username", user.UserName),
                new Claim("given_name", user.GivenName),
                new Claim("roles", user.Role),
            ],
            SchemeName,
            "preferred_username",
            "roles");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
