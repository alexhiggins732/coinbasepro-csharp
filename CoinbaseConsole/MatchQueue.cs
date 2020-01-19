using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Shared.Types;
using CoinbasePro.WebSocket.Models.Response;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseConsole
{
    public class MatchFactory
    {
        public static MatchedTrade Create(Match order)
        {
            var matched = new MatchedTrade()
            {
                Side = order.Side,
                Size = order.Size,
                Price = order.Price,
                Product = order.ProductId,
            };
            return matched;
        }

        public static MatchedTrade Create(LastMatch order)
        {
            var matched = new MatchedTrade()
            {
                Side = order.Side,
                Size = order.Size,
                Price = order.Price,
                Product = order.ProductId,
            };
            return matched;
        }

    }
    public class MatchedTrade
    {
        public OrderSide Side { get; internal set; }
        public decimal Size { get; internal set; }
        public decimal Price { get; internal set; }
        public ProductType Product { get; internal set; }
        public decimal Total => Size * Price;
    }

    public class MatchedTradeAggregate
    {
        public OrderSide Side { get; internal set; }
        public ProductType Product { get; internal set; }
        public decimal Total { get; internal set; }
        public decimal Size { get; internal set; }
    }
    public class MatchQueue
    {
        ConcurrentQueue<MatchedTrade> queue = new ConcurrentQueue<MatchedTrade>();
        public int Count => queue.Count;

        public decimal QueueTotal => queue.Count > 0 ? queue.Sum(x => x.Total) : 0m;

        public MatchedTradeAggregate Aggregate()
        {
            if (queue.TryPeek(out MatchedTrade t))
            {

                var result = new MatchedTradeAggregate()
                {
                    Side = t.Side,
                    Product = t.Product
                };
                while (queue.Count > 0)
                {
                    if (queue.TryDequeue(out t))
                    {
                        result.Total += t.Total;
                        result.Size += t.Size;
                    }
                }
                return result;
            }
            return null;
        }
        public void Enqueue(MatchedTrade match) => queue.Enqueue(match);


        public static MatchQueue MatchedBuys = new MatchQueue();
        public static MatchQueue MatchedSells = new MatchQueue();
        public static void Add(Match match)
        {
            var matched = MatchFactory.Create(match);
            Add(matched);
        }
        public static void Add(LastMatch lastMatch)
        {
            var matched = MatchFactory.Create(lastMatch);
            Add(matched);

        }
        public static void Add(MatchedTrade matched)
        {
            MatchQueue queue = matched.Side == OrderSide.Buy ? MatchedBuys : MatchedSells;
            queue.Enqueue(matched);
        }

        public static void Clear()
        {
            while (MatchedBuys.queue.Count > 0)
            {
                MatchedBuys.queue.TryDequeue(out MatchedTrade result);
            }
            while (MatchedSells.queue.Count > 0)
            {
                MatchedSells.queue.TryDequeue(out MatchedTrade result);
            }
        }
    }
}
