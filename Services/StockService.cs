using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace BlazorApp.Services;

public class StockData
{
    public required string Symbol { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public required List<decimal> HistoricalPrices { get; set; }
    public required List<DateTime> HistoricalDates { get; set; }
}

public interface IStockService
{
    Task<StockData> GetStockDataAsync(string symbol);
}

public class StockService : IStockService
{
    private readonly HttpClient _httpClient;
    private const string API_KEY = "demo"; // Replace with your Alpha Vantage API key
    
    public StockService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StockData> GetStockDataAsync(string symbol)
    {
        try
        {
            // Get daily prices
            var url = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={symbol}&apikey={API_KEY}";
            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement>(response);

            if (!data.TryGetProperty("Time Series (Daily)", out var timeSeries))
            {
                throw new Exception("Invalid response from Alpha Vantage");
            }

            var historicalData = timeSeries.EnumerateObject()
                .Take(30)
                .Select(x => new
                {
                    Date = DateTime.Parse(x.Name),
                    Close = decimal.Parse(x.Value.GetProperty("4. close").GetString() ?? "0")
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Get current quote
            var quoteUrl = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={symbol}&apikey={API_KEY}";
            var quoteResponse = await _httpClient.GetStringAsync(quoteUrl);
            var quoteData = JsonSerializer.Deserialize<JsonElement>(quoteResponse);

            if (!quoteData.TryGetProperty("Global Quote", out var quote))
            {
                throw new Exception("Invalid quote response from Alpha Vantage");
            }

            var price = decimal.Parse(quote.GetProperty("05. price").GetString() ?? "0");
            var change = decimal.Parse(quote.GetProperty("09. change").GetString() ?? "0");
            var changePercent = decimal.Parse((quote.GetProperty("10. change percent").GetString() ?? "0%").TrimEnd('%')) / 100;

            return new StockData
            {
                Symbol = symbol,
                Name = symbol,
                Price = price,
                Change = change,
                ChangePercent = changePercent,
                HistoricalPrices = historicalData.Select(x => x.Close).ToList(),
                HistoricalDates = historicalData.Select(x => x.Date).ToList()
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Error fetching stock data for {symbol}: {ex.Message}");
        }
    }
} 