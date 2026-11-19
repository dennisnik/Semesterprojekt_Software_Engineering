using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TNWatch.Domain.Assignments;

namespace TNWatch.WebUI.Auth;

/// <summary>Development: assignable users come from configuration (Auth:DevelopmentUser + fixed demo users).</summary>
public sealed class ConfiguredUserDirectory(IOptions<AuthOptions> options) : IUserDirectory
{
    public Task<IReadOnlyList<UserRef>> GetAssignableUsersAsync(CancellationToken ct)
    {
        var users = new List<UserRef>
        {
            new("markus", "Markus"),
            new("kevin", "Kevin"),
            new("anja", "Anja"),
        };
        var dev = options.Value.DevelopmentUser;
        if (!users.Any(u => u.UserName == dev.UserName))
        {
            users.Add(new UserRef(dev.UserName, dev.GivenName));
        }

        return Task.FromResult<IReadOnlyList<UserRef>>(users.OrderBy(u => u.DisplayName, StringComparer.CurrentCulture).ToList());
    }
}

/// <summary>
/// Production: reads the users with the realm roles Techniker/Betriebsleiter from the Keycloak Admin REST API,
/// authenticated as the service account of the app's own client (client credentials, role realm-management/view-users).
/// Results are cached for 5 minutes; on errors the last known list is returned.
/// </summary>
public sealed class KeycloakUserDirectory(
    IHttpClientFactory httpFactory,
    IOptions<AuthOptions> options,
    TimeProvider time,
    ILogger<KeycloakUserDirectory> logger) : IUserDirectory, IDisposable
{
    public const string HttpClientName = "keycloak-admin";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<UserRef> _cached = [];
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<UserRef>> GetAssignableUsersAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (time.GetUtcNow() - _cachedAt < CacheDuration)
            {
                return _cached;
            }

            _cached = await LoadAsync(ct);
            _cachedAt = time.GetUtcNow();
        }
#pragma warning disable CA1031 // The UI must keep working if Keycloak is briefly unavailable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning("Could not load users from Keycloak: {Message}", ex.GetBaseException().Message);
        }
        finally
        {
            _lock.Release();
        }

        return _cached;
    }

    public void Dispose() => _lock.Dispose();

    /// <summary>Realm base URL for server-to-server calls (internal MetadataAddress if configured).</summary>
    internal static Uri RealmBase(AuthOptions o)
    {
        var source = !string.IsNullOrWhiteSpace(o.MetadataAddress)
            ? o.MetadataAddress.Replace("/.well-known/openid-configuration", string.Empty, StringComparison.Ordinal)
            : o.Authority;
        return new Uri(source.TrimEnd('/') + "/");
    }

    private async Task<IReadOnlyList<UserRef>> LoadAsync(CancellationToken ct)
    {
        var o = options.Value;
        var http = httpFactory.CreateClient(HttpClientName);
        var realmBase = RealmBase(o);
        var adminBase = new Uri(realmBase.AbsoluteUri.Replace("/realms/", "/admin/realms/", StringComparison.Ordinal));

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(realmBase, "protocol/openid-connect/token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = o.ClientId,
                ["client_secret"] = o.ClientSecret ?? string.Empty,
            }),
        };
        using var tokenResponse = await http.SendAsync(tokenRequest, ct);
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(ct)
            ?? throw new InvalidOperationException("Empty token response.");

        var users = new Dictionary<string, UserRef>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in new[] { Roles.Techniker, Roles.Betriebsleiter })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(adminBase, $"roles/{role}/users?briefRepresentation=true&max=500"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            foreach (var u in await response.Content.ReadFromJsonAsync<List<KeycloakUser>>(ct) ?? [])
            {
                if (u.Enabled && !string.IsNullOrWhiteSpace(u.Username))
                {
                    users[u.Username] = new UserRef(u.Username, string.IsNullOrWhiteSpace(u.FirstName) ? u.Username : u.FirstName);
                }
            }
        }

        return users.Values.OrderBy(u => u.DisplayName, StringComparer.CurrentCulture).ToList();
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record KeycloakUser(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("enabled")] bool Enabled);
}
