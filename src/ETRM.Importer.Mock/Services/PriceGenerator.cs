using System.Text;
using Shared.DTOs;

namespace ETRM.Importer.Mock.Services;

/// <summary>
/// Service for generating realistic mock EOD price data.
/// </summary>
public class PriceGenerator
{
    private readonly Random _random = new();

    private readonly int[] _contractIds = { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 };
    private readonly int[] _customerIds = { 201, 202, 203, 204, 205, 206, 207, 208 };
    private readonly string[] _currencies = { "EUR", "USD", "GBP" };
    private readonly string[] _marketZones = { "EU-CENTRAL", "EU-WEST", "EU-NORTH", "US-EAST", "US-WEST" };

    /// <summary>
    /// Generates EOD prices for all contracts and customers.
    /// </summary>
    public List<EndOfDaySettlementPrice> GeneratePrices(DateTime tradingPeriod)
    {
        var prices = new List<EndOfDaySettlementPrice>();
        var publicationTime = tradingPeriod.Date.AddHours(16); // Published at 16:00 UTC

        // Generate prices for a subset of contract/customer combinations
        var combinations = _random.Next(5, 15);
        for (int i = 0; i < combinations; i++)
        {
            var contractId = _contractIds[_random.Next(_contractIds.Length)];
            var customerId = _customerIds[_random.Next(_customerIds.Length)];
            var currency = _currencies[_random.Next(_currencies.Length)];
            var marketZone = _marketZones[_random.Next(_marketZones.Length)];

            var basePrice = currency == "USD" ? 70.0m : 75.0m;
            var price = basePrice + (decimal)(_random.NextDouble() * 15 - 7.5);

            var eodPrice = new EndOfDaySettlementPrice
            {
                ContractId = contractId,
                CustomerId = customerId,
                TradingPeriod = tradingPeriod.Date,
                PublicationTime = publicationTime,
                Price = Math.Round(price, 2),
                Currency = currency,
                PriceSource = "Exchange",
                MarketZone = marketZone
            };

            prices.Add(eodPrice);
        }

        return prices;
    }

    /// <summary>
    /// Converts EOD prices to CSV format.
    /// </summary>
    public string PricesToCsv(List<EndOfDaySettlementPrice> prices)
    {
        var sb = new StringBuilder();
        sb.AppendLine("contract_id,customer_id,trading_period,publication_time,price,currency,price_source,market_zone");

        foreach (var price in prices)
        {
            sb.AppendLine($"{price.ContractId},{price.CustomerId}," +
                         $"{price.TradingPeriod:yyyy-MM-ddTHH:mm:ssZ}," +
                         $"{price.PublicationTime:yyyy-MM-ddTHH:mm:ssZ}," +
                         $"{price.Price},{price.Currency},{price.PriceSource},{price.MarketZone}");
        }

        return sb.ToString();
    }
}
