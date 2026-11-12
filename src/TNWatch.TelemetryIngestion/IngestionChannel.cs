using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace TNWatch.TelemetryIngestion;

/// <summary>
/// Bounded in-memory queue between producers (MQTT worker, simulator) and the single pipeline consumer (ADR-07).
/// When full, producers wait (back-pressure) instead of dropping messages.
/// </summary>
public sealed class IngestionChannel
{
    private readonly Channel<RawMessage> _channel;

    public IngestionChannel(IOptions<IngestionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _channel = Channel.CreateBounded<RawMessage>(new BoundedChannelOptions(options.Value.ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public ChannelWriter<RawMessage> Writer => _channel.Writer;

    public ChannelReader<RawMessage> Reader => _channel.Reader;
}
