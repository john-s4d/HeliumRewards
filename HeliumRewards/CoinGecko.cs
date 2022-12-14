using CoinGecko.Clients;
using CoinGecko.Interfaces;
using System.Net;

namespace HeliumRewards
{
    internal class CoinGecko
    {
        private readonly ICoinGeckoClient _client;
        private const int DELAY = 10000;        

        internal CoinGecko()
        {
            _client = CoinGeckoClient.Instance;
        }

        internal decimal? CoinHistory(string coinId, DateTime date, string currency)
        {   
            while (true)
            {
                try
                {
                    var result = _client.CoinsClient.GetHistoryByCoinId(coinId, date.ToString("dd-MM-yyyy"), "false").Result;

                    if (result.MarketData.CurrentPrice.ContainsKey(currency))
                    {
                        return result.MarketData.CurrentPrice[currency];
                    }

                    return null;
                }
                catch (AggregateException ex)
                {
                    if ((ex.InnerException as HttpRequestException)?.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        Console.WriteLine($"CoinGecko TooManyRequests. Sleeping {DELAY}ms");
                        Thread.Sleep(DELAY);                        
                        continue;
                    }
                    throw;
                }
            }
        }
    }
}
