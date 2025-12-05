using Infrastructure.Database;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.DTOs;

namespace ETRM.PositionAggregator;

public class AggregatorWorker : BackgroundService
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly ILogger<AggregatorWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public AggregatorWorker(
        ITradeRepository tradeRepository,
        IPositionRepository positionRepository,
        ILogger<AggregatorWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        _tradeRepository = tradeRepository;
        _positionRepository = positionRepository;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Position Aggregator starting at: {time}", DateTimeOffset.Now);

            // Fetch all trades
            var trades = await _tradeRepository.GetAllTradesAsync(stoppingToken);
            _logger.LogInformation("Fetched {Count} trades for aggregation", trades.Count());

            // Aggregate trades into positions
            var positions = AggregatePositions(trades);
            _logger.LogInformation("Aggregated into {Count} positions", positions.Count);

            // Upsert positions
            await _positionRepository.UpsertPositionsAsync(positions, stoppingToken);
            _logger.LogInformation("Successfully upserted {Count} positions", positions.Count);

            _logger.LogInformation("Position Aggregator completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during position aggregation");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private List<Position> AggregatePositions(IEnumerable<Trade> trades)
    {
        var grouped = trades.GroupBy(t => new
        {
            t.ContractId,
            t.CustomerId,
            t.BookId,
            t.TraderId,
            t.DepartmentId,
            t.ProductType,
            t.Currency,
            t.Side
        });

        var positions = new List<Position>();
        foreach (var group in grouped)
        {
            positions.Add(new Position
            {
                ContractId = group.Key.ContractId,
                CustomerId = group.Key.CustomerId,
                BookId = group.Key.BookId,
                TraderId = group.Key.TraderId,
                DepartmentId = group.Key.DepartmentId,
                ProductType = group.Key.ProductType,
                Currency = group.Key.Currency,
                Side = group.Key.Side,
                Volume = group.Sum(t => t.Volume),
                TimeUpdated = group.Max(t => t.TimeUpdated),
                Source = "Aggregated"
            });
        }

        return positions;
    }
}
