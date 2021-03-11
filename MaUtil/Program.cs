﻿using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using CoinbaseUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Statistics;

namespace MaUtil
{
    class GoldTrade
    {
        public DateTime BuyDate;
        public decimal BuyPrice;
        public decimal BuyQty;
        public decimal BuyValue;

        public DateTime SellDate;
        public decimal SellPrice;
        public decimal SellQty;
        public decimal SellValue;
        internal decimal Commission;

        public override string ToString()
        {
            var buyString = $"Buy {BuyValue.ToString("C2")} ({BuyPrice.ToString("C2")} x {BuyQty.ToString("N4")}) ";
            if (SellDate == DateTime.MinValue) return buyString;

            var selltring = $" - Sell @ {SellPrice.ToString("C2")} ({SellValue.ToString("C2")})";

            return $"Profit {(SellValue - BuyValue).ToString("C2")} - {buyString} {selltring}";

        }
    }
    class GoldAgent
    {
        public string Name;
        public bool IsGold = false;
        bool holding = false;

        public List<GoldTrade> Trades = new List<GoldTrade>();
        public decimal CoinQty = 0;
        public decimal UsdQty = 1000;
        public decimal PortfolioValue => CoinQty != 0 ? CoinQty * latestClose : UsdQty;

        public decimal PortfolioNetProfit => PortfolioValue / 1000m - 1;

        public int AgentIndex { get; internal set; }
        public int K { get; internal set; }
        public int I { get; internal set; }

        public decimal Commission;

        private decimal latestClose;
        public int ShortPeriod;
        public int LongPeriod;
        //GoldTrade lastTrade = new GoldTrade();
        //int TradeCount;
        public void SetGold(DateTime dt, decimal close, bool isGold)
        {
            //lock (lastTrade)
            //{
            latestClose = close;
            if (isGold != IsGold)
            {
                //
                if (!holding)
                {
                    if (isGold)
                    {
                        holding = true;
                        var commisionAmount = (UsdQty * Commission);
                        var qty = Commission == 0 ? UsdQty : UsdQty - commisionAmount;
                        var coinQty = qty / close;
                        var trade = new GoldTrade { BuyDate = dt, BuyPrice = close, BuyQty = coinQty, BuyValue = qty, Commission = commisionAmount };
                        Trades.Add(trade);
                        //lastTrade = new GoldTrade { BuyDate = dt, BuyPrice = close, BuyQty = coinQty, BuyValue = qty, Commission = commisionAmount };
                        //lastTrade.BuyDate = dt;
                        //lastTrade.BuyPrice = close;
                        //lastTrade.BuyQty = coinQty;
                        //lastTrade.BuyValue = qty;
                        //lastTrade.Commission = commisionAmount;
                        //TradeCount += 1;
                        this.CoinQty = coinQty;
                        this.UsdQty = 0;

                    }
                }
                else
                {
                    holding = false;
                    //var trade = lastTrade;
                    var trade = Trades.Last();
                    trade.SellDate = dt;
                    trade.SellPrice = close;
                    trade.SellQty = trade.BuyQty;
                    UsdQty = trade.SellValue = close * trade.SellQty;
                    trade.Commission = UsdQty * Commission;
                    trade.SellValue -= trade.Commission;
                    UsdQty -= trade.Commission;
                    CoinQty = 0;
                }
            }
            IsGold = isGold;
            //}
        }
        public override string ToString()
        {
            //return $"{Name} - {(IsGold ? "(Gold)" : "(Dark)")} {PortfolioValue.ToString("C2")} ({PortfolioNetProfit.ToString("P2")}) - {TradeCount.ToString("N0")} Trades ";
            return $"{Name} - {(IsGold ? "(Gold)" : "(Dark)")} {PortfolioValue.ToString("C2")} ({PortfolioNetProfit.ToString("P2")}) - {Trades.Count.ToString("N0")} Trades ";

        }
        public static GoldAgent[] CreateAgents(int maxMa, decimal commission = 0)
        {
            var result = new List<GoldAgent>();
            for (var i = 0; i < maxMa; i++)
            {
                for (var k = 0; k < maxMa; k++)
                {
                    int low = i + 1;
                    int high = k + 1;
                    result.Add(new GoldAgent() { ShortPeriod = low, LongPeriod = high, Name = $"MA {i + 1}x{k + 1}", Commission = commission });
                }

            }
            return result.ToArray();
        }


    }
    class Program
    {

        static ProductType SelectedProductType = ProductType.LtcUsd;
        static void Main(string[] args)
        {
            var startDate = DateTime.Parse("1/1/2020");

            ProductType productType = ParseProductType();

            SelectedProductType = productType;
            //Todo: Allow console based selection;
            TestMaStrategy(CandleGranularity.Hour1, startDate, 625, SelectedProductType);
        }

