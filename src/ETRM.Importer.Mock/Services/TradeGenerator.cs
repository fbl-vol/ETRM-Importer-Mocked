using System.Text;
using Shared.DTOs;

namespace ETRM.Importer.Mock.Services;

/// <summary>
/// Service for generating realistic mock trade data.
/// </summary>
public class TradeGenerator
{
    private readonly Random _random = new();
    private long _nextTradeId = 2000;

    private readonly int[] _contractIds = { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 };
    private readonly int[] _customerIds = { 201, 202, 203, 204, 205, 206, 207, 208 };
    private readonly int[] _bookIds = { 301, 302, 303, 304, 305 };
    private readonly decimal[] _traderIds = { 1.5m, 2.3m, 3.1m, 4.2m, 5.1m };
    private readonly int[] _departmentIds = { 401, 402, 403, 404, 405 };
    private readonly string[] _productTypes = { "Future", "Swap", "Option", "Forward" };
    private readonly string[] _currencies = { "EUR", "USD", "GBP" };
    private readonly string[] _sides = { "Buy", "Sell" };
    private readonly int[] _counterpartyIds = { 501, 502, 503, 504, 505, 506 };

    /// <summary>
    /// Generates a batch of random trades.
    /// </summary>
    public List<Trade> GenerateTrades(int count)
    {
        var trades = new List<Trade>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            var contractId = _contractIds[_random.Next(_contractIds.Length)];
            var customerId = _customerIds[_random.Next(_customerIds.Length)];
            var bookId = _bookIds[_random.Next(_bookIds.Length)];
            var traderId = _traderIds[_random.Next(_traderIds.Length)];
            var departmentId = _departmentIds[_random.Next(_departmentIds.Length)];
            var productType = _productTypes[_random.Next(_productTypes.Length)];
            var currency = _currencies[_random.Next(_currencies.Length)];
            var side = _sides[_random.Next(_sides.Length)];
            var counterpartyId = _counterpartyIds[_random.Next(_counterpartyIds.Length)];

            var volume = _random.Next(100, 5000);
            var basePrice = currency == "USD" ? 70.0m : 75.0m;
            var price = basePrice + (decimal)(_random.NextDouble() * 20 - 10);

            var deliveryStart = now.AddMonths(_random.Next(1, 12));
            var deliveryEnd = deliveryStart.AddMonths(_random.Next(1, 6));

            var trade = new Trade
            {
                TradeId = Interlocked.Increment(ref _nextTradeId),
                ContractId = contractId,
                CustomerId = customerId,
                BookId = bookId,
                TraderId = traderId,
                DepartmentId = departmentId,
                TradeDate = now,
                TimeUpdated = now,
                Volume = volume,
                Price = Math.Round(price, 2),
                Currency = currency,
                Side = side,
                CounterpartyId = counterpartyId,
                DeliveryStart = deliveryStart,
                DeliveryEnd = deliveryEnd,
                ProductType = productType,
                Source = "MockedETRM"
            };

            trades.Add(trade);
        }

        return trades;
    }

    /// <summary>
    /// Converts trades to CSV format.
    /// </summary>
    public string TradesToCsv(List<Trade> trades)
    {
        var sb = new StringBuilder();
        sb.AppendLine("trade_id,contract_id,customer_id,book_id,trader_id,department_id,trade_date,time_updated,volume,price,currency,side,counterparty_id,delivery_start,delivery_end,product_type,source");

        foreach (var trade in trades)
        {
            sb.AppendLine($"{trade.TradeId},{trade.ContractId},{trade.CustomerId},{trade.BookId}," +
                         $"{trade.TraderId},{trade.DepartmentId},{trade.TradeDate:yyyy-MM-ddTHH:mm:ssZ}," +
                         $"{trade.TimeUpdated:yyyy-MM-ddTHH:mm:ssZ},{trade.Volume},{trade.Price}," +
                         $"{trade.Currency},{trade.Side},{trade.CounterpartyId}," +
                         $"{trade.DeliveryStart:yyyy-MM-ddTHH:mm:ssZ},{trade.DeliveryEnd:yyyy-MM-ddTHH:mm:ssZ}," +
                         $"{trade.ProductType},{trade.Source}");
        }

        return sb.ToString();
    }
}
