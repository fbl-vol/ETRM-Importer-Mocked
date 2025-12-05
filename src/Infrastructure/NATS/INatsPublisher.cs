namespace Infrastructure.NATS;

public interface INatsPublisher
{
    Task PublishAsync<T>(string subject, T message, CancellationToken cancellationToken = default);
}
