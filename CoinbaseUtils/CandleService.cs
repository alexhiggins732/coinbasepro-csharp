using CoinbaseData;
using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace CoinbaseUtils
{
    public class CandleService : CoinbaseService
    {


        public List<Candle> GetCandles(ProductType productPair,
           DateTime start,
           DateTime end,
           CandleGranularity granularity,
           bool useLocalTime = true)
        {
            if (start.Year < 2010)
            {
                var diff = end - start;
                start = Candles.GetMinCandleDateFromApi(productPair);
                end = start + diff;
            }
            if (useLocalTime)
            {

                start = start.ToUniversalTime();
                end = end.ToUniversalTime();
            }
            var t = System.Threading.Tasks.Task.Run(() => client
                .ProductsService
                .GetHistoricRatesAsync(productPair, start, end, granularity)
                .GetAwaiter()
                .GetResult()
                .Where(x => x.Time <= end && x.Time >= start)
                .ToList());
            t.Wait();
            var result = t.Result;
            //var result = client
            //    .ProductsService
            //    .GetHistoricRatesAsync(productPair, start, end, granularity)
            //    .GetAwaiter()
            //    .GetResult()
            //    .Where(x => x.Time <= end && x.Time >= start)
            //    .ToList();
            if (useLocalTime)
                result.ForEach(x => x.Time = x.Time.ToLocalTime());
            return result;

        }

        public static List<Candle> GetDbCandles(ProductType productType,
          DateTime start,
          DateTime end,
          CandleGranularity granularity,
          bool useLocalTime = true) => DbCandles.GetDbCandles
            (productType, start, end, granularity, useLocalTime);

        public static DateTime GetMinDbCandleDate(ProductType productType, CandleGranularity granularity)
            => DbCandles.GetMinDbCandleDate(productType, granularity);

        public static DateTime GetMinDbCandleDateAfterDate(ProductType productType, CandleGranularity granularity, DateTime startDate)
           => DbCandles.GetMinDbCandleDateAfterDate(productType, granularity, startDate);

        public static DateTime GetMaxDbCandleDate(ProductType productType, CandleGranularity granularity)
            => DbCandles.GetMaxDbCandleDate(productType, granularity);

        public static DateTime GetMinDbCandleDateForSampleSizeAndStartDate(ProductType productType, CandleGranularity granularity, DateTime startDate, int sampleSize)
            => DbCandles.GetMinDbCandleDateForSampleSizeAndStartDate(productType, granularity, startDate, sampleSize);

        public static int GetCandleCountBeforeDate(ProductType productType, CandleGranularity granularity, DateTime startDate)
             => DbCandles.GetCandleCountBeforeDate(productType, granularity, startDate);

        public static void UpdateCandles(ProductType selectedProductType, bool force = false)
        {
            AssureCandlesTables(selectedProductType);
            var maxDt = CandleService.GetMaxDbCandleDate(selectedProductType, CandleGranularity.Hour24);
            if (force || maxDt.Date != DateTime.UtcNow.Date)
            {
                var granularities = new List<CandleGranularity>();
                foreach (CandleGranularity granularity in Enum.GetValues(typeof(CandleGranularity)))
                {
                    granularities.Add(granularity);
                }
                granularities = granularities.OrderByDescending(x => (int)x).ToList();
                foreach (CandleGranularity granularity in granularities)
                {
                    var latest = GetMaxDbCandleDate(selectedProductType, granularity);
                    //DbCandles.DeleteLatest(selectedProductType, granularity);
                    Candles.SaveCandles(selectedProductType, granularity, latest, DateTime.UtcNow);
                }
            }
        }

        public static void AssureCandlesTables(ProductType productType)
        {
            bool exists = DbCandles.CandleTableExists(productType, CandleGranularity.Hour24);
            if (!exists)
            {
                var grans = new[] { CandleGranularity.Minutes1, CandleGranularity.Minutes5, CandleGranularity.Minutes15, CandleGranularity.Hour1, CandleGranularity.Hour6, CandleGranularity.Hour24 };
                var products = new[] { productType };
                Candles.SaveCandles(products, grans);
            }
        }



        public static Candle GetLastCandleBeforeDate(ProductType productType, CandleGranularity granularity, DateTime startDate)
              => DbCandles.GetLastCandleBeforeDate(productType, granularity, startDate);

    }
    public class CandleDbReader : IEnumerable<Candle>
    {

        public ProductType ProductType { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }
        public CandleGranularity Granularity { get; private set; }
        private string tableName;

        public CandleDbReader(ProductType productType,
          DateTime start,
          DateTime end,
          CandleGranularity granularity)
        {
            this.ProductType = productType;
            this.StartDate = start;
            this.EndDate = end;
            this.Granularity = granularity;
            this.tableName = $"{productType}{tableName}";
        }

        public CandleDbReader(ProductType productType, CandleGranularity granularity, DateTime? startDate = null)
            : this(productType,
                  startDate == null ? CandleService.GetMinDbCandleDate(productType, granularity) : startDate.Value,
                  CandleService.GetMaxDbCandleDate(productType, granularity),
                  granularity)
        {
        }

        public IEnumerator<Candle> GetEnumerator()
            => new DbCandleEnumerator(ProductType, StartDate, EndDate, Granularity);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    }

    public class DbCandleEnumerator : IEnumerator<Candle>
    {

        DbCandleEnumeratorWithMissing enumerator;
        int incMinutes;
        ProductType productType;
        CandleGranularity granularity;
        public DbCandleEnumerator(ProductType productType, DateTime startDate, DateTime endDate, CandleGranularity granularity)
        {
            this.productType = productType;
            this.granularity = granularity;
            incMinutes = ((int)granularity) / 60;
            this.enumerator = new DbCandleEnumeratorWithMissing(productType, startDate, endDate, granularity);
        }

        public Candle Current => CurrentCandle;
        private Candle CurrentCandle = null;
        private Candle NextCandle = null;
        private Candle GetCurrentCandle()
        {
            return CurrentCandle;
        }
        object IEnumerator.Current => Current;

        public void Dispose()
        {
            enumerator.Dispose();
            enumerator = null;
        }


        public bool MoveNext()
        {
            bool result = false;
            if (CurrentCandle == null)
            {
                result = enumerator.MoveNext();
                if (result)
                {
                    CurrentCandle = enumerator.Current;

                    // TODO: bounds checked to see candle exists
                    if (CurrentCandle.Time != enumerator.StartDate)
                    {
                        CurrentCandle = CandleService.GetLastCandleBeforeDate(productType, granularity, enumerator.StartDate);
                        CurrentCandle.Open = CurrentCandle.High = CurrentCandle.Low = CurrentCandle.Close;
                        CurrentCandle.Volume = 0;
                        CurrentCandle.Time = enumerator.StartDate;
                        NextCandle = enumerator.Current;
                    }
                }
            }
            else
            {
                if (NextCandle == null)
                {
                    result = enumerator.MoveNext();
                    if (result)
                    {
                        NextCandle = enumerator.Current;
                        CurrentCandle.Time = CurrentCandle.Time.AddMinutes(incMinutes);
                        if (CurrentCandle.Time == NextCandle.Time)
                        {
                            CurrentCandle = NextCandle;
                            NextCandle = null;
                        }
                        else
                        {
                            //set all values to the previous close and set the volume to 0;
                            CurrentCandle.Open = CurrentCandle.High = CurrentCandle.Low = CurrentCandle.Close;
                            CurrentCandle.Volume = 0;
                        }
                    }
                }
                else // we have a bufferred next candle due to missing candles.
                {
                    CurrentCandle.Time = CurrentCandle.Time.AddMinutes(incMinutes);
                    result = CurrentCandle.Time <= enumerator.EndDate;
                    if (result)
                    {
                        //if the buffered next candle is the correct time return it as current
                        if (CurrentCandle.Time == NextCandle.Time)
                        {
                            CurrentCandle = NextCandle;
                            NextCandle = null;

                        }
                        else
                        {

                            //properties should already be cleared but...
                            // explicitiy set them in case the if branch that sets the properties gets changed.
                            CurrentCandle.Open = CurrentCandle.High = CurrentCandle.Low = CurrentCandle.Close;
                            CurrentCandle.Volume = 0;
                        }
                    }
                    else
                    {

                    }
                }
            }
            if (!result)
                CurrentCandle = null;
            return result;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
    public class DbCandleEnumeratorWithMissing : IEnumerator<Candle>
    {
        public List<Candle> Buffer { get; private set; }
        public ProductType productType { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }
        public CandleGranularity Granularity { get; private set; }
        private string tableName;

        private DateTime CurrentStartDate;
        private DateTime CurrentEndDate;
        private int bufferSize;
        private int secondsPerBuffer;
        private int bufferIndex;
        public DbCandleEnumeratorWithMissing(ProductType productType, DateTime startDate, DateTime endDate, CandleGranularity granularity)
        {
            this.productType = productType;
            StartDate = startDate;
            EndDate = endDate;
            Granularity = granularity;

            this.tableName = $"{productType}{tableName}";
            this.Buffer = new List<Candle>();
            this.bufferSize = 1000;
            this.secondsPerBuffer = bufferSize * (int)granularity;
            this.bufferIndex = Buffer.Count;
            CurrentEndDate = startDate.AddSeconds(-(int)granularity);


        }

        public Candle Current => bufferIndex < Buffer.Count ? Buffer[bufferIndex] : null;

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            Buffer = null;
        }

        public bool MoveNext()
        {
            if (++bufferIndex < Buffer.Count)
            {
                return true;
            }
            Buffer.Clear();

            if (CurrentStartDate <= EndDate)
            {
                while (Buffer.Count == 0 && CurrentStartDate <= EndDate)
                {
                    CurrentStartDate = CurrentEndDate.AddSeconds((int)Granularity);
                    this.Buffer = DbCandles.GetTopNDbCandles(productType, Granularity, CurrentStartDate, bufferSize, false);
                    CurrentEndDate = Buffer.Count > 0 ? Buffer.Max(x => x.Time) : EndDate;
                }

                this.bufferIndex = 0;
                return Buffer.Count > 0;
            }
            else
            {
                return false;

            }
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
}
