using System.Text.Json;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;

namespace Infrastructure.NATS;

public class NatsSubscriber : INatsSubscriber, IAsyncDisposable
{
    private readonly NatsConnection _connection;
    private readonly ILogger<NatsSubscriber> _logger;

    public NatsSubscriber(IOptions<NatsOptions> options, ILogger<NatsSubscriber> logger)
    {
        _logger = logger;
        var opts = NatsOpts.Default with { Url = options.Value.Url };
        _connection = new NatsConnection(opts);
    }

    public async Task SubscribeAsync<T>(string subject, Func<T, Task> handler, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Subscribing to subject {Subject}", subject);
            
            await foreach (var msg in _connection.SubscribeAsync<string>(subject, cancellationToken: cancellationToken))
            {
                try
                {
                    if (msg.Data != null)
                    {
                        var message = JsonSerializer.Deserialize<T>(msg.Data);
                        if (message != null)
                        {
                            await handler(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from subject {Subject}", subject);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to subject {Subject}", subject);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
