using HeliumDotNet;

namespace HeliumRewards
{
    internal class Program
    {
        static readonly DateTime DEFAULT_START = DateTime.Today;
        static readonly DateTime DEFAULT_END = DateTime.Today;
        static readonly string? DEFAULT_INPUT_FILENAME = null;
        static readonly string DEFAULT_OUTPUT_FILENAME = "output.csv";
        static readonly string DEFAULT_TOTAL_AS = "day";
        static readonly string DEFAULT_CURRENCY = "usd";

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

                DateTime start = parameters.ContainsKey("s") ? DateTime.Parse(parameters["s"]) : DEFAULT_START; // TODO: Adjust for UTC
                DateTime end = parameters.ContainsKey("e") ? DateTime.Parse(parameters["e"]) : DEFAULT_END; // TODO: Adjust for UTC
                string? inputFileName = parameters.ContainsKey("i") ? parameters["i"] : DEFAULT_INPUT_FILENAME;
                string outputFileName = parameters.ContainsKey("o") ? parameters["o"] : DEFAULT_OUTPUT_FILENAME;
                string totalAs = parameters.ContainsKey("t") ? parameters["t"] : DEFAULT_TOTAL_AS; // TODO: Enum & Validate
                string currency = parameters.ContainsKey("c") ? parameters["c"] : DEFAULT_CURRENCY; // TODO: lowercase, validate valid currency?

                List<string> minerNamesAndAddresses;

                if (inputFileName == null)
                {
                    Console.WriteLine("Input file not specified.");
                    ShowHelp();
                    return;
                }
                else
                {
                    minerNamesAndAddresses = File.Exists(inputFileName) ? File.ReadAllLines(inputFileName).ToList() : throw new FileNotFoundException($"Unable to find file at path: {inputFileName}");
                }

                HeliumApi helium = new HeliumApi();
                CoinGecko coinGecko = new CoinGecko();

                Dictionary<DateTime, decimal> exchangeRates = new Dictionary<DateTime, decimal>();
                Dictionary<string, Dictionary<DateTime, decimal>> rowData = new Dictionary<string, Dictionary<DateTime, decimal>>();

                foreach (string minerRecord in minerNamesAndAddresses)
                {
                    // Records are provided in the form of "name,address"
                    // This isn't normalized, but name can be associated to multiple addresses so we need to be sure we have the right address.

                    var minerDetails = minerRecord.Split(",");
                    string name = minerDetails[0];
                    string address = minerDetails[1];

                    // FIXME: Helium API uses end date exclusive, so the last day is missing. Need to adjust end DateTime to next day.
                    // FIXME: Also need to adjust for Daylight Savings & UTC.

                    // Always gather rewards and exchange rates on a daily basis
                    List<RewardSum> rewards = helium.RewardTotalForHotspot(address, start.ToString("o"), end.ToString("o"), "day");

                    rowData.Add(name, new Dictionary<DateTime, decimal>());

                    foreach (RewardSum data in rewards)
                    {
                        if (!exchangeRates.ContainsKey(data.Timestamp))
                        {
                            exchangeRates.Add(data.Timestamp, coinGecko.CoinHistory("helium", data.Timestamp, currency) ?? throw new ArgumentNullException($"Currency {currency} not found."));
                        }

                        rowData[name].Add(data.Timestamp, data.Total * exchangeRates[data.Timestamp]);

                        Console.WriteLine($"{data.Timestamp} : {name} : {data.Total} : {exchangeRates[data.Timestamp]}");
                    }
                }

                // Now sum it up into the required totalAs bucket

                foreach (string miner in rowData.Keys)
                {
                    switch (totalAs)
                    {
                        case "day":
                            rowData[miner] = rowData[miner].GroupBy(x => x.Key.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
                            break;
                        case "month":
                            rowData[miner] = rowData[miner].GroupBy(x => new DateTime(x.Key.Year, x.Key.Month, 1)).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
                            break;
                        case "year":
                            rowData[miner] = rowData[miner].GroupBy(x => new DateTime(x.Key.Year, 1, 1)).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
                            break;
                    }
                }

                // Prepare for CSV

                List<string[]> csvData = new List<string[]>();

                // Create header row
                string[] headerRow = new string[rowData.Values.First().Count + 1];
                headerRow[0] = "";
                int i = 1;
                foreach (KeyValuePair<DateTime, decimal> innerPair in rowData.Values.First())
                {
                    headerRow[i] = innerPair.Key.ToString();
                    i++;
                }
                csvData.Add(headerRow);

                // Create data rows
                foreach (KeyValuePair<string, Dictionary<DateTime, decimal>> outerPair in rowData)
                {
                    string[] csvRow = new string[outerPair.Value.Count + 1];
                    csvRow[0] = outerPair.Key;
                    i = 1;
                    foreach (KeyValuePair<DateTime, decimal> innerPair in outerPair.Value)
                    {
                        csvRow[i] = innerPair.Value.ToString();
                        i++;
                    }
                    csvData.Add(csvRow);
                }

                WriteToCSV(outputFileName, csvData);

            }
            catch (ArgumentException ex)
            {
                ShowHelp();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void WriteToCSV(string filePath, List<string[]> data)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                foreach (string[] row in data)
                {
                    string line = string.Join(",", row);
                    sw.WriteLine(line);
                }
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Could not parse args. Valid format is:");
            Console.WriteLine("-s start_date [MM-dd-yyyy]\r\n-e end_date [MM-dd-yyyy]\r\n-i input_file_name\r\n-o output_file_name\r\n-t total_as (day|month|year)\r\n-c currency");
            Console.WriteLine("Rewards are reported daily using that day's exchange rate.");
        }


        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            for (int i = 0; i < args.Length - 1; i = i + 2)
            {
                if (args[i].StartsWith("-"))
                {
                    // TODO: Args can only be in the set: s,e,i,o,t,c

                    result.Add(args[i].Substring(1), args[i + 1]);
                }
                else
                {
                    throw new ArgumentException();
                }
            }

            return result;
        }
    }
}