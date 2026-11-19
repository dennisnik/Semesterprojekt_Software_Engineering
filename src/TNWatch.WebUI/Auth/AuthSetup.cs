using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace TNWatch.WebUI.Auth;

/// <summary>OIDC (Keycloak) + cookie authentication and RBAC policies (ADR-06).</summary>
public static class AuthSetup
{
    public const string ManagerPolicy = "Betriebsleiter";

    public static IServiceCollection AddTnWatchAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(AuthOptions.SectionName);
        services.Configure<AuthOptions>(section);
        var options = section.Get<AuthOptions>() ?? new AuthOptions();

        if (string.Equals(options.Mode, "Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Auth:Mode=Development is only allowed in the Development environment.");
            }

            services.AddAuthentication(DevelopmentAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthHandler>(DevelopmentAuthHandler.SchemeName, null);
        }
        else
        {
            AddOidc(services, options);
        }

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(ManagerPolicy, p => p.RequireRole(Roles.Betriebsleiter));
        services.AddCascadingAuthenticationState();
        return services;
    }

    private static void AddOidc(IServiceCollection services, AuthOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            throw new InvalidOperationException("Auth:Authority is required for OIDC (or set Auth:Mode=Development in Development).");
        }

        services.AddAuthentication(o =>
            {
                o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(o =>
            {
                o.Cookie.Name = "tnwatch.auth";
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.ExpireTimeSpan = TimeSpan.FromHours(10);
                o.SlidingExpiration = true;
                o.Events.OnRedirectToLogin = ctx => ApiAwareRedirect(ctx, StatusCodes.Status401Unauthorized);
                o.Events.OnRedirectToAccessDenied = ctx => ApiAwareRedirect(ctx, StatusCodes.Status403Forbidden);
            })
            .AddOpenIdConnect(o =>
            {
                o.Authority = options.Authority;
                if (!string.IsNullOrWhiteSpace(options.MetadataAddress))
                {
                    o.MetadataAddress = options.MetadataAddress;
                }

                o.ClientId = options.ClientId;
                o.ClientSecret = options.ClientSecret;
                o.RequireHttpsMetadata = options.RequireHttpsMetadata;
                o.ResponseType = OpenIdConnectResponseType.Code;
                o.UsePkce = true;

                // Query response mode keeps correlation cookies SameSite=Lax, so login also works over plain HTTP locally.
                o.ResponseMode = OpenIdConnectResponseMode.Query;
                o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                o.NonceCookie.SameSite = SameSiteMode.Lax;
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                o.SaveTokens = true;
                o.GetClaimsFromUserInfoEndpoint = true;
                o.MapInboundClaims = false;
                o.Scope.Clear();
                o.Scope.Add("openid");
                o.Scope.Add("profile");
                o.TokenValidationParameters.NameClaimType = "preferred_username";
                o.TokenValidationParameters.RoleClaimType = "roles";
                o.ClaimActions.MapJsonKey("roles", "roles");

                // API and hub clients get 401 instead of a redirect to the login page.
                o.Events.OnRedirectToIdentityProvider = ctx =>
                {
                    if (IsApiRequest(ctx.Request))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.HandleResponse();
                    }

                    return Task.CompletedTask;
                };
            });
    }

    private static Task ApiAwareRedirect(RedirectContext<CookieAuthenticationOptions> ctx, int statusCode)
    {
        if (IsApiRequest(ctx.Request))
        {
            ctx.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }

        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    }

    private static bool IsApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
        request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);

    /// <summary>/account/login and /account/logout endpoints.</summary>
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/account");
        group.MapGet("/login", (string? returnUrl) =>
                Results.Challenge(new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) }))
            .AllowAnonymous();
        group.MapPost("/logout", (IConfiguration configuration) =>
        {
            var oidc = !string.Equals(configuration["Auth:Mode"], "Development", StringComparison.OrdinalIgnoreCase);
            var schemes = oidc
                ? new[] { CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme }
                : Array.Empty<string>();
            return oidc ? Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, schemes) : Results.Redirect("/");
        });
        return endpoints;
    }

    private static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) ? returnUrl : "/";
}
