using CoinbasePro.Network.Authentication;
using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoinbaseData;
using System.IO;

namespace CoinbaseConsole
{
    class Program
    {
        static void LogError(string message)
        {
            var sw = new StreamWriter("error.log", true);
            sw.WriteLine($"[{DateTime.Now}] [ERROR] {message}");
            sw.Close();
        }
        static void SaveCandles(ProductType productPair, CandleGranularity granularity)
        {


            var svc = new CandleService();
            int minutesPerCandle = ((int)granularity) / 60;
            int minutesPerOffset = 300 * minutesPerCandle;
            var startDate = DateTime.Parse("12/31/2016");
            var endDate = startDate.AddMinutes(minutesPerOffset);
            var tableName = $"{productPair}{granularity}";
            while (startDate < DateTime.Now)
            {
                try
                {
                    Console.Title = $"Fetch {productPair} Candles: {startDate} - {endDate}";
                    var candles = svc
                                      .GetCandles(productPair, startDate, endDate, granularity)
                                      .OrderBy(x => x.Time)
                                      .ToList();
                    if (candles.Count > 0)
                    {
                        Console.Title = $"Fetch {productPair} Candles: {startDate} - {endDate} - {candles.Count}";
                        TableHelper.Save(() => candles, tableName);
                    }
                    System.Threading.Thread.Sleep(400);
                    startDate = startDate.AddMinutes(minutesPerOffset);
                    endDate = startDate.AddMinutes(minutesPerOffset);
                }
                catch (Exception ex)
                {
                    LogError(ex.Message);
                }



            }


        }
        static void Main(string[] args)
        {
            SaveCandles(ProductType.LtcUsd, CandleGranularity.Minutes1);
            TableHelper.AssureCandlesTables(ProductType.LtcUsd);
            //create an authenticator with your apiKey, apiSecret and passphrase
            var authenticator = new Authenticator(Creds.ApiKey, Creds.ApiSecret, Creds.PassPhrase);

            var client = Client.Instance = new CoinbasePro.CoinbaseProClient(authenticator);
            //create the CoinbasePro client

            //use one of the services 
            var allAccounts = client.AccountsService.GetAllAccountsAsync().GetAwaiter().GetResult();

            var candleService = new CandleService();
            //var now = DateTime.UtcNow;
            //var start = now.AddMinutes(-(300 * 15));
            var now = DateTime.Now;
            var start = now.AddMinutes(-(300 * 15));
            var candles = candleService
                .GetCandles(ProductType.LtcUsd, start, now, CandleGranularity.Minutes15);

        }

        static List<Candle> GetCandles(ProductType productPair,
            DateTime start,
            DateTime end,
            CandleGranularity granularity)
        {
            return Client.Instance
                .ProductsService
                .GetHistoricRatesAsync(productPair, start, end, granularity)
                .GetAwaiter()
                .GetResult()
                .ToList(); ;
        }
    }

    public class CoinbaseServiceBase
    {
        public CoinbasePro.CoinbaseProClient client = Client.Instance;
    }

    public class DateHelper
    {

        public static DateTime ConvertToLocal(DateTime value)
        {
            var now = DateTime.Now;
            var utcNow = DateTime.UtcNow;
            var diff = now.Subtract(utcNow).TotalHours;
            return value.AddHours(diff);
        }
    }
    public class CandleService : CoinbaseServiceBase
    {

        public List<Candle> GetCandles(ProductType productPair,
           DateTime start,
           DateTime end,
           CandleGranularity granularity,
           bool useLocalTime = true)
        {
            if (useLocalTime)
            {

                start = start.ToUniversalTime();
                end = end.ToUniversalTime();
            }
            var result = client
                .ProductsService
                .GetHistoricRatesAsync(productPair, start, end, granularity)
                .GetAwaiter()
                .GetResult()
                .ToList();
            if (useLocalTime)
                result.ForEach(x => x.Time = x.Time.ToLocalTime());
            return result;

        }
    }
}
