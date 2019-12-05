using CoinbasePro.Services.Accounts.Models;
using CoinbasePro.Services.Orders.Models.Responses;
using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Shared.Types;
using CoinbasePro.WebSocket.Models.Response;
using CoinbaseUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseConsole
{
    public class ConsoleDriver
    {
        static char[] space = " ".ToCharArray();
        public static ProductType DriverDefaultProductType = ProductType.LtcUsd;
        public static ProductType DefaultProductType => DriverDefaultProductType;

        public ProductType ProductType { get; private set; }
        public ConsoleDriver() : this(DriverDefaultProductType) { }

        public Func<Ticker, String> FormatTicker => (ticker) =>
            $"{DateTime.Now}: {ticker.ProductId}: {ticker.BestBid}-{ticker.BestAsk}";

        public ConsoleDriver(ProductType productType)
        {
            SetProductType(productType);
        }
        public CoinbaseTicker ticker;


        void SetProductType(ProductType productType)
        {
            this.ProductType = productType;
            SetTicker(productType);
        }
        public void Run()
        {
            this.ticker = CoinbaseTicker.Create(ProductType.LtcUsd);
            ticker.OnTickerReceived += (sender, e) => OnTickerReceived(ticker, e);

            while (true)
            {
                var line = Console.ReadLine();
                var args = ParseArguments(line);
                args.Execute();

            }

        }

        private void OnTickerReceived(CoinbaseTicker ticker, WebfeedEventArgs<Ticker> e)
            => Console.Title = FormatTicker(e.LastOrder);

        void SetTicker(ProductType productType)
        {
            if (ticker != null)
            {
                ticker.OnTickerReceived -= (sender, e) => OnTickerReceived(ticker, e);
                ticker.Stop();
            }

            ticker = CoinbaseTicker.Create(productType);
            ticker.OnTickerReceived += (sender, e) => OnTickerReceived(ticker, e);

        }



        public DriverOptions ParseArguments(string line)
        {
            var result = new DriverOptions();
            var arguments = ReadArgs(line,
                out string arg0,
                out string arg1,
                out string arg2,
                out string arg3,
                out string arg4,
                out string arg5,
                out string arg6);

            ProductType productType = DefaultProductType;

            List<Action> commandActions = new List<Action>();
            if (arg0 == "cancel")
            {
                commandActions.AddRange(ParseCancellationOptions(arguments));
                //cancel all, cancel buys, cancel sells, cancel all buys, cancel all sells

            }
            else if (arg0 == "orders")
            {
                commandActions.AddRange(ParseOrdersOptions(arguments));
            }
            else if (arg0 == "fills")
            {
                commandActions.AddRange(ParseFillsOptions(arguments));
            }
            else if (arg0 == "balance")
            {
                commandActions.AddRange(ParseBalanceOptions(arguments));
            }
            else if (arg0 == "spread")
            {
                commandActions.AddRange(ParseSpreadOptions(arguments));
            }
            else if (arg0 == "product")
            {
                commandActions.AddRange(ParseChangeProductType(arguments));
            }
            else
            {
                commandActions.Add(arguments.InvalidAction());
            }

            result.Actions = commandActions;
            return result;

        }

        private IEnumerable<Action> ParseFillsOptions(ArgumentList arguments)
        {
            List<Action> result = new List<Action>();
            if (arguments.Length == 1)
            {
                var svc = new CoinbaseService();
                var fillsLists = svc.client.FillsService.GetFillsByProductIdAsync(ProductType, 20, 1).Result;
                var fills = fillsLists.SelectMany(x => x).ToList();
                result.AddRange(
                    fills.Select(fill =>
                    {
                        return (Action)(() =>
                        {
                            Console.WriteLine($"{fill.CreatedAt.ToLocalTime()} {fill.Side} {fill.Size} @ {fill.Price.ToString("c")} ({fill.Fee.ToString("c")}) = {(fill.Size * fill.Price).ToString("c")}");
                        });
                    })
                 );

            }
            else
            {
                result.Add(arguments.InvalidAction());
            }
            return result;
        }

        private IEnumerable<Action> ParseOrdersOptions(ArgumentList arguments)
        {
            List<Action> result = new List<Action>();

            bool addBuys = false;
            bool addSells = false;
            if (arguments.Length == 1)
            {
                addBuys = true;
                addSells = true;
            }
            else if (arguments.Length == 1)
            {

                switch (arguments.arg1)
                {

                    case "buy":
                        addBuys = true;
                        break;
                    case "sell":
                        addSells = true;
                        break;

                    default:
                        result.Add(arguments.InvalidAction());
                        break;
                }


            }
            else
            {
                result.Add(arguments.InvalidAction());
            }

            OrderManager.Refresh();

            if (result.Count == 0)
            {

                result.Add(() => Console.WriteLine($"[{DateTime.Now}] {OrderManager.AllOrders.Count} Orders:"));
                if (addBuys)
                {
                    var orders = OrderManager.BuyOrders.Values.OrderBy(x => x.Price).ThenBy(x => x.Price * x.Size).ToList();
                    if (orders.Count > 0) { result.Add(() => Console.WriteLine()); }
                    result.Add(() => Console.WriteLine($"Buys {orders.Count}"));
                    result.AddRange(Enumerable.Range(0, orders.Count).Select(i =>
                        {
                            var x = orders[i];
                            return (Action)(() => Console.WriteLine($" [{i}] {x.ProductId} {x.Side} {x.Size} @ {x.Price.ToString("c")} = {(x.Size * x.Price).ToString("c")}"));
                        }
                    ));
                    if (orders.Count > 0) { result.Add(() => Console.WriteLine()); }

                }
                if (addSells)
                {
                    var orders = OrderManager.SellOrders.Values.OrderBy(x => x.Price).ThenBy(x => x.Price * x.Size).ToList();
                    if (orders.Count > 0) { result.Add(() => Console.WriteLine()); }
                    result.Add(() => Console.WriteLine($"Sells {orders.Count}"));
                    result.AddRange(Enumerable.Range(0, orders.Count).Select(i =>
                    {
                        var x = orders[i];
                        return (Action)(() => Console.WriteLine($" [{i}] {x.ProductId} {x.Side} {x.Size} @ {x.Price.ToString("c")} = {(x.Size * x.Price).ToString("c")}"));
                    }
                    ));
                    if (orders.Count > 0) { result.Add(() => Console.WriteLine()); }
                }
            }
            return result;
        }

        private IEnumerable<Action> ParseBalanceOptions(ArgumentList arguments)
        {
            List<Action> result = new List<Action>();
            result.Add(() =>
            {
                var svc = new AccountService();
                var pair = new CurrencyPair(ProductType);
                var sellAvailable = svc.GetBalance(ProductType, OrderSide.Sell);
                var buyAvailable = svc.GetBalance(ProductType, OrderSide.Buy);
                Console.WriteLine($"[{DateTime.Now}] {pair.BuyCurrency}: {buyAvailable.ToString("c")} - {pair.SellCurrency}: {sellAvailable}");
            });
            return result;
        }

        public IEnumerable<Action> ParseChangeProductType(ArgumentList arguments)
        {
            List<Action> result = new List<Action>();
            if (arguments.arg1.StartsWith("producttype."))
            {
                arguments.arg1 = arguments.arg1.Substring("producttype.".Length);
            }
            if (arguments.arg1.ParseEnum<ProductType>(out ProductType productType))
            {
                result.Add(() => SetProductType(productType));
            }
            else
            {
                result.Add(arguments.InvalidAction($"Unable to parse {nameof(ProductType)}"));
            }
            return result;
        }
        public static IEnumerable<Action> ParseSpreadOptions(ArgumentList arguments)
        {
            var result = new List<Action>();

            var options = new SpreadOptions();
            int idx = 1;
            if (idx > arguments.Length)
            {
                result.Add(arguments.InvalidAction());
            }
            else
            {
                switch (arguments.parts[idx++])
                {
                    case "sell":
                        options.OrderSide = OrderSide.Sell;
                        break;
                    case "buy":
                        //spread buy [options] => start price, order size, increment, [max]               
                        options.OrderSide = OrderSide.Buy;
                        break;
                    default:
                        result.Add(arguments.InvalidAction($"Unable to parse {nameof(OrderSide)}"));
                        break;
                }

                if (idx < arguments.Length && Enum.TryParse<ProductType>(arguments.parts[idx], out ProductType productType))
                {
                    idx++;
                    options.ProductType = productType;
                }
                else
                {
                    productType = options.ProductType = DefaultProductType;
                }

                if (idx < arguments.Length && arguments[idx++].ParseDecimal(out decimal startPrice))
                {
                    options.StartPrice = startPrice;
                    if (idx < arguments.Length && arguments[idx++].ParseDecimal(out decimal orderSize))
                    {
                        options.OrderSize = orderSize;
                        if (idx < arguments.Length && arguments[idx++].ParseDecimal(out decimal increment))
                        {
                            options.Increment = increment;
                            if (idx == arguments.Length)
                            {
                                options.TotalAmount = AccountService.GetAccountBalance(productType, options.OrderSide);
                            }
                            else if (idx < arguments.Length && arguments[idx++].ParseDecimal(out decimal orderMax))
                            {
                                options.TotalAmount = orderMax;
                            }
                            else
                            {
                                result.Add(arguments.InvalidAction($"Unable to parse {nameof(orderMax)}: {arguments.line}"));
                            }
                        }
                        else
                        {
                            result.Add(arguments.InvalidAction($"Unable to parse {nameof(increment)}: {arguments.line}"));
                        }
                    }
                    else
                    {
                        result.Add(arguments.InvalidAction($"Unable to parse {nameof(orderSize)}: {arguments.line}"));
                    }
                }
                else
                {
                    result.Add(arguments.InvalidAction($"Unable to parse {nameof(startPrice)}: {arguments.line}"));
                }
            }

            if (result.Count == 0)
            {
                result.AddRange(options.GetOrderActions());

            }

            return result;
        }
        private static IEnumerable<Action> ParseCancellationOptions(ArgumentList arguments)
        {
            var result = new List<Action>();


            //arg0= cancel, arg1=[all|buys|sells], arg2 =[if(args[1]=all)[buys|sells]]
            // cancel all, cancel buys, cancel sells, call all buys, cancel all sells
            switch (arguments.arg1)
            {
                case "all": //cancel all [options]

                    switch (arguments.arg2)
                    {
                        case "buys": //cancel all buys
                            result.Add(() => OrderService.CancelAllBuys());
                            break;
                        case "sells": //cancel all sells
                            result.Add(() => OrderService.CancelAllSells());
                            break;
                        case null: // cancel all
                            result.Add(() => OrderService.CancelAllOrders());
                            break;
                        default: // cancel all [uknown]
                            result.Add(arguments.InvalidAction());
                            break;
                    }

                    break;
                case "buys": // cancel buys [options]
                    switch (arguments.arg2)
                    {
                        case null: // cancel buys
                            result.Add(() => OrderService.CancelAllBuys());
                            break;

                        default: //cancel buys index
                            int cancelBuyIndex = arguments.arg2.ToIndex();
                            if (cancelBuyIndex > -1)
                            {
                                result.Add(() => OrderService.CancelBuy(cancelBuyIndex));
                            }
                            else
                            {
                                result.Add(arguments.InvalidAction());
                            }

                            break;
                    }
                    break;

                case "sells":
                    switch (arguments.arg2)
                    {
                        case null:
                            result.Add(() => OrderService.CancelAllSells());
                            break;
                        default: //cancel buys index
                            int cancelSellIndex = arguments.arg2.ToIndex();
                            if (cancelSellIndex > -1)
                            {
                                result.Add(() => OrderService.CancelSell(cancelSellIndex));
                            }
                            else
                            {
                                result.Add(arguments.InvalidAction());
                            }

                            break;
                    }


                    break;
                default:
                    result.Add(arguments.InvalidAction());
                    break;

            }
            return result;
        }

        public static Action InvalidArgumentAction(string line)
        {
            return () => InvalidArguments(line);
        }
        private static void InvalidArguments(string line)
        {
            Console.WriteLine($"Invalid arguments: {line}");
        }

        private static ArgumentList ReadArgs(string line, out string arg0, out string arg1, out string arg2, out string arg3, out string arg4, out string arg5, out string arg6)
        {
            var parts = (line.ToLower() ?? "").Trim().Split(space, StringSplitOptions.RemoveEmptyEntries);
            arg0 = arg1 = arg2 = arg3 = arg4 = arg5 = arg6 = null;

            var idx = 0;
            if (parts.Length > idx)
                arg0 = parts[idx++];
            if (parts.Length > idx)
                arg1 = parts[idx++];
            if (parts.Length > idx)
                arg2 = parts[idx++];
            if (parts.Length > idx)
                arg3 = parts[idx++];
            if (parts.Length > idx)
                arg4 = parts[idx++];
            if (parts.Length > idx)
                arg5 = parts[idx++];
            return new ArgumentList(line, parts, arg0, arg1, arg2, arg3, arg4, arg5, arg6);
        }
    }

    public class DriverOptions
    {
        public DriverOptions()
        {
        }

        public void Execute()
        {
            Actions.ForEach(x => x());
        }
        public List<Action> Actions;
    }

    public class SpreadOptions
    {
        public OrderSide OrderSide { get; set; }
        public ProductType ProductType { get; set; }
        public decimal StartPrice { get; set; }
        public decimal OrderSize { get; set; }
        public decimal Increment { get; set; }
        public decimal TotalAmount { get; set; }

        internal IEnumerable<Action> GetOrderActions()
        {

            var actions = new List<Action>();
            var currentPrice = StartPrice;
            var amountRemaining = TotalAmount;

            while (amountRemaining > 0)
            {
                var order = OrderService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, OrderSize);
                amountRemaining -= order.TotalAmount;
                if (amountRemaining >= 0m && order.IsValid())
                {
                    actions.Add(() =>
                    {
                        OrderService.PlaceOrder(order);
                        Console.WriteLine($"{DateTime.Now}: Placed order {order}");
                    });
                }

                else if (actions.Count > 0)
                {
                    amountRemaining += order.TotalAmount;
                    actions.RemoveAt(actions.Count - 1);
                    if (order.OrderSide == OrderSide.Sell)
                    {
                        order = OrderService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, OrderSize + amountRemaining);
                    }
                    else
                    {
                        var newSize = Math.Round(amountRemaining / currentPrice, 8);
                        order = OrderService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, newSize);
                        while (order.TotalAmount > amountRemaining)
                        {
                            newSize -= 0.00000001m;
                            order = OrderService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, newSize);
                        }

                    }
                    amountRemaining -= order.TotalAmount;
                    if (amountRemaining >= 0m && order.IsValid())
                    {
                        actions.Add(() =>
                        {
                            OrderService.PlaceOrder(order);
                            Console.WriteLine($"{DateTime.Now}: Placed order {order}");
                        });
                        break;
                    }

                }
                currentPrice += (OrderSide == OrderSide.Buy) ? (-Increment) : Increment;
            }
            return actions;
        }
    }
    public class ArgumentList
    {
        public string line;
        public string[] parts;
        public string arg0;
        public string arg1;
        public string arg2;
        public string arg3;
        public string arg4;
        public string arg5;
        public string arg6;
        public int Length => parts.Length;

        public string this[int index] => parts[index];

        public ArgumentList(string line, string[] parts, string arg0, string arg1, string arg2, string arg3, string arg4, string arg5, string arg6)
        {

            this.line = line;
            this.parts = parts;
            this.arg0 = arg0;
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.arg4 = arg4;
            this.arg5 = arg5;
            this.arg6 = arg6;
        }

        public Action InvalidAction() => InvalidAction($"Invalid arguments: {this.line}");

        public Action InvalidAction(string message)
        {
            return () => Console.WriteLine(message);
        }
    }



    public class OrderManager : CoinbaseService
    {
        public static Dictionary<Guid, OrderResponse> BuyOrders;
        public static Dictionary<Guid, OrderResponse> SellOrders;
        public static Dictionary<Guid, OrderResponse> AllOrders;
        public OrderManager()
        {

        }
        public static void Refresh()
        {
            var svc = new CoinbaseService();
            var statusList = new[] { OrderStatus.Active, OrderStatus.Open, OrderStatus.Pending, };
            var orders = svc.client.OrdersService.GetAllOrdersAsync(statusList).Result.SelectMany(x => x).ToList();
            AllOrders = orders.ToDictionary(x => x.Id, x => x);
            BuyOrders = orders.Where(x => x.Side == OrderSide.Buy).OrderBy(x => x.Price)
                .ToDictionary(x => x.Id, x => x);
            SellOrders = orders.Where(x => x.Side == OrderSide.Sell).OrderBy(x => x.Price)
                .ToDictionary(x => x.Id, x => x);
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


    }

    public class OrderService : CoinbaseService
    {
        public static OrderService service = new OrderService();
        public static List<Guid> CancelAllOrders()
        {
            var task = service.client.OrdersService.CancelAllOrdersAsync();
            var response = task.Result;
            return response.OrderIds.ToList();
        }
        public static IEnumerable<Guid> CancelAllOrders(Func<OrderResponse, bool> filter)
        {
            var orders = GetAllOrders();
            var cancellations = orders.Where(filter).ToList();
            return CancelOrders(cancellations.Select(x => x.Id));
        }

        public static IEnumerable<Guid> CancelAllSells()
            => CancelAllOrders(x => x.Side == OrderSide.Sell);

        public static IEnumerable<Guid> CancelAllBuys()
      => CancelAllOrders(x => x.Side == OrderSide.Buy);

        public static IEnumerable<Guid> CancelOrders(IEnumerable<Guid> orderIds)
        {
            foreach (var orderId in orderIds)
            {
                yield return CancelOrderById(orderId);
            }
        }

        private static Guid CancelOrderById(Guid orderId)
        {
            var task = service.client.OrdersService.CancelOrderByIdAsync(orderId.ToString());
            var result = task.Result;
            return result.OrderIds.First();
        }

        public static List<OrderResponse> GetAllOrders()
        {
            var task = service.client.OrdersService.GetAllOrdersAsync();
            var result = task.Result;
            return result.SelectMany(lst => lst.Select(x => x)).ToList();
        }

        internal static Guid CancelBuy(int cancelBuyIndex)
        {
            Guid orderId = OrderManager.GetBuyOrderIdByIndex(cancelBuyIndex);
            return CancelOrderById(orderId);
        }

        internal static Guid CancelSell(int cancelSellIndex)
        {
            Guid orderId = OrderManager.GetSellOrderIdByIndex(cancelSellIndex);
            return CancelOrderById(orderId);
        }

        internal static TradeOrder CreatePostOnlyOrder(ProductType productType, OrderSide orderSide, decimal currentPrice, decimal orderSize)
        {
            return OrderHelper.CreatePostOnlyOrder(productType, orderSide, currentPrice, orderSize);
        }

        public static OrderResponse PlaceOrder(TradeOrder order)
        {
            if (order.Market)
            {
                return PlaceMarketOrder(order);
            }
            else
            {
                return PlaceLimitOrder(order);
            }

        }

        public static OrderResponse PlaceLimitOrder(TradeOrder order)
        {
            if (order.OrderSide == OrderSide.Buy)
            {
                return PlaceLimitBuyOrder(order);
            }
            else
            {
                return PlaceLimitSellOrder(order);
            }
        }

        public static OrderResponse PlaceLimitSellOrder(TradeOrder order)
        {
            var t =
                  service.client.OrdersService
                  .PlaceLimitOrderAsync(order.OrderSide, order.ProductType, order.OrderSize, order.Price,
                  clientOid: order.ClientId);
            return t.Result;

        }

        public static OrderResponse PlaceLimitBuyOrder(TradeOrder order)
        {
            var t =
              service.client.OrdersService
              .PlaceLimitOrderAsync(order.OrderSide, order.ProductType, order.OrderSize, order.Price,
              clientOid: order.ClientId);
            return t.Result;
        }

        public static OrderResponse PlaceMarketOrder(TradeOrder order)
        {
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
            throw new NotImplementedException();
        }

        public static OrderResponse PlaceMarketBuyOrder(TradeOrder order)
        {
            throw new NotImplementedException();
        }

    }

    public class OrderHelper
    {
        public static TradeOrder CreatePostOnlyOrder(ProductType productType, OrderSide orderSide, decimal price, decimal orderSize)
        {
            var service = new AccountService();
            decimal feerate = service.MakerFeeRate;
            decimal total = price * orderSize;
            decimal fee = total * feerate;
            var totalAmount = total + fee;
            return new TradeOrder(productType, orderSide, price, orderSize, fee, totalAmount);

        }
    }

    public class TradeOrder
    {
        public ProductType ProductType { get; set; }
        public OrderSide OrderSide { get; set; }
        public decimal Price { get; set; }
        public decimal OrderSize { get; set; }
        public decimal Fee { get; set; }
        public decimal TotalAmount { get; set; }

        public bool Market { get; set; }

        public Guid ClientId { get; set; }
        public TradeOrder(ProductType productType, OrderSide orderSide, decimal price, decimal orderSize, decimal fee, decimal totalAmount)
        {
            this.ProductType = productType;
            this.OrderSide = orderSide;
            this.Price = price;
            this.OrderSize = orderSize;
            this.Fee = fee;
            this.TotalAmount = totalAmount;
            this.ClientId = Guid.NewGuid();
        }


        public override string ToString()
        {
            return $"{ProductType} {OrderSide} {OrderSize} @ {Price} = {TotalAmount}";
        }
        internal bool IsValid()
        {
            return TotalAmount >= 10;
            //throw new NotImplementedException();
        }
    }

}
