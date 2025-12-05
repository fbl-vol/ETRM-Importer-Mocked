using Shared.DTOs;

namespace Infrastructure.Database;

public interface IEodPriceRepository
{
    Task InsertEodPricesAsync(IEnumerable<EndOfDaySettlementPrice> prices, CancellationToken cancellationToken = default);
}
