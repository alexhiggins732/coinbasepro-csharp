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
        public HighPrecisionMTFMaStream(DateTime? startDate = null)
        {
            if (startDate == null)
                startDate = CandleService.GetMinDbCandleDate(ProductType.LtcUsd, CandleGranularity.Minutes1);
            else
                startDate = CandleService.GetMinDbCandleDateAfterDate(ProductType.LtcUsd, CandleGranularity.Minutes1, startDate.Value);

            var grans = Enum.GetValues(typeof(CandleGranularity)).Cast<CandleGranularity>().Select(x => ((int)x) / 60).ToList();
            var range = Enumerable.Range(1, 256).Where(x => x == 1 || x % 5 == 0).ToList();
            var mas = range.SelectMany(i => grans.Select(gran => gran * i)).Distinct().OrderBy(x => x).ToList();
            var maxMa = mas.Max();

            var sampleStartDate = CandleService.GetMinDbCandleDateForSampleSizeAndStartDate
                (ProductType.LtcUsd, CandleGranularity.Minutes1, startDate.Value, maxMa);

            var candleStream = new CandleDbReader(ProductType.LtcUsd, CandleGranularity.Minutes1, sampleStartDate);
            var samples = new List<decimal>(maxMa);

            //var sampleStream = candleStream.Where(x => x.Time < startDate).OrderByDescending(x => x.Time).Take(maxMa).OrderBy(x => x.Time);
            //var sampleCandles = sampleStream.ToArray();
            samples.AddRange(candleStream.Take(maxMa).Select(x => (decimal)x.Close));
            MTFMa = new HighPrecisionMTFMa(mas, samples);

            //var skipCount = CandleService.GetCandleCountBeforeDate(ProductType.LtcUsd, CandleGranularity.Minutes1, startDate.Value);
            candleStream = new CandleDbReader(ProductType.LtcUsd, CandleGranularity.Minutes1, startDate);
            candles = candleStream.GetEnumerator();
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
