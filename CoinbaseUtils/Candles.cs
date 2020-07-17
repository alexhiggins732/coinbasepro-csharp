using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using System;
using System.Collections.Generic;
using System.Linq;

using Dapper;
using CoinbaseData;

namespace CoinbaseUtils
{
    public class Candles
    {
        public static CandleService candleService = new CandleService();

        public static DateTime SaveCandles(ProductType productType, CandleGranularity granularity, DateTime startDate, DateTime maxDate)
        {
            var svc = candleService;
            int minutesPerCandle = ((int)granularity) / 60;
            int minutesPerOffset = 300 * minutesPerCandle;

            var endDate = startDate.AddMinutes(minutesPerOffset);
            if (endDate > maxDate)
            {
                endDate = maxDate;
            }
            var tableName = $"{productType}{granularity}";
            System.Diagnostics.Stopwatch sw = null;
            DateTime minDate = maxDate;

            bool useTitle = true;
            try
            {
                Console.Title = "Test";
            }
            catch
            {
                useTitle = false;
            }
            var minDbDate = DbCandles.GetMaxDbCandleDate(productType, granularity);
            while (startDate < maxDate)
            {
                try
                {
                    string message = $"Fetch {productType} {granularity} Candles: {startDate} - {endDate}";
                    if (useTitle)
                    {
                        Console.Title = message;
                    }
                    sw = System.Diagnostics.Stopwatch.StartNew();
                    var candles = svc
                                      .GetCandles(productType, startDate, endDate, granularity, false)
                                      .OrderBy(x => x.Time)
                                      .ToList();
                    if (candles.Count > 0)
                    {
                        message = $"Fetch {productType} {granularity} Candles: {startDate} - {endDate} - {candles.Count}";
                        if (useTitle)
                        {
                            Console.Title = message;
                        }
                        else
                        {
                            Console.WriteLine(message);
                        }
                        TableHelper.Update(() => candles.Where(x => x.Time <= minDbDate), tableName, "Time");
                        TableHelper.Save(() => candles.Where(x => x.Time > minDbDate), tableName);
                        var minCandlesDate = candles.Min(x => x.Time);
                        if (minCandlesDate < minDate)
                        {
                            minDate = minCandlesDate;
                        }
                    }

                    startDate = startDate.AddMinutes(minutesPerOffset);
                    endDate = startDate.AddMinutes(minutesPerOffset);
                    //if (endDate > maxDate)
                    //{
                    //    endDate = maxDate;
                    //}
                    var elapsedMs = (int)sw.ElapsedMilliseconds;
                    //600 exceeded
                    var sleep = Math.Max(0, 1000 - elapsedMs);
                    System.Threading.Thread.Sleep(sleep);

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now}]: {ex.Message}");
                    var elapsedMs = (int)sw.ElapsedMilliseconds;
                    var sleep = Math.Max(0, 600 - elapsedMs);
                    System.Threading.Thread.Sleep(sleep);
                }

            }
            DeDupeCandleTable(tableName);
            return minDate;
        }



        /// <summary>
        /// Dedupes the target table name to the disctinct [time], [low], [high], [open], [close], and [volume] columns.
        /// </summary>
        /// <param name="tableName"></param>
        /// <remarks>This is a guard against Coinbase Pro returning candles outside of the requested date range.
        /// </remarks>
        public static void DeDupeCandleTable(string tableName)
        {
            var escapedTableName = $"[{tableName}]";
            var distinctTableName = $"[{tableName}_distinct]";

            using (var conn = new System.Data.SqlClient.SqlConnection(TableHelper.ConnectionString))
            {
                conn.Open();
                var hasDupesQuery = $@"
                        declare @count int = (select count(0) from {escapedTableName})
                        declare @dateCount int = (select count(distinct [time]) from {escapedTableName})
                        select HasDups = Cast(case when @count=@dateCount then 0 else 1 end as bit)";

                bool hasDupes = conn.QuerySingle<bool>(hasDupesQuery);
                if (hasDupes)
                {
                    System.Data.SqlClient.SqlTransaction trans = null;
                    try
                    {
                        using (trans = conn.BeginTransaction())
                        {

                            var cmd = conn.CreateCommand();
                            cmd.Transaction = trans;
                            cmd.CommandType = System.Data.CommandType.Text;

                            var query = $"select distinct [time], [low], [high], [open], [close], [volume] into {distinctTableName} from {escapedTableName}";

                            cmd.CommandText = query;
                            cmd.ExecuteScalar();
                            //conn.Execute(query);

                            query = $"truncate table {escapedTableName}";
                            cmd.CommandText = query;
                            cmd.ExecuteScalar();
                            //conn.Execute(query);

                            query = $@"insert into {escapedTableName}
							([time], [low], [high], [open], [close], [volume]) 
					        select [time], [low], [high], [open], [close], [volume] from {distinctTableName}
					        order by [time]";
                            cmd.CommandText = query;
                            cmd.ExecuteScalar();

                            query = $"drop table {distinctTableName}";
                            cmd.CommandText = query;
                            cmd.ExecuteScalar();

                            trans.Commit();
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            trans?.Rollback();
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine($"Transaction Rollback failed for {tableName} - {ex2.Message}");

                        }
                        Console.WriteLine($"Deduplication failed for {tableName} - {ex.Message}");

                    }
                }
            }
        }
        public static DateTime GetMinCandleDateFromApi(ProductType productType)
        {
            var endDate = DateTime.Now.Date.AddDays(1).ToUniversalTime().Date;
            var granularity = CandleGranularity.Hour24;
            int granSeconds = (int)(granularity) * 300;
            var startDate = endDate.AddSeconds(-granSeconds);
            bool found = true;
            List<Candle> candles = null;
            var result = endDate;
            while (found)
            {
                candles = candleService
                                .GetCandles(productType, startDate, endDate, granularity, false)
                                .OrderBy(x => x.Time)
                                .ToList();
                found = (candles.Count > 0);
                if (found)
                {
                    result = candles.Min(x => x.Time);
                }
                startDate = startDate.AddSeconds(-granSeconds);
                endDate = endDate.AddSeconds(-granSeconds);
                System.Threading.Thread.Sleep(800);

            }
            return result;

        }
        public static void SaveCandles(IEnumerable<ProductType> productTypes, IEnumerable<CandleGranularity> candleGranularities)
        {
            var maxDate = DateTime.Now.Date.AddDays(1).ToUniversalTime().Date;
            var minDates = productTypes.ToDictionary(x => x, x => GetMinCandleDateFromApi(x));

            foreach (var productType in productTypes)
            {
                TableHelper.AssureCandlesTables(productType);
            }

            foreach (var candleGranularity in candleGranularities.OrderByDescending(x => (int)x))
            {
                foreach (var productType in productTypes)
                {
                    SaveCandles(productType, candleGranularity, minDates[productType], maxDate);
                }
            }
        }
    }
}
