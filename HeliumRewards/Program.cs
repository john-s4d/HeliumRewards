using System.Globalization;

namespace HeliumRewards
{
    internal class Program
    {
        private static readonly DateTime DEFAULT_START_TIME = DateTime.Today;
        private static readonly DateTime DEFAULT_END_TIME = DateTime.Today;
        private const string DEFAULT_INPUT_FILENAME = "input.csv";
        private const string DEFAULT_OUTPUT_FILENAME = "output.csv";
        private const string DEFAULT_AGGREGATE_PERIOD = "month";
        private const string DEFAULT_CURRENCY = "cad";
        private const string DEFAULT_EXCHANGE_RATE_CACHE = "exchange-rates.csv";

        private static Dictionary<string, Dictionary<DateTime, decimal>> _exchangeRates = new();

        static void Main(string[] args)
        {
            try
            {
                Dictionary<string, string> parameters = ParseArgs(args);

                if (parameters.ContainsKey("h"))
                {
                    ShowHelp();
                    return;
                }

                DateTime start = parameters.ContainsKey("s") ? DateTime.Parse(parameters["s"]) : DEFAULT_START_TIME;
                DateTime end = parameters.ContainsKey("e") ? DateTime.Parse(parameters["e"]) : DEFAULT_END_TIME;
                string inputFileName = parameters.ContainsKey("i") ? parameters["i"] : DEFAULT_INPUT_FILENAME;
                string outputFileName = parameters.ContainsKey("o") ? parameters["o"] : DEFAULT_OUTPUT_FILENAME;
                string aggregatePeriod = parameters.ContainsKey("p") ? parameters["p"] : DEFAULT_AGGREGATE_PERIOD;
                string currency = parameters.ContainsKey("c") ? parameters["c"] : DEFAULT_CURRENCY;
                string exchangeRateCacheFileName = parameters.ContainsKey("x") ? parameters["x"] : DEFAULT_EXCHANGE_RATE_CACHE;

                if (!File.Exists(inputFileName))
                {
                    Console.WriteLine($"Input file does not exist: {inputFileName}");
                    ShowHelp();
                    return;
                }

                if (!File.Exists(exchangeRateCacheFileName))
                {
                    Console.WriteLine($"Exchange rate cache file does not exist: {exchangeRateCacheFileName}");
                    ShowHelp();
                    return;
                }
                
                _exchangeRates = LoadExchangeRates(exchangeRateCacheFileName);

                ProcessCSV(inputFileName, outputFileName, start, end, currency, aggregatePeriod, exchangeRateCacheFileName);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                ShowHelp();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        class RewardRecord
        {
            public string HotspotId { get; set; } = string.Empty;
            public string HotspotName { get; set; } = string.Empty;
            public string TokenType { get; set; } = string.Empty;
            public DateTime StartTimestamp { get; set; }
            public DateTime EndTimestamp { get; set; }
            public decimal Amount { get; set; }
        }

        private static void ProcessCSV(string inputFileName, string outputFileName, DateTime start, DateTime end, string currency, string aggregatePeriod, string exchangeRateCacheFileName)
        {
            var rewardRecords = new List<RewardRecord>();

            foreach (var line in File.ReadAllLines(inputFileName).Skip(1))
            {
                try
                {
                    var parts = line.Split(',');

                    if (parts.Length != 6)
                    {
                        continue;
                    }

                    var startTimestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[3].Trim())).DateTime;
                    var endTimestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[4].Trim())).DateTime;

                    if (startTimestamp >= start && endTimestamp <= end)
                    {
                        rewardRecords.Add(new RewardRecord
                        {
                            HotspotId = parts[0],
                            HotspotName = parts[1],
                            TokenType = parts[2],
                            StartTimestamp = startTimestamp,
                            EndTimestamp = endTimestamp,
                            Amount = decimal.Parse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture)
                        });
                    }
                }
                catch (FormatException)
                {
                    Console.WriteLine($"Format Exception in Line: {line}");
                    throw;
                }
            }

            var groupedData = GroupDataByPeriod(rewardRecords, aggregatePeriod, currency, exchangeRateCacheFileName);
            WriteAggregatedToCSV(outputFileName, groupedData, aggregatePeriod);
        }

        private static Dictionary<string, Dictionary<DateTime, decimal>> GroupDataByPeriod(List<RewardRecord> data, string period, string currency, string exchangeRateCacheFileName)
        {
            var result = new Dictionary<string, Dictionary<DateTime, decimal>>();

            foreach (var record in data)
            {
                decimal exchangeRate = GetExchangeRate(record.EndTimestamp.Date, currency, record.TokenType, exchangeRateCacheFileName);

                if (exchangeRate <= 0)
                {
                    throw new Exception($"Invalid exchange rate for record: {record.HotspotName} {record.EndTimestamp}");
                }

                DateTime periodKey = DeterminePeriodKey(record.EndTimestamp, period);

                if (!result.ContainsKey(record.HotspotName))
                {
                    result[record.HotspotName] = new Dictionary<DateTime, decimal>();
                }

                if (!result[record.HotspotName].ContainsKey(periodKey))
                {
                    result[record.HotspotName][periodKey] = 0;
                }

                result[record.HotspotName][periodKey] += record.Amount * exchangeRate;
            }

            return result;
        }

        private static DateTime DeterminePeriodKey(DateTime date, string period)
        {
            switch (period.ToLower())
            {
                case "day":
                    return new DateTime(date.Year, date.Month, date.Day);
                case "month":
                    return new DateTime(date.Year, date.Month, 1);
                case "year":
                    return new DateTime(date.Year, 1, 1);
                default:
                    throw new ArgumentException("Invalid period type.");
            }
        }

        private static Dictionary<string, Dictionary<DateTime, decimal>> LoadExchangeRates(string fileName)
        {
            var rates = new Dictionary<string, Dictionary<DateTime, decimal>>();
            foreach (var line in File.ReadAllLines(fileName).Skip(1))
            {
                var parts = line.Split(',');
                var date = DateTime.ParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var iotCadRate = decimal.Parse(parts[1]);
                var hntCadRate = decimal.Parse(parts[2]);

                if (!rates.ContainsKey("iot-cad"))
                    rates["iot-cad"] = new Dictionary<DateTime, decimal>();
                if (!rates.ContainsKey("hnt-cad"))
                    rates["hnt-cad"] = new Dictionary<DateTime, decimal>();

                rates["iot-cad"][date] = iotCadRate;
                rates["hnt-cad"][date] = hntCadRate;
            }
            return rates;
        }

        private static decimal GetExchangeRate(DateTime date, string currency, string tokenType, string exchangeRateCacheFileName)
        {
            string cacheKey = $"{tokenType.ToLower()}-{currency.ToLower()}";
            if (_exchangeRates.ContainsKey(cacheKey) && _exchangeRates[cacheKey].ContainsKey(date))
            {
                return _exchangeRates[cacheKey][date];
            }
            throw new Exception("Exchange rate not found for the given date and token type.");
        }

        private static void WriteAggregatedToCSV(string filePath, Dictionary<string, Dictionary<DateTime, decimal>> data, string totalAs)
        {
            var periods = data.SelectMany(d => d.Value.Keys).Distinct().OrderBy(x => x).ToList();
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine($"Hotspot Name,{string.Join(",", periods.Select(p => p.ToString(totalAs == "month" ? "yyyy-MM" : "yyyy-MM-dd")))}");
                foreach (var entry in data)
                {
                    sw.WriteLine($"{entry.Key},{string.Join(",", periods.Select(p => entry.Value.TryGetValue(p, out var amount) ? amount.ToString(CultureInfo.InvariantCulture) : "0"))}");
                }
            }
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var result = new Dictionary<string, string>();
            for (int i = 0; i < args.Length - 1; i += 2)
            {
                if (args[i].StartsWith("-"))
                {
                    result.Add(args[i].Substring(1), args[i + 1]);
                }
                else
                {
                    throw new ArgumentException("Invalid argument format");
                }
            }
            return result;
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("-s start_date [MM-dd-yyyy] -e end_date [MM-dd-yyyy] -i input_file_name -o output_file_name -p aggregate_period (day|month|year) -c currency -x exchange_rate_cache_filename");
            Console.WriteLine("Example: -s 01/01/2023 -e 01/01/2024 -p month -i input.csv -o output.csv -c cad -x exchange-rates.csv");
        }

    }
}