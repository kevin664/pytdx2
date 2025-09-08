using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tdx.Core;

namespace Tdx.Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Required for 'gbk' encoding
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var client = new TdxClient(heartbeat: true, autoRetry: true);

            Console.WriteLine("Connecting to the best server...");
            var connected = await client.ConnectAsync();
            if (!connected)
            {
                Console.WriteLine("Failed to connect.");
                return;
            }

            Console.WriteLine("Logging in...");
            var loggedIn = await client.LoginAsync();
            if (!loggedIn)
            {
                Console.WriteLine("Failed to log in.");
                await client.DisconnectAsync();
                return;
            }

            Console.WriteLine("Login successful. Starting tests...");

            try
            {
                // --- Run a series of tests ---

                PrintObject("Get Security Count (SZ)", await client.GetSecurityCountAsync(Market.SZ));

                PrintObject("Get Security List (SZ, 0, 10)", await client.GetSecurityListAsync(Market.SZ, 0, 10));

                PrintObject("Get Security Bars (SZ, 000001)", await client.GetSecurityBarsAsync(Market.SZ, "000001", KlineType.DayK, 0, 10));

                PrintObject("Get Index Bars (SH, 999999)", await client.GetSecurityBarsAsync(Market.SH, "999999", KlineType.DayK, 0, 10));

                PrintObject("Get Orders (SZ, 000001)", await client.GetOrdersAsync(Market.SZ, "000001"));

                PrintObject("Get History Orders (SZ, 000001)", await client.GetHistoryOrdersAsync(Market.SZ, "000001", new DateTime(2023, 10, 10)));

                PrintObject("Get Security Quotes (SZ, 000001 and SH, 600519)", await client.GetSecurityQuotesAsync(
                    (Market.SZ, "000001"),
                    (Market.SH, "600519")
                ));

                PrintObject("Get Company Info (SZ, 000001)", await client.GetCompanyInfoAsync(Market.SZ, "000001"));

            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during tests: {ex}");
            }
            finally
            {
                await client.DisconnectAsync();
                Console.WriteLine("Tests finished. Disconnected.");
            }
        }

        private static void PrintObject(string title, object? obj)
        {
            Console.WriteLine($"\n--- {title} ---");
            if (obj == null)
            {
                Console.WriteLine("null");
                return;
            }

            if (obj is IEnumerable list && obj is not string)
            {
                var count = 0;
                foreach (var item in list)
                {
                    Console.WriteLine(JsonSerializer.Serialize(item));
                    count++;
                }
                Console.WriteLine($"Total items: {count}");
            }
            else
            {
                Console.WriteLine(JsonSerializer.Serialize(obj));
            }
        }
    }
}
