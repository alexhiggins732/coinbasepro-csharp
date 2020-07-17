using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using CoinbaseUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UtilsWinFormApp
{
    class TradeSimulator
    {
    }

    public class IndexedTrade
    {
        public IndexedSearch BuySearch;
        public IndexedSearch SellSearch;

        public IndexedTrade(IndexedSearch buySearch, IndexedSearch sellSearch)
        {
            BuySearch = buySearch;
            SellSearch = sellSearch;
        }
        public override string ToString()
        {
            var atl = BuySearch;
            var ath = SellSearch;
            var result = $@"Buy {atl.CurrentCandle.Time} {atl.CurrentCandle.Close.Value.ToString("C")}";
            if (ath != null)
                result += $" - Sell {ath.CurrentCandle.Time} {ath.CurrentCandle.Close.Value.ToString("C")}";
            return result;
        }
    }
    public class IndexedSearch
    {
        private List<Candle> Candles;

        public int Index { get; private set; } = 0;
        public Candle CurrentCandle { get; private set; }
        public IndexedSearch(List<Candle> candles) : this(candles, -1)
        {

        }
        public IndexedSearch(List<Candle> candles, int index)
        {
            this.Candles = candles;
            this.Index = index;
            if (index > -1 && index < candles.Count)
                CurrentCandle = candles[index];
        }
        public IndexedSearch Clone()
        {
            var result = new IndexedSearch(Candles, Index);
            return result;
        }
        public IndexedSearch NextATL(int startIndex = -1, int limit = -1)
        {
            var idx = -1;
            Candle result = null;
            if (limit == -1) limit = Candles.Count;
            if (startIndex == -1) startIndex = this.Index + 1;
            for (var i = startIndex; i < limit; i++)
            {
                var candle = Candles[i];
                if (result != null)
                {
                    if (candle.Close < result.Close)
                    {
                        result = candle;
                        idx = i;
                    }

                }
                else if (CurrentCandle == null)
                {
                    result = candle;
                    idx = i;
                }
                else if (candle.Close < CurrentCandle.Close) // result= null && currentCandle!=null
                {
                    idx = i;
                    result = candle;
                }
            }
            return new IndexedSearch(Candles, idx);
        }
        public IndexedSearch NextATH(int startIndex = -1, int limit = -1)
        {
            var idx = -1;
            Candle result = null;
            if (limit == -1) limit = Candles.Count;
            if (startIndex == -1) startIndex = this.Index + 1;
            for (var i = startIndex; i < limit; i++)
            {
                var candle = Candles[i];
                if (result != null)
                {
                    if (candle.Close > result.Close)
                    {
                        result = candle;
                        idx = i;
                    }

                }
                else if (CurrentCandle == null)
                {
                    result = candle;
                    idx = i;
                }
                else if (candle.Close > CurrentCandle.Close) // result= null && currentCandle!=null
                {
                    idx = i;
                    result = candle;
                }
            }
            return new IndexedSearch(Candles, idx);
        }

        public IndexedTrade NextTrade()
        {
            var buySearch = NextATL();
            var sellSearch = NextATH();
            return new IndexedTrade(buySearch, sellSearch);
        }
        public List<IndexedTrade> GetIndexedTrades()
        {
            var result = new List<IndexedTrade>();
            DateTime matchTime = DateTime.Parse("5/11/2020");
            for (var i = 0; i < Candles.Count - 1; i++)
            {
                var current = Candles[i];
                if (current.Time == matchTime)
                {
                    string bp = "";
                }
                var next = Candles[i + 1];
                if (next.Close > current.Close)
                {
                    var buySearch = new IndexedSearch(Candles, i);
                    var trade = new IndexedTrade(buySearch, null);

                    for (var k = i + 1; trade.SellSearch == null && k < Candles.Count - 1; k++)
                    {
                        current = Candles[k];
                        next = Candles[k + 1];
                        if (next.Close < current.Close)
                        {
                            trade.SellSearch = new IndexedSearch(Candles, k);
                        }
                        i = k;
                    }
                    result.Add(trade);
                }

            }
            return result;
        }
        public List<IndexedTrade> GetIndexedTrades1()
        {
            var result = new List<IndexedTrade>();
            var atl = NextATL();
            var ath = NextATH();
            while (atl.CurrentCandle != null)
            {
                var buyPrice = atl.CurrentCandle.Close;
                var nextAtl = atl.NextATL();
                if (nextAtl.CurrentCandle != null)
                {
                    throw new Exception("Conflicting All Time Low");
                }
                if (ath.CurrentCandle != null)
                {
                    var sellPrice = ath.CurrentCandle.Close;
                    result.Add(new IndexedTrade(atl, ath));
                    atl = ath.NextATL();
                    if (atl.CurrentCandle != null)
                    {
                        ath = atl.NextATH();
                    }
                    else
                    {
                        Console.WriteLine($"ATH not found after {ath.CurrentCandle.Time} {ath.CurrentCandle.Close.Value.ToString("C")}");
                        break;
                    }
                }
            }

            int resultCount = 0;
            var result2 = new List<IndexedTrade>();
            while (resultCount != result.Count)
            {
                resultCount = result.Count;
                foreach (var trade in result)
                {
                    var buySearch = trade.BuySearch;
                    // either find an high between atl and ath with a low between high and ath
                    // or find a low between atl and ath, with a high between ath and low.


                    // start at sell - 1 move to buy+1
                    // find a high less than ath that has a low between high and ath.
                    IndexedSearch buyTrade = null;
                    IndexedSearch sellTrade = null;
                    for (var sellindex = trade.SellSearch.Index - 2; buyTrade == null && sellindex > buySearch.Index + 1; sellindex--)
                    {
                        var testSellHigh = Candles[sellindex];
                        for (var buyindex = sellindex + 1; buyTrade == null && buyindex < trade.SellSearch.Index - 1; buyindex++)
                        {
                            var testBuyLow = Candles[buyindex];
                            if (testBuyLow.Close < testSellHigh.Close)
                            {
                                buyTrade = new IndexedSearch(Candles, buyindex);
                                sellTrade = new IndexedSearch(Candles, sellindex);
                            }
                        }
                    }
                    if (buyTrade == null)
                    {
                        result2.Add(trade);
                    }
                    else
                    {
                        var trade1 = new IndexedTrade(trade.BuySearch, sellTrade);
                        var trade2 = new IndexedTrade(buyTrade, trade.SellSearch);
                        result2.Add(trade1);
                        result2.Add(trade2);
                    }
                }
                result = result2.OrderBy(x => x.BuySearch.CurrentCandle.Time).ToList();
                result2.Clear();
            }
            return result;

        }
    }
    public class MaxProfitAnalyzer
    {
        public static void Run()
        {
            ProductType productType = ProductType.LtcUsd;
            CandleGranularity candleGranularity = CandleGranularity.Hour24;

            var candleStream = new CandleDbReader(productType, candleGranularity);

            var candles = candleStream.ToList();
            SaveCandles(candles, productType, candleGranularity);
            var search = new IndexedSearch(candles);
            var trades = search.GetIndexedTrades();

            var atl = search.NextATL();
            var ath = search.NextATH();
            while (atl.CurrentCandle != null)
            {
                var buyPrice = atl.CurrentCandle.Close;
                Console.WriteLine($"Buy {atl.CurrentCandle.Time} {atl.CurrentCandle.Close.Value.ToString("C")}");
                atl = atl.NextATL();
                if (atl.CurrentCandle != null)
                {
                    string bp = "Error";
                }
                if (ath.CurrentCandle != null)
                {
                    var sellPrice = ath.CurrentCandle.Close;
                    Console.WriteLine($"Sell {ath.CurrentCandle.Time} {ath.CurrentCandle.Close.Value.ToString("C")}");
                    atl = ath.NextATL();
                    if (atl.CurrentCandle != null)
                    {
                        ath = atl.NextATH();
                    }
                    else
                    {
                        string bp = "Error";
                        Console.WriteLine($"ATH not found after {ath.CurrentCandle.Time} {ath.CurrentCandle.Close.Value.ToString("C")}");
                    }
                }
            }


        }

        private static void SaveCandles(List<Candle> candles, ProductType productType, CandleGranularity candleGranularity)
        {
            var root = @"C:\Users\alexander.higgins\source\repos\alexhiggins732\coinbasepro-csharp\UtilsWebApp\js\";
            var prodPart = productType.ToString();
            var granPart = candleGranularity.ToString();
            var fileName = $"{prodPart}-{granPart}.js";
            var destFile = Path.Combine(root, fileName);
            var dtoes = candles.Select(x =>
              new { x.Close, x.High, x.Low, x.Open, x.Time, x.Volume }).OrderBy(x=> x.Time).ToList();
            var json = JsonConvert.SerializeObject(dtoes);
            File.WriteAllText(destFile, json);
        }
    }
}
