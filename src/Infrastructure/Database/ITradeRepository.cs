using Shared.DTOs;

namespace Infrastructure.Database;

public interface ITradeRepository
{
    Task InsertTradesAsync(IEnumerable<Trade> trades, CancellationToken cancellationToken = default);
    Task<IEnumerable<Trade>> GetAllTradesAsync(CancellationToken cancellationToken = default);
}
