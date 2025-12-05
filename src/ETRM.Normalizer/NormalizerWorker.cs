using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Infrastructure.Database;
using Infrastructure.NATS;
using Infrastructure.S3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using Shared.Events;

namespace ETRM.Normalizer;

public class NormalizerWorker : BackgroundService
{
    private readonly IS3Client _s3Client;
    private readonly INatsSubscriber _natsSubscriber;
    private readonly ITradeRepository _tradeRepository;
    private readonly IEodPriceRepository _eodPriceRepository;
    private readonly ILogger<NormalizerWorker> _logger;

    public NormalizerWorker(
        IS3Client s3Client,
        INatsSubscriber natsSubscriber,
        ITradeRepository tradeRepository,
        IEodPriceRepository eodPriceRepository,
        ILogger<NormalizerWorker> logger)
    {
        _s3Client = s3Client;
        _natsSubscriber = natsSubscriber;
        _tradeRepository = tradeRepository;
        _eodPriceRepository = eodPriceRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ETRM Normalizer starting at: {time}", DateTimeOffset.Now);

        await _natsSubscriber.SubscribeAsync<RawImportedEvent>(
            "etrm.raw.imported",
            HandleImportEvent,
            stoppingToken);
    }

    private async Task HandleImportEvent(RawImportedEvent @event)
    {
        try
        {
            _logger.LogInformation(
                "Processing import event: ImportId={ImportId}, FileType={FileType}",
                @event.ImportId,
                @event.FileType);

            // Download file from S3
            using var stream = await _s3Client.DownloadFileAsync(@event.ObjectKey);
            using var reader = new StreamReader(stream);

            // Parse based on file type
            if (@event.FileType == "trades.csv")
            {
                await ProcessTradesAsync(reader, @event.ImportId);
            }
            else if (@event.FileType == "eod-prices.csv")
            {
                await ProcessEodPricesAsync(reader, @event.ImportId);
            }
            else
            {
                _logger.LogWarning("Unknown file type: {FileType}", @event.FileType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing import event: {ImportId}", @event.ImportId);
        }
    }

    private async Task ProcessTradesAsync(StreamReader reader, string importId)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            PrepareHeaderForMatch = args => args.Header.ToLower()
        };

        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<TradeRecordMap>();
        var records = csv.GetRecords<TradeRecord>();

        var trades = new List<Trade>();
        foreach (var record in records)
        {
            trades.Add(new Trade
            {
                TradeId = record.TradeId,
                ContractId = record.ContractId,
                CustomerId = record.CustomerId,
                BookId = record.BookId,
                TraderId = record.TraderId,
                DepartmentId = record.DepartmentId,
                TradeDate = DateTime.Parse(record.TradeDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                TimeUpdated = DateTime.Parse(record.TimeUpdated, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                Volume = decimal.Parse(record.Volume, CultureInfo.InvariantCulture),
                Price = decimal.Parse(record.Price, CultureInfo.InvariantCulture),
                Currency = record.Currency.ToUpperInvariant(),
                Side = NormalizeSide(record.Side),
                CounterpartyId = string.IsNullOrEmpty(record.CounterpartyId) ? null : int.Parse(record.CounterpartyId),
                DeliveryStart = string.IsNullOrEmpty(record.DeliveryStart) ? null : DateTime.Parse(record.DeliveryStart, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                DeliveryEnd = string.IsNullOrEmpty(record.DeliveryEnd) ? null : DateTime.Parse(record.DeliveryEnd, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                ProductType = record.ProductType,
                Source = record.Source
            });
        }

        await _tradeRepository.InsertTradesAsync(trades);
        _logger.LogInformation("Processed {Count} trades for ImportId: {ImportId}", trades.Count, importId);
    }

    private async Task ProcessEodPricesAsync(StreamReader reader, string importId)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            PrepareHeaderForMatch = args => args.Header.ToLower()
        };

        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<EodPriceRecordMap>();
        var records = csv.GetRecords<EodPriceRecord>();

        var prices = new List<EndOfDaySettlementPrice>();
        foreach (var record in records)
        {
            prices.Add(new EndOfDaySettlementPrice
            {
                ContractId = record.ContractId,
                CustomerId = record.CustomerId,
                TradingPeriod = DateTime.Parse(record.TradingPeriod, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                PublicationTime = DateTime.Parse(record.PublicationTime, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                Price = decimal.Parse(record.Price, CultureInfo.InvariantCulture),
                Currency = record.Currency.ToUpperInvariant(),
                PriceSource = record.PriceSource,
                MarketZone = record.MarketZone
            });
        }

        await _eodPriceRepository.InsertEodPricesAsync(prices);
        _logger.LogInformation("Processed {Count} EOD prices for ImportId: {ImportId}", prices.Count, importId);
    }

    private static string NormalizeSide(string side)
    {
        return side.ToLowerInvariant() switch
        {
            "buy" => "Buy",
            "sell" => "Sell",
            _ => side
        };
    }

    private class TradeRecord
    {
        public long TradeId { get; set; }
        public int ContractId { get; set; }
        public int CustomerId { get; set; }
        public int BookId { get; set; }
        public decimal TraderId { get; set; }
        public int DepartmentId { get; set; }
        public string TradeDate { get; set; } = string.Empty;
        public string TimeUpdated { get; set; } = string.Empty;
        public string Volume { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public string CounterpartyId { get; set; } = string.Empty;
        public string DeliveryStart { get; set; } = string.Empty;
        public string DeliveryEnd { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }

    private class EodPriceRecord
    {
        public int ContractId { get; set; }
        public int CustomerId { get; set; }
        public string TradingPeriod { get; set; } = string.Empty;
        public string PublicationTime { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string PriceSource { get; set; } = string.Empty;
        public string MarketZone { get; set; } = string.Empty;
    }

    private sealed class TradeRecordMap : CsvHelper.Configuration.ClassMap<TradeRecord>
    {
        public TradeRecordMap()
        {
            Map(m => m.TradeId).Name("trade_id");
            Map(m => m.ContractId).Name("contract_id");
            Map(m => m.CustomerId).Name("customer_id");
            Map(m => m.BookId).Name("book_id");
            Map(m => m.TraderId).Name("trader_id");
            Map(m => m.DepartmentId).Name("department_id");
            Map(m => m.TradeDate).Name("trade_date");
            Map(m => m.TimeUpdated).Name("time_updated");
            Map(m => m.Volume).Name("volume");
            Map(m => m.Price).Name("price");
            Map(m => m.Currency).Name("currency");
            Map(m => m.Side).Name("side");
            Map(m => m.CounterpartyId).Name("counterparty_id");
            Map(m => m.DeliveryStart).Name("delivery_start");
            Map(m => m.DeliveryEnd).Name("delivery_end");
            Map(m => m.ProductType).Name("product_type");
            Map(m => m.Source).Name("source");
        }
    }

    private sealed class EodPriceRecordMap : CsvHelper.Configuration.ClassMap<EodPriceRecord>
    {
        public EodPriceRecordMap()
        {
            Map(m => m.ContractId).Name("contract_id");
            Map(m => m.CustomerId).Name("customer_id");
            Map(m => m.TradingPeriod).Name("trading_period");
            Map(m => m.PublicationTime).Name("publication_time");
            Map(m => m.Price).Name("price");
            Map(m => m.Currency).Name("currency");
            Map(m => m.PriceSource).Name("price_source");
            Map(m => m.MarketZone).Name("market_zone");
        }
    }
}