        private static ProductType ParseProductType()
        {
            bool parsed = false;
            ProductType productType = ProductType.Unknown;
            while (!parsed)
            {
                Console.WriteLine("Enter a valid product type");
                string val = Console.ReadLine();
                parsed = val.ParseEnum<ProductType>(out productType);
            }
            return productType;
        }

        static void TestMa15()
        {
            var startDate = DateTime.Parse("3/1/2020");
            TestMaStrategy(CandleGranularity.Minutes15, startDate);
        }
        static void TestMa60()
        {
            var startDate = DateTime.Parse("1/1/2019");
            TestMaStrategy(CandleGranularity.Hour1, startDate, 625);
        }
        static void TestMa360()
        {
            TestMaStrategy(CandleGranularity.Hour6);
        }
        static void TestMaStrategy(CandleGranularity granularity, DateTime? startDate = null, int maxMa = 256, ProductType productType = ProductType.LtcUsd)
        {
            CandleService.UpdateCandles(productType, true);
            DateTime readerStart = DateTime.MinValue;
            if (startDate == null)
            {
                readerStart = CandleService.GetMinDbCandleDate(productType, granularity);
            }
            else
            {
                readerStart = CandleService.GetMinDbCandleDateForSampleSizeAndStartDate(productType, granularity, startDate.Value, maxMa);
            }

            var reader = new MaReader(maxMa, productType, granularity, readerStart);

            bool stepped = reader.Step();

            var agents = GoldAgent.CreateAgents(maxMa, 0.0025m);
            int y = 0;
            for (var i = 0; i < reader.MaxMa; i++)
            {
                //Console.Title = $"Calculating {i} of {m.Length}";
                for (var k = 0; k < reader.MaxMa; k++)
                {
                    y = i * reader.MaxMa + k;
                    agents[y].AgentIndex = y;
                    agents[y].K = k;
                    agents[y].I = i;


                }
            }

            bool isGold;
            int x = 0;
            y = 0;
            var swStep = new System.Diagnostics.Stopwatch();
            var swMatrix = new System.Diagnostics.Stopwatch();
            var swGold = new System.Diagnostics.Stopwatch();
            ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = 16 };
            DateTime CurrentTime= DateTime.MinValue;
            while (stepped)
            {

                CurrentTime = reader.Current.Time;
                if (reader.Current.Time.Day == 1 && reader.Current.Time.Hour == 0 && reader.Current.Time.Minute == 0)
                {
                    Console.WriteLine($"[{DateTime.Now}] - {reader.Current.Time.Date}");
                    Console.WriteLine($" => Step:  {swStep.Elapsed}  => Matrix: {swMatrix.Elapsed}  Gold=> {swGold.Elapsed}");
                    swStep = new System.Diagnostics.Stopwatch();
                    swMatrix = new System.Diagnostics.Stopwatch();
                    swGold = new System.Diagnostics.Stopwatch();
                }
                swStep.Start();
                if (startDate != null)
                {
                    if (reader.Current.Time < startDate.Value)
                    {
                        x++;
                        stepped = reader.Step();
                        continue;
                    }
                }
                swStep.Stop();

                swGold.Start();
                var m = reader.Matrix;
                swGold.Stop();

                swMatrix.Start();
                //Parallel.For(0, m.Length, parallelOptions, (i) =>
                //{
                //    for (var i = 0; i < m.Length; i++)
                //    {
                //        Console.Title = $"Calculating {i} of {m.Length}";
                //        var row = m[i];
                //        var offset = i * reader.MaxMa;
                //        for (var k = 0; k < reader.MaxMa; k++)
                //        {
                //            y = offset + k;
                //            isGold = row[k];
                //            agents[y].SetGold(reader.Current.Time, reader.Current.Close.Value, isGold);
                //        }
                //    }
                //});

                for (var i = 0; i < m.Length; i++)
                {
                    //Console.Title = $"Calculating {i} of {m.Length}";
                    for (var k = 0; k < reader.MaxMa; k++)
                    {
                        y = i * reader.MaxMa + k;
                        isGold = m[i][k];
                        agents[y].SetGold(reader.Current.Time, reader.Current.Close.Value, isGold);
                    }
                }
                swMatrix.Stop();
                x++;
                swMatrix.Start();
                stepped = reader.Step();
                swStep.Stop();
            }

            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{productType}-{granularity}-MaAgent";
            var sortedAgents = agents.OrderByDescending(a => a.PortfolioValue).ToArray();
            CreateTrainingData(sortedAgents, maxMa, productType, granularity, readerStart, startDate.Value, CurrentTime, fileName, ts);
            var sortedSummaryLines = sortedAgents.Select(a => a.ToString()).ToArray();
            var sortedSummary = string.Join("\r\n", sortedSummaryLines);



