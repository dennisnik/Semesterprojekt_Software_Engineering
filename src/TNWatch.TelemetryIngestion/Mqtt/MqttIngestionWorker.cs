using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using TNWatch.Domain.Telemetry;
using TNWatch.TelemetryIngestion.Parsing;

namespace TNWatch.TelemetryIngestion.Mqtt;

/// <summary>
/// Subscribes to the configured MQTT topics and forwards every message into the ingestion channel.
/// Reconnects automatically (NFA03); a persistent session lets the broker buffer QoS 1 messages meanwhile.
/// </summary>
public sealed class MqttIngestionWorker(
    IOptions<MqttOptions> mqttOptions,
    TelemetryParser telemetryParser,
    BatteryEventParser batteryParser,
    IngestionChannel channel,
    TimeProvider time,
    ILogger<MqttIngestionWorker> logger) : BackgroundService
{
    private readonly MqttOptions _options = mqttOptions.Value;

    public bool IsConnected { get; private set; }

    public IReadOnlyList<string> SubscriptionFilters =>
        telemetryParser.Patterns.Concat(batteryParser.Patterns)
            .Select(p => p.SubscriptionFilter)
            .Concat(_options.ExtraSubscriptions)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogWarning("MQTT ingestion is disabled (Mqtt:Enabled=false)");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            logger.LogError("MQTT credentials missing. Set Mqtt__Username and Mqtt__Password (environment or user-secrets).");
            return;
        }

        using var client = new MqttFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += async e =>
        {
            var msg = e.ApplicationMessage;
            await channel.Writer.WriteAsync(
                new RawMessage(msg.Topic, msg.PayloadSegment.ToArray(), time.GetUtcNow(), TelemetrySource.Mqtt),
                stoppingToken);
        };
        client.DisconnectedAsync += e =>
        {
            if (IsConnected)
            {
                logger.LogWarning(e.Exception, "MQTT connection lost ({Reason})", e.Reason);
            }

            IsConnected = false;
            return Task.CompletedTask;
        };

        var clientOptions = BuildClientOptions();
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!client.IsConnected)
            {
                await TryConnectAsync(client, clientOptions, stoppingToken);
            }

            try
            {
                await Task.Delay(_options.ReconnectInterval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (client.IsConnected)
        {
            await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), CancellationToken.None);
        }
    }

    private async Task TryConnectAsync(IMqttClient client, MqttClientOptions clientOptions, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var result = await client.ConnectAsync(clientOptions, timeout.Token);
            if (result.ResultCode != MqttClientConnectResultCode.Success)
            {
                logger.LogError("MQTT connect refused: {Code} {Reason}", result.ResultCode, result.ReasonString);
                return;
            }

            var subscribe = new MqttClientSubscribeOptionsBuilder();
            foreach (var filter in SubscriptionFilters)
            {
                subscribe.WithTopicFilter(filter, MqttQualityOfServiceLevel.AtLeastOnce);
            }

            var sub = await client.SubscribeAsync(subscribe.Build(), timeout.Token);
            foreach (var item in sub.Items)
            {
                logger.LogInformation("MQTT subscribed {Filter}: {Result}", item.TopicFilter.Topic, item.ResultCode);
            }

            IsConnected = true;
            logger.LogInformation("MQTT connected to {Host}:{Port} as {ClientId} (session present: {Session})",
                _options.Host, _options.Port, _options.ClientId, result.IsSessionPresent);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
#pragma warning disable CA1031 // Any connect failure → retry on next tick.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning("MQTT connect to {Host}:{Port} failed: {Message}", _options.Host, _options.Port, ex.GetBaseException().Message);
        }
    }

    private MqttClientOptions BuildClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithCredentials(_options.Username, _options.Password)
            .WithClientId(_options.ClientId)
            .WithCleanSession(_options.CleanSession)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
            .WithTimeout(TimeSpan.FromSeconds(10));

        if (_options.UseTls)
        {
            builder.WithTlsOptions(o => o
                .UseTls()
                .WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13)
                .WithCertificateValidationHandler(ctx => ValidateCertificate(ctx.SslPolicyErrors, ctx.Chain, _options.TolerateUnknownRevocation)));
        }

        return builder.Build();
    }

    /// <summary>
    /// Strict chain + hostname validation. Optionally tolerates only an unknown/offline revocation status.
    /// </summary>
    public static bool ValidateCertificate(SslPolicyErrors errors, X509Chain? chain, bool tolerateUnknownRevocation)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        if (!tolerateUnknownRevocation || errors != SslPolicyErrors.RemoteCertificateChainErrors || chain is null)
        {
            return false;
        }

        return chain.ChainStatus.All(s => s.Status is X509ChainStatusFlags.RevocationStatusUnknown or X509ChainStatusFlags.OfflineRevocation);
    }
}
