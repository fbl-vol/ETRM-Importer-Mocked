using System.Diagnostics;
using System.Text.Json;
using Infrastructure.Configuration;
using Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using OpenTelemetry.Trace;

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
        using var activity = Telemetry.ActivitySource.StartActivity("nats.publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "nats");
        activity?.SetTag("messaging.destination", subject);
        activity?.SetTag("messaging.operation", "publish");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var json = JsonSerializer.Serialize(message);
            activity?.SetTag("messaging.message.size", json.Length);
            
            await _connection.PublishAsync(subject, json, cancellationToken: cancellationToken);
            
            stopwatch.Stop();
            Telemetry.EventPublishDuration.Record(stopwatch.Elapsed.TotalSeconds, 
                new KeyValuePair<string, object?>("subject", subject));
            Telemetry.EventsPublished.Add(1, 
                new KeyValuePair<string, object?>("subject", subject));
            
            _logger.LogInformation("Published message to subject {Subject} in {Duration}ms", 
                subject, stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Failed to publish message to subject {Subject} after {Duration}ms", 
                subject, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
