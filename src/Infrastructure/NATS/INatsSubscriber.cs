namespace Infrastructure.NATS;

public interface INatsSubscriber
{
    Task SubscribeAsync<T>(string subject, Func<T, Task> handler, CancellationToken cancellationToken = default);
}
