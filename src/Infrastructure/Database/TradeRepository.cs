using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Shared.DTOs;

namespace Infrastructure.Database;

public class TradeRepository : ITradeRepository
{
    private readonly string _connectionString;
    private readonly ILogger<TradeRepository> _logger;

    public TradeRepository(IOptions<DatabaseOptions> options, ILogger<TradeRepository> logger)
    {
        _connectionString = options.Value.ConnectionString;
        _logger = logger;
    }

    public async Task InsertTradesAsync(IEnumerable<Trade> trades, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var trade in trades)
        {
            const string sql = @"
                INSERT INTO trades (
                    trade_id, contract_id, customer_id, book_id, trader_id, department_id,
                    trade_date, time_updated, volume, price, currency, side,
                    counterparty_id, delivery_start, delivery_end, product_type, source
                ) VALUES (
                    @trade_id, @contract_id, @customer_id, @book_id, @trader_id, @department_id,
                    @trade_date, @time_updated, @volume, @price, @currency, @side,
                    @counterparty_id, @delivery_start, @delivery_end, @product_type, @source
                )
                ON CONFLICT (trade_id, trade_date) DO UPDATE SET
                    time_updated = EXCLUDED.time_updated,
                    volume = EXCLUDED.volume,
                    price = EXCLUDED.price";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("trade_id", trade.TradeId);
            cmd.Parameters.AddWithValue("contract_id", trade.ContractId);
            cmd.Parameters.AddWithValue("customer_id", trade.CustomerId);
            cmd.Parameters.AddWithValue("book_id", trade.BookId);
            cmd.Parameters.AddWithValue("trader_id", trade.TraderId);
            cmd.Parameters.AddWithValue("department_id", trade.DepartmentId);
            cmd.Parameters.AddWithValue("trade_date", trade.TradeDate);
            cmd.Parameters.AddWithValue("time_updated", trade.TimeUpdated);
            cmd.Parameters.AddWithValue("volume", trade.Volume);
            cmd.Parameters.AddWithValue("price", trade.Price);
            cmd.Parameters.AddWithValue("currency", trade.Currency);
            cmd.Parameters.AddWithValue("side", trade.Side);
            cmd.Parameters.AddWithValue("counterparty_id", trade.CounterpartyId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("delivery_start", trade.DeliveryStart ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("delivery_end", trade.DeliveryEnd ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("product_type", trade.ProductType);
            cmd.Parameters.AddWithValue("source", trade.Source);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Inserted {Count} trades into database", trades.Count());
    }

    public async Task<IEnumerable<Trade>> GetAllTradesAsync(CancellationToken cancellationToken = default)
    {
        var trades = new List<Trade>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM trades ORDER BY trade_date DESC LIMIT 1000";
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            trades.Add(new Trade
            {
                TradeId = reader.GetInt64(0),
                ContractId = reader.GetInt32(1),
                CustomerId = reader.GetInt32(2),
                BookId = reader.GetInt32(3),
                TraderId = reader.GetDecimal(4),
                DepartmentId = reader.GetInt32(5),
                TradeDate = reader.GetDateTime(6),
                TimeUpdated = reader.GetDateTime(7),
                Volume = reader.GetDecimal(8),
                Price = reader.GetDecimal(9),
                Currency = reader.GetString(10),
                Side = reader.GetString(11),
                CounterpartyId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                DeliveryStart = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                DeliveryEnd = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                ProductType = reader.GetString(15),
                Source = reader.GetString(16)
            });
        }

        return trades;
    }
}
