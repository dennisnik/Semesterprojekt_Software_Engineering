namespace TNWatch.TelemetryIngestion.Mqtt;

/// <summary>Configuration section "Mqtt". Credentials come from environment/user-secrets, never from appsettings.</summary>
public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    /// <summary>Disable to run the app without broker access (e.g. simulator-only demo, CI).</summary>
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "mqtt.ti-labs.ai";

    public int Port { get; set; } = 8883;

    public bool UseTls { get; set; } = true;

    /// <summary>
    /// Accept certificates whose only problem is an unknown/offline revocation status (Let's Encrypt dropped OCSP in 2025).
    /// Chain and hostname are still validated.
    /// </summary>
    public bool TolerateUnknownRevocation { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>Stable client ID + persistent session lets the broker buffer QoS 1 messages during restarts (ADR-04).</summary>
    public string ClientId { get; set; } = $"tnwatch-{Environment.MachineName}".ToLowerInvariant();

    public bool CleanSession { get; set; }

    /// <summary>Interval of the connection check / reconnect loop (NFA03: reconnect ≤ 10 s).</summary>
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Additional raw subscription filters besides the ones derived from topic patterns.</summary>
    public IList<string> ExtraSubscriptions { get; } = [];
}
