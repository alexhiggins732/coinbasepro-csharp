using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
namespace CoinbaseData
{
    public class DbCandle
    {

        public DateTimeOffset Time { get; set; }


        public decimal? Low { get; set; }


        public decimal? High { get; set; }


        public decimal? Open { get; set; }


        public decimal? Close { get; set; }


        public decimal Volume { get; set; }

        public static implicit operator Candle(DbCandle dbCandle)
        {
            return new Candle
            {
                Time = dbCandle.Time.UtcDateTime,
                Low = dbCandle.Low,
                High = dbCandle.High,
                Open = dbCandle.Open,
                Close = dbCandle.Close,
                Volume = dbCandle.Volume
            };
        }

    }
    public class DbCandles
    {
        public static List<Candle> GetDbCandles(ProductType productType,
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

            var tableName = $"{productType}{granularity}";
            var query = $"select * from {tableName} where [time] >=@start and [time] < @end order by [time]";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.Query<DbCandle>(query, new { Start = new DateTimeOffset(start), End = new DateTimeOffset(end) }).ToList();
                return result.Select(x => (Candle)x).ToList();
            }
        }

        public static List<Candle> GetTopNDbCandles(ProductType productType,
            CandleGranularity granularity, DateTime start, int bufferSize, bool useLocalTime = false)
        {
            if (useLocalTime)
            {

                start = start.ToUniversalTime();

            }

            var tableName = $"{productType}{granularity}";
            var query = $"select top {bufferSize} * from {tableName} where [time] >=@start order by [time]";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.Query<DbCandle>(query, new { Start = new DateTimeOffset(start) }).ToList();
                return result.Select(x => (Candle)x).ToList();
            }
        }

        public static void DeleteLatest(ProductType productType, CandleGranularity granularity)
        {
            var time = GetMaxDbCandleDate(productType, granularity);
            var tableName = $"{productType}{granularity}";
            var query = $"delete from {tableName} where [time] = @time; select @@ROWCOUNT";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        int count = conn.QuerySingle<int>(query, new { time }, transaction: trans);
                        if (count == 1)
                            trans.Commit();
                        else throw new Exception("Delete count did not match 1");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{ex.Message}");
                        trans.Rollback();
                    }

                }


            }
        }

        public static DateTime GetMinDbCandleDate(ProductType productType, CandleGranularity granularity)
        {
            var tableName = $"{productType}{granularity}";
            var query = $"select min([time]) from {tableName}";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.QuerySingle<DateTimeOffset>(query);
                return result.UtcDateTime;
            }
        }

        public static DateTime GetMaxDbCandleDate(ProductType productType, CandleGranularity granularity)
        {
            var tableName = $"{productType}{granularity}";
            var query = $"select max([time]) from {tableName}";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.QuerySingle<DateTimeOffset>(query);
                return result.UtcDateTime;
            }
        }


    }
}
