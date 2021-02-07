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
        public static implicit operator DbCandle(Candle dbCandle)
        {
            return new DbCandle
            {
                Time = dbCandle.Time,
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

        public static DateTime GetMinDbCandleDateForSampleSizeAndStartDate
            (ProductType productType, CandleGranularity granularity, DateTime startDate, int sampleSize)
        {
            var tableName = $"{productType}{granularity}";
            var query = $@"select time from (
                   select time, 
	                ROW_NUMBER() over (order by time desc) as RowNum
	                from [{tableName}]
	                where time<@startDate
              ) a
              where RowNum =@sampleSize";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.QuerySingleOrDefault<DateTimeOffset?>(query, new { startDate, sampleSize });
                if (result == null)
                {
                    var minDate = GetMinDbCandleDate(productType, granularity);
                    int granMinutes = ((int)granularity) / 60;
                    result = minDate.AddMinutes(granMinutes * sampleSize);
                    result = conn.QuerySingle<DateTimeOffset>($"select top 1 time from [{tableName}] where time>=@result order by time",
                        new { result });
                }
                return result.Value.UtcDateTime;
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
        public static bool CandleTableExists(ProductType productType, CandleGranularity granularity)
        {
            var tableName = $"{productType}{granularity}";
            var query = $"select count(0) from sys.tables where name=@tableName";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                conn.Open();
                int count = conn.QuerySingle<int>(query, new { tableName });
                return count > 0;
            }
        }

        public static void AssureCandlesTables(ProductType productType)
        {
            TableHelper.AssureCandlesTables(productType);
        }

        public static Candle GetLastCandleBeforeDate(ProductType productType, CandleGranularity granularity, DateTime startDate)
        {
            var tableName = $"{productType}{granularity}";
            var query = $"select top 1 * from {tableName} where [time] <@startDate order by [time] desc";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.QuerySingle<DbCandle>(query, new { startDate });
                return result;
            }
        }

        public static DateTime GetMinDbCandleDate(ProductType productType, CandleGranularity granularity)
        {
            AssureCandlesTables(productType);
            var tableName = $"{productType}{granularity}";
            var query = $"select min([time]) from {tableName}";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.QuerySingleOrDefault<DateTimeOffset?>(query);
                return (result.HasValue ? result.Value : default(DateTimeOffset)).UtcDateTime;
            }
        }

        public static DateTime GetMinDbCandleDateAfterDate(ProductType productType, CandleGranularity granularity, DateTime startDate)
        {
            var tableName = $"{productType}{granularity}";
            var query = $"select min([time]) from {tableName} where [time]>=@startDate";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.QuerySingle<DateTimeOffset?>(query, new { startDate });
                return (result.HasValue ? result.Value : default(DateTimeOffset)).UtcDateTime;

            }
        }

        public static DateTime GetMaxDbCandleDate(ProductType productType, CandleGranularity granularity)
        {
            var tableName = $"{productType}{granularity}";
            var query = $"select max([time]) from {tableName}";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.QuerySingleOrDefault<DateTimeOffset?>(query);
                return (result.HasValue ? result.Value : default(DateTimeOffset)).UtcDateTime;
            }
        }

        public static int GetCandleCountBeforeDate(ProductType productType, CandleGranularity granularity, DateTime startDate)
        {
            var tableName = $"{productType}{granularity}";
            var query = $"select count(0) from {tableName} where [time]<@startDate";
            using (var conn = new SqlConnection(TableHelper.ConnectionString))
            {
                var result = conn.QuerySingle<int>(query, new { startDate });
                return result;
            }
        }

    }
}


