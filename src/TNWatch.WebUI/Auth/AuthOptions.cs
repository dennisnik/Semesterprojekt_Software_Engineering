namespace TNWatch.WebUI.Auth;

/// <summary>Configuration section "Auth".</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>"Oidc" (Keycloak, default) or "Development" (fixed demo user, only allowed in the Development environment).</summary>
    public string Mode { get; set; } = "Oidc";

    /// <summary>Public issuer URL as seen by the browser, e.g. http://localhost:8081/realms/tnwatch.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Optional internal discovery URL (container network), e.g. http://keycloak:8080/realms/tnwatch/.well-known/openid-configuration.</summary>
    public string? MetadataAddress { get; set; }

    public string ClientId { get; set; } = "tnwatch-web";

    public string? ClientSecret { get; set; }

    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Link for "Benutzerverwaltung" (Keycloak admin console), shown to Betriebsleiter.</summary>
    public string? UserManagementUrl { get; set; }

    public DevelopmentUserOptions DevelopmentUser { get; set; } = new();
}

public sealed class DevelopmentUserOptions
{
    public string UserName { get; set; } = "markus";

    public string GivenName { get; set; } = "Markus";

    public string Role { get; set; } = Roles.Betriebsleiter;
}
