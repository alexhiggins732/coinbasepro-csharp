using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using CoinbaseUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Statistics;

namespace Utils
{
    public class HighPrecisionMTFMaTests
    {
        public static void Run()
        {
            
            TestMultipleStartDates();
            TestMultipleReaderStartDates();
        }

        public static void TestMultipleReaderStartDates()
        {
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);

            int dayMinutes = 1440;
            int maxMa = dayMinutes * 255;
            var yesterdaySampleStartDate = yesterday.AddMinutes(-maxMa);
            var todaySampleStartDate = today.AddMinutes(-maxMa);
            var yesterdayStream = new CandleDbReader(ProductType.LtcUsd, CandleGranularity.Minutes1, yesterdaySampleStartDate);
            var todayStream = new CandleDbReader(ProductType.LtcUsd, CandleGranularity.Minutes1, todaySampleStartDate);

            var yesterdayEnumerator = yesterdayStream.GetEnumerator();
            var todayEnumerator = todayStream.GetEnumerator();

            todayEnumerator.MoveNext();
            yesterdayEnumerator.MoveNext();

            var previous = todayEnumerator.Current.Time.AddMinutes(-1);
            while (yesterdayEnumerator.Current.Time != previous)
            {
                yesterdayEnumerator.MoveNext();
            }
            bool moved = yesterdayEnumerator.MoveNext();
            while (moved)
            {
                if (yesterdayEnumerator.Current.Time != todayEnumerator.Current.Time)
                {
                    string bp = $"Time Mismatch: {yesterdayEnumerator.Current.Time } - Today: {todayEnumerator.Current.Time}";
                    throw new Exception(bp);
                }
                if (yesterdayEnumerator.Current.Close != todayEnumerator.Current.Close)
                {
                    string bp = $"Close Mismatch: {yesterdayEnumerator.Current.Time } {yesterdayEnumerator.Current.Close} - Today: {todayEnumerator.Current.Time} {todayEnumerator.Current.Close}";
                    throw new Exception(bp);
                }
                moved = todayEnumerator.MoveNext() && yesterdayEnumerator.MoveNext();
            }

        }
        public static void TestMultipleStartDates()
        {
            var productType = ProductType.LtcUsd;
            CandleService.UpdateCandles(productType, true);
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);
            var yesterdayStream = new HighPrecisionMTFMaStream(productType, yesterday);
            while (yesterdayStream.MoveNext() && yesterdayStream.Current.Time != today)
            {
                //do nothing
            }
            var todayStream = new HighPrecisionMTFMaStream(productType, today);
            todayStream.MoveNext();

            var yesterdayMatrix = yesterdayStream.GoldMatrix();
            var todayMatrix = todayStream.GoldMatrix();
            var sampleStartDate = today.AddMinutes(-todayStream.MTFMa.History.Length);
            if (!yesterdayStream.MTFMa.History.SequenceEqual(todayStream.MTFMa.History))
            {
                var yesterdayHistory = yesterdayStream.MTFMa.History;

                var todayHistory = todayStream.MTFMa.History;

                for (var i = 0; i < yesterdayHistory.Length; i++)
                {
                    bool matches = yesterdayHistory[i] == todayHistory[i];
                    if (!matches)
                    {
                        var misMatchDate = sampleStartDate.AddMinutes(i);
                        var yesValue = yesterdayHistory[i];
                        var toValue = todayHistory[i];
                        string bp = $"[{misMatchDate}] Mismatch at row {i}";
                        Console.WriteLine($"Error: {i} {bp} - Yesterday {yesValue} - Today: {toValue}");
                    }
                }
            }

