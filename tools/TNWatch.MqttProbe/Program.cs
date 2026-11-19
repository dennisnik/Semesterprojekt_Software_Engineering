// TN-Watch MQTT probe: read-only discovery of topics and payloads on the course broker.
// Credentials are read from environment variables, never from code:
//   MQTT_HOST, MQTT_PORT, MQTT_USER, MQTT_PASSWORD
// Usage: dotnet run -- <topicFilter> <seconds> <outputFile>
using System.Security.Authentication;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

var filter = args.Length > 0 ? args[0] : "traffic-device/#";
var seconds = args.Length > 1 ? int.Parse(args[1]) : 120;
var outFile = args.Length > 2 ? args[2] : "mqtt-probe.log";

string Env(string name) => Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Environment variable {name} is not set.");

var host = Env("MQTT_HOST");
var port = int.Parse(Environment.GetEnvironmentVariable("MQTT_PORT") ?? "8883");

var options = new MqttClientOptionsBuilder()
    .WithTcpServer(host, port)
    .WithCredentials(Env("MQTT_USER"), Env("MQTT_PASSWORD"))
    .WithClientId($"tnwatch-probe-{Guid.NewGuid():N}"[..24])
    .WithCleanSession()
    .WithTlsOptions(o => o
        .UseTls()
        .WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13)
        .WithCertificateValidationHandler(ctx =>
        {
            // Chain + hostname are validated strictly. The only tolerated problem is an
            // unknown/offline revocation status: Let's Encrypt dropped OCSP in 2025 and the
            // platform revocation check cannot always complete.
            var errors = ctx.SslPolicyErrors;
            if (errors == System.Net.Security.SslPolicyErrors.None) return true;
            if (errors != System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors) return false;
            return (ctx.Chain?.ChainStatus ?? []).All(s =>
                s.Status is System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.RevocationStatusUnknown
                         or System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.OfflineRevocation);
        }))
    .Build();

using var client = new MqttFactory().CreateMqttClient();
await using var writer = new StreamWriter(outFile, append: false, Encoding.UTF8) { AutoFlush = true };
var count = 0;
var topics = new SortedDictionary<string, int>();

client.ApplicationMessageReceivedAsync += e =>
{
    var payload = e.ApplicationMessage.ConvertPayloadToString() ?? "";
    lock (topics)
    {
        count++;
        topics[e.ApplicationMessage.Topic] = topics.GetValueOrDefault(e.ApplicationMessage.Topic) + 1;
        writer.WriteLine($"{DateTimeOffset.UtcNow:O}\t{e.ApplicationMessage.Topic}\tretain={e.ApplicationMessage.Retain}\t{payload}");
    }
    return Task.CompletedTask;
};

var connect = await client.ConnectAsync(options);
Console.WriteLine($"Connected: {connect.ResultCode}");

var sub = await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
    .WithTopicFilter(filter, MqttQualityOfServiceLevel.AtMostOnce).Build());
foreach (var item in sub.Items)
    Console.WriteLine($"Subscribed '{item.TopicFilter.Topic}': {item.ResultCode}");

await Task.Delay(TimeSpan.FromSeconds(seconds));
await client.DisconnectAsync();

Console.WriteLine($"Received {count} messages on {topics.Count} topics:");
foreach (var (topic, n) in topics) Console.WriteLine($"  {n,5}  {topic}");
