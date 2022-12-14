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
                
                DateTime start = parameters.ContainsKey("s") ? DateTime.Parse(parameters["s"]): DEFAULT_START; // TODO: Adjust for UTC
                DateTime end = parameters.ContainsKey("e") ? DateTime.Parse(parameters["e"]) : DEFAULT_END; // TODO: Adjust for UTC
                string? inputFileName = parameters.ContainsKey("i") ? parameters["i"] : DEFAULT_INPUT_FILENAME;
                string outputFileName = parameters.ContainsKey("o") ? parameters["o"] : DEFAULT_OUTPUT_FILENAME;
                string totalAs = parameters.ContainsKey("t") ? parameters["t"] : DEFAULT_TOTAL_AS; // TODO: Enum & Validate
                string currency = parameters.ContainsKey("c") ? parameters["c"] : DEFAULT_CURRENCY; // TODO: lowercase, validate valid currency?

                HeliumApi helium = new HeliumApi();
                CoinGecko coinGecko = new CoinGecko();

                // TODO: Read list from file
                string address = helium.HotspotsForName("kind-snowy-fish")[0].Address;

                // Always bucket rewards on a daily basis
                List<RewardSum> rewards = helium.RewardTotalForHotspot(address, start.ToString("o"), end.ToString("o"), "day");

                foreach (RewardSum data in rewards)
                {
                    decimal? xRate = coinGecko.CoinHistory("helium", data.Timestamp, currency);
                    Console.WriteLine($"{data.Timestamp}: {data.Total} : {xRate}");
                }
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("Could not parse args. Valid format is:");
                Console.WriteLine("-s start_date [MM-dd-yyyy]\r\n-e end_date [MM-dd-yyyy]\r\n-i input_file_name\r\n-o output_file_name\r\n-t total_as (day|week|month|year)\r\n-c currency");
                Console.WriteLine("Rewards are reported daily using that day's exchange rate.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        static Dictionary<string, string> ParseArgs(string[] args)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            for (int i = 0; i < args.Length - 1; i = i + 2)
            {
                if (args[i].StartsWith("-"))
                {
                    // TODO: Args can only be in the set: s,e,i,o,t,c

                    result.Add(args[i].Substring(1), args[i+1]);
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