using System.Text.Json;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;

namespace Infrastructure.NATS;

public class NatsPublisher : INatsPublisher, IAsyncDisposable
{
    private readonly NatsConnection _connection;
    private readonly ILogger<NatsPublisher> _logger;

    public NatsPublisher(IOptions<NatsOptions> options, ILogger<NatsPublisher> logger)
    {
        _logger = logger;
        var opts = NatsOpts.Default with { Url = options.Value.Url };
        _connection = new NatsConnection(opts);
    }

    public async Task PublishAsync<T>(string subject, T message, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            await _connection.PublishAsync(subject, json, cancellationToken: cancellationToken);
            _logger.LogInformation("Published message to subject {Subject}", subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to subject {Subject}", subject);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
