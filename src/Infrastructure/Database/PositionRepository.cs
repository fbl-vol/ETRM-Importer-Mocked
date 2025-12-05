using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Shared.DTOs;

namespace Infrastructure.Database;

public class PositionRepository : IPositionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PositionRepository> _logger;

    public PositionRepository(IOptions<DatabaseOptions> options, ILogger<PositionRepository> logger)
    {
        _connectionString = options.Value.ConnectionString;
        _logger = logger;
    }

    public async Task UpsertPositionsAsync(IEnumerable<Position> positions, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // NOTE: For production with large datasets, consider using batch operations for better performance
        foreach (var position in positions)
        {
            const string sql = @"
                INSERT INTO positions (
                    contract_id, customer_id, book_id, trader_id, department_id,
                    time_updated, volume, product_type, currency, side, source
                ) VALUES (
                    @contract_id, @customer_id, @book_id, @trader_id, @department_id,
                    @time_updated, @volume, @product_type, @currency, @side, @source
                )
                ON CONFLICT (contract_id, customer_id, book_id, trader_id, department_id, product_type, currency, side)
                DO UPDATE SET
                    time_updated = EXCLUDED.time_updated,
                    volume = EXCLUDED.volume,
                    source = EXCLUDED.source";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("contract_id", position.ContractId);
            cmd.Parameters.AddWithValue("customer_id", position.CustomerId);
            cmd.Parameters.AddWithValue("book_id", position.BookId);
            cmd.Parameters.AddWithValue("trader_id", position.TraderId);
            cmd.Parameters.AddWithValue("department_id", position.DepartmentId);
            cmd.Parameters.AddWithValue("time_updated", position.TimeUpdated);
            cmd.Parameters.AddWithValue("volume", position.Volume);
            cmd.Parameters.AddWithValue("product_type", position.ProductType);
            cmd.Parameters.AddWithValue("currency", position.Currency);
            cmd.Parameters.AddWithValue("side", position.Side);
            cmd.Parameters.AddWithValue("source", position.Source);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Upserted {Count} positions into database", positions.Count());
    }
}
