using CoinbaseUtils;
using CoinbaseData;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils
{
    class Program
    {
        static void Main(string[] args)
        {
            Statistics.SmaTests.RunTests();

            var candleStream = new CandleDbReader(ProductType.LtcUsd, CandleGranularity.Minutes1);
            int count =0;
            var avg = candleStream.Sum(x => x.Volume);
            foreach(var candle in candleStream)
            {
                count++;
                if ((count & 65535) == 0)
                    Console.WriteLine(count);
            }

            RunPotentialTest();
        }

        private static void RunMaTest()
        {

        }
        private static void RunPotentialTest()
        {
            var pc = new PotentialCalculator(ProductType.LtcUsd, CandleGranularity.Hour24);
            var trades = pc.GetBestTrades();
            var profit = trades.Sum(x => x.NetProfit);
        }
    }
    public class PotentialCalculator
    {
        ProductType ProductType;
        CandleGranularity Granularity;
        string TableName => $"{ProductType}{Granularity}";
        string TableNameEscaped => $"[{TableName}]";
        string TimeColumn => $"Time";
        string TimeColumnEscaped => $"[{TimeColumn}]";

        string CloseColumn => $"Close";
        string CloseColumnEscaped => $"[{CloseColumn}]";

        DateTimeOffset MinDate;
        DateTimeOffset MaxDate;

        decimal Fee = 0.00m;
        decimal FeePerTrade = 0.00m;
        int GranularityMinutes;
        public PotentialCalculator(ProductType productType, CandleGranularity granularity, decimal fee = .005m)
        {
            this.ProductType = productType;
            this.Granularity = granularity;
            this.GranularityMinutes = ((int)Granularity / 60);
            this.FeePerTrade = 2 * (this.Fee = fee);
        }

        public void RunOld()
        {
            decimal low;
            decimal high;
            DateTimeOffset minLowDate;
            DateTimeOffset maxLowDate;
            DateTimeOffset minHighDate;
            DateTimeOffset maxHighDate;
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                this.MinDate = conn.QuerySingle<DateTimeOffset>($"select min([time]] from {TableName}");
                this.MaxDate = conn.QuerySingle<DateTimeOffset>($"select max([time]] from {TableName}");
                low = conn.QuerySingle<decimal>($"select min([close]] from {TableName}");
                high = conn.QuerySingle<decimal>($"select max([close]] from {TableName}");
                minLowDate = conn.QuerySingle<DateTimeOffset>($"select min([time] from {TableName} where [close]=@low", new { low });
                maxLowDate = conn.QuerySingle<DateTimeOffset>($"select max([time] from {TableName} where [close]=@low", new { low });

                minHighDate = conn.QuerySingle<DateTimeOffset>($"select min([time] from {TableName} where [close]=@high", new { high });
                maxHighDate = conn.QuerySingle<DateTimeOffset>($"select max([time] from {TableName} where [close]=@high", new { high });
            }

            System.Diagnostics.Debug.Assert(minLowDate == maxLowDate);
            System.Diagnostics.Debug.Assert(minHighDate == maxHighDate);

            // we now have 3 time periods.
            // start to atl, atl-ath, ath to end date.
            // find the highest combination of trades in each.
        }

        public List<BestTrade> GetBestTrades()
        {

            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                this.MinDate = conn.QuerySingle<DateTimeOffset>($"select min({TimeColumnEscaped}) from {TableNameEscaped}").AddMinutes(-GranularityMinutes);
                this.MaxDate = conn.QuerySingle<DateTimeOffset>($"select max({TimeColumnEscaped}) from {TableNameEscaped}").AddMinutes(GranularityMinutes);
            }

            var result = GetBestTrades(this.MinDate, this.MaxDate);
            return result;
        }

        public List<BestTrade> GetBestTrades(DateTimeOffset rangeStartDate, DateTimeOffset rangeEndDate)
        {

            List<BestTrade> result = new List<BestTrade>();
            
            decimal rangeLow;
            decimal rangeHigh;

            var timeFilter = $"{TimeColumnEscaped} >= @{nameof(rangeStartDate)} and {TimeColumnEscaped} < @{nameof(rangeEndDate)}";
            DateTimeOffset rangeHighDate;
            DateTimeOffset rangeLowDate;
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                // 1) Find the High.
                rangeHigh = conn.QuerySingle<decimal>($"select IsNull(max({CloseColumnEscaped}), 0) from {TableName} where {timeFilter}",
                    new { rangeStartDate, rangeEndDate });
                
                if (rangeHigh == 0)
                {
                    return result;
                }
                // 2) Get the first date the high was hit.
                rangeHighDate = conn.QuerySingle<DateTimeOffset>(
                    $"select min({TimeColumnEscaped}) from {TableName} where {timeFilter} and [close]=@{nameof(rangeHigh)}",
                    new { rangeHigh, rangeStartDate, rangeEndDate });

                var lowTimeFilter = $"({TimeColumnEscaped} >= @{nameof(rangeStartDate)} and {TimeColumnEscaped} < @{nameof(rangeHighDate)})";
                // 3) Find the Lowest value in the range before the date of the High
                rangeLow = conn.QuerySingle<decimal>($"select IsNull(min({CloseColumnEscaped}), 0) from {TableName} where {lowTimeFilter}",
                    new { rangeStartDate, rangeHighDate });

                if (rangeLow == 0)
                {
                    return result;
                }

                // 4) Get the closed date to the high the low was hit.
                rangeLowDate = conn.QuerySingle<DateTimeOffset>(
                   $"select Max({TimeColumnEscaped}) from {TableName} where {lowTimeFilter} and [close]=@{nameof(rangeLow)}",
                   new { rangeLow, rangeStartDate, rangeHighDate });
            }

            var initial = new BestTrade
            {
                EntryDate = rangeLowDate,
                EntryPrice = rangeLow,
                ExitDate = rangeHighDate,
                ExitPrice = rangeHigh,
                Gross = rangeHigh / rangeLow,
                Net = (rangeHigh / rangeLow) - FeePerTrade
            };
            if (initial.Net < 1m)
            {
                result.Add(initial);
            } else
            {
                // we have three ranges to check.
                if (rangeStartDate==rangeLowDate && rangeEndDate == rangeHighDate)
                {
                    string bp = "";
                }
                var bestLeft = GetBestTrades(rangeStartDate, rangeLowDate);
                var bestMid = GetBestTrades(rangeLowDate, rangeHighDate);
                var bestRight= GetBestTrades(rangeHighDate, rangeEndDate);
                var leftSum = bestLeft.Sum(x => x.NetProfit);
                var midSum = bestMid.Sum(x => x.NetProfit);
                var rightSum = bestRight.Sum(x => x.NetProfit);

                // 

                var splitSum = leftSum + midSum + rightSum;
                var initialSum = initial.NetProfit;

            }


            return result;
        }

    }

    public class BestTrade
    {
        public DateTimeOffset EntryDate { get; internal set; }
        public decimal EntryPrice { get; internal set; }
        public DateTimeOffset ExitDate { get; internal set; }
        public decimal ExitPrice { get; internal set; }
        public decimal Net { get; internal set; }
        public decimal Gross { get; internal set; }
        public decimal NetProfit { get; private set; }
      
    }
}
