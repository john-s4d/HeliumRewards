using RestSharp;
using Microsoft.Extensions.Configuration;

namespace HeliumRewards
{
    internal class CoinGecko
    {
        private readonly RestClient _client;
        private readonly string _apiKey;
        private const int BACKOFF_DELAY = 10000;

        internal CoinGecko()
        {
            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
               .AddUserSecrets<Program>();

            var configuration = builder.Build();
            _apiKey = configuration["CoinGeckoApiKey"];
            
            var options = new RestClientOptions("https://api.coingecko.com/api/v3/")
            {
                MaxTimeout = 10000
            };
            _client = new RestClient(options);
                        
            _client.AddDefaultHeader("accept", "application/json");
        }

        internal decimal? CoinHistory(string coinId, DateTime date, string currency)
        {
            var request = new RestRequest($"coins/{coinId}/history", Method.Get);
            request.AddQueryParameter("date", date.ToString("dd-MM-yyyy"));
            request.AddQueryParameter("x-cg-demo-api-key", $"{_apiKey}");

            try
            {
                var response = _client.GetAsync<dynamic>(request).Result;
                if (response.MarketData.CurrentPrice.TryGetValue(currency, out decimal price))
                {
                    return Convert.ToDecimal(price);
                }
                return null;
            }
            catch (Exception ex) when (ex.InnerException is HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests })
            {
                Console.WriteLine($"Rate limit exceeded, retrying after {BACKOFF_DELAY} milliseconds...");
                Task.Delay(BACKOFF_DELAY);
                return CoinHistory(coinId, date, currency);  // Recursive retry
            }
        }
    }
}
