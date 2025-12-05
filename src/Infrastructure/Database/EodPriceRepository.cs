using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Shared.DTOs;

namespace Infrastructure.Database;

public class EodPriceRepository : IEodPriceRepository
{
    private readonly string _connectionString;
    private readonly ILogger<EodPriceRepository> _logger;

    public EodPriceRepository(IOptions<DatabaseOptions> options, ILogger<EodPriceRepository> logger)
    {
        _connectionString = options.Value.ConnectionString;
        _logger = logger;
    }

    public async Task InsertEodPricesAsync(IEnumerable<EndOfDaySettlementPrice> prices, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var price in prices)
        {
            const string sql = @"
                INSERT INTO eod_prices (
                    contract_id, customer_id, trading_period, publication_time,
                    price, currency, price_source, market_zone
                ) VALUES (
                    @contract_id, @customer_id, @trading_period, @publication_time,
                    @price, @currency, @price_source, @market_zone
                )";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("contract_id", price.ContractId);
            cmd.Parameters.AddWithValue("customer_id", price.CustomerId);
            cmd.Parameters.AddWithValue("trading_period", price.TradingPeriod);
            cmd.Parameters.AddWithValue("publication_time", price.PublicationTime);
            cmd.Parameters.AddWithValue("price", price.Price);
            cmd.Parameters.AddWithValue("currency", price.Currency);
            cmd.Parameters.AddWithValue("price_source", price.PriceSource);
            cmd.Parameters.AddWithValue("market_zone", price.MarketZone);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Inserted {Count} EOD prices into database", prices.Count());
    }
}