            for (var i = 0; i < yesterdayMatrix.Length; i++)
            {
                bool matches = yesterdayMatrix[i].SequenceEqual(todayMatrix[i]);
                if (!matches)
                {
                    string bp = $"Mismatch at row {i}";
                    Console.WriteLine("Error: bp");
                }
            }
        }
        public static void EnumerationTest()
        {
            var grans = Enum.GetValues(typeof(CandleGranularity)).Cast<CandleGranularity>().Select(x => ((int)x) / 60).ToList();
            var range = Enumerable.Range(1, 256).Where(x => x == 1 || x % 5 == 0).ToList();
            var mas = range.SelectMany(i => grans.Select(gran => gran * i)).Distinct().OrderBy(x => x).ToList();
            var maxMa = mas.Max();
            var candleStream = new CandleDbReader(ProductType.LtcUsd, CandleGranularity.Minutes1);
            var samples = new List<decimal>(maxMa);
            samples.AddRange(candleStream.Take(maxMa).Select(x => (decimal)x.Close));
            var mtf = new HighPrecisionMTFMa(mas, samples);
            var candles = candleStream.Skip(maxMa).GetEnumerator();
            int sampled = 0;
            while (candles.MoveNext())
            {
                var current = candles.Current;
                mtf.AddSample(current.Close);
                if ((++sampled & 2047) == 0)
                {
                    Console.WriteLine($"[{DateTime.Now}] - {current.Time} Sampled {sampled}");
                }
            }
        }
    }

    public class HighPrecisionMTFMaStream
    {
        public HighPrecisionMTFMaStream(ProductType productType, DateTime? streamStartDate = null)
        {
            var minDbDate = CandleService.GetMinDbCandleDate(productType, CandleGranularity.Minutes1);
            DateTime startDate = minDbDate;

            if (streamStartDate != null)
                startDate = streamStartDate.Value; // CandleService.GetMinDbCandleDateAfterDate(ProductType.LtcUsd, CandleGranularity.Minutes1, streamStartDate.Value);

            var grans = Enum.GetValues(typeof(CandleGranularity)).Cast<CandleGranularity>().Select(x => ((int)x) / 60).ToList();
            var range = Enumerable.Range(1, 256).Where(x => x == 1 || x % 5 == 0).ToList();
            var mas = range.SelectMany(i => grans.Select(gran => gran * i)).Distinct().OrderBy(x => x).ToList();
            var maxMa = mas.Max();

 
            var initialSampleStartDate = startDate.AddMinutes(-(maxMa));

            var candleStream = new CandleDbReader(productType, CandleGranularity.Minutes1, initialSampleStartDate);
            candles = candleStream.GetEnumerator();
            candles.MoveNext();
            var firstSample = (CoinbaseData.DbCandle)candles.Current;//9/15/2019 12:00:00 AM
            var samples = new List<decimal>(maxMa);
            samples.Add(firstSample.Close.Value);
            Candle lastSample = null;
            while (samples.Count < maxMa)
            {
                bool moved = candles.MoveNext();
                if (!moved) throw new ArgumentOutOfRangeException($"End of candle stream at {lastSample.Time}");
                lastSample = candles.Current;
                samples.Add(lastSample.Close.Value);
            }

            MTFMa = new HighPrecisionMTFMa(mas, samples);

            goldMatrix = new bool[MTFMa.MovingAverages.Length][];
            for (var i = 0; i < MTFMa.MovingAverages.Length; i++)
            {
                goldMatrix[i] = new bool[MTFMa.MovingAverages.Length];
            }

        }
        private bool[][] goldMatrix;
        public HighPrecisionMTFMa MTFMa;
        private IEnumerator<Candle> candles;
        public IEnumerator<Candle> CandleEnumerator => candles;
        public bool[][] GoldMatrix()
        {
            for (var i = 0; i < MTFMa.MovingAverages.Length; i++)
            {
                var sourceAvg = MTFMa.MovingAverages[i];
                bool[] source = goldMatrix[i];
                for (var k = 0; k < MTFMa.MovingAverages.Length; k++)
                    source[k] = MTFMa.MovingAverages[k] < sourceAvg;
            }
            return goldMatrix;
        }

        public Candle Current => candles.Current;
        public bool MoveNext()
        {
            bool result = candles.MoveNext();
            if (result)
            {

                MTFMa.AddSample(candles.Current.Close.Value);
            }
            return result;
        }

    }
    public class HighPrecisionMTFMa
    {
        public decimal[] History;
        public int[] MASizes;
        public decimal[] Pcts;
        public decimal[] MovingAverages;


        public HighPrecisionMTFMa(IEnumerable<int> sampleSizes, IEnumerable<decimal> samples)
        {
            var maxSampleSize = sampleSizes.Max();
            MASizes = sampleSizes.ToArray();

            Pcts = MASizes.Select(x => 1m / (decimal)x).ToArray();

            History = samples.ToArray();
            if (History.Length != maxSampleSize)
            {
                throw new ArgumentException("Samples must be of length max(smapleSizes)");
            }
            MovingAverages = MASizes.Select(maSize => samples.Skip(History.Length - maSize).Take(maSize).Average()).ToArray();

        }

        internal void AddSample(decimal? close)
        {
            decimal value = close.Value;
            for (var i = 0; i < Pcts.Length; i++)
            {
                var maSize = MASizes[i];
                MovingAverages[i] -= (History[History.Length - maSize] * Pcts[i]);
                MovingAverages[i] += (Pcts[i] * value);
            }
            Array.Copy(History, 1, History, 0, History.Length - 1);
            History[History.Length - 1] = close.Value;
        }


    }


    public class TwoDimensionalSma
    {
        public SmaOfDecimal[][] histories;
        public int width = 0;
        public int height = 0;

        public TwoDimensionalSma(int width, int height, int historySize)
        {
            this.width = width;
            this.height = height;

            histories = new SmaOfDecimal[height][];
            buffer = new decimal[height][];
            for (var y = 0; y < height; y++)
            {
                histories[y] = new SmaOfDecimal[width];
                buffer[y] = new decimal[width];
                for (var x = 0; x < width; x++)
                {
                    histories[y][x] = new SmaOfDecimal(historySize);
                }
            }

        }
        public void AddSample(decimal[][] matrix)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    histories[y][x].AddSample(matrix[y][x]);
                }
            }
        }
        private decimal[][] buffer;
        public decimal[][] Current()
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    buffer[y][x] = histories[y][x].Average;
                }
            }
            return buffer;
        }
    }
}
