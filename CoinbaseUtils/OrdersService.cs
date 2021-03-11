using CoinbasePro.Services.Fills.Models.Responses;
using CoinbasePro.Services.Orders.Models.Responses;
using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Services.Products.Models;
using CoinbasePro.Shared.Types;
using CoinbasePro.WebSocket.Models.Response;
using CoinbasePro.WebSocket.Types;
using CoinbaseUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

namespace CoinbaseUtils
{
    public class OrdersService : CoinbaseService
    {
        public static OrdersService service = new OrdersService();

        public static TResult TryExecute<TResult>(Func<TResult> del)
        {
            try
            {
                return del();
            }
            catch (Exception ex)
            {
                Log.Error($"{ex.Message}: {ex.ToString()}");
                return default;
            }
        }

        public static IEnumerable<Guid> CancelAllOrders(Func<OrderResponse, bool> filter)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CancelAllOrders)}(Func<OrderResponse, bool> filter)");
            //OrderManager.Refresh();
            var orders = GetAllOrders();
            var cancellations = orders.Where(filter).ToList();
            if (cancellations.Count > 0)
            {
                if (cancellations.First().Side == OrderSide.Buy)
                {
                    cancellations = cancellations.OrderByDescending(x => x.Price).ToList();
                }
                else
                {
                    cancellations = cancellations.OrderBy(x => x.Price).ToList();
                }
            }

            var cancelOrderIds = cancellations.Select(x => x.Id).ToList();
            var cancelledIds = CancelOrders(cancelOrderIds).ToList();
            var result = cancelledIds.Where(x => x != Guid.Empty).ToList();

            cancelOrderIds = cancelOrderIds.Where(x => !result.Contains(x)).ToList();

            while (cancelOrderIds.Count > 0)
            {
                cancelledIds = CancelOrders(cancelOrderIds).ToList();
                result.AddRange(cancelledIds.Where(x => x != Guid.Empty));
                cancelOrderIds = cancelOrderIds.Where(x => !result.Contains(x)).ToList();
            }




            return result;
        }

        public static IEnumerable<Guid> CancelAllSells()
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CancelAllSells)}");
            return CancelAllOrders(x => x.Side == OrderSide.Sell);
        }

        public static IEnumerable<Guid> CancelAllBuys()
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CancelAllBuys)}");
            return CancelAllOrders(x => x.Side == OrderSide.Buy);
        }

        public static IEnumerable<Guid> CancelOrders(IEnumerable<Guid> orderIds)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CancelOrders)}({string.Join(", ", orderIds)})");
            foreach (var orderId in orderIds)
            {
                yield return CancelOrderById(orderId);
            }
        }



        private static Dictionary<ProductType, Product> Products = null;
        public static Product GetProduct(ProductType productType)
        {
            if (Products == null)
            {
                Log.Information("Caching products");
                var products = TryExecute(() => service.client.ProductsService.GetAllProductsAsync().Result);
                products = products.OrderBy(x => x.QuoteCurrency.ToString()).ThenBy(x => x.BaseCurrency.ToString());
                var productIds = products.Select(x => x.Id).OrderBy(x => x).ToList();
                if (products == null)
                {
                    string error = $"{nameof(CoinbasePro.Services.Products.ProductsService)}.{nameof(CoinbasePro.Services.Products.ProductsService.GetAllProductsAsync)} returned null";
                    Log.Error(error);
                    throw new Exception($"Unable to get get products: {error}");
                }
                else
                {
                    products = products.Where(x => x.BaseCurrency != Currency.Unknown && x.QuoteCurrency != Currency.Unknown);
                    //Products = products.ToDictionary(x => new CurrencyPair(x.QuoteCurrency, x.BaseCurrency).ProductType, x => x);
                    Products = products.Where(x => x.Id != ProductType.Unknown)
                        .ToDictionary(x => x.Id, x => x);
                    Log.Information($"Retrieved {Products.Count} products: {string.Join(", ", Products.Keys)}");
                }
                Products = Products.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
            }
            return Products[productType];
        }


        public static Guid CancelBuyByIndex(int cancelBuyIndex)
        {
            if (cancelBuyIndex < OrderManager.BuyOrders.Count)
            {
                Log.Information($"Called {nameof(OrdersService)}.{nameof(CancelBuyByIndex)}({cancelBuyIndex})");
                var order = OrderManager.BuyOrders.Values.OrderBy(x => x.Price).ToList()[cancelBuyIndex];
                return CancelOrderById(order.Id);
            }
            else
            {
                Log.Information($"Invalid Index: {nameof(OrdersService)}.{nameof(CancelBuyByIndex)}({cancelBuyIndex})");
                return Guid.Empty;
            }
        }

        public static Guid CancelSellByIndex(int cancelSellIndex)
        {
            if (cancelSellIndex < OrderManager.SellOrders.Count)
            {
                Log.Information($"Called {nameof(OrdersService)}.{nameof(CancelSellByIndex)}({cancelSellIndex})");
                //Guid orderId = OrderManager.GetSellOrderIdByIndex(cancelSellIndex);
                var order = OrderManager.SellOrders.Values.OrderBy(x => x.Price).ToList()[cancelSellIndex];
                return CancelOrderById(order.Id);
            }
            else
            {
                Log.Information($"Invalid Index: {nameof(OrdersService)}.{nameof(CancelSellByIndex)}({cancelSellIndex})");
                return Guid.Empty;
            }
        }

        public static TradeOrder CreatePostOnlyOrder(ProductType productType, OrderSide orderSide, decimal currentPrice, decimal orderSize)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CreatePostOnlyOrder)}");
            return OrderHelper.CreatePostOnlyOrder(productType, orderSide, currentPrice, orderSize);
        }
        public static TradeOrder CreatePostOnlyBuyOrder(ProductType productType, decimal currentPrice, decimal funds)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CreatePostOnlyOrder)}");
            return OrderHelper.CreatePostOnlyBuyOrder(productType, currentPrice, funds);
        }

        public static TradeOrder CreateMarketSellOrder(ProductType productType, decimal orderSize, Guid? clientId = null)
        {
            var product = OrdersService.GetProduct(productType);
            orderSize = orderSize.ToPrecision(product.BaseIncrement);
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CreatePostOnlyOrder)}");
            return OrderHelper.CreateMarketOrder(productType, OrderSide.Sell, orderSize, clientId);
        }
        public static TradeOrder CreateMarketBuyOrder(ProductType productType, decimal orderSize, Guid? clientId = null)
        {
            var product = OrdersService.GetProduct(productType);
            orderSize = orderSize.ToPrecision(product.QuoteIncrement);
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CreatePostOnlyOrder)}");
            return OrderHelper.CreateMarketOrder(productType, OrderSide.Buy, orderSize, clientId);
        }


        public static OrderResponse PlaceOrder(TradeOrder order, IConsoleDriver driver = null)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(PlaceOrder)}");
            if (order.Market)
            {
                return PlaceMarketOrder(order);
            }
            else
            {
                return PlaceLimitOrder(order, driver);
            }

        }

        public static OrderResponse PlaceLimitOrder(TradeOrder order, IConsoleDriver driver = null)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(PlaceLimitOrder)}");
            if (order.OrderSide == OrderSide.Buy)
            {
                return PlaceLimitBuyOrder(order, driver);
            }
            else
            {
                return PlaceLimitSellOrder(order, driver);
            }
        }

        public static OrderResponse PlaceMarketOrder(TradeOrder order)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(PlaceMarketOrder)}");
            if (order.OrderSide == OrderSide.Buy)
            {
                return PlaceMarketBuyOrder(order);
            }
            else
            {
                return PlaceMarketSellOrder(order);
            }
        }

        public static OrderResponse PlaceMarketSellOrder(TradeOrder order)
        {
            if (order.ClientId == null) order.ClientId = Guid.NewGuid();
            var result = TryExecute(() =>
                    service.client.OrdersService
                    .PlaceMarketOrderAsync(order.OrderSide, order.ProductType, order.OrderSize, MarketOrderAmountType.Size, order.ClientId)
                    .Result);

            Log.Information($"[{order.ClientId}] Placed: {result.ToDebugString()}");
            return result;

        }

        public static OrderResponse PlaceMarketFundsBuyOrder(ProductType productType, decimal funds)
        {
            var clientId = Guid.NewGuid();
            var result = TryExecute(() =>
                    service.client.OrdersService
                    .PlaceMarketOrderAsync(OrderSide.Buy, productType, funds, MarketOrderAmountType.Funds, clientId)
                    .Result);
            Log.Information($"[{clientId}] Placed: {result.ToDebugString()}");
            return result;
        }

        public static OrderResponse PlaceMarketFundsSellOrder(ProductType productType, decimal size)
        {
            var product = OrdersService.GetProduct(productType);
            if (size < product.BaseMinSize)
                size = product.BaseMinSize;
            var clientId = Guid.NewGuid();
            var result = TryExecute(() =>
            {
                var orderResult = service.client.OrdersService
                .PlaceMarketOrderAsync(OrderSide.Sell, productType, size, MarketOrderAmountType.Size, clientId)
                .Result;
                return orderResult;
            }
                    );
            Log.Information($"[{clientId}] Placed: {result.ToDebugString()}");
            return result;
        }

        public static OrderResponse PlaceMarketBuyOrder(TradeOrder order)
        {
            if (order.ClientId == null) order.ClientId = Guid.NewGuid();
            var result = TryExecute(() =>
                    service.client.OrdersService
                    .PlaceMarketOrderAsync(order.OrderSide, order.ProductType, order.OrderSize, MarketOrderAmountType.Funds, order.ClientId)
                    .Result);

            Log.Information($"[{order.ClientId}] Placed: {result.ToDebugString()}");
            return result;
        }


        public static OrderResponse GetOrderById(Guid orderId)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(GetOrderById)}({orderId})");
            return GetOrderById(orderId.ToString());
        }


        public static List<Guid> CancelAllOrders()
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CancelAllOrders)}");
            List<Guid> result = TryExecute(() => service.client.OrdersService.CancelAllOrdersAsync().Result.OrderIds.ToList());
            if (result != null)
            {
                Log.Information($"Cancelled {string.Join(", ", result)}");
            }
            return result;
        }



        public static List<OrderResponse> GetAllOrders()
        {
            //Log.Information($"Called {nameof(OrdersService)}.{nameof(GetAllOrders)}");
            var statusList = new[] { OrderStatus.Active, OrderStatus.Open, OrderStatus.Pending, };
            var orders = OrdersService.GetAllOrders(statusList);
            //var apiResult = TryExecute(() => service.client.OrdersService.GetAllOrdersAsync().Result);
            //if (apiResult == null)
            //{
            //    Console.WriteLine("Failed to retrieve orders");
            //    return orders;
            //}
            //var result = apiResult.SelectMany(lst => lst.Select(x => x)).ToList();
            //Log.Information($"Retrieved orders {string.Join(", ", result.Select(x => x.Id))}");
            return orders;
        }

        public static List<OrderResponse> GetAllOrders(OrderStatus[] statusList)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(GetAllOrders)}({string.Join(", ", statusList)})");

            if (!OrderManager.IsDirty)
            {
                Console.WriteLine($"[{DateTime.Now}] Retrieved All Orders From Cache");
                return OrderManager.AllOrders.Values.ToList();
            }

            var apiResult = TryExecute(() => service.client.OrdersService.GetAllOrdersAsync(statusList).Result);
            var result = apiResult?.SelectMany(lst => lst.Select(x => x)).ToList();

            Log.Information($"Retrieved orders {string.Join(", ", result.Select(x => x.Id))}");
            return result;
        }


        public static OrderResponse GetOrderById(string orderId)
        {
            Log.Information($"[{orderId}] Called {nameof(OrdersService)}.{nameof(GetOrderById)}({orderId})");
            if (OrderManager.IsDirty)
            {
                OrderManager.Refresh();
            }
            if (OrderManager.AllOrders.ContainsKey(Guid.Parse(orderId)))
            {
                //Console.WriteLine($"[{DateTime.Now}] Retrieved Order From Cache {orderId}");
                return OrderManager.AllOrders[Guid.Parse(orderId)];
            }
            else
            {
                var result = TryExecute(() => service.client.OrdersService.GetOrderByIdAsync(orderId.ToString()).Result);
                Log.Information($"[{orderId}] Result: {result.ToJson()}");
                return result;
            }

        }
        private static Guid CancelOrderById(Guid orderId)
        {
            Log.Information($"[{orderId}] Called {nameof(OrdersService)}.{nameof(CancelOrderById)}({orderId})");
            var order = GetOrderById(orderId);
            if (order == null)
            {
                Log.Information($"[{orderId}] Not Found");
                return orderId;
            }
            var result = TryExecute(() =>
            {
                var task = service.client.OrdersService.CancelOrderByIdAsync(orderId.ToString());
                return task.Result.OrderIds.First();
            });

            Log.Information($"[{orderId}] Canceled: {orderId} - {order.ToDebugString()}");
            return result;
        }

        public static OrderResponse PlaceLimitSellOrder(TradeOrder order, IConsoleDriver driver = null)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(PlaceLimitSellOrder)}: {order.ToJson()}");
            int tryCount = 0;
            int maxRetries = 3;
            var sleep = 1000;
            retryPlaceLimitSellOrder:
            var result = TryExecute(() =>
                 service.client.OrdersService
                 .PlaceLimitOrderAsync(order.OrderSide, order.ProductType, order.OrderSize, order.Price,
                 clientOid: order.ClientId).Result);

            if (result is null)
            {
                string error = $"Failed to place limit {order.OrderSide} order: {order}";
                Log.Error(error);
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(error);
                Console.ResetColor();

                if (tryCount++ < maxRetries)
                {
                    System.Threading.Thread.Sleep(sleep);
                    sleep <<= 1;
                    if (driver != null && driver.LastTicker != null && order.Price < driver.LastTicker.BestAsk)
                    {
                        order.Price = driver.LastTicker.BestAsk;
                    }
                    goto retryPlaceLimitSellOrder;
                }
                else
                {
                    error = "Retry limit exceeded: " + error;
                    Log.Error(error);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(error);
                    Console.ResetColor();
                }
                //throw new Exception(error);
            }
            else
            {
                Log.Information($"[{result.Id}] Placed limit {order.OrderSide} order: {result.ToJson()}");
            }
            return result;

        }

        public static OrderResponse PlaceLimitBuyOrder(TradeOrder order, IConsoleDriver driver = null)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(PlaceLimitBuyOrder)}: {order.ToJson()}");
            int tryCount = 0;
            int maxRetries = 3;
            var sleep = 1000;
            retryPlaceLimitBuyOrder:
            var result = TryExecute(() =>
                service.client.OrdersService
                .PlaceLimitOrderAsync(order.OrderSide, order.ProductType, order.OrderSize, order.Price,
                clientOid: order.ClientId).Result);

            if (result is null)
            {
                string error = $"Failed to place limit {order.OrderSide} order: {order}";
                Log.Error(error);
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(error);
                Console.ResetColor();
                if (tryCount++ < maxRetries)
                {
                    System.Threading.Thread.Sleep(sleep);
                    sleep <<= 1;
                    if (driver != null && driver.LastTicker != null && order.Price > driver.LastTicker.BestBid)
                    {
                        order.Price = driver.LastTicker.BestBid;
                    }
                    goto retryPlaceLimitBuyOrder;
                }
                else
                {
                    error = "Retry limit exceeded: " + error;
                    Log.Error(error);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(error);
                    Console.ResetColor();
                }

                //throw new Exception(error);
            }
            else
            {
                Log.Information($"[{result.Id}] Placed limit {order.OrderSide} order: {result.ToJson()}");
            }
            return result;
        }




        internal static CancelOrderResponse CancelOrder(OrderResponse order)
        {
            Log.Information($"Called {nameof(OrdersService)}.{nameof(CancelOrder)}: {order.ToJson()}");
            var result = TryExecute(() => service.client.OrdersService.CancelOrderByIdAsync(order.Id.ToString()).Result);
            if (result != null)
            {
                Log.Information($"Canceled order: {result.ToJson()}");
            }
            return result;
        }

    }


    public class OrderManager : CoinbaseService
    {
        public static ConcurrentDictionary<Guid, OrderResponse> BuyOrders = new ConcurrentDictionary<Guid, OrderResponse>();
        public static ConcurrentDictionary<Guid, OrderResponse> SellOrders = new ConcurrentDictionary<Guid, OrderResponse>();
        public static ConcurrentDictionary<Guid, OrderResponse> AllOrders = new ConcurrentDictionary<Guid, OrderResponse>();
        private static OrderSyncer syncer = new OrderSyncer();
        internal static List<ProductType> ProductTypes = new List<ProductType>();

        public OrderManager()
        {

        }
        public static void SetDirty()
        {
            var now = DateTime.Now;
            if (now > DirtyDate)
            {
                DirtyDate = now;
            }
        }
        public static bool IsDirty => DirtyDate > LastRefresh
            || LastRefresh == DateTime.MinValue 
            || DateTime.Now.Subtract(LastRefresh).TotalSeconds > 5;

        public static DateTime DirtyDate;
        private static bool isUpdating;
        public static DateTime LastRefresh { get; private set; }
        public static void Refresh()
        {
            if (IsDirty )
            {
                if (isUpdating)
                {
                    while (isUpdating) { System.Threading.Thread.Sleep(0100); }
                    //Console.WriteLine($"[{DateTime.Now}] Order Manager - Refreshed on seperate thread {LastRefresh}");
                    return;
                }
                while (DateTime.Now.Subtract(DirtyDate).Milliseconds < 250)
                {
                    System.Threading.Tasks.Task.Delay(DateTime.Now.Subtract(DirtyDate).Milliseconds).Wait();
                }
                isUpdating = true;
                var svc = new CoinbaseService();
                var statusList = new[] { OrderStatus.Active, OrderStatus.Open, OrderStatus.Pending, };
                var orders = OrdersService.GetAllOrders(statusList);
                Update(orders);
                LastRefresh = DateTime.Now;
                isUpdating = false;
            }
            else
            {
                if (isUpdating)
                {
                    while (isUpdating) { System.Threading.Thread.Sleep(0100); }
                    //Console.WriteLine($"[{DateTime.Now}] Order Manager - Refreshed on seperate thread {LastRefresh}");
                    return;
                }
                //Console.WriteLine($"[{DateTime.Now}] Order Manager - Using cache {LastRefresh}");
            }

        }

        public static void Update(List<OrderResponse> orders)
        {
            AllOrders = orders.ToConcurrentDictionary(x => x.Id, x => x);
            BuyOrders = orders.Where(x => x.Side == OrderSide.Buy).OrderBy(x => x.Price)
                .ToConcurrentDictionary(x => x.Id, x => x);
            SellOrders = orders.Where(x => x.Side == OrderSide.Sell).OrderBy(x => x.Price)
                .ToConcurrentDictionary(x => x.Id, x => x);
            lock (ProductTypes)
            {
                ProductTypes.Clear();
                ProductTypes.AddRange(orders.Select(x => x.ProductId).Distinct().OrderBy(x => x));
            }
        }

        public static OrderResponse GetOrderByIndex(int orderIndex)
        {
            var key = GetOrderIdByIndex(orderIndex);
            return AllOrders[key];
        }

        public static OrderResponse GetBuyOrderByIndex(int buyOrderIndex)
        {
            var key = GetBuyOrderIdByIndex(buyOrderIndex);
            return BuyOrders[key];
        }

        public static OrderResponse GetSellOrderByIndex(int sellOrderIndex)
        {
            var key = GetSellOrderIdByIndex(sellOrderIndex);
            return BuyOrders[key];
        }

        public static Guid GetOrderIdByIndex(int orderIndex)
        {
            return AllOrders.Keys.ToArray()[orderIndex];
        }

        public static Guid GetBuyOrderIdByIndex(int buyOrderIndex)
        {
            return BuyOrders.Keys.ToArray()[buyOrderIndex];
        }

        public static Guid GetSellOrderIdByIndex(int sellOrderIndex)
        {
            return SellOrders.Keys.ToArray()[sellOrderIndex];
        }

        public static void AddOrUpdate(Guid orderId)
        {

            var order = OrdersService.GetOrderById(orderId);
            if (order == null)
            {
                AllOrders.TryRemove(orderId, out OrderResponse ex);
                return;
            }
            AllOrders.AddOrUpdate(order.Id, order, (id, existing) => order);
            var side = order.Side;
            if (side == OrderSide.Buy)
            {
                BuyOrders.AddOrUpdate(order.Id, order, (id, existing) => order);
            }
            else
            {
                SellOrders.AddOrUpdate(order.Id, order, (id, existing) => order);
            }
        }

        public static void AddOrUpdate(Open order)
        {
            AddOrUpdate(order.OrderId);
        }

        public static void AddOrUpdate(Done doneOrder)
        {
            if (doneOrder.Reason == DoneReasonType.Canceled)
            {
                if (AllOrders.ContainsKey(doneOrder.OrderId))
                {
                    var order = AllOrders[doneOrder.OrderId];
                    order.DoneReason = doneOrder.Reason.ToString();
                    order.FilledSize = order.Size - doneOrder.RemainingSize;
                    order.DoneAt = doneOrder.Time.UtcDateTime;
                }
            }

        }

        public static void AddOrUpdate(Received order)
        {
            AddOrUpdate(order.OrderId);
        }

        public static OrderResponse TryGetById(Guid orderId)
        {
            if (AllOrders.ContainsKey(orderId))
            {
                return AllOrders[orderId];
            }
            return null;
        }

        public static OrderResponse TryGetByFirstIds(params Guid[] orderIds)
        {
            OrderResponse result = null;
            for (var i = 0; result == null && i < orderIds.Length; i++)
            {
                var orderId = orderIds[i];
                result = TryGetById(orderId);
            }
            return result;
        }


    }





    public class OrderHelper
    {
        public static TradeOrder CreatePostOnlyOrder(ProductType productType, OrderSide orderSide, decimal price, decimal orderSize)
        {
            var product = OrdersService.GetProduct(productType);
            if (orderSide == OrderSide.Buy)
            {
                orderSize = orderSize.ToPrecision(product.BaseIncrement);
                price = price.ToPrecision(product.QuoteIncrement);
            }
            else
            {
                orderSize = orderSize.ToPrecision(product.BaseIncrement);//precison of sell side 
                price = price.ToPrecision(product.QuoteIncrement); //precions of buy side
            }

            var service = new AccountService();
            decimal feerate = service.MakerFeeRate;
            decimal total = price * orderSize;
            decimal fee = total * feerate;
            var totalAmount = total + fee;
            return new TradeOrder(productType, orderSide, price, orderSize, fee, feerate, totalAmount);

        }

        internal static TradeOrder CreateMarketOrder(ProductType productType, OrderSide orderSide, decimal orderSize, Guid? clientId = null)
        {
            return new TradeOrder(productType, orderSide, orderSize, clientId);
        }

        internal static TradeOrder CreatePostOnlyBuyOrder(ProductType productType, decimal price, decimal funds)
        {
            var service = new AccountService();
            decimal feerate = service.MakerFeeRate;
            var product = OrdersService.GetProduct(productType);

            var rawTotal = funds / price;
            //rawTotal = 0.1587377545660382620923218316
            var rawFee = rawTotal * feerate;
            var rawSubtotal = rawTotal - rawFee;
            var orderFee = (funds * feerate);//.ToPrecision(product.QuoteIncrement);


            var orderFunds = funds - orderFee;
            var rawSize = (orderFunds / price);
            var orderSize = rawSize.ToPrecision(product.BaseIncrement);
            decimal total = (price * orderSize).ToPrecision(product.QuoteIncrement);
            decimal fee = orderFee;
            var totalAmount = total + fee;
            var roundedPrice = price.ToPrecision(product.QuoteIncrement);
            //0.15861088
            return new TradeOrder(productType, OrderSide.Buy, roundedPrice, orderSize, fee, feerate, totalAmount);
        }
    }

    public class TradeOrder
    {
        public ProductType ProductType { get; set; }
        public OrderSide OrderSide { get; set; }
        public decimal Price { get; set; }
        public decimal OrderSize { get; set; }
        public decimal Fee { get; set; }
        public decimal FeeRate { get; set; }
        public decimal TotalAmount { get; set; }

        public bool Market { get; set; }

        public Guid ClientId { get; set; }
        public TradeOrder(ProductType productType, OrderSide orderSide, decimal price, decimal orderSize, decimal fee, decimal feeRate, decimal totalAmount)
        {
            this.ProductType = productType;
            this.OrderSide = orderSide;
            this.Price = price;
            this.OrderSize = orderSize;
            this.Fee = fee;
            this.FeeRate = feeRate;
            this.TotalAmount = totalAmount;
            this.ClientId = Guid.NewGuid();
        }
        public TradeOrder(ProductType productType, OrderSide orderSide, decimal orderSize, Guid? clientId = null)
        {
            this.ProductType = productType;
            this.OrderSide = orderSide;
            this.OrderSize = orderSize;
            this.ClientId = clientId ?? Guid.NewGuid();
        }



        public override string ToString()
        {
            return $"{ProductType} {OrderSide} {OrderSize} @ {Price} = {TotalAmount}";
        }
        public bool IsValid()
        {
            //ISSUE: Cash market orders require min 10. Limit orders vary by product.
            if (OrderSide == OrderSide.Sell) return OrderSize >= .1m;
            return TotalAmount >= 10;
            //throw new NotImplementedException();
        }
    }

    public interface IConsoleDriver
    {
        void SetProductFeeds(IEnumerable<ProductType> productTypes);

        Ticker LastTicker { get; }
    }
    public class OrderSyncer : IDisposable
    {

        private Timer SyncTimer;
        private bool isRunning;
        private double syncInterval;
        public double SyncInterval
        {
            get
            {
                return syncInterval;
            }
            set
            {
                syncInterval = value;
                if (SyncTimer != null) SyncTimer.Interval = syncInterval;
            }
        }

        DateTime LastSync;
        private IConsoleDriver consoleDriver;
        public OrderSyncer(int syncIntervalSeconds = 60)
        {
            SyncInterval = syncIntervalSeconds * 1000;
        }

        public OrderSyncer(IConsoleDriver consoleDriver)
        {
            this.consoleDriver = consoleDriver;
        }



        public List<ProductType> ProductTypes => OrderManager.ProductTypes;
        private void SyncOrders()
        {
            OrderManager.Refresh();
        }
        private void SyncTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var current = ProductTypes.ToList();
            SyncOrders();
            if (!ProductTypes.SequenceEqual(current))
            {
                var added = ProductTypes.Except(current).ToList();
                var removed = current.Except(ProductTypes).ToList();
            }
            consoleDriver.SetProductFeeds(ProductTypes);
        }
        public override string ToString()
        {
            return $"=> OrderSync: Running: {isRunning}({(int)(syncInterval / 1000)}s): {string.Join(",", ProductTypes.Select(x => x))}";
        }

        public void Start()
        {
            Stop();

            this.SyncTimer = new Timer();
            SyncTimer.Interval = syncInterval;
            SyncTimer.Elapsed += SyncTimer_Elapsed;
            SyncTimer.Start();

            isRunning = true;
        }

        private void Stop()
        {
            if (SyncTimer != null)
            {
                SyncTimer.Stop();
                SyncTimer.Elapsed -= SyncTimer_Elapsed;
                SyncTimer = null;
            }
            isRunning = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    public class Log
    {
        public static Log Instance = new Log(DefaultLogger());
        public static TextWriter DefaultLogger()
        {
            return new StreamWriter("socket.log", true);
        }
        public TextWriter output;
        public Log(TextWriter output)
        {
            this.output = output;
        }
        public static void Error(string message)
        {
            lock (Instance)
            {
                Instance.output.WriteLine($"[{DateTime.UtcNow.ToJson()}] [Error] {message}");
                Instance.output.Flush();
            }

        }

        public static void Information(string message)
        {
            lock (Instance)
            {
                Instance.output.WriteLine($"[{DateTime.UtcNow.ToJson()}] [Info] {message}");
                Instance.output.Flush();
            }
        }
    }
}
