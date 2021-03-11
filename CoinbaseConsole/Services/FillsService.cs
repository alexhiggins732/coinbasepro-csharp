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
        static FillsManager()
        {
            FillsCache = new ConcurrentDictionary<ProductType, FillsCache>();
        }


        public static FillsCache Cache(ProductType productType)
        {
            var result = FillsCache.GetOrAdd(productType, (id) => new FillsCache(productType));
            return result;
        }


        private static int lastPages = 1;
        public static LastOrder GetLast(ProductType ProductType, LastSide wantedSide = LastSide.Any)
        {

            LastOrder result = null;
            var cache = Cache(ProductType);

            if (wantedSide == LastSide.Buy || (wantedSide == LastSide.Any && cache.LastFill.Side == OrderSide.Buy))
            {
                result = cache.LastBuy;
            }
            else
            {
                result = cache.LastSell;
            }
            return result;

        }
        public static LastOrder GetLastWithApi(ProductType ProductType, LastSide wantedSide = LastSide.Any)
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

        internal static void MarkUpdated(ProductType productType)
        {
            Cache(productType).QueueUpdate();
        }
    }
    public class FillsCache
    {
        FillResponse lastFill;
        LastOrder lastBuy;
        LastOrder lastSell;
        ConcurrentQueue<DateTime> updateRequests;
        public ProductType ProductType { get; }
        public Product Product { get; }
        public bool Stale => updateRequests.Count > 0;

        public FillResponse LastFill
        {
            get
            {
                if (Stale) UpdateFills();
                return lastFill;
            }
            private set { lastFill = value; }
        }
        public List<FillResponse> SellFills => LastSell.Fills ?? new List<FillResponse>();

        public List<FillResponse> BuyFills => LastBuy.Fills ?? new List<FillResponse>();


        public LastOrder LastBuy
        {
            get { if (Stale) UpdateFills(); return lastBuy; }
            internal set { lastBuy = value; }
        }
        public LastOrder LastSell
        {
            get { if (Stale) UpdateFills(); return lastSell; }
            internal set { lastSell = value; }
        }

        public FillsCache(ProductType productType)
        {
            this.ProductType = productType;
            Product = OrdersService.GetProduct(productType);
            updateRequests = new ConcurrentQueue<DateTime>();
            QueueUpdate();
            UpdateFills();
        }
        public void QueueUpdate()
        {
            updateRequests.Enqueue(DateTime.Now);
        }

        private DateTime lastUpdated;
        private bool updating = false;
        private Guid updatingId;
        public void UpdateFills()
        {
            // if no update requests, another thread is performing an update.
            Guid callId = Guid.NewGuid();
            if (updateRequests.Count == 0)
            {
                while (updating) // wait until the thread completes
                {
                    System.Threading.Thread.Sleep(100);
                    //Console.WriteLine($"Update in progress: {callId}");
                }
                return; // no need to update, so return
            }
            else
            {
                updating = true;
                updatingId = callId; // 
                //Console.WriteLine($"{updateRequests.Count} pending: {callId}");
            }
            while (updateRequests.Count > 0 && updatingId == callId)
            {
                updateRequests.TryDequeue(out DateTime RequestDate);
                lastUpdated = RequestDate > lastUpdated? RequestDate: lastUpdated;
                System.Threading.Thread.Sleep(100);
                //Console.WriteLine($"Dequeuing in progress: {callId}");
            }
            if (updatingId != callId) // race condition
            {
                while (updating) // wait until the thread completes
                {
                    System.Threading.Thread.Sleep(100);
                    //Console.WriteLine($"Race in progress: {callId}");
                }
                return; // no need to update, so return
            }
            //Console.WriteLine($"Getting last : {callId}");
            var last = FillsManager.GetLastWithApi(ProductType, LastSide.Any);
            LastOrder previous = null;
            if (last != null)
            {
                LastFill = last.Fills.First();
                if (last.Side == OrderSide.Buy)
                {
                    lastBuy = last;
                    previous = FillsManager.GetLastWithApi(ProductType, LastSide.Sell);
                    if (previous != null)
                    {
                        lastSell = previous;
                    }
                }
                else
                {
                    lastSell = last;
                    previous = FillsManager.GetLastWithApi(ProductType, LastSide.Buy);
                    if (previous != null)
                    {
                        lastBuy = previous;
                    }
                }
            }
            updating = false;
        }
    }

    public enum LastSide
    {
        Buy = OrderSide.Buy,
        Sell = OrderSide.Sell,
        Any
    }
}
