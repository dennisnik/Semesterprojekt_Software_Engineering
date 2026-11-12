namespace TNWatch.WebUI.Auth;

/// <summary>Realm roles (ADR-06, RBAC).</summary>
public static class Roles
{
    /// <summary>Fleet manager (persona Markus): full access, simulator control, user management.</summary>
    public const string Betriebsleiter = "Betriebsleiter";

    /// <summary>Service technician (personas Kevin, Anja): read access, route planning.</summary>
    public const string Techniker = "Techniker";
}