            if (startDate != null)
            {
                fileName = $"{productType}-{granularity}-{maxMa}-{startDate.Value.ToString("MMddyyyy")}-MaAgent";
            }
            System.IO.File.WriteAllText(fileName + ts + ".log", sortedSummary);
            sortedSummaryLines = sortedAgents.Select(a => $"{a.ShortPeriod}\t{a.LongPeriod}\t{(a.IsGold ? 1 : 0)}\t{a.PortfolioNetProfit}").ToArray();
            sortedSummary = string.Join("\r\n", sortedSummaryLines);

            System.IO.File.WriteAllText(fileName + ts + ".tsv", sortedSummary);

            var surfaces = new decimal[maxMa][];
            var goldSurfaces = new decimal[maxMa][];
            for (var i = 0; i < maxMa; i++)
            {
                surfaces[i] = new decimal[maxMa];
                goldSurfaces[i] = new decimal[maxMa];
            }
            foreach (var agent in agents)
            {
                var name = agent.Name;
                var coords = name.Split(' ')[1];
                var xy = coords.Split('x');
                var agentX = int.Parse(xy[0]);
                var agentY = int.Parse(xy[1]);
                var surface = agent.PortfolioNetProfit;
                surfaces[agentX - 1][agentY - 1] = surface;
                goldSurfaces[agentX - 1][agentY - 1] = agent.IsGold ? 1 : 0;
            }
            var json = JsonConvert.SerializeObject(surfaces);
            var plotJson = fileName + ts + ".json";
            File.WriteAllText(plotJson, $"var z_data={json};");
            json = JsonConvert.SerializeObject(goldSurfaces);
            var plotGoldJson = fileName + ts + "-gold.json";
            File.WriteAllText(plotGoldJson, $"var z_data={json};");

            SetSrc(plotJson, "plot.html");
            SetSrc(plotGoldJson, "plot-gold.html");

        }

        private static void CreateTrainingData(
            GoldAgent[] sortedAgents,
            int maxMa,
            ProductType productType,
            CandleGranularity granularity,
            DateTime readStartDate,
            DateTime maStartDate,
            DateTime endDate,
            string fileName,
            string timeStamp)
        {
            var f = sortedAgents.First();
            var reader = new MaReader(maxMa, productType, granularity, readStartDate);
            var stepped = reader.Step();
            while (reader.Current.Time < maStartDate)
            {
                stepped = reader.Step();
            }

            bool[][] m;
            bool isGold;
            var i = f.I;
            var k = f.K;
            Console.WriteLine($"[{DateTime.Now}] - Creating Training data for {f}");
            var dataFolder = Directory.CreateDirectory( "train-" + fileName + timeStamp );
            var tsvFile = Path.Combine(dataFolder.FullName, "training-data.txt");
            int x = 0;
            using (var sw = new StreamWriter(tsvFile))
            {
                sw.WriteLine($"i\time\tside\timage");
                while (stepped)
                {
                    if (reader.Current.Time.Day == 1 && reader.Current.Time.Hour == 0 && reader.Current.Time.Minute == 0)
                    {
                        Console.WriteLine($"[{DateTime.Now}] - {reader.Current.Time.Date}");
                    }
                    m = reader.Matrix;
                    isGold = m[i][k];
              
                    var bmp = reader.Bitmap;
                    var candleStamp = reader.Current.Time.ToString("yyyyMMdd_HHmm");
                    var imageName = fileName + timeStamp + $"-{candleStamp}.bmp";
                    var imagePath = Path.Combine(dataFolder.FullName, imageName);
                    bmp.Save(imagePath, System.Drawing.Imaging.ImageFormat.Bmp);
                    var lineData = $"{++x}\t{reader.Current.Time}\t{(isGold ? 1.0 : 0.0)}\t{imagePath}";
                    sw.WriteLine(lineData);
                    stepped = reader.Step();

                }

            }

        }

