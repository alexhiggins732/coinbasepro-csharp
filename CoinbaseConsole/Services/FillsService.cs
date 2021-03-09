using CoinbasePro.Services.Fills.Models.Responses;
using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Services.Products.Models;
using CoinbasePro.Shared.Types;
using CoinbaseUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CoinbaseConsole.ConsoleDriver;

namespace CoinbaseConsole
{
    public class FillsManager
    {
        public static ConcurrentDictionary<ProductType, FillsCache> FillsCache;

        public static FillsCache Cache(ProductType productType)
        {
            var result = FillsCache.GetOrAdd(productType, (id) => new FillsCache(productType));
            if (result.Stale)
                result.UpdateFills();
            return result;
        }


        private static int lastPages = 1;
        public static LastOrder GetLast(ProductType ProductType, LastSide wantedSide = LastSide.Any)
        {
            OrderManager.Refresh();
            var svc = new CoinbaseService();
            if (lastPages == 0) lastPages = 1;
            var allFills = svc.client.FillsService.GetFillsByProductIdAsync(ProductType, 100, lastPages).Result.SelectMany(x => x).ToList();

            //var firstSide = allFills.First().Side;

            var firstSide = wantedSide == LastSide.Any ? allFills.First().Side : (OrderSide)wantedSide;
            var notWanted = firstSide == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
            bool readCompleted = false;
            bool endOfStream = false;
            int pages = lastPages;
            while (!readCompleted)
            {
                var firstWanted = allFills.FirstOrDefault(x => x.Side == firstSide);
                if (firstWanted == null)
                {
                    var candidates = svc.client.FillsService.GetFillsByProductIdAsync(ProductType, 100, pages).Result.SelectMany(x => x).ToList();
                    if (candidates.Count == allFills.Count)
                    {
                        endOfStream = true;
                    }
                    allFills = candidates;
                }
                else
                {
                    var firstIdx = allFills.IndexOf(firstWanted);
                    if (firstIdx > -1)
                    {
                        var firstNotWanted = allFills.FirstOrDefault(x => x.Side == notWanted && allFills.IndexOf(x) > firstIdx);
                        if (firstNotWanted != null)
                        {
                            var notWantedIndex = allFills.IndexOf(firstNotWanted);
                            allFills = allFills.Skip(firstIdx).Take(notWantedIndex - firstIdx).ToList();
                            readCompleted = true;
                        }
                        else
                        {
                            pages++;
                            allFills = svc.client.FillsService.GetFillsByProductIdAsync(ProductType, 100, pages).Result.SelectMany(x => x).ToList();
                            continue;
                        }
                    }
                }
                if (endOfStream)
                {
                    readCompleted = true;
                }
            }


            var fillsLists = allFills.ToList();
            var f = fillsLists.First();
            var side = f.Side;
            var qty = f.Size;
            var subtotal = f.Size * f.Price;
            var fee = f.Fee;
            int idx = 0;
            var orderIds = new List<Guid>(new[] { f.OrderId });
            var l = new List<CoinbasePro.Services.Fills.Models.Responses.FillResponse>();
            l.Add(f);
            while (++idx < fillsLists.Count && fillsLists[idx].Side == f.Side)
            {
                f = fillsLists[idx];
                qty += f.Size;
                subtotal += (f.Size * f.Price);
                fee += f.Fee;
                l.Add(f);
                if (!orderIds.Contains(f.OrderId))
                    orderIds.Add(f.OrderId);
            }
            if (wantedSide == LastSide.Any)
                lastPages = Math.Max(1, (int)(Math.Ceiling(l.Count / 100m)));
            var total = subtotal;
            if (side == OrderSide.Buy)
            {
                var total2 = total + fee;
                var price2 = (total2) / qty;
                total = total2;
            }
            else
            {
                var total2 = total - fee;
                var price2 = (total2) / qty;
                total = total2;

            }
            var price = (total) / qty;
            var result = new LastOrder
            {
                Side = l.First().Side,
                Price = price,
                Size = qty,
                Total = total,
                SubTotal = subtotal,
                Fee = fee,
                Fills = l,
                OrderIds = orderIds,
            };
            return result;

        }

    }
    public class FillsCache
    {
        public FillResponse LastFill;
        public List<FillResponse> SellFills;
        public List<FillResponse> BuyFills;
        public ProductType ProductType;
        public Product Product;
        ConcurrentQueue<DateTime> updateRequests;
        public FillsCache(ProductType productType)
        {
            this.ProductType = productType;
            Product = OrdersService.GetProduct(productType);
            SellFills = new List<FillResponse>();
            BuyFills = new List<FillResponse>();
            updateRequests = new ConcurrentQueue<DateTime>();
            UpdateFills();
        }
        public void QueueUpdate()
        {
            updateRequests.Enqueue(DateTime.Now);
        }
        public bool Stale => updateRequests.Count > 0;
        public void UpdateFills()
        {
            while (updateRequests.Count > 0)
            {
                updateRequests.TryDequeue(out DateTime RequestDate);
                System.Threading.Thread.Sleep(100);
            }
            var last = FillsManager.GetLast(ProductType, LastSide.Any);
            LastOrder previous = null;
            if (last != null)
            {
                LastFill = last.Fills.First();
                if (last.Side == OrderSide.Buy)
                {
                    BuyFills = last.Fills.ToList();
                    previous = FillsManager.GetLast(ProductType, LastSide.Sell);
                    if (previous != null)
                    {
                        SellFills = previous.Fills.ToList();
                    }
                }
                else
                {
                    SellFills = last.Fills.ToList();
                    previous = FillsManager.GetLast(ProductType, LastSide.Buy);
                    if (previous != null)
                    {
                        BuyFills = previous.Fills.ToList();
                    }
                }
            }
        }
    }

    public enum LastSide
    {
        Buy = OrderSide.Buy,
        Sell = OrderSide.Sell,
        Any
    }
}