        private static void SetSrc(string plotFile, string dest)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.Load(dest);
            var el = doc.GetElementbyId("plotsrc");
            el.SetAttributeValue("src", plotFile);
            doc.Save(dest);
        }

        static void MaWithBitmap()
        {
            var reader = new MaReader(256, ProductType.LtcUsd, CandleGranularity.Hour24);
            int count = CandleService.GetCandleCountBeforeDate(ProductType.LtcUsd, CandleGranularity.Hour24, DateTime.Today.AddDays(1));
            var bmp = new Bitmap(count - reader.MaxMa, 256 * 256);
            var dates = new DateTime[count - reader.MaxMa];
            var closes = new Decimal[count - reader.MaxMa];
            //var closeDict = Enumerable.Range(0, dates.Length).ToDictionary(i => dates[i], i => closes[i]);

            using (var g = Graphics.FromImage(bmp))
                g.FillRectangle(Brushes.Black, 0, 0, bmp.Width, bmp.Height);
            //for (var x = 0; x < bmp.Width; x++)
            //{
            //    for (var y = 0; y < bmp.Height; y++)
            //    {
            //        bmp.SetPixel(x, y, Color.White);
            //    }
            //}
            bool stepped = reader.Step();
            int x = 0;
            int y = 0;
            bool isGold;
            var agents = GoldAgent.CreateAgents(256);
            while (stepped)
            {


                if (reader.Current.Time.Day == 1)
                    Console.WriteLine($"[{DateTime.Now}] - {reader.Current.Time.Date}");
                dates[x] = reader.Current.Time;
                closes[x] = reader.Current.Close.Value;
                var m = reader.Matrix;
                for (var i = 0; i < m.Length; i++)
                {
                    for (var k = 0; k < reader.MaxMa; k++)
                    {
                        y = i * reader.MaxMa + k;
                        isGold = m[i][k];
                        bmp.SetPixel(x, y, isGold ? Color.Gold : Color.Black);
                        agents[y].SetGold(reader.Current.Time, reader.Current.Close.Value, isGold);
                    }
                }
                x++;
                stepped = reader.Step();
            }

            var sortedAgents = agents.OrderByDescending(a => a.PortfolioValue).ToArray();
            var sortedSummaryLines = sortedAgents.Select(a => a.ToString()).ToArray();
            var sortedSummary = string.Join("\r\n", sortedSummaryLines);
            var bmp2 = (Bitmap)bmp.Clone();
            using (var g = Graphics.FromImage(bmp2))
            {
                for (var i = 0; i < reader.MaxMa; i++)
                {
                    x = i * reader.MaxMa + 20;
                    var f = new Font("Calibri", 10);
                    g.DrawString($"MA {i + 1}", f, Brushes.White, 10, x);
                }
            }
            bmp2.Save("ma-out2.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
            bmp.Save("ma-out.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
            while (reader.Step())
            {

            }
        }
    }

    public class MaReader
    {
        public DateTime Time;
        public MaReader(int maxMa, ProductType productType, CandleGranularity candleGranularity, DateTime? startDate = null)
        {
            this.MaxMa = maxMa;
            mtf = new MultiTimeFrameSma<decimal>(maxMa);
            crossOver = new MultiTimeFrameCrossOver<decimal>(mtf);
            candleStream = new CandleDbReader(productType, candleGranularity, startDate);
            this.candleEnumerator = candleStream.GetEnumerator();

            for (var i = 0; i < maxMa; i++)
            {
                if (MoveNext())
                {
                    crossOver.AddSample(Current.Close.Value);
                }
            }
        }

        public bool Step()
        {
            bool result = MoveNext();
            if (result)
            {
                crossOver.AddSample(Current.Close.Value);
            }
            else
            {
                this.Completed = true;
            }
            return result;
        }
        public Bitmap Bitmap => Matrix.ToBitmap();

        public int MaxMa;
        MultiTimeFrameSma<decimal> mtf;
        MultiTimeFrameCrossOver<decimal> crossOver;
        CandleDbReader candleStream;
        private IEnumerator<Candle> candleEnumerator;

        public Candle Current => candleEnumerator.Current;
        public decimal[] History => crossOver.History;
        public MultiTimeFrameSma<decimal> MtfSma => crossOver.SimpleMovingAverages;
        public Dictionary<int, SmaBase<decimal>> SimpleMovingAverages => MtfSma.SimpleMovingAverages;
        public decimal[] Averages => crossOver.SimpleMovingAverages.Averages;
        bool MoveNext() => candleEnumerator.MoveNext();
        public bool[][] Matrix => crossOver.GetMatrix();
        public bool Running { get; private set; }
        public bool Completed { get; private set; }
    }

    public static class MatrixExtensions
    {
        public static Bitmap ToBitmap(this bool[][] source)
        {
            var w = source.Length;
            var h = source[0].Length;
            var result = new Bitmap(w, h);
            for (var x = 0; x < h; x++)
            {
                for (var y = 0; y < w; y++)
                {
                    result.SetPixel(w - x - 1, y, source[x][y] ? Color.Gold : Color.Black);
                }
            }
            return result;
        }

        public static List<T> ToList<T>(this Array array)
        {
            var result = new List<T>();
            foreach (T item in array) { result.Add(item); }
            return result;
        }
    }
}