using CoinbasePro.Services.Accounts.Models;
using CoinbasePro.Services.Fills.Models.Responses;
using CoinbasePro.Services.Orders.Models.Responses;
using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Services.Payments.Models;
using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using CoinbasePro.WebSocket;
using CoinbasePro.WebSocket.Models.Response;
using CoinbasePro.WebSocket.Types;
using CoinbaseUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoinbaseConsole
{


    public class ConsoleDriver : IConsoleDriver
    {
        private StreamWriter log;
        const string tab = "    ";
        const string tabbracket = tab + "->";
        static char[] space = " ".ToCharArray();
        public static ProductType DriverDefaultProductType = ProductType.LtcUsd;
        public static ProductType DefaultProductType => DriverDefaultProductType;

        public ProductType ProductType { get; private set; }
        public ConsoleDriver() : this(DriverDefaultProductType) { }

        public OrderSyncer OrderSyncer;
        public Dictionary<string, object> Session;
        public ConsoleDriver(ProductType productType)
        {
            Session = new Dictionary<string, object>();
            SetProductType(productType);
            log = new StreamWriter("ConsoleDriver.log", true);
            log.AutoFlush = true;
            OrderSyncer = new OrderSyncer(this);
        }

        public Func<Ticker, String> FormatTicker => (ticker) =>
            $"{DateTime.Now}: {ticker.ProductId}: {ticker.BestBid}-{ticker.BestAsk}";


        public CoinbaseTicker ticker;
        private CoinbaseWebSocket userFeed;

        public Product Product;
        void SetProductType(ProductType productType)
        {
            this.ProductType = productType;
            this.Product = OrdersService.GetProduct(productType);
            Console.WriteLine($"{tabbracket}Set Product: {productType}");
            SetTicker(productType);
            if (profitMeter != null)
            {
                profitMeter.Stop();
                var svc = new CoinbaseService();
                var fillsLists = svc.client.FillsService.GetFillsByProductIdAsync(ProductType, 30, 1).Result;
                profitMeter = new ProfitMeter(this);
                if (fillsLists.Count > 0)
                {
                    var fills = fillsLists[0];
                    var fill = fills[0];
                    profitMeter.LastPrice = fill.Price;
                    profitMeter.Side = fill.Side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
                }
            }
        }

        public Ticker LastTicker { get; internal set; } = null;
        public ProfitMeter profitMeter { get; private set; }

        private List<ProductType> ProductSubscriptions = null;
        void SetTicker(ProductType productType)
        {
            this.ProductType = productType;
            if (ticker != null)
            {
                ticker.OnTickerReceived -= Ticker_OnTickerReceived;
                ticker.Stop();
                ticker = null;
            }

            ticker = CoinbaseTicker.Create(productType);
            ticker.OnTickerReceived += Ticker_OnTickerReceived;

            ProductSubscriptions = ProductSubscriptions ?? new List<ProductType>();
            if (ProductSubscriptions.Contains(productType))
            {

            }
            if (!ProductSubscriptions.Contains(productType))
            {
                SetProductFeeds(ProductSubscriptions.Concat(new[] { productType }).ToList());
            }

        }

        public void SetProductFeeds(IEnumerable<ProductType> productTypes)
        {
            ProductSubscriptions = ProductSubscriptions ?? new List<ProductType>();
            if (!ProductSubscriptions.OrderBy(x => x).SequenceEqual(productTypes.OrderBy(x => x)))
            {
                lock (ProductSubscriptions)
                {
                    ProductSubscriptions.Clear();
                    ProductSubscriptions.AddRange(productTypes);
                }
                this.userFeed?.Stop();
                this.userFeed?.Start(ProductSubscriptions, (new[] { ChannelType.User }).ToList());
            }

        }

        public void Run()
        {

            CreateCoinbaseWebSocket();
            if (File.Exists("consoledriver.ini"))
            {
                var lines = File.ReadAllLines("consoledriver.ini").Where(x => !string.IsNullOrEmpty(x)).ToList(); ;
                Console.WriteLine("Found 'consoledriver.ini'. Execute? ([y]es or [n]o)");
                lines.ForEach(x => Console.WriteLine($"{tabbracket} {x}"));
                var result = Console.ReadLine().Trim().ToLower();
                if (result == "y" || result == "yes")
                {
                    lines.ForEach(x =>
                    {
                        Console.WriteLine(x);
                        if (!x.StartsWith("#"))
                        {
                            ParseArguments(x).Execute();
                            System.Threading.Thread.Sleep(200);
                        }

                    });
                }
            }
            while (true)
            {
                var line = Console.ReadLine();
                var args = ParseArguments(line);
                if (args.Actions.Count == 0)
                {
                    Console.WriteLine("invalid actions");
                }
                args.Execute();

            }

        }

        private void Ticker_OnTickerReceived(object sender, WebfeedEventArgs<Ticker> e)
        {
            if (this.profitMeter == null)
                Console.Title = FormatTicker(LastTicker = e.LastOrder);
        }

        private ConcurrentDictionary<Guid, bool> doneOrders = new ConcurrentDictionary<Guid, bool>();
        private ConcurrentDictionary<Guid, bool> recievedOrders = new ConcurrentDictionary<Guid, bool>();
        private ConcurrentDictionary<Guid, bool> openedOrders = new ConcurrentDictionary<Guid, bool>();
        private ConcurrentDictionary<long, bool> matchedOrders = new ConcurrentDictionary<long, bool>();
        public WebSocketFeedLogger SocketLogger;
        private void CreateCoinbaseWebSocket()
        {
            this.SocketLogger = new WebSocketFeedLogger();
            if (this.userFeed != null)
            {
                this.userFeed.Stop();
                userFeed.OnOpenReceived -= UserFeed_OnOpenReceived;
                userFeed.OnReceivedReceived -= UserFeed_OnReceivedReceived;
                userFeed.OnMatchReceived -= UserFeed_OnMatchReceived;
                userFeed.OnDoneReceived -= UserFeed_OnDoneReceived;
                this.userFeed = null;
            }
            this.userFeed = new CoinbaseWebSocket();
            this.userFeed.OnWebSocketOpenAndSubscribed += UserFeed_OnWebSocketOpenAndSubscribed;
            this.userFeed.OnWebSocketClose += UserFeed_OnWebSocketClose;
            this.userFeed.Start((new[] { ProductType.LtcUsd }).ToList(), (new[] { ChannelType.User }).ToList());

            OrderManager.Refresh();

            userFeed.OnSubscriptionReceived += (sender, e) =>
            {
                string message = $"[{DateTime.Now}] {e.LastOrder.ToJson()}";
                //Console.WriteLine();
                if (bool.Parse(bool.FalseString)) log.WriteLine(message);

            };

            userFeed.OnOpenReceived += UserFeed_OnOpenReceived;
            userFeed.OnReceivedReceived += UserFeed_OnReceivedReceived;
            userFeed.OnMatchReceived += UserFeed_OnMatchReceived;
            userFeed.OnDoneReceived += UserFeed_OnDoneReceived;


        }

        private void UserFeed_OnOpenReceived(object sender, WebfeedEventArgs<Open> e)
        {
            if (!openedOrders.TryAdd(e.LastOrder.OrderId, true))
            {
                openedOrders.Clear();
                return;
            }
            else
            {
                openedOrders.Clear();
            }
            OrderManager.SetDirty();

            //if (profitMeter != null && e.LastOrder.ProductId == profitMeter.ProductType)
            //    profitMeter.Side = e.LastOrder.Side;
            string message = $"[{DateTime.Now}] {e.LastOrder.ToDebugString()}";
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(message);
            Console.ResetColor();
            log.WriteLine(message);
            System.Threading.Thread.Sleep(250);// avoid Private rate limit exceeded message
            OrderManager.AddOrUpdate(e.LastOrder);
        }

        private void UserFeed_OnReceivedReceived(object sender, WebfeedEventArgs<Received> e)
        {
            if (!recievedOrders.TryAdd(e.LastOrder.OrderId, true))
            {
                recievedOrders.Clear();
                return;
            }
            else
            {
                recievedOrders.Clear();
            }
            OrderManager.SetDirty();
            //if (profitMeter != null && e.LastOrder.ProductId == profitMeter.ProductType)
            //    profitMeter.Side = e.LastOrder.Side;
            string message = $"[{DateTime.Now}] {e.LastOrder.ToDebugString()}";
            log.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(message);
            Console.ResetColor();
            OrderManager.AddOrUpdate(e.LastOrder);
        }

        private void UserFeed_OnMatchReceived(object sender, WebfeedEventArgs<Match> e)
        {
            if (!matchedOrders.TryAdd(e.LastOrder.Sequence, true))
            {
                matchedOrders.Clear();
                return;
            }
            else
            {
                matchedOrders.Clear();
            }
            OrderManager.SetDirty();
            FillsManager.MarkUpdated(ProductType);
            if (profitMeter != null && e.LastOrder.ProductId == ProductType)
            {
                var x = e.LastOrder;
                TimedOptions options = null;
                if (this.Session.ContainsKey(nameof(TimedOptions)))
                {
                    options = this.Session[nameof(TimedOptions)] as TimedOptions;
                }
                OrderSide side;
                if (x.Side == OrderSide.Buy)
                {
                    if (x.MakerUserId == null)//limit buy
                    {
                        side = OrderSide.Buy;
                    }
                    else
                    {
                        side = OrderSide.Sell; // maker of buy
                    }
                }
                else
                {
                    if (x.MakerUserId == null) // limit sell
                    {
                        side = OrderSide.Sell;
                    }
                    else
                    {
                        side = OrderSide.Buy; // market sell
                    }
                }


                if (this.Session.ContainsKey(nameof(TimedOptions)))
                {
                    options = this.Session[nameof(TimedOptions)] as TimedOptions;
                }
                if (options == null)
                {
                    if (side == OrderSide.Buy)
                    {
                        profitMeter.Side = OrderSide.Sell;
                    }
                    else
                    {
                        profitMeter.Side = OrderSide.Buy;
                    }
                }
                else
                {
                    if (!options.Started)
                    {
                        profitMeter.LastPrice = e.LastOrder.Price;
                    }
                }

            }


            var myOrder = OrderManager.TryGetByFirstIds(e.LastOrder.MakerOrderId, e.LastOrder.TakerOrderId);
            string message = $"[{DateTime.Now}] {e.LastOrder.ToDebugString()}";
            log.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private void UserFeed_OnDoneReceived(object sender, WebfeedEventArgs<Done> e)
        {

            if (!doneOrders.TryAdd(e.LastOrder.OrderId, true))
            {
                doneOrders.Clear();
                return;
            }
            else
            {
                doneOrders.Clear();
            }
            OrderManager.SetDirty();
            FillsManager.MarkUpdated(ProductType);
            string message = $"[{DateTime.Now}] {e.LastOrder.ToDebugString()}";
            log.WriteLine(message);
            Console.ForegroundColor = (e.LastOrder.Reason != DoneReasonType.Canceled) ? ConsoleColor.Gray : ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
            OrderManager.AddOrUpdate(e.LastOrder);
            if (e.LastOrder.Reason != DoneReasonType.Canceled && e.LastOrder.ProductId == ProductType)
            {
                TimedOptions options = null;
                if (this.Session.ContainsKey(nameof(TimedOptions)))
                {
                    options = this.Session[nameof(TimedOptions)] as TimedOptions;
                }
                if (e.LastOrder.Side == OrderSide.Sell)
                {
                    if (profitMeter != null)
                        profitMeter.Side = OrderSide.Buy;
                    if (options != null && options.OrderSide == OrderSide.Sell)
                    {

                        if (options.TimedFlipPct != 0 && options.Started)
                        {
                            var args = ParseArguments($"flip {options.TimedFlipPct.ToString("P")}");
                            args.Execute();
                        }
                        else
                        {
                            var args = ParseArguments("Last");
                            args.Execute();
                        }

                    }
                    else
                    {
                        bool AutoFlipSells = Session.ContainsKey(nameof(AutoFlipSells)) && (bool)Session[nameof(AutoFlipSells)] == true;
                        if (AutoFlipSells && options != null && options.OrderSide == OrderSide.Buy && options.TimedFlipPct != 0 && options.Started)
                        {
                            var order = OrdersService.GetOrderById(e.LastOrder.OrderId);
                            var flipPrice = order.Price;
                            var target = order.Price * (1m - (options.TimedFlipPct));
                            var value = order.ExecutedValue - order.FillFees;
                            var qty = value / target;
                            if (qty < Product.BaseMinSize)
                                qty = Product.BaseMinSize;
                            var buyOrder = OrderHelper.CreatePostOnlyOrder(ProductType, OrderSide.Buy, target, qty);
                            
                            OrdersService.PlaceLimitBuyOrder(buyOrder);
                            Console.ForegroundColor = ConsoleColor.DarkCyan;
                            Console.WriteLine($"[{DateTime.Now}] Flipped order {order.ToDebugString()} to buy {buyOrder.OrderSize} @ {buyOrder.Price}");
                            Console.ResetColor();
                        }
                        //ParseArguments("cancel all").Execute();
                        if (!Session.ContainsKey("TimedAutoBuy") || (bool)Session["TimedAutoBuy"] == true)
                            ParseArguments("buy 10").Execute();
                        var args = ParseArguments("Last");
                        args.Execute();
                    }

                }
                if (e.LastOrder.Side == OrderSide.Buy)
                {
                    if (profitMeter != null)
                        profitMeter.Side = OrderSide.Sell;
                    System.Threading.Thread.Sleep(100);
                    if (options != null && options.OrderSide == OrderSide.Buy)
                    {
                        var last = FillsManager.GetLast(ProductType, LastSide.Any);

                        if (last.Side == OrderSide.Buy)
                        {
                            var args = ParseArguments($"flip {options.TimedFlipPct.ToString("P")} delay 250");
                            args.Execute();
                        }

                    }
                    else
                    {
                        var args = ParseArguments("Last");
                        args.Execute();
                    }
                }
                if (options?.OrderSide != e.LastOrder.Side)
                {
                    LogBalance($"{e.LastOrder.ToDebugString()}");
                    ParseArguments("orders").Execute();
                    ParseArguments("last").Execute();
                }
            }
        }


        private void UserFeed_OnWebSocketClose(object sender, WebfeedEventArgs<EventArgs> e)
        {
            WebSocket4Net.WebSocket sock = (WebSocket4Net.WebSocket)sender;
            sock.MessageReceived -= WebSocketFeed_MessageReceived;
        }

        private void UserFeed_OnWebSocketOpenAndSubscribed(object sender, WebfeedEventArgs<EventArgs> e)
        {
            WebSocket4Net.WebSocket sock = (WebSocket4Net.WebSocket)sender;
            sock.MessageReceived += WebSocketFeed_MessageReceived;
        }

        private void WebSocketFeed_MessageReceived(object sender, global::WebSocket4Net.MessageReceivedEventArgs e)
        {
            this.SocketLogger.LogMessageRecieved(e);
        }


        private ProductTicker GetProductTicker(ProductType productType)
        {
            var svc = new CoinbaseService();
            return svc.client.ProductsService.GetProductTickerAsync(productType).GetAwaiter().GetResult();
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
            result.Arguments = arguments;
            ProductType productType = ProductType;// DefaultProductType;

            List<Action> commandActions = new List<Action>();
            if (arg0 == "cancel")
            {
                commandActions.AddRange(ParseCancellationOptions(arguments));
                //cancel all, cancel buys, cancel sells, cancel all buys, cancel all sells
            }
            else if ((arg1 == "cancel" || arg1 == "c") && (arg0 == "buys" || arg0 == "sells")
                || ((arg0 == "buys" || arg0 == "sells") && (arg1 == "last" || arg1 == "first" || arg1.ToIndex() != -1) && (arg2.ParseDecimal(out decimal newPrice) || arg2 == "cancel")))
            {
                line = $"cancel {arg0}";
                if ((arg1 == "last" || arg1 == "first" || arg1.ToIndex() != -1))
                {
                    line = $"{line} {arg1}";
                    if (arg2 != "cancel")
                    {
                        line = $"{line} {arg2}";
                    }
                }
                else
                {
                    if (arg2 == "c" || arg1 == "c") line = $"{line} c";
                    if (arg2 == "last" || arg2 == "first" || arg2.ToIndex() > -1) line = $"{line} {arg2}";
                }

                arguments = ReadArgs(line,
                out arg0,
                out arg1,
                out arg2,
                out arg3,
                out arg4,
                out arg5,
                out arg6);
                commandActions.AddRange(ParseCancellationOptions(arguments));
            }
            else if (arg0 == "allin")
            {
                commandActions.Add(() => GoAllIn());
            }
            else if (arg0 == "dump")
            {
                commandActions.Add(() => Dump());
            }
            else if (arg0 == "session")
            {
                if (arg1 == "load")
                {
                    Session["loading"] = true;
                    if (File.Exists($"session-{productType}.json"))
                    {
                        var json = File.ReadAllText($"session-{productType}.json");
                        var d = json.FromJson<Dictionary<string, object>>();
                        foreach (var key in d.Keys)
                        {
                            var value = d[key];
                            var valueType = value.GetType().Name;
                            switch (valueType)
                            {
                                case "Int32":
                                case "String":
                                case "Decimal":
                                case "Boolean":
                                case "DateTime":
                                    Session[key] = value;
                                    break;
                                case "Double":
                                    Session[key] = (decimal)(double)value;
                                    break;
                                default:
                                    switch (key)
                                    {
                                        case "flip":
                                            {
                                                var flipObject = (Newtonsoft.Json.Linq.JObject)value;
                                                var flipLine = flipObject["line"];
                                                var flipArgs = flipLine.ToString();
                                                Session["flip"] = ParseArguments(flipArgs).Arguments;
                                            }
                                            break;
                                        case "TimedOptions":
                                            {
                                                decimal TimedAmount = 0m;
                                                int TimedInterval = 0;
                                                OrderSide TimedSide = OrderSide.Buy;
                                                bool TimedStarted = false;
                                                decimal TimedFlipPCT = .01m;
                                                var timedObject = (Newtonsoft.Json.Linq.JObject)value;
                                                timedObject["Amount"].ToString().ParseDecimal(out TimedAmount);
                                                int.TryParse(timedObject["Interval"].ToString(), out TimedInterval);
                                                timedObject["OrderSide"].ToString().ParseEnum<OrderSide>(out TimedSide);
                                                timedObject["TimedFlipPct"].ToString().ParseDecimal(out TimedFlipPCT);
                                                bool.TryParse(timedObject["Started"].ToString(), out TimedStarted);
                                                var timedLine = $"timed {TimedSide} {TimedAmount} {TimedInterval} {TimedFlipPCT.ToString("P4")}";
                                                ParseArguments(timedLine).Execute();
                                                if (!TimedStarted)
                                                {
                                                    ParseArguments("timed stop").Execute();
                                                }
                                            }
                                            break;
                                        default:
                                            string bp = "";
                                            Console.WriteLine($"Invalid session value: {key} {value}");
                                            break;
                                    }
                                    break;

                            }
                        }
                        Session["loading"] = false;
                        SaveSession();
                        commandActions.Add(() => Console.WriteLine("Loaded Session"));
                        ParseArguments("Session").Execute();
                    }
                }
                else
                {
                    var copy = Session.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                    var json = copy.ToJson();
                    File.WriteAllText($"session-{productType}.json", json);
                    commandActions.Add(() => Console.WriteLine($"[{DateTime.Now}] Session:{Environment.NewLine} {Session.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value).ToJson()}"));
                }
            }
            else if (arg0 == "cls")
            {
                commandActions.Add(() => Console.Clear());
            }
            else if (arg0 == "order")
            {
                Guid orderId = Guid.Empty;
                if (Guid.TryParse(arg1, out orderId))
                {
                    var order = OrdersService.GetOrderById(orderId);
                    Console.WriteLine($"order: {order.ToDebugString()}");
                }          
                commandActions.Add(() => { });
            }
            else if (arg0 == "timed")
            {
                commandActions.AddRange(ParseTimedOptions(arguments));
            }
            else if (arg0 == "fees")
            {
                commandActions.AddRange(ParseFeeOptions(arguments));
            }
            else if (arg0 == "volume")
            {
                commandActions.AddRange(ParseVolumeOptions(arguments));
            }
            else if (arg0 == "matchqueue")
            {
                commandActions.AddRange(ParseMatchQueueOptions(arguments));
            }
            else if (arg0 == "tickers")
            {
                ProductSubscriptions.ForEach(product =>
                {
                    var ticker = GetProductTicker(product);
                    commandActions.Add(() => Console.WriteLine($"\t{product.ToString().PadRight(9, ' ')}Last\t{ticker.Price.ToString().PadRight(11, ' ')}Bid\t{ticker.Bid.ToString().PadRight(11, ' ')}Ask\t{ticker.Ask.ToString().PadRight(11, ' ')}"));
                });
            }
            else if (arg0 == "stop")
            {
                commandActions.AddRange(ParseStopOptions(arguments));
            }
            else if (arg0 == "ticker")
            {
                if (arguments.Length == 1)
                {


                    commandActions.Add(() =>
                    {
                        if (LastTicker != null)
                            Console.WriteLine(JsonConvert.SerializeObject(LastTicker));
                    });
                }
                else
                {
                    //if (arguments[idx].ParseEnum<ProductType>(out ProductType productType))
                    //{
                    //    idx++;
                    //    SetProductType(productType);
                    //}
                    if (arg1.ParseEnum<ProductType>(out ProductType parsed))
                    {
                        var svc = new CoinbaseService();
                        var ticker = svc.client.ProductsService.GetProductTickerAsync(parsed).GetAwaiter().GetResult();
                        commandActions.Add(() => Console.WriteLine(JsonConvert.SerializeObject(ticker, Formatting.Indented)));
                    }

                }
            }
            else if (arg0 == "consolidate" || arg0 == "c")
            {
                const string consolidateUsage = "usage: consolidate [buys|sells] precision";
                if (arg1.TrimEnd('s').ParseEnum<OrderSide>(out OrderSide side) && arg2.ParseDecimal(out decimal precision))
                {
                    if (side == OrderSide.Buy)
                    {
                        if (precision < Product.QuoteIncrement) precision = Product.QuoteIncrement;
                        decimal TimedConsolidate = precision;
                        Session[nameof(TimedConsolidate)] = TimedConsolidate;
                        string bp = "";
                        OrderManager.Refresh();
                        var buys = OrderManager.BuyOrders.Values.OrderBy(x => x.Price).ToList();
                        if (buys.Count < 2)
                        {
                            Console.WriteLine($"[{DateTime.Now}] Nothing to consolidated.");
                            commandActions.Add(() => { });
                            goto Done;
                        }
                        var strike = buys.First().Price - (buys.First().Price % precision);
                        bool done = false;
                        var idx = 0;
                        var limit = strike + precision;
                        int consolidated = 0;
                        while (!done)
                        {
                            var l = (new[] { buys[idx] }).ToList();
                            while (limit <= l[0].Price)
                            {
                                strike = limit;
                                limit += precision;
                            }
                            for (; idx + 1 < buys.Count && buys[++idx].Price < limit;)
                            {
                                l.Add(buys[idx]);
                            }
                            if (l.Count > 1)
                            {
                                //consolidate l
                                var qty = 0m;
                                var total = 0m;
                                foreach (var o in l)
                                {
                                    var orderSize = (o.Size - o.FilledSize);
                                    qty += orderSize;
                                    total += (o.Price * orderSize);
                                }

                                var avgPrice = total / qty;
                                if (avgPrice >= strike & avgPrice < limit)
                                {
                                    consolidated += l.Count;
                                    var cancelIds = l.Select(x => x.Id).ToList();
                                    var cancelled = OrdersService.CancelOrders(cancelIds).ToList();
                                    var price = avgPrice.ToPrecision(Product.QuoteIncrement);
                                    var size = qty.ToPrecision(Product.BaseIncrement);
                                    var order = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Buy, price, size);
                                    OrdersService.PlaceLimitBuyOrder(order);
                                }
                                else
                                {
                                    result.Actions.Add(() => Console.WriteLine($"Failed to consolide buys."));
                                }

                            }
                            strike = limit;
                            limit += precision;

                            done = idx >= buys.Count - 1;
                        }
                        Console.WriteLine($"[{DateTime.Now}] Consolidated {consolidated} {(consolidated == 1 ? "order" : "orders")}.");
                        commandActions.Add(() => { });
                    }
                    else
                    {
                        string bp = "";
                    }
                }
                else
                {
                    commandActions.Add(arguments.InvalidAction(consolidateUsage));
                }
            }
            else if (arg0 == "buy" && arg1.ParseDecimal(out decimal buyOrderFundsSize))
            {
                // buyorder

                if (arg2 == null || arg2 == "market")
                {
                    var size = buyOrderFundsSize;
                    if (size < Product.MinMarketFunds)
                        size = Product.MinMarketFunds;
                    var orderResult = OrdersService.PlaceMarketFundsBuyOrder(productType, size);
                }
                else if (arg2 == "limit")
                {
                    if (arg4 != null)
                    {
                        commandActions.Add(arguments.InvalidAction());
                        goto Done;
                    }
                    decimal price = LastTicker.BestBid;
                    if (arg3 != null && !decimal.TryParse(arg3, out price))
                    {
                        commandActions.Add(arguments.InvalidAction());
                        goto Done;
                    }
                    var size = buyOrderFundsSize / price;
                    if (size < Product.BaseMinSize)
                        if (size < Product.BaseMinSize) size = Product.BaseMinSize;
                    var order = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Buy, price, size);
                    var orderResult = OrdersService.PlaceLimitBuyOrder(order, this);
                }
                else if (arg2.IndexOf("%") > -1 && arg2.TryParsePercent(out decimal buyPct))
                {
                    var price = LastTicker.BestBid;
                    price *= (1m - buyPct);

                    var size = buyOrderFundsSize / price;
                    if (size < Product.BaseMinSize)
                        if (size < Product.BaseMinSize) size = Product.BaseMinSize;
                    var order = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Buy, price, size);
                    var orderResult = OrdersService.PlaceLimitBuyOrder(order, this);
                }
                else if (arg2.ParseDecimal(out decimal price))
                {
                    if (arg3 != null)
                    {
                        commandActions.Add(arguments.InvalidAction());
                        goto Done;
                    }
                    var size = buyOrderFundsSize / price;
                    if (size < Product.BaseMinSize)
                        if (size < Product.BaseMinSize) size = Product.BaseMinSize;
                    var order = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Buy, price, size);
                    var orderResult = OrdersService.PlaceLimitBuyOrder(order, this);
                }
                else if (arg2 == "flip")
                {
                    if (!Session.ContainsKey("flip")) Session["flip"] = "flip 1%";
                    commandActions.AddRange(ParseSwingOptions((ArgumentList)Session["flip"]));
                }
                commandActions.Add(() => Console.WriteLine("Placed Order."));
            }
            else if (arg0 == "sell" && arg1.ParseDecimal(out decimal sellOrderSize))
            {
                if (arg2 == null || arg2 == "market")
                {
                    var orderResult = OrdersService.PlaceMarketFundsSellOrder(productType, sellOrderSize);
                }
                else if (arg2 == null || arg2 == "market")
                {
                    var orderResult = OrdersService.PlaceMarketFundsSellOrder(productType, sellOrderSize);
                }
                else if (arg2 == "limit")
                {
                    if (arg4 != null)
                    {
                        commandActions.Add(arguments.InvalidAction());
                        goto Done;
                    }
                    decimal price = LastTicker.BestAsk;
                    if (arg3 != null && !decimal.TryParse(arg3, out price))
                    {
                        commandActions.Add(arguments.InvalidAction());
                        goto Done;
                    }
                    var size = sellOrderSize / price;
                    if (size < Product.BaseMinSize)
                        if (size < Product.BaseMinSize) size = Product.BaseMinSize;
                    var order = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Sell, price, size);
                    var orderResult = OrdersService.PlaceLimitSellOrder(order, this);
                }
                else if (arg2.IndexOf("%") > -1 && arg2.TryParsePercent(out decimal sellPct))
                {
                    if (arg3 != null)
                    {
                        commandActions.Add(arguments.InvalidAction());
                        goto Done;
                    }
                    var price = LastTicker.BestBid;
                    price *= (1m + sellPct);

                    var size = sellOrderSize / price;
                    if (size < Product.BaseMinSize)
                        if (size < Product.BaseMinSize) size = Product.BaseMinSize;
                    var order = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Sell, price, size);
                    var orderResult = OrdersService.PlaceLimitSellOrder(order, this);
                }
                else if (arg2.ParseDecimal(out decimal limitPrice))
                {
                    if (arg3 != null)
                    {
                        commandActions.Add(arguments.InvalidAction());
                        goto Done;
                    }
                    var order = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Sell, limitPrice, sellOrderSize);
                    var orderResult = OrdersService.PlaceLimitSellOrder(order, this);
                }
                else if (arg2 == "flip")
                {
                    if (arg3 != null)
                    {
                        commandActions.Add(arguments.InvalidAction());
                        goto Done;
                    }
                    if (!Session.ContainsKey("flip")) Session["flip"] = "flip 1%";
                    commandActions.AddRange(ParseSwingOptions((ArgumentList)Session["flip"]));
                }
                else
                {
                    commandActions.Add(arguments.InvalidAction());
                }
                if (commandActions.Count == 0)
                    commandActions.Add(() => Console.WriteLine("Placed Order."));
            }
            else if (arg0 == "orders")
            {
                commandActions.AddRange(ParseOrdersOptions(arguments));
            }
            else if (arg0 == "fills" || arg0 == "last")
            {
                commandActions.AddRange(ParseFillsOptions(arguments));
            }
            else if (arg0 == "candles" || arg0 == "stats")
            {
                if (arg1 == "refresh" || arg1 == "update")
                {
                    Task.Run(() => CandleService.UpdateCandles(ProductType, true));
                    commandActions.Add(() => Console.WriteLine("Updating candles"));
                }
                else if (arg1 == "risk" || arg0 == "stats")
                {
                    var reader = new CandleDbReader(productType, CandleGranularity.Hour1);
                    var l = new List<Candle>();
                    decimal price = 0m;
                    if ((arg1 == "risk" && !arg2.ParseDecimal(out price)) || (arg0 == "stats" && !arg1.ParseDecimal(out price)))
                        price = LastTicker.Price;
                    foreach (var candle in reader)
                    {
                        if (price >= candle.Low && price <= candle.High)
                        {
                            l.Add(candle);
                        }
                    }
                    var groups = new List<List<Candle>>();
                    var current = new List<Candle>();
                    decimal upCount = 0;
                    decimal downCount = 0;
                    foreach (var candle in l)
                    {
                        if (current.Count == 0 || candle.Time <= current.Last().Time.AddHours(6))
                        {
                            current.Add(candle);
                        }
                        else
                        {
                            if (current.Last().Close.Value > price)
                                upCount++;
                            else
                                downCount++;
                            groups.Add(current.ToList());
                            current = new List<Candle>();
                        }
                    }
                    if (current.Count > 0)
                    {
                        if (current.Last().Close.Value > price)
                            upCount++;
                        else
                            downCount++;
                        groups.Add(current.ToList());
                    }

                    decimal ratioUp = upCount / groups.Count;
                    decimal ratioDown = downCount / groups.Count;
                    int groupNumber = 0;
                    var risks = groups.Select(x => x.Min(y => y.Low.Value)).ToList();
                    var rewards = groups.Select(x => x.Max(y => y.High.Value)).ToList();
                    var minRisk = groups.Min(x => x.Count > 0 ? x.Min(y => y.Low.Value) : 0m).ToPrecision(Product.QuoteIncrement);
                    var maxRisk = groups.Max(x => x.Count > 0 ? x.Max(y => y.Low.Value) : 0m).ToPrecision(Product.QuoteIncrement);
                    var minReward = groups.Min(x => x.Count > 0 ? x.Max(y => y.High.Value) : 0m).ToPrecision(Product.QuoteIncrement);
                    var maxReward = groups.Max(x => x.Count > 0 ? x.Max(y => y.High.Value) : 0m).ToPrecision(Product.QuoteIncrement);
                    var avgLow = groups.Average(x => x.Count > 0 ? x.Average(y => y.Low.Value) : 0m).ToPrecision(Product.QuoteIncrement);
                    var avgHigh = groups.Average(x => x.Count > 0 ? x.Average(y => y.High.Value) : 0m).ToPrecision(Product.QuoteIncrement);
                    var netLow = (avgLow - price).ToPrecision(Product.QuoteIncrement);
                    var netHigh = (avgHigh - price).ToPrecision(Product.QuoteIncrement);
                    var net = (netHigh + netLow).ToPrecision(Product.QuoteIncrement);
                    var downPct = ratioDown.ToString("P");

                    minRisk = risks.Min().ToPrecision(Product.QuoteIncrement);
                    maxRisk = risks.Max().ToPrecision(Product.QuoteIncrement);
                    minReward = rewards.Min().ToPrecision(Product.QuoteIncrement);
                    maxReward = rewards.Max().ToPrecision(Product.QuoteIncrement);

                    Console.WriteLine($"{price}: Net {net} Risk {avgLow} [{minRisk}-{maxRisk}] ({netLow} - {downPct}) Reward {avgHigh} [{minReward}-{maxReward}] ({netHigh} - {ratioUp.ToString("P")})");
                    foreach (var group in groups)
                    {
                        var risk = group.Min(x => x.Low).Value;
                        var reward = group.Max(x => x.High).Value;
                        var minDate = group.Min(x => x.Time);
                        var maxDate = group.Max(x => x.Time).AddHours(1);
                        var span = maxDate.Subtract(minDate).Hours;
                        var riskStr = risk.ToPrecision(Product.QuoteIncrement);
                        var rewardStr = reward.ToPrecision(Product.QuoteIncrement);
                        var close = group.Last().Close.Value;
                        var clostStr = close.ToPrecision(Product.QuoteIncrement);
                        string dir = close < price ? "D" : "U";
                        Console.WriteLine($"\t{riskStr}\t{rewardStr}\tClose: {dir} {clostStr}\t({minDate.ToString("MM/dd/yy HH")} - {span} hours)");
                    }

                    string bp = "";
                    commandActions.Add(() => { });
                }
                else
                {
                    commandActions.Add(arguments.InvalidAction("Usage: candles refresh"));
                }
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
            else if (arg0 == "flip" && arguments.Length == 1)
            {
                if (Session.ContainsKey("flip"))
                {
                    commandActions.AddRange(ParseSwingOptions((ArgumentList)Session["flip"]));
                }
                else
                {
                    commandActions.Add(() => Console.WriteLine("No flip."));
                }
            }
            else if (arg0 == "flip" && arg1 == "list")
            {
                if (Session.ContainsKey("flip"))
                {
                    commandActions.Add(() => Console.WriteLine(((ArgumentList)Session["flip"]).line));
                }
                else
                {
                    commandActions.Add(() => Console.WriteLine("No flip."));
                }
            }
            else if (arg0 == "swing" || arg0 == "flip")
            {
                commandActions.AddRange(ParseSwingOptions(arguments));
            }
            else if (arg0 == "feeds")
            {
                commandActions.Add((Action)(() => Console.WriteLine($"=> {string.Join(",", ProductSubscriptions)}")));
            }
            else if (arg0 == "profitmeter" || arg0 == "meter")
            {
                commandActions.AddRange(ParseProfitMeterOptions(arguments));
            }
            else
            {
                commandActions.Add(arguments.InvalidAction());
            }
            Done:
            result.Actions = commandActions;
            return result;

        }

        public class TimedOptions
        {
            public OrderSide OrderSide;
            public decimal Amount;
            public ProductType ProductType;
            public int Interval;
            public decimal TimedFlipPct;
            public bool Started;
            public override string ToString()
            {
                return $"timed {OrderSide} {Amount} {Interval} {TimedFlipPct.ToString("P")}  ({ProductType}, Running = {Started})";
            }
        }
        void SaveSession()
        {
            if (!Session.ContainsKey("loading") || (bool)Session["loading"] == false)
            {
                var copy = Session.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                var json = copy.ToJson();
                File.WriteAllText($"session-{ProductType}.json", json);
            }
        }
        private List<Action> ParseTimedOptions(ArgumentList arguments)
        {
            var result = new List<Action>();
            const string usage = tabbracket + "usage: timed [buy|sell] amount minuteInterval flipPercentage";

            if (arguments.arg1 == "limit")
            {
                decimal TimedLimitMaxPrice = 0m;
                if (arguments.arg2 == "null")
                {
                    if (Session.ContainsKey(nameof(TimedLimitMaxPrice)))
                    {
                        Session.Remove(nameof(TimedLimitMaxPrice));
                        Console.WriteLine($"Cleared {nameof(TimedLimitMaxPrice)}.");
                    }
                    else
                    {
                        Console.WriteLine($"{nameof(TimedLimitMaxPrice)} not set.");
                    }
                }
                else if (arguments.arg2.ParseDecimal(out TimedLimitMaxPrice))
                {
                    Session[nameof(TimedLimitMaxPrice)] = TimedLimitMaxPrice;
                    Console.WriteLine($"{nameof(TimedLimitMaxPrice)} = {TimedLimitMaxPrice}");

                }
                SaveSession();
                result.Add(() => { });
            }
            else if (arguments.arg1 == "madelta")
            {
                Task.Run(() =>
                {
                    CandleService.UpdateCandles(ProductType, true);
                    var previousCandles = CandleService.GetDbCandles(ProductType, DateTime.Now.AddMinutes(-240), DateTime.Now.AddMinutes(-120), CandleGranularity.Minutes1);
                    var currentCandles = CandleService.GetDbCandles(ProductType, DateTime.Now.AddMinutes(-120), DateTime.Now, CandleGranularity.Minutes1);
                    var previousMa = previousCandles.Average(x => x.Close).Value;
                    var ma = currentCandles.Average(x => x.Close).Value; ;
                    var delta = ma / previousMa;
                    Console.WriteLine($"Ma Delta {(1m - delta).ToString("p4")} - Previous: {previousMa.ToPrecision(Product.QuoteIncrement)} Current: {ma.ToPrecision(Product.QuoteIncrement)}");
                    if (arguments.arg2 == "set" || arguments.arg2 == "update")
                    {
                        var max = Math.Max(.001m, (1m - delta));
                        ParseArguments($"timed discount {(max).ToString("p4")}");
                    }
                });
                result.Add(() => { });
            }
            else if (arguments.arg1 == "market")
            {
                bool MarketOnWeighted = true;
                MarketOnWeighted = Session.ContainsKey(nameof(MarketOnWeighted)) && (bool)Session[nameof(MarketOnWeighted)] == true;
                if (arguments.arg2 == "null")
                {
                    if (Session.ContainsKey(nameof(MarketOnWeighted)))
                    {
                        Session.Remove(nameof(MarketOnWeighted));
                        Console.WriteLine($"[{DateTime.Now}] {nameof(MarketOnWeighted)}: null");
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now}] {nameof(MarketOnWeighted)} already null");
                    }
                }
                else if (arguments.arg2 == "false")
                {
                    Session[nameof(MarketOnWeighted)] = false;
                    Console.WriteLine($"[{DateTime.Now}] {nameof(MarketOnWeighted)}: {(bool)Session[nameof(MarketOnWeighted)]}");
                }
                else if (arguments.arg2 == "true")
                {
                    Session[nameof(MarketOnWeighted)] = true;
                    Console.WriteLine($"[{DateTime.Now}] {nameof(MarketOnWeighted)}: {(bool)Session[nameof(MarketOnWeighted)]}");
                }
                else if (arguments.arg2 == null || arguments.arg2 == "list")
                {
                    Console.WriteLine($"[{DateTime.Now}] {nameof(MarketOnWeighted)}: {(bool)Session[nameof(MarketOnWeighted)]}");
                }
                else
                {
                    Console.WriteLine($"Usage: timed market ([true|null|false]|[list])");
                }
                SaveSession();
                result.Add(() => { });
            }
            else if (arguments.arg1 == "dynamic")
            {
                bool TimedDynamic = false;
                TimedDynamic = Session.ContainsKey(nameof(TimedDynamic)) && (bool)Session[nameof(TimedDynamic)] == true;
                if (arguments.arg2 == "null" || arguments.arg2 == "false")
                {
                    if (Session.ContainsKey(nameof(TimedDynamic)))
                    {
                        Session.Remove(nameof(TimedDynamic));
                        Console.WriteLine("Cleared Dynamic Discount.");
                    }
                    else
                    {
                        Console.WriteLine("Dynamic Discount not set.");
                    }
                }
                else if (arguments.arg2 == "true")
                {
                    Session[nameof(TimedDynamic)] = true;
                    Console.WriteLine($"[{DateTime.Now}] {nameof(TimedDynamic)}: {(bool)Session[nameof(TimedDynamic)]}");
                }
                else if (arguments.arg2 == "list")
                {
                    Console.WriteLine($"[{DateTime.Now}] {nameof(TimedDynamic)}: {(bool)Session[nameof(TimedDynamic)]}");
                }
                else
                {
                    Console.WriteLine($"Usage: timed dynamic ([true|null|false]|[list])");
                }
                SaveSession();
                result.Add(() => { });
            }
            else if (arguments.arg1 == "flip")
            {
                decimal TimedFlipPct = 0m;
                if (!Session.ContainsKey("TimedOptions"))
                {
                    Console.WriteLine($"Timed not set.");
                }
                else if (arguments.arg2.TryParsePercent(out TimedFlipPct))
                {
                    var options = (TimedOptions)Session["TimedOptions"];
                    options.TimedFlipPct = TimedFlipPct;
                    Console.WriteLine($"{options}");
                    var side = options.OrderSide == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
                    ParseArguments($"flip {TimedFlipPct.ToString("P")}").Execute();
                }
                SaveSession();
                result.Add(() => { });
            }
            else if (arguments.arg1 == "amount")
            {
                decimal amount = 0m;
                if (!Session.ContainsKey("TimedOptions"))
                {
                    Console.WriteLine($"Timed not set.");
                }
                else if (arguments.arg2.ParseDecimal(out amount))
                {
                    var options = (TimedOptions)Session["TimedOptions"];
                    options.Amount = amount;
                    Console.WriteLine($"{options}");
                }
                SaveSession();
                result.Add(() => { });
            }
            else if (arguments.arg1 == "interval")
            {
                decimal interval = 0m;
                if (!Session.ContainsKey("TimedOptions"))
                {
                    Console.WriteLine($"Timed not set.");
                }
                else if (arguments.arg2.ParseDecimal(out interval))
                {
                    var options = (TimedOptions)Session["TimedOptions"];
                    options.Interval = (int)interval;
                    Console.WriteLine($"{options}");
                }
                SaveSession();
                result.Add(() => { });
            }
            else if (arguments.arg1 == "consolidate" || arguments.arg1 == "c")
            {
                decimal TimedConsolidate = 0m;
                if (arguments.arg2.ParseDecimal(out TimedConsolidate))
                {
                    Session[nameof(TimedConsolidate)] = TimedConsolidate;
                    SaveSession();
                    Console.WriteLine($"{nameof(TimedConsolidate)}: {TimedConsolidate.ToPrecision(Product.QuoteIncrement)}.");
                }
                else if (arguments.arg2 == "null")
                {
                    if (Session.ContainsKey(nameof(TimedConsolidate)))
                    {
                        Session.Remove(nameof(TimedConsolidate));
                        Console.WriteLine($"Cleared {nameof(TimedConsolidate)}.");
                    }
                    else
                    {
                        Console.WriteLine($"{nameof(TimedConsolidate)} not set.");
                    }
                }
                SaveSession();
                result.Add(() => { });
            }
            else if (arguments.arg1 == "discount")
            {
                decimal TimedDiscount = 0m;
                if (arguments.arg2.TryParsePercent(out TimedDiscount))
                {
                    Session[nameof(TimedDiscount)] = TimedDiscount;
                    Console.WriteLine($"{nameof(TimedDiscount)}: {TimedDiscount.ToString("P4")}.");
                }
                else if (arguments.arg2 == null && Session.ContainsKey(nameof(TimedDiscount)))
                {
                    TimedDiscount = (decimal)Session[nameof(TimedDiscount)];
                    Console.WriteLine($"{nameof(TimedDiscount)}: {TimedDiscount.ToString("P4")}");
                }
                else if (arguments.arg2 == "null")
                {
                    if (Session.ContainsKey(nameof(TimedDiscount)))
                    {
                        Session.Remove(nameof(TimedDiscount));
                        Console.WriteLine($"Cleared {nameof(TimedDiscount)}.");
                    }
                    else
                    {
                        Console.WriteLine($"{nameof(TimedDiscount)} not set.");
                    }
                }
                SaveSession();
                result.Add(() => { });
            }
            else if (arguments.arg1 == "autobuy")
            {

                if (arguments.arg2 == "true" || arguments.arg2 == null)
                {
                    Session["TimedAutoBuy"] = true;
                }
                else if (arguments.arg2 == "false" || arguments.arg2 == "null")
                {
                    Session["TimedAutoBuy"] = false;
                }
                else
                {
                    result.Add(() => Console.WriteLine("usage: timed auotobuy [true|false]"));
                }
                SaveSession();
                result.Add(() => Console.WriteLine($"[{DateTime.Now}] TimedAutoBuy: {Session["TimedAutoBuy"]}"));
            }
            else if (arguments.arg1 == "uptick")
            {

                if (arguments.arg1 == "true")
                {
                    Session["TimedUptickRule"] = true;
                }
                else if (arguments.arg1 == "false")
                {
                    Session["TimedUptickRule"] = false;
                }
                else
                {
                    result.Add(() => Console.WriteLine("usage: timed uptick [true|false]"));
                }
                SaveSession();
                result.Add(() => Console.WriteLine($"[{DateTime.Now}] TimedUptickRule: {Session["TimedUptickRule"]}"));
            }
            else if (arguments.arg1.ParseEnum<OrderSide>(out OrderSide side))
            {
                if (arguments.arg2.ParseDecimal(out decimal amount))
                {
                    if (arguments.arg3.ParseInt(out int interval))
                    {
                        if (!arguments.arg4.TryParsePercent(out Decimal timedFlipPct))
                        {
                            timedFlipPct = 0.1m;
                        }
                        var options = new TimedOptions
                        {
                            ProductType = ProductType,
                            OrderSide = side,
                            Amount = amount,
                            Interval = interval,
                            TimedFlipPct = timedFlipPct,
                            Started = true
                        };

                        if (Session.ContainsKey(nameof(TimedOptions)))
                        {
                            var existing = (TimedOptions)Session[nameof(TimedOptions)];

                            if (existing.Started && (existing.ProductType != options.ProductType || existing.OrderSide != options.OrderSide))
                            {
                                StopOrderTimer();
                            }
                            if (existing.Started)
                            {

                                existing.TimedFlipPct = options.TimedFlipPct;
                                existing.Interval = options.Interval;
                                existing.Amount = options.Amount;
                                result.Add(() =>
                                {
                                    var args = ParseArguments("timed list");
                                    args.Execute();
                                });
                                if (existing.TimedFlipPct != options.TimedFlipPct)
                                {
                                    result.Add(() =>
                                    {
                                        var args = ParseArguments($"flip {((TimedOptions)Session[nameof(TimedOptions)]).TimedFlipPct.ToString("P")}");
                                        args.Execute();
                                    });
                                }
                                if (arguments.arg5 == "flip")
                                {
                                    ParseArguments($"flip {options.TimedFlipPct.ToString("P")}").Execute();
                                }
                                SaveSession();
                                return result;
                            }
                        }



                        Session[nameof(TimedOptions)] = options;


                        PreviousTicker = LastTicker.Price;
                        this.OrderTimer = new System.Timers.Timer();
                        this.OrderTimer.Interval = 1000 * 60 * interval;
                        this.OrderTimer.Elapsed += OrderTimer_Elapsed;
                        this.OrderTimer.Start();
                        this.OrderTimer.AutoReset = false;
                        ParseArguments("last").Execute();
                        result.Add(() => Console.WriteLine($"{tabbracket} {options}"));
                        if (arguments.arg5 == "flip")
                        {
                            ParseArguments($"flip {options.TimedFlipPct.ToString("P")}").Execute();
                        }
                        SaveSession();
                    }
                    else
                    {
                        result.Add(() => Console.WriteLine("Unable to parse minuteInterval"));
                        result.Add(() => Console.WriteLine(usage));
                    }
                }
                else
                {
                    result.Add(() => Console.WriteLine("Unable to parse amount"));
                    result.Add(() => Console.WriteLine(usage));
                }
            }
            else if (arguments.arg1 == "cancel" || arguments.arg1 == "stop")
            {
                StopOrderTimer();
                SaveSession();
                result.Add(() => Console.WriteLine("Stopped timed"));
            }
            else if (arguments.arg1 == "start")
            {
                if (Session.ContainsKey(nameof(TimedOptions)))
                {
                    var options = (TimedOptions)Session[nameof(TimedOptions)];
                    if (options.Started)
                    {
                        result.Add(() => Console.WriteLine($"Already running: {(TimedOptions)Session[nameof(TimedOptions)]}"));
                    }
                    else
                    {
                        var line = $"timed {options.OrderSide} {options.Amount} {options.Interval} {options.TimedFlipPct.ToString("P")}";
                        if (OrderTimer != null)
                            StopOrderTimer();
                        this.OrderTimer = new System.Timers.Timer();
                        this.OrderTimer.Interval = 1000 * 60 * options.Interval;
                        this.OrderTimer.Elapsed += OrderTimer_Elapsed;
                        this.OrderTimer.Start();
                        this.OrderTimer.AutoReset = false;
                        options.Started = true;
                        ParseArguments("last").Execute();
                        result.Add(() => Console.WriteLine($"{tabbracket} {(TimedOptions)Session[nameof(TimedOptions)]}"));
                        SaveSession();
                    }
                }
                else
                {
                    result.Add(() => Console.WriteLine("No timed to start"));
                }
            }
            else if (arguments.arg1 == "list")
            {
                if (Session.ContainsKey(nameof(TimedOptions)))
                {
                    result.Add(() => Console.WriteLine(Session[nameof(TimedOptions)]));
                }
                else
                {
                    result.Add(() => Console.WriteLine("No timed options"));
                }

            }
            else
            {
                result.Add(arguments.InvalidAction(usage));
            }
            SaveSession();
            return result;
        }

        static double GetSecondInterval()
        {
            DateTime now = DateTime.Now;
            return ((60 - now.Second) * 1000 - now.Millisecond);
        }

        decimal PreviousTicker = 0;

        private bool OrderTimerAction()
        {
            if (!Session.ContainsKey(nameof(TimedOptions)))
            {
                return false;
            }
            bool TimedUptickRule = false;
            if (!Session.ContainsKey(nameof(TimedUptickRule)))
            {
                Session[nameof(TimedUptickRule)] = TimedUptickRule;
            }
            TimedUptickRule = (bool)Session[nameof(TimedUptickRule)];
            var options = (TimedOptions)Session[nameof(TimedOptions)];
            var side = options.OrderSide;
            if (side == OrderSide.Buy)
            {

                bool isDownTick = LastTicker.Price < PreviousTicker;
                bool execute = bool.Parse(bool.TrueString); //
                decimal lastPrice = 0;
                lastPrice = (decimal)this.Session[nameof(lastPrice)];


                //only buy downtick when at profit
                bool atLoss = LastTicker.Price < lastPrice;
                if (atLoss)
                {
                    decimal profit = (LastTicker.Price / lastPrice) - 1m;

                    if (!Session.ContainsKey("StopThreshold"))
                    {
                        Session["StopThreshold"] = .05m;
                    }
                    var thresholdLoss = (decimal)Session["StopThreshold"];
                    if (-profit > thresholdLoss)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{DateTime.Now}] STOP TRIGGERED - Loss: {profit.ToString("P")} Stop Threshold {thresholdLoss.ToString("P")} ");
                        LogBalance($"STOP TRIGGERED - Loss: {profit.ToString("P")} Stop Threshold {thresholdLoss.ToString("P")}");
                        ParseArguments("dump").Execute();
                    }

                }
                execute = !TimedUptickRule ? true : (isDownTick ? true : atLoss);
                if (execute)
                {
                    execute = WeightedExecute();
                    //execute = bool.Parse(bool.TrueString); //
                    var orderPrice = LastTicker.BestBid;
                    bool uptickOverride = false;
                    List<string> reasons = new List<string>();
                    if ((!Session.ContainsKey("MarketOnWeighted") || (bool)Session["MarketOnWeighted"] == true) && !execute)
                    {
                        orderPrice = lastPrice * .99m;
                        uptickOverride = execute = true;
                        reasons.Add("Weighted");
                    }
                    else if (Session.ContainsKey("MarketOnWeighted") && (bool)Session["MarketOnWeighted"] == false && !execute)
                    {
                        uptickOverride = execute = true;
                        reasons.Add("LimitWeighted");
                    }
                    decimal TimedLimitMaxPrice = 0m;
                    decimal TimedDiscount = 0m;
                    if (!uptickOverride && Session.ContainsKey(nameof(TimedDiscount)))
                    {
                        TimedDiscount = (decimal)Session[nameof(TimedDiscount)];
                        if (TimedDiscount != 0)
                        {
                            var discount = 1m - TimedDiscount;
                            orderPrice *= discount;
                            reasons.Add("TimedDiscount");
                        }
                    }


                    bool TimedDynamic = false;
                    TimedDynamic = Session.ContainsKey(nameof(TimedDynamic)) && (bool)Session[nameof(TimedDynamic)] == true;
                    var baseOrderSize = options.Amount / orderPrice;

                    decimal profit = (LastTicker.Price / lastPrice) - 1m;
                    profit = -profit;

                    if (TimedDynamic && profit > TimedDiscount)
                    {
                        decimal dynamicDiscount = (1m - profit);
                        var dynamicPrice = LastTicker.BestBid * dynamicDiscount;
                        if (dynamicPrice < orderPrice)
                        {
                            orderPrice = dynamicPrice;
                            reasons.Clear();
                            reasons.Add("TimedDynamic");
                        }
                    }


                    if (Session.ContainsKey(nameof(TimedLimitMaxPrice)))
                    {
                        TimedLimitMaxPrice = (decimal)Session[nameof(TimedLimitMaxPrice)];
                        if (TimedLimitMaxPrice < orderPrice)
                        {
                            orderPrice = TimedLimitMaxPrice;
                            reasons.Clear();
                            reasons.Add("TimedLimitMaxPrice");
                        }
                    }
                    if (execute)
                    {
                        string MarketOnWeighted = nameof(MarketOnWeighted);
                        bool marketMode = true;
                        if (Session.ContainsKey("MarketOnWeighted"))
                        {
                            marketMode = (bool)Session[nameof(MarketOnWeighted)];
                        }

                        if (reasons[0] == "Weighted" && marketMode)
                        {
                            reasons.Add(MarketOnWeighted);
                        }
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[{ DateTime.Now}] Timed Buy: Last {PreviousTicker} - Ticker {LastTicker.Price} (loss={atLoss}) (downtick={isDownTick}) ({string.Join("|", reasons)})");
                        Console.ResetColor();

                        //OrdersService.PlaceMarketFundsBuyOrder(options.ProductType, options.Amount);
                        //var size = options.Amount / LastTicker.BestBid;


                        //Product.BaseMinSize = Minimum Coin Size (LTC: 0.01000000)
                        //Product.MinMarketFunds = Minimum USD amount for market order
                        //increase buy size by percentage of loss

                        var size = baseOrderSize;
                        if (size < Product.BaseMinSize)
                        {
                            size = Product.BaseMinSize;
                            var minMarketSize = Product.MinMarketFunds / orderPrice;
                            if (size < minMarketSize)
                            {
                                //size = minMarketSize;
                            }
                        }
                        if (profit > 0)
                        {
                            size *= (1m + profit);
                        }


                        if (reasons[0] == "Weighted" && marketMode)
                        {
                            var marketSize = size; // options.Amount;
                            if (marketSize < Product.MinMarketFunds)
                                marketSize = Product.MinMarketFunds;
                            var order = OrdersService.CreateMarketBuyOrder(options.ProductType, marketSize);
                            var orderResponse = OrdersService.PlaceMarketBuyOrder(order);


                        }
                        else
                        {

                            var order = OrderHelper.CreatePostOnlyOrder(options.ProductType, OrderSide.Buy, orderPrice, size);
                            OrdersService.PlaceLimitBuyOrder(order, this);
                        }

                        decimal TimedConsolidate = 0m;
                        if (Session.ContainsKey(nameof(TimedConsolidate)))
                        {
                            TimedConsolidate = (decimal)Session[nameof(TimedConsolidate)];
                            ParseArguments($"consolidate buys {TimedConsolidate}").Execute();
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now}] Uptick Dropout: MA {lastPrice.ToPrecision(0.0001m)} (loss={atLoss}) - Last {PreviousTicker} - Ticker {LastTicker.Price} (downtick={isDownTick})");
                }

                PreviousTicker = LastTicker.Price;
                return execute;

            }
            else
            {


                bool execute = !TimedUptickRule ? true : LastTicker.Price > PreviousTicker;
                if (execute)
                {
                    execute = WeightedExecute();
                }
                if (execute)
                {
                    Console.WriteLine($"[{ DateTime.Now}] Timed Sell: Last {PreviousTicker} - Ticker {LastTicker.Price}");
                    OrdersService.PlaceMarketFundsSellOrder(options.ProductType, options.Amount);
                }

                PreviousTicker = LastTicker.Price;
                return execute;

            }

        }
        private bool WeightedExecute()
        {
            if (!Session.ContainsKey(nameof(TimedOptions)))
            {
                return false;
            }
            var options = (TimedOptions)Session[nameof(TimedOptions)];

            if (!this.Session.ContainsKey("lastPrice"))
            {
                ParseArguments("last").Execute();
            }
            decimal lastPrice = (decimal)this.Session[nameof(lastPrice)];
            decimal profit = (LastTicker.Price / lastPrice) - 1m;

            var rnd = (decimal)(new Random().NextDouble());
            if (profit > 0)
            {
                var target = options.TimedFlipPct == 0 ? .1m : options.TimedFlipPct;
                //10% or .1
                //eg profit 9%, then weight= .09/.1= 0.9
                //eg profit 5%, then weight= .05/.1= 0.5
                //eg profit 2%, then weight= .05/.1= 0.2
                var rawWeight = profit / target;
                var weight = rawWeight * .5m;

                //eg profit 9%, then delta= .01
                //eg profit 5%, then delat= .05
                //eg profit 2%, then weight= .08
                //eg profit .0001%, then weight= .9999
                var delta = 0.5m * weight;

                var threshold = .5m + delta;
                //execute = weightedTarget > rnd;


                bool shoudExecute = rnd > threshold;
                bool showAll = bool.Parse(bool.TrueString);
                if (showAll || !shoudExecute)
                {
                    Console.WriteLine($"[{DateTime.Now}] Weighted Dropout: {shoudExecute} - Profit: {profit.ToString("P")} (Weighted {weight.ToPrecision(0.0001m)}) Rand: {rnd.ToPrecision(0.0001m)} Threshold: {threshold.ToPrecision(0.0001m)}");
                }
                return shoudExecute;
            }
            else
            {
                var target = options.TimedFlipPct == 0 ? .1m : options.TimedFlipPct;
                //10% or .1
                //eg profit 9%, then weight= .09/.1= 0.9
                //eg profit 5%, then weight= .05/.1= 0.5
                //eg profit 2%, then weight= .05/.1= 0.2
                profit = -profit;

                var weight = profit / target;

                var delta = weight;
                var weightBias = .80m;

                var weightedTarget = weightBias + ((1m - weightBias) * weight);
                bool shoudExecute = weightedTarget >= rnd;
                var threshold = .5m - weight;

                shoudExecute = threshold >= rnd;
                if (!shoudExecute)
                {
                    Console.WriteLine($"[{DateTime.Now}] Ignored Weighted Dropout");
                }
                //return shoudExecute;
                return true;
            }

        }
        private void OrderTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            TimedOptions options = (TimedOptions)Session[nameof(TimedOptions)];
            OrderTimer.Interval = (options.Interval - 1) + GetSecondInterval();
            OrderTimer.Start();
            bool executed = OrderTimerAction();

            var pct = options.TimedFlipPct;
            bool isMarket = bool.Parse(bool.TrueString);
            if (isMarket && executed && pct != 0)
            {
                if (FillsManager.Cache(ProductType).LastFill.Side == OrderSide.Sell)
                {
                    //var line = $"flip {pct.ToString("P")}%";
                    //var arguments = ParseArguments(line).Arguments;
                    //ParseSwingOptions(arguments);
                }

            }




        }

        private System.Timers.Timer OrderTimer;
        private void StopOrderTimer()
        {
            if (OrderTimer != null)
            {
                Console.WriteLine($"{tabbracket} - Stopped: {Session[nameof(TimedOptions)]}");
                OrderTimer.Stop();
                OrderTimer.Elapsed -= OrderTimer_Elapsed;
                OrderTimer = null;
            }
            if (Session.ContainsKey(nameof(TimedOptions)))
            {
                ((TimedOptions)Session[nameof(TimedOptions)]).Started = false;
            }
        }

        private IEnumerable<Action> ParseProfitMeterOptions(ArgumentList arguments)
        {
            var result = new List<Action>();
            decimal lastPrice = 0m;
            if (arguments.Length == 1 || (arguments.Length == 2 && arguments[1].ParseDecimal(out lastPrice)))
            {
                result.Add(() =>
                {
                    this.profitMeter = new ProfitMeter(this);
                    if (lastPrice != 0)
                    {
                        this.profitMeter.LastPrice = lastPrice;
                    }
                });

            }
            else if (arguments.Length == 2 && arguments[1] == "refresh" && profitMeter != null)
            {
                var svc = new AccountService();
                profitMeter.TakerFeeRate = svc.TakerFeeRate;
                result.Add(() => Console.WriteLine("Refresh Profit Meter."));
            }
            else if (arguments.Length == 2 && arguments[1] == "stop")
            {
                result.Add(() =>
                {
                    this.profitMeter.Stop();
                    this.profitMeter = null;
                });
            }
            else if (arguments.Length == 2 && arguments[1].ParseEnum<OrderSide>(out OrderSide side))
            {
                profitMeter.Side = side;
                result.Add(() => Console.WriteLine("Set Profit Meter Side."));
            }
            else
            {
                result.Add(arguments.InvalidAction("usage: profitmeter [stop]"));
            }
            return result;
        }

        private IEnumerable<Action> ParseFeeOptions(ArgumentList arguments)
        {
            var service = new AccountService(true);
            var result = new List<Action>();
            result.Add(() => Console.WriteLine($"{tabbracket}: Maker Fee: {service.MakerFeeRate.ToString("P")} Taker Fee: {service.TakerFeeRate.ToString("P")}"));
            return result;
        }

        private IEnumerable<Action> ParseVolumeOptions(ArgumentList arguments)
        {
            var result = new List<Action>();


            if (arguments.Length == 1)
            {
                result.Add(() => Console.WriteLine($"{tabbracket}: 30 Day Volume {OrderVolumeService.Get30DayVolume().ToCurrency(Currency.USD)}"));
            }
            else
            {
                result.Add(arguments.InvalidAction());
            }
            return result;
        }

        private void Dump()
        {
            Console.WriteLine($"Dumping: {FormatTicker(LastTicker)}");
            CancelStop(ProductType);
            OrderManager.Refresh();

            OrdersService.CancelAllOrders(x => x.ProductId == ProductType && x.Side == OrderSide.Sell);
            var product = OrdersService.GetProduct(ProductType);
            var svc = new AccountService();
            var balance = svc.GetBalance(product.BaseCurrency);
            var roundedBalance = balance.ToPrecision(product.BaseIncrement);
            TradeOrder order = null;
            var dumpargs = arguments.line;
            if (arguments.arg1.ParseDecimal(out decimal price))
            {
                order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide.Sell, price, roundedBalance);
                Console.WriteLine($"Placing {ProductType} Sell Order: {order.ToJson()}");
                var result = OrdersService.PlaceLimitSellOrder(order, this);

            }
            if (arguments.arg1 == "limit")
            {
                if (!arguments.arg2.ParseDecimal(out price))
                {
                    price = this.LastTicker.BestBid;
                }
                order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide.Sell, price, roundedBalance);
                Console.WriteLine($"Placing {ProductType} Sell Order: {order.ToJson()}");
                var result = OrdersService.PlaceLimitSellOrder(order, this);

            }
            else if (arguments.arg1 != null)
            {
                Console.WriteLine($"Usage: Dump price|[limit price]");
            }
            else
            {
                var last = GetLast(ProductType, LastSide.Buy);
                LogBalance($"Dumping: {last}");
                order = OrdersService.CreateMarketSellOrder(ProductType, roundedBalance);
                Console.WriteLine($"Placing {ProductType} Sell Order: {order.ToJson()}");
                var result = OrdersService.PlaceMarketSellOrder(order);
                System.Threading.Thread.Sleep(3000);
                var filled = GetLast(ProductType);
                LogBalance($"{dumpargs.Replace("dump", "Dump")}: {filled}");
            }
        }

        void LogBalance(string message)
        {
            File.AppendAllText("balance.log", $"[{DateTime.Now}] {message}{Environment.NewLine}");
        }


        private void GoAllIn()
        {
            Console.WriteLine($"Allin: {FormatTicker(LastTicker)}");
            CancelStop(ProductType);
            OrderManager.Refresh();
            OrdersService.CancelAllOrders(x => x.ProductId == ProductType && x.Side == OrderSide.Buy);
            var product = OrdersService.GetProduct(ProductType);
            var svc = new AccountService();
            var balance = svc.GetBalance(product.QuoteCurrency);
            var roundedBalance = balance.ToPrecision(product.QuoteIncrement);
            TradeOrder order = null;
            OrderResponse result = null;
            if (arguments.Length > 1 && arguments[1] == "limit")
            {
                var price = LastTicker.BestAsk;
                order = OrdersService.CreatePostOnlyBuyOrder(ProductType, price, roundedBalance);
                result = OrdersService.PlaceLimitBuyOrder(order, this);
            }
            else
            {
                order = OrdersService.CreateMarketBuyOrder(ProductType, roundedBalance);
                result = OrdersService.PlaceMarketBuyOrder(order);
            }
            Console.WriteLine($"Placing {ProductType} Buy Order: {order.ToJson()}");

        }


        private class StopOptions
        {
            private ProductType _productType;
            public ProductType ProductType
            {
                get { return _productType; }
                set
                {
                    _productType = value;
                    Product = OrdersService.GetProduct(ProductType);
                }
            }
            public OrderSide OrderSide;

            private decimal price;
            public decimal Price
            {
                get => price;
                set { price = value; if (StartPrice == 0) { StartPrice = value; } }
            }
            private decimal _startPrice;
            bool doCallBack = true;
            public decimal StartPrice
            {
                get => _startPrice;
                set => Price = _startPrice = value;
            }
            public bool Active;
            public decimal TrailingStopPct;
            public Product Product;
            public CoinbaseTicker Ticker { get; internal set; }

            public EventHandler OnCompleted;
            ConsoleDriver Driver;


            public StopOptions(ConsoleDriver driver)
            {
                Active = true;
                Driver = driver;
            }

            public override string ToString()
            {
                return $"{ProductType} {OrderSide} {Price} Trail: {TrailingStopPct.ToString("P")}";
            }

            internal void On_BuyTickerReceived(object sender, WebfeedEventArgs<Ticker> e)
            {
                if (!Active) return;
                var lastPrice = e.LastOrder.Price;
                if (e.LastOrder.Price > Price)
                {
                    Complete();
                    Console.WriteLine($"[{DateTime.Now}] Stop buy hit for {ProductType}. Refreshing orders");
                    OrderManager.Refresh();

                    OrdersService.CancelAllOrders(x => x.ProductId == ProductType && x.Side == OrderSide.Buy);
                    //var product = OrdersService.GetProduct(ProductType);
                    var svc = new AccountService();
                    var balance = svc.GetBalance(Product.QuoteCurrency);
                    var roundedBalance = balance.ToPrecision(Product.QuoteIncrement);
                    var order = OrdersService.CreateMarketBuyOrder(ProductType, roundedBalance);

                    var result = OrdersService.PlaceMarketBuyOrder(order);
                    Active = false;
                    if (doCallBack && bool.Parse(bool.FalseString))
                    {
                        decimal callBackAmount = e.LastOrder.Price * (1 - (TrailingStopPct));
                        var roundedCallBackAmount = callBackAmount.ToPrecision(Product.QuoteIncrement);
                        string callBack = $"stop {ProductType} sell {roundedCallBackAmount} {TrailingStopPct}";
                        Console.WriteLine($"Stop callback: {callBack}");
                        Thread.Sleep(500);
                        Driver.ParseArguments(callBack);
                    }


                }
                else if (TrailingStopPct > 0m)
                {
                    var calcLimit = (e.LastOrder.Price * (1 + TrailingStopPct)).ToPrecision(Product.QuoteIncrement);
                    if (calcLimit < price)
                    {
                        Price = calcLimit;
                        string pct = "";
                        if (Driver.profitMeter != null)
                        {
                            var change = (Driver.profitMeter.LastPrice / price) - 1m;
                            pct = $" ({change.ToString("P")})";
                        }

                        Console.WriteLine($"[{DateTime.Now}] {e.LastOrder.Price}: {this} {pct}");
                    }

                }
            }



            internal void On_SellTickerReceived(object sender, WebfeedEventArgs<Ticker> e)
            {
                if (!Active) return;
                var lastPrice = e.LastOrder.Price;
                if (e.LastOrder.Price < Price)
                {
                    Complete();
                    Console.WriteLine($"[{DateTime.Now}] Stop sell hit for {ProductType}. Refreshing orders");
                    OrderManager.Refresh();

                    OrdersService.CancelAllOrders(x => x.ProductId == ProductType && x.Side == OrderSide.Sell);
                    //var product = OrdersService.GetProduct(ProductType);
                    var svc = new AccountService();
                    var balance = svc.GetBalance(Product.BaseCurrency);
                    var roundedBalance = balance.ToPrecision(Product.BaseIncrement);
                    var order = OrdersService.CreateMarketSellOrder(ProductType, roundedBalance);

                    var result = OrdersService.PlaceMarketSellOrder(order);
                    Active = false;

                    if (doCallBack && bool.Parse(bool.FalseString))
                    {
                        decimal callBackAmount = e.LastOrder.Price * (1m + TrailingStopPct);
                        var roundedCallBackAmount = callBackAmount.ToPrecision(Product.QuoteIncrement);
                        string callBack = $"stop {ProductType} buy {roundedCallBackAmount} {TrailingStopPct}";
                        Console.WriteLine($"Stop callback: {callBack}");
                        Thread.Sleep(500);
                        Driver.ParseArguments(callBack);
                    }


                }
                else if (TrailingStopPct > 0m)
                {
                    var limitPct = 1m - TrailingStopPct;
                    var calcLimitRaw = e.LastOrder.Price * limitPct;
                    var calcLimit = calcLimitRaw.ToPrecision(Product.QuoteIncrement);
                    if (calcLimit > price)
                    {
                        Price = calcLimit;
                        var pct = "";
                        if (Driver.profitMeter != null)
                        {
                            var change = (price / Driver.profitMeter.LastPrice) - 1m;
                            pct = $" ({change.ToString("P")})";
                        }
                        Console.WriteLine($"[{DateTime.Now}] {e.LastOrder.Price}: {this} {pct}");
                    }
                }
            }
            internal void Complete()
            {
                Active = false;
                Ticker.OnTickerReceived -= On_SellTickerReceived;
                Ticker.OnTickerReceived -= On_BuyTickerReceived;
                Ticker.Stop();
                this.OnCompleted?.Invoke(this, null);
                this.OnCompleted?.GetInvocationList().ToList().ForEach(x =>
                {
                    this.OnCompleted -= (EventHandler)x;
                });
            }
        }

        private Dictionary<ProductType, List<StopOptions>> stopOptions = new Dictionary<ProductType, List<StopOptions>>();

        private void CancelStop(ProductType productType)
        {
            if (stopOptions.ContainsKey(productType) && stopOptions[productType].Count > 0)
            {

                var opts = stopOptions[productType];
                Console.WriteLine($"Canceling {opts.Count} for {productType}");

                foreach (var opt in opts.ToList())
                {

                    Console.WriteLine($"{tabbracket}Canceled stop option: {opt}");
                    opt.Complete();
                    opt.Ticker.OnTickerReceived -= opt.On_BuyTickerReceived;
                    opt.Ticker.OnTickerReceived -= opt.On_SellTickerReceived;
                    opt.Ticker.Stop();
                }
                opts.Clear();
                stopOptions.Remove(productType);

            }
            else
            {
                Console.WriteLine($"No stops to cancel for {productType}");
            }
        }
        private IEnumerable<Action> ParseStopOptions(ArgumentList arguments)
        {
            string usage = " Usage: stop [producttype] (buy|sell|cancel|list) [price] [trailing %]";
            var i = 1;
            List<Action> actions = new List<Action>();
            if (arguments.Length == 2 && arguments.arg1.TryParsePercent(out decimal StopThreshold))
            {
                Session[nameof(StopThreshold)] = StopThreshold;
                Console.WriteLine($"{nameof(StopThreshold)}: {StopThreshold.ToString("P")}");
                actions.Add(() => { });
            }
            else if (arguments.Length > 1)
            {
                var options = new StopOptions(this);
                bool parsedProduct = false;
                if (arguments[i].ParseEnum<ProductType>(out ProductType productType))
                {
                    parsedProduct = true;
                    i++;
                }
                else
                {
                    productType = ProductType;
                }
                options.ProductType = productType;

                if (i >= arguments.Length)
                {
                    actions.Add(arguments.InvalidAction($"{tabbracket} {usage}"));
                    return actions;
                }
                switch (arguments[i++].ToLower())
                {
                    case "buy":

                        options.OrderSide = OrderSide.Buy;
                        break;
                    case "sell":
                        options.OrderSide = OrderSide.Sell;
                        break;
                    case "list":
                        if (Session.ContainsKey("StopThreshold") && Session.ContainsKey("lastPrice"))
                        {
                            actions.Add(() => Console.WriteLine($"{tabbracket}Stop Threshold {((decimal)Session["StopThreshold"]).ToString("P")}. ({((decimal)Session["lastPrice"]) * (1 - ((decimal)Session["StopThreshold"]))})"));
                        }
                        else if (Session.ContainsKey("StopThreshold"))
                        {
                            actions.Add(() => Console.WriteLine($"{tabbracket}Stop Threshold {((decimal)Session["StopThreshold"]).ToString("P")}."));
                        }
                        if (arguments.Length == 2 || parsedProduct)
                        {
                            if (stopOptions.ContainsKey(productType))
                            {
                                var l = stopOptions[ProductType];
                                if (l.Count == 0)
                                {
                                    actions.Add(() => Console.WriteLine($"{tabbracket}No stops for {productType}."));
                                }
                                else
                                {
                                    l.ForEach(x => actions.Add(() => Console.WriteLine($"{tabbracket}{x}")));
                                }
                            }
                            else
                            {
                                actions.Add(() => Console.WriteLine($"{tabbracket}No stops for {productType}."));
                            }
                        }
                        else
                        {
                            foreach (var kvp in stopOptions)
                            {
                                actions.Add(() => Console.WriteLine($"{tabbracket}{kvp.Key}:"));
                                if (kvp.Value.Count == 0)
                                {
                                    actions.Add(() => Console.WriteLine($"{tabbracket}{tabbracket}No stops for {productType}."));
                                }
                                else
                                {
                                    kvp.Value.ForEach(x => actions.Add(() => Console.WriteLine($"{tab}{tabbracket}{x}")));
                                }
                            }
                        }

                        break;
                    case "cancel":
                        actions.Add(() => CancelStop(productType));
                        break;
                    default:
                        actions.Add(arguments.InvalidAction($"{tabbracket}{usage}"));
                        break;

                }
                decimal optionPrice = 0m;
                if (actions.Count == 0)
                {
                    if (i < arguments.Length && arguments[i].ParseDecimal(out optionPrice))
                    {

                    }
                    else
                    {
                        actions.Add(arguments.InvalidAction($"{tabbracket}Unable to parse Price: {usage}"));
                    }
                }
                decimal stopPct = 0;
                if (actions.Count == 0)
                {
                    if (++i < arguments.Length)
                    {
                        if (arguments[i].IndexOf("%") > -1 && arguments[i].TryParsePercent(out stopPct))
                        {
                            string bp = $"PCT = {stopPct.ToString("P")}";
                        }
                        else if (arguments[i].ParseDecimal(out stopPct))
                        {
                            string bp = $"PCT = {stopPct.ToString("P")}";
                        }
                        else
                        {
                            actions.Add(arguments.InvalidAction($"{tabbracket}Unable to parse trailing stop %. {usage}"));
                        }
                    }
                }
                if (actions.Count == 0)
                {
                    CancelStop(productType);
                    options.Price = optionPrice;
                    if (options.OrderSide == OrderSide.Sell && options.Price > LastTicker.BestBid)
                    {
                        Console.WriteLine($"Sell price ({options.Price}) must be less than best bid ({LastTicker.BestBid})");
                        actions.Add(() => Console.WriteLine("Invalid stop sell price."));
                    }
                    if (options.OrderSide == OrderSide.Buy && options.Price > LastTicker.BestAsk)
                    {
                        Console.WriteLine($"Buy price ({options.Price}) must be more than best bid ({LastTicker.BestAsk})");
                        actions.Add(() => Console.WriteLine("Invalid stop buy price."));
                    }

                    options.Ticker = CoinbaseTicker.Create(productType);
                    options.TrailingStopPct = stopPct;

                    if (options.OrderSide == OrderSide.Buy)
                    {
                        options.Ticker.OnTickerReceived += options.On_BuyTickerReceived;
                    }
                    else
                    {
                        options.Ticker.OnTickerReceived += options.On_SellTickerReceived;
                    }
                    if (!stopOptions.ContainsKey(productType))
                    {
                        stopOptions[productType] = new List<StopOptions>();
                    }
                    stopOptions[productType].Add(options);
                    options.OnCompleted += (sender, e) =>
                    {
                        if (stopOptions.ContainsKey(options.ProductType))
                        {
                            stopOptions[options.ProductType].Remove(options);
                        }

                    };
                    actions.Add(() => Console.WriteLine($"Created stop: {options}"));


                }


            }
            else
            {
                actions.Add(arguments.InvalidAction($"{tabbracket}{usage}"));
            }


            return actions;
        }

        private IEnumerable<Action> ParseMatchQueueOptions(ArgumentList arguments)
        {
            List<Action> actions = new List<Action>();

            Action addShowActions = () =>
            {
                actions.Add(() => Console.WriteLine($"{tabbracket}{nameof(MatchQueue)}.{nameof(MatchQueue.MatchedBuys)}: {MatchQueue.MatchedBuys.QueueTotal}"));
                actions.Add(() => Console.WriteLine($"{tabbracket}{nameof(MatchQueue)}.{nameof(MatchQueue.MatchedSells)}: {MatchQueue.MatchedSells.QueueTotal}"));
            };
            if (arguments.Length == 1)
            {
                addShowActions();
            }
            else if (arguments.Length == 2 && arguments.arg1 == "clear")
            {
                actions.Add(() => MatchQueue.Clear());
                addShowActions();
            }
            else
            {
                actions.Add(arguments.InvalidAction($"{tabbracket}Usage: matchqueue [clear]"));
            }
            return actions;
        }
        private IEnumerable<Action> ParseSwingCancelOptions(ArgumentList arguments)
        {
            List<Action> result = new List<Action>();
            if (SwingOptions.Count == 0)
            {
                result.Add(arguments.InvalidAction("No swings to cancel"));
                if (bool.Parse(bool.TrueString))
                {
                    return result;
                }
                var options = SwingOptions.Values.First();
                result.Add(() => StopSwing(ProductType, options));
            }
            else if (arguments.Length == 3 && arguments[1].ParseEnum<ProductType>(out ProductType productType) && SwingOptions.ContainsKey(productType))
            {
                result.Add(() => StopSwing(ProductType, SwingOptions[productType]));
            }
            else if (arguments.Length == 2 && SwingOptions.ContainsKey(ProductType))
            {
                result.Add(() => StopSwing(ProductType, SwingOptions[ProductType]));
            }
            else
            {
                result.Add(arguments.InvalidAction("Usage: swing cancel [producttype]"));
            }
            return result;
        }

        private int lastPages = 1;
        private IEnumerable<Action> ParseSwingOptions(ArgumentList arguments)
        {
            List<Action> result = new List<Action>();
            //var p = OrdersService.GetProduct(ProductType);
            if (arguments.line.IndexOf("cancel") > -1)
            {
                result.AddRange(ParseSwingCancelOptions(arguments));
            }
            else if (arguments.arg0 == "flip" &&
                arguments.arg1.ParseEnum<OrderSide>(out OrderSide flipOrderSide)
                && arguments.arg2 == "order")
            {
                string bp = "";
                decimal flipPct = 0;
                decimal flipPrice = 0;
                if (arguments.arg3.IndexOf("%") != -1 && arguments.arg3.TryParsePercent(out flipPct))
                {

                }
                else if (arguments.arg3.TryParsePercent(out flipPrice))
                {

                }
                else
                {
                    result.Add(arguments.InvalidAction("Usage: flip [sell|buy] order [price|pct]"));
                }

                var last = GetLast(ProductType, (LastSide)flipOrderSide);

                var f = last.Fills.First();
                var orderFills = last.Fills.Take(0).ToList();
                var total = 0m;
                var fee = 0m;
                for (var i = 0; i < last.Fills.Count; i++)
                {
                    if (last.Fills[i].OrderId == f.OrderId)
                    {
                        var fill = last.Fills[i];
                        total += fill.Size * f.Price;
                        fee += fill.Fee;
                        orderFills.Add(fill);
                    }
                }

                var size = orderFills.Sum(x => x.Size);
                var price = (total - fee) / size;
                if (flipPrice != 0)
                {
                    price = flipPrice;
                }
                else
                {
                    if (flipOrderSide == OrderSide.Buy)
                    {
                        price *= (1m + (flipPct)); //flip to sell
                    }
                    else
                    {
                        price *= (1m - (flipPct)); //flip to buy
                    }

                }
                if (flipOrderSide == OrderSide.Buy)
                {
                    var order = OrderHelper.CreatePostOnlyOrder(ProductType, OrderSide.Sell, price, size);
                    OrdersService.PlaceLimitSellOrder(order, this);
                }
                else //flip sell to buy;
                {
                    var newSize = (total - fee) / price;
                    var order = OrderHelper.CreatePostOnlyOrder(ProductType, OrderSide.Buy, price, newSize);
                    OrdersService.PlaceLimitBuyOrder(order, this);
                }
                result.Add(() => { });
            }
            else if ((arguments.arg0 == "flip" && arguments.Length >= 2) 
                && ((arguments[1].ParseDecimal(out decimal flipPCT)) ||
                    //(arguments.arg1.ParseEnum<LastSide>(out LastSide wantedSide) && arguments.arg2.TryParsePercent(out flipPCT)) ||
                    (arguments.arg1 == "last" && arguments.arg3.ParseDecimal(out flipPCT)))
            )
            {
                if (arguments.arg2 == "delay" && arguments.arg3.ParseInt(out int delayTime))
                {
                    Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(delayTime);
                        var line = $"flip {flipPCT.ToString("P")}";
                        var args = ParseArguments(line);
                        ParseSwingOptions(args.Arguments);
                    });
                    result.Add(() => { });
                    goto Done;
                }
                DateTime lastFlipDate = DateTime.MinValue;
                if (Session.ContainsKey(nameof(lastFlipDate)))
                {
                    lastFlipDate = (DateTime)Session[nameof(lastFlipDate)];
                }

                if (DateTime.Now.Subtract(lastFlipDate).TotalSeconds < 5)
                {
                    if (Session.ContainsKey("flip"))
                    {
                        var flipArguments = (ArgumentList)Session["flip"];
                        if (flipArguments.line == arguments.line)
                        {
                            result.Add(() => { });
                            return result;
                        }
                    }
                }

                Session[nameof(lastFlipDate)] = DateTime.Now;
                bool lastOnly = arguments.arg1 == "last";

                if (!arguments.arg2.ParseEnum<LastSide>(out LastSide flipSide))
                {
                    flipSide = LastSide.Any;
                    Session["flip"] = arguments;
                }

                var last = GetLast(ProductType, flipSide);


                var price = last.Price.ToPrecision(Product.QuoteIncrement);
                var f = last.Fills.First();
                var qty = last.Size.ToPrecision(Product.BaseIncrement);
                var product = Product;
                var subtotal = last.SubTotal.ToPrecision(Product.QuoteIncrement);
                var orderIds = last.OrderIds;
                var total = last.Total.ToPrecision(Product.QuoteIncrement);

                Session["lastPrice"] = price;
                if (f.Side == OrderSide.Buy)
                {
                    decimal balance = 0;

                    var target = (arguments.arg1.IndexOf("%") == -1 ? flipPCT : price * (1m + flipPCT)).ToPrecision(product.QuoteIncrement);
                    if (lastOnly)
                    {
                        balance = qty.ToPrecision(product.BaseIncrement);
                    }
                    else
                    {
                        if (OrderManager.SellOrders.Count == 1)
                        {
                            var sellBalance = OrderManager.SellOrders.Sum(x => x.Value.Size);
                            var sell = OrderManager.SellOrders.First().Value;
                            if (qty == sellBalance && sell.Price == target)
                            {
                                result.Add(() => Console.WriteLine($"{tabbracket} Already flipped"));
                                goto setPrice;
                            }
                        }


                        OrdersService.CancelAllSells();
                        balance = AccountService.GetAccountBalance(ProductType, OrderSide.Sell);
                    }


                    var order = OrderHelper.CreatePostOnlyOrder(ProductType, OrderSide.Sell, target, balance);
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"[{DateTime.Now}] Flipping buy {qty.ToPrecision(product.BaseIncrement)} @ {price.ToPrecision(product.QuoteIncrement)} ({subtotal.ToPrecision(product.QuoteIncrement)}) {flipPCT.ToString("P")} to sell @ {target} ({(qty * target).ToPrecision(product.QuoteIncrement)}) - {1 + orderIds.Count} Orders");
                    Console.ResetColor();
                    OrdersService.PlaceLimitSellOrder(order, this);
                }
                else
                {
                    decimal balance = 0;
                    if (lastOnly)
                    {
                        balance = subtotal.ToPrecision(product.QuoteIncrement);
                    }
                    else
                    {
                        OrdersService.CancelAllBuys();
                        balance = AccountService.GetAccountBalance(ProductType, OrderSide.Buy);
                    }


                    var flipArg = lastOnly ? arguments.arg3 : arguments.arg1;
                    var target = flipArg.IndexOf("%") == -1 ? flipPCT : price * (1m - flipPCT);

                    var order = OrdersService.CreatePostOnlyBuyOrder(ProductType, target, balance);
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"[{DateTime.Now}] Flipping sell {qty} @ {price} ({total.ToPrecision(product.QuoteIncrement)}) {flipPCT.ToString("P")} to buy {order.OrderSize} @ {target} ({balance.ToPrecision(product.QuoteIncrement)}) - {1 + orderIds.Count} Orders");
                    Console.ResetColor();

                    OrdersService.PlaceLimitBuyOrder(order, this);


                }
                setPrice:
                if (profitMeter != null)
                {
                    profitMeter.LastPrice = price;
                    profitMeter.Side = f.Side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
                    result.Add(() => Task.Run(() =>
                     {
                         System.Threading.Thread.Sleep(1000);
                         profitMeter.LastPrice = price;
                     }));
                }

                result.Add(() => { });
            }
            else
            {
                //TODO: incorporate initial order size/side
                string options = "swing [producttype] lowprice highprice";
                Action<string> invalid = (x) =>
                {
                    result.Add(arguments.InvalidAction(x));
                };
                int idx = 1;
                if (arguments.Length >= 7)
                {
                    var parseTest = DateTime.TryParse(arguments[1], out DateTime startTest);
                    if (arguments.Length == 8)
                    {
                        if (arguments[idx].ParseEnum<ProductType>(out ProductType productType))
                        {
                            idx++;
                            SetProductType(productType);
                        }
                        else
                        {
                            invalid($"Swing requires 8 arguments when product type is specified.\r\n{tabbracket}{options}");
                            goto Done;
                        }
                    }
                    if (
                         DateTime.TryParse(arguments[idx++], out DateTime start)
                         && DateTime.TryParse(arguments[idx++], out DateTime end)
                         && decimal.TryParse(arguments[idx++], out decimal startBuyPrice)
                         && decimal.TryParse(arguments[idx++], out decimal endBuyPrice)
                         && decimal.TryParse(arguments[idx++], out decimal startSellPrice)
                         && decimal.TryParse(arguments[idx++], out decimal endSellPrice)
                         ) // args. length=7  EX: "swing 3/18/2020 8/27/2020 32.37 43.50  54.00 65.75";
                    {
                        var swingOrderOptions = new TrendLineOrderOptions(ProductType, start, startBuyPrice, endBuyPrice, end, startSellPrice, endSellPrice);

                        result.Add(() => StartSwing(swingOrderOptions));
                    }
                    else
                    {
                        invalid($"Invalid swing trend line arguments");
                        result.Add(() => Console.WriteLine(
                            "Usage swing [producttype] startDate endDate startBuyPrice endBuyPrice startSellPrice  endSellPrice")
                        );
                    }
                }
                else if (arguments.Length >= 3)
                {
                    if (arguments[idx].ParseEnum<ProductType>(out ProductType productType))
                    {
                        if (arguments.Length < 4)
                        {
                            invalid($"Swing requires at least 4 arguments when product type is specified.\r\n{tabbracket}{options}");
                        }
                        else
                        {
                            idx++;
                            SetProductType(productType);
                        }
                    }

                    if (result.Count == 0 && arguments.Length - idx >= 2)
                    {
                        if (!(arguments[idx++].ParseDecimal(out decimal buyPrice)))
                        {
                            invalid($"Invalid low price specified.\r\n{tabbracket}{options}");
                        }
                        else if (!(arguments[idx++].ParseDecimal(out decimal sellPrice)))
                        {
                            invalid($"Invalid high price specified.\r\n{tabbracket}{options}");
                        }
                        else
                        {
                            if (!(buyPrice < sellPrice))
                            {
                                invalid($"Invalid price range specified.\r\n{tabbracket}{options}");
                            }
                            else
                            {
                                var swingOrderOptions = new SwingOrderOptions(ProductType, buyPrice, sellPrice);
                                if (!(swingOrderOptions.BuyPrice < swingOrderOptions.SellPrice))
                                {
                                    invalid($"Invalid price range specified.\r\n{tabbracket}{options}");
                                }
                                else
                                {
                                    result.Add(() => StartSwing(swingOrderOptions));
                                }

                            }

                        }
                    }

                }
                else if (arguments.Length == 2 && arguments.arg1 == "list")
                {
                    result.Add(() => Console.WriteLine($"Found {SwingOptions.Count} Swing Entries"));
                    int i = 1;
                    foreach (var kvp in SwingOptions)
                    {
                        SwingOrderOptions opt = kvp.Value;
                        result.Add(() => Console.WriteLine($"\t{i++} {kvp.Key}:  {opt}"));
                    }
                }

                else
                {
                    result.Add(arguments
                           .InvalidAction($"Swing requires at least 3 arguments when product type is specified.\r\n{tabbracket}{options}")
                           );
                }
            }
            Done:
            return result;
        }


        Dictionary<ProductType, SwingOrderOptions> SwingOptions = new Dictionary<ProductType, SwingOrderOptions>();

        private void StartSwing(SwingOrderOptions orderOptions)
        {
            if (!SwingOptions.ContainsKey(this.ProductType))
            {
                SwingOptions.Add(this.ProductType, orderOptions);
            }
            else
            {
                //Stop the current swing (to remove event handlers)
                StopSwing(ProductType, SwingOptions[this.ProductType]);
                SwingOptions[this.ProductType] = orderOptions;
            }
            orderOptions.MatchHandler = UserFeedSwing_OnMatchReceived;
            orderOptions.LastMatchHandler = UserFeedSwing_OnLastMatchReceived;
            orderOptions.DoneHandler = UserFeedSwing_OnDoneReceived;


            this.userFeed.OnMatchReceived += orderOptions.MatchHandler;
            this.userFeed.OnLastMatchReceived += orderOptions.LastMatchHandler;
            this.userFeed.OnDoneReceived += orderOptions.DoneHandler;
            Console.WriteLine($"{tabbracket}Swing:{orderOptions.BuyPrice}-{orderOptions.SellPrice}");
        }
        private void StartSwing(decimal lowPrice, decimal highPrice)
        {
            var orderOptions = new SwingOrderOptions(ProductType, lowPrice, highPrice);
            if (!SwingOptions.ContainsKey(this.ProductType))
            {
                SwingOptions.Add(this.ProductType, orderOptions);
            }
            else
            {
                //Stop the current swing (to remove event handlers)
                StopSwing(ProductType, SwingOptions[this.ProductType]);
                SwingOptions[this.ProductType] = orderOptions;
            }
            orderOptions.MatchHandler = UserFeedSwing_OnMatchReceived;
            orderOptions.LastMatchHandler = UserFeedSwing_OnLastMatchReceived;
            orderOptions.DoneHandler = UserFeedSwing_OnDoneReceived;


            this.userFeed.OnMatchReceived += orderOptions.MatchHandler;
            this.userFeed.OnLastMatchReceived += orderOptions.LastMatchHandler;
            this.userFeed.OnDoneReceived += orderOptions.DoneHandler;
            Console.WriteLine($"{tabbracket}Swing:{orderOptions.BuyPrice}-{orderOptions.SellPrice}");

        }


        private ConcurrentDictionary<string, Task> MatchCache = new ConcurrentDictionary<string, Task>();
        private void UserFeedSwing_OnDoneReceived(object sender, WebfeedEventArgs<Done> e)
        {
            var order = e.LastOrder;
            Log.Information($"[{order.OrderId}] Handling UserFeed_OnDoneReceived");

            if (bool.Parse(bool.TrueString))
            {
                Log.Information($"[{order.OrderId}] Skipping UserFeed_OnDoneReceived");
                return;
            }
            if (order.Reason != DoneReasonType.Canceled)
            {
                // Avoid race condition and let individual matches process before processing done.
                // by runnint the done processor on a seperate delayed task.
                Task.Run(() =>
                {
                    //int retryCount = 0;
                    if (!OrderManager.AllOrders.ContainsKey(order.OrderId))
                    {
                        string message = $"[{order.OrderId}] No existing order for done message";
                        Log.Information(message);
                        Console.WriteLine(message);
                        return;
                    }
                    var tradeOrder = OrderManager.AllOrders[order.OrderId];
                    var isMarket = tradeOrder.OrderType == OrderType.Market;
                    var remaining = tradeOrder.Size - tradeOrder.FilledSize;
                    var match = new Match
                    {
                        ProductId = order.ProductId,
                        Price = order.Price,
                        Time = order.Time,
                        MakerOrderId = isMarket ? order.OrderId : Guid.Empty,
                        Sequence = order.Sequence,
                        Side = order.Side,
                        Size = remaining,
                        TakerOrderId = isMarket ? Guid.Empty : order.OrderId,
                        TradeId = order.Sequence,
                        Type = ResponseType.LastMatch
                    };
                    var args = new WebfeedEventArgs<Match>(match);
                    System.Threading.Thread.Sleep(3000);
                    UserFeedSwing_OnMatchReceived(sender, args);
                });
            }

        }

        private void UserFeedSwing_OnLastMatchReceived(object sender, WebfeedEventArgs<LastMatch> e)
        {
            var order = e.LastOrder;
            var myOrder = OrderManager.TryGetByFirstIds(order.TakerOrderId, order.MakerOrderId);
            if (myOrder == null)
            {
                string message = $"[{order.TakerOrderId}] [{order.MakerOrderId}] UserFeed_OnLastMatchReceived No existing order for Last Match: {order.ToJson()}";
                Log.Information(message);
                Console.WriteLine(message);
                return;
            }
            Log.Information($"[{myOrder.Id}] Handling UserFeed_OnLastMatchReceived");

            var match = new Match
            {
                ProductId = order.ProductId,
                Price = order.Price,
                Time = order.Time,
                MakerOrderId = order.MakerOrderId,
                Sequence = order.Sequence,
                Side = order.Side,
                Size = order.Size,
                TakerOrderId = order.TakerOrderId,
                TradeId = order.Sequence,
                Type = ResponseType.LastMatch
            };
            var args = new WebfeedEventArgs<Match>(match);
            UserFeedSwing_OnMatchReceived(sender, args);



        }
        private void UserFeedSwing_OnMatchReceived(object sender, WebfeedEventArgs<Match> e)
        {

            var feedOrder = e.LastOrder;
            var orderKey = $"[{feedOrder.TakerOrderId}] [{feedOrder.MakerOrderId}]";
            if (MatchCache.ContainsKey(orderKey))
            {
                string message = $"Match for orderKey was already handled: {feedOrder.ToJson()}";
                Log.Information(message);
                return;
            }
            else
            {
                MatchCache[orderKey] = Task.Run(() =>
               {
                   Thread.Sleep(10000);
                   MatchCache.TryRemove(orderKey, out Task removed);
               });

            }


            var myOrder = OrderManager.TryGetByFirstIds(feedOrder.TakerOrderId, feedOrder.MakerOrderId);
            if (myOrder == null)
            {
                string message = $"[{feedOrder.TakerOrderId}] [{feedOrder.MakerOrderId}] UserFeed_OnMatchReceived No existing order for Match: {feedOrder.ToJson()}";
                Log.Information(message);
                Console.WriteLine(message);
                return;
            }

            Log.Information($"[{myOrder.Id}] Handling UserFeed_OnMatchReceived");
            bool filled = feedOrder.Size == (myOrder.Size - myOrder.FilledSize) || myOrder.FilledSize == myOrder.Size;
            //guard against multiple events.
            if (filled)
            {
                Log.Information($"[{myOrder.Id}] Removing filled order from {nameof(OrderManager)}.{nameof(OrderManager.AllOrders)}");
                bool removed = OrderManager.AllOrders.TryRemove(myOrder.Id, out OrderResponse removedOrder);
                Log.Information($"[{myOrder.Id}] Removal result: {removed}");
            }
            else
            {
                myOrder.FilledSize += feedOrder.Size;

            }
            feedOrder.Side = myOrder.Side;
            Log.Information($"[{myOrder.Id}] Adding match to {nameof(MatchQueue)}");
            MatchQueue.Add(feedOrder);


            var side = feedOrder.Side;
            var productType = feedOrder.ProductId;
            SwingOrderOptions options = SwingOptions.ContainsKey(productType) ? SwingOptions[productType] : null;
            if (options == null)
                return;
            var product = OrdersService.GetProduct(productType);
            var increment = product.QuoteIncrement;// buyside currency increment USD for BTCUSD
            var sellIncement = product.BaseIncrement; //sellside currency incrrement BTC for BTCUSD
            if (side == OrderSide.Buy)
            {
                var queuedTotal = MatchQueue.MatchedBuys.QueueTotal;
                if (queuedTotal >= 10m)
                {
                    var aggregate = MatchQueue.MatchedBuys.Aggregate();

                    var size = aggregate.Size;
                    var sellPrice = options.SellPrice.ToPrecision(product.QuoteIncrement);
                    var newOrder = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Sell, sellPrice, size);
                    if (filled)
                    {
                        Log.Information($"[{myOrder.Id}] {nameof(OrderManager)}.{nameof(OrderManager.BuyOrders)}.TryRemove({myOrder.Id}, out)");
                        OrderManager.BuyOrders.TryRemove(myOrder.Id, out OrderResponse removedBuy);
                    }
                    Log.Information($"[{myOrder.Id}] {nameof(OrdersService)}.{nameof(OrdersService.PlaceLimitSellOrder)}({newOrder.ToJson()})");
                    OrdersService.PlaceLimitSellOrder(newOrder, this);
                }
                else
                {
                    string message = $"[{myOrder.Id}] queued matched buy fill {queuedTotal.ToCurrency(myOrder.ProductId)}";
                    Log.Information(message);
                    Console.WriteLine(message);
                }
            }
            else
            {
                var queuedTotal = MatchQueue.MatchedSells.QueueTotal;
                if (queuedTotal >= 10m)
                {
                    var aggregate = MatchQueue.MatchedSells.Aggregate();
                    var total = aggregate.Total;
                    var svc = new AccountService();
                    var feeRate = svc.MakerFeeRate;
                    var fee = total * feeRate;
                    var totalRemaining = (total - fee);
                    var amountRemaining = totalRemaining;//.ToPrecision(product.QuoteIncrement);
                    var nextFee = amountRemaining * feeRate;
                    amountRemaining -= nextFee;

                    var preciseAmountRemaining = amountRemaining.ToPrecision(product.QuoteIncrement);

                    var price = options.BuyPrice.ToPrecision(product.QuoteIncrement);
                    var newOrderSize = amountRemaining.ToPrecision(product.QuoteIncrement) / price;


                    newOrderSize = newOrderSize.ToPrecision(product.BaseIncrement);
                    var newOrder = OrdersService.CreatePostOnlyOrder(productType, OrderSide.Buy, price, newOrderSize);

                    while (newOrder.TotalAmount > totalRemaining)
                    {
                        //amountRemaining -= increment;
                        //newOrderSize = (amountRemaining / price).ToPrecision(product.BaseIncrement);
                        newOrderSize -= product.BaseIncrement;
                        newOrder = OrdersService.CreatePostOnlyOrder(productType, OrderSide.Buy, price, newOrderSize);

                    }
                    if (filled)
                    {
                        Log.Information($"[{myOrder.Id}] {nameof(OrderManager)}.{nameof(OrderManager.SellOrders)}.TryRemove({myOrder.Id}, out)");
                        OrderManager.SellOrders.TryRemove(myOrder.Id, out OrderResponse removedSell);
                    }

                    Log.Information($"[{myOrder.Id}] {nameof(OrdersService)}.{nameof(OrdersService.PlaceLimitBuyOrder)}({newOrder.ToJson()})");
                    var result = OrdersService.PlaceLimitBuyOrder(newOrder, this);
                }
                else
                {
                    string message = $"[{myOrder.Id}] queing matched sell fill {queuedTotal.ToCurrency(myOrder.ProductId)}";
                    Log.Information(message);
                    Console.WriteLine(message);

                }

            }

        }



        private void StopSwing(ProductType productType, SwingOrderOptions options)
        {
            if (options.LastMatchHandler != null)
            {
                this.userFeed.OnLastMatchReceived -= options.LastMatchHandler;
                options.LastMatchHandler = null;
            }

            if (options.MatchHandler != null)
            {
                this.userFeed.OnMatchReceived -= options.MatchHandler;
                options.MatchHandler = null;
            }

            if (options.DoneHandler != null)
            {
                this.userFeed.OnDoneReceived -= options.DoneHandler;
                options.DoneHandler = null;
            }
            if (SwingOptions.ContainsKey(productType))
            {
                SwingOptions.Remove(productType);
            }
            Console.WriteLine($" {tabbracket}Stop Swing: {options.BuyPrice}-{options.SellPrice}");
        }

        private IEnumerable<Action> ParseFillsOptions(ArgumentList arguments)
        {
            List<Action> result = new List<Action>();
            if (arguments.Length == 1 && arguments.arg0 != "last")
            {
                var svc = new CoinbaseService();
                var fillsLists = svc.client.FillsService.GetFillsByProductIdAsync(ProductType, 100, 1).Result;
                var product = OrdersService.GetProduct(ProductType);
                var fills = fillsLists.SelectMany(x => x).ToList();
                result.AddRange(
                    fills.Select(fill =>
                    {
                        return (Action)(() =>
                        {
                            var size = fill.Size.ToPrecision(product.BaseIncrement);
                            var price = fill.Price.ToPrecision(product.QuoteIncrement);
                            var fee = fill.Fee.ToPrecision(0.00000001m);
                            var net = (fill.Size * fill.Price).ToPrecision(product.QuoteIncrement);
                            Console.WriteLine($"{fill.CreatedAt.ToLocalTime()} {fill.Side} {size} @ ${price} (${fee}) = ${net}");
                        });
                    })
                 );

            }
            else if (arguments.Length >= 2 && arguments[1].ToLower() == "export")
            {
                var svc = new CoinbaseService();
                var products = svc.client.ProductsService.GetAllProductsAsync().Result.ToList();
                Console.WriteLine($"Getting fills for {products.Count} products");
                List<CoinbaseData.DbFill> dbFills = new List<CoinbaseData.DbFill>();
                foreach (var product in products)
                {
                    Console.WriteLine($"[{DateTime.Now}] Getting fills for {product.Id}");
                    var fillProduct = product.Id;
                    if (fillProduct == ProductType.Unknown) continue;
                    if (product.QuoteCurrency == Currency.EUR || product.QuoteCurrency == Currency.GBP) continue;
                    var productFills = svc.client.FillsService.GetFillsByProductIdAsync(fillProduct, 100, int.MaxValue).Result.SelectMany(x => x).ToList();
                    dbFills.AddRange(productFills.Select(x =>
                        new CoinbaseData.DbFill(x.TradeId, x.ProductId, x.Price, x.Size, x.OrderId, x.CreatedAt, x.Liquidity, x.Fee, x.Settled, x.Side)
                    ));
                    Console.WriteLine($"[{DateTime.Now}] Found {productFills.Count} fills Total: {dbFills.Count}");

                }
                Console.WriteLine($"Sorting {dbFills.Count} fills");
                dbFills = dbFills.OrderBy(x => x.CreatedAt).ThenBy(x => x.TradeId).ToList();
                Console.WriteLine($"Saving {dbFills.Count} fills");

                var maxFillPrecision = dbFills.Max(x => x.Fee.GetPrecision());
                var maxPricerecision = dbFills.Max(x => x.Price.GetPrecision());
                var maxSizePrecision = dbFills.Max(x => x.Size.GetPrecision());
                CoinbaseData.DbFills.Save(dbFills);
                var fillProducts = dbFills.Select(x => x.ProductId).Distinct().ToList();
                foreach (var fillProduct in fillProducts)
                {
                    foreach (var product in products)
                    {
                        if (fillProduct == product.Id.ToString())
                        {
                            Console.WriteLine($"[{DateTime.Now}] Updating Candles for {fillProduct}");
                            CandleService.UpdateCandles(product.Id);
                            break;
                        }
                    }
                }
            }
            else if (arguments.arg0 == "last" || arguments.arg1 == "last")
            {
                var product = OrdersService.GetProduct(ProductType);

                if (!arguments.arg1.ParseEnum<LastSide>(out LastSide wantedSide))
                {
                    wantedSide = LastSide.Any;
                }
                var last = GetLast(ProductType, wantedSide);
                if (arguments.arg1 == "over" || arguments.arg1 == "above" || arguments.arg1 == "under" || arguments.arg1 == "below")
                {
                    if (!arguments.arg2.ParseDecimal(out decimal priceThreshold))
                    {
                        result.Add(arguments.InvalidAction("unable to parse price threshold"));
                        return result;
                    }
                    bool over = arguments.arg1 == "over" || arguments.arg1 == "above";
                    var filtered = last.Fills.Take(0).ToList();
                    for (var i = 0; i < last.Fills.Count; i++)
                    {
                        var f = last.Fills[i];
                        if (over ? f.Price > priceThreshold : f.Price < priceThreshold)
                        {
                            filtered.Add(f);
                        }
                    }
                    var qty = 0m;
                    var subtotal = 0m;
                    var fee = 0m;
                    for (var i = 0; i < filtered.Count; i++)
                    {
                        var f = filtered[i];
                        qty += f.Size;
                        fee += f.Fee;
                        subtotal += (f.Size * f.Price);
                    }
                    var total = subtotal;
                    if (filtered[0].Side == OrderSide.Buy)
                    {
                        var total2 = total + fee; //- fee;
                        var price2 = (total2) / qty;
                        total = total2;
                    }
                    else
                    {
                        total -= fee;
                    }
                    var price = (total) / qty;
                    last = new LastOrder
                    {
                        Side = filtered[0].Side,
                        Price = price,
                        Size = qty,
                        Total = total,
                        SubTotal = subtotal,
                        Fee = fee,
                        Fills = filtered,
                        OrderIds = filtered.Select(x => x.OrderId).Distinct().ToList(),
                    };
                    Console.WriteLine($"[{DateTime.Now}] {ProductType} Fills {arguments.arg1} {priceThreshold.ToPrecision(product.QuoteIncrement)}: {last.Side} {last.Size.ToPrecision(product.BaseIncrement)} @ {last.Price.ToPrecision(product.QuoteIncrement)} = {last.Total.ToPrecision(product.QuoteIncrement)} ({last.OrderIds.Count} orders, {last.Fills.Count} fills)");
                    result.Add(() => { });
                    return result;
                }
                Session["lastPrice"] = last.Price;
                Console.WriteLine($"[{DateTime.Now}] {ProductType} Fill Summary: {last.Side} {last.Size.ToPrecision(product.BaseIncrement)} @ {last.Price.ToPrecision(product.QuoteIncrement)} = {last.Total.ToPrecision(product.QuoteIncrement)} ({last.OrderIds.Count} orders, {last.Fills.Count} fills)");
                result.Add(() => { });
                if (profitMeter != null)
                {
                    profitMeter.LastPrice = last.Price;
                    profitMeter.Side = last.Side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
                }
            }
            else if (arguments.Length >= 2 && arguments[1].ToLower() == "summary")
            {
                var product = OrdersService.GetProduct(ProductType);
                var orders = OrderManager.SellOrders.Where(x => x.Value.ProductId == ProductType);
                var totalSells = orders.Sum(x => x.Value.Size - x.Value.FilledSize);
                var balance = AccountService.GetAccountBalance(ProductType, OrderSide.Sell);
                var totalBalance = totalSells + balance;


                var svc = new CoinbaseService();
                //int pages = 1;
                //int page = 0;
                //var fillsLists = svc.client.FillsService.GetFillsByProductIdAsync(ProductType, 100, pages).Result;
                var fillsLists = svc.client.FillsService.GetFillsByProductIdAsync(ProductType).Result.SelectMany(x => x).ToList();

                var fifo = new Fifo();
                if (fillsLists.Count > 0)
                {
                    int fillIndex = 0;
                    var fills = fillsLists; // fillsLists[page];
                    decimal current = 0;

                    Func<bool> loopControl = null;
                    if (arguments.Length == 3 && arguments[2] == "all")
                    {
                        loopControl = () => fillIndex < fills.Count;
                    }
                    else
                    {
                        loopControl = () => totalBalance != current;
                    }

                    while (loopControl())
                    {
                        if (fillIndex == fills.Count)
                        {
                            //fillsLists = svc.client.FillsService.GetFillsByProductIdAsync(ProductType, 100, ++pages).Result;
                            //fills = fillsLists[++page];
                            //fillIndex = 0;
                        }
                        fillIndex++;
                        if (fillIndex >= fills.Count)
                            break;
                        var fill = fills[fillIndex];
                        fifo.Add(fill);
                        if (fill.Side == OrderSide.Buy)
                        {
                            current += fill.Size;
                        }
                        else
                        {
                            current -= fill.Size;
                        }

                    }
                    if (totalBalance == current)
                    {
                        string bp1 = fifo.BreakEvenPrice.ToString();
                    }
                    fifo.Update();

                    var value = totalBalance * LastTicker.Price;
                    var sellTotal = fifo.SellTotal + value;
                    var bePct = fifo.BuyTotal == 0 ? 0 : sellTotal / fifo.BuyTotal; //(value / fifo.BreakEvenTotal) - 1;
                    var beNet = sellTotal - fifo.BuyTotal;
                    Action act = () =>
                    {
                        var ticker = LastTicker;
                        Console.WriteLine($"{ProductType} Fill Summary");
                        var sellValue = totalSells * ticker.Price;
                        var holdValue = balance * ticker.Price;
                        Console.WriteLine($"==> Balance {totalBalance.ToPrecision(product.QuoteIncrement)} - Open Sell Qty {totalSells.ToPrecision(product.QuoteIncrement)} ({sellValue.ToPrecision(product.QuoteIncrement)}) - Holding QTY: {balance.ToPrecision(product.QuoteIncrement)} ({holdValue.ToPrecision(product.QuoteIncrement)}) Total: {value.ToPrecision(product.QuoteIncrement)}");
                        Console.WriteLine($" ==> BE: {fifo.BalanceQty.ToPrecision(product.BaseIncrement)} @ {fifo.BreakEvenPrice.ToPrecision(product.QuoteIncrement)} = {fifo.BreakEvenTotal.ToPrecision(product.QuoteIncrement)}");
                        Console.WriteLine($" ==> Net: {beNet.ToPrecision(product.QuoteIncrement)} {bePct.ToString("P")}");

                        Console.WriteLine($" ==> Buys: {fifo.BuyQty.ToPrecision(product.BaseIncrement)} @ {fifo.BuyAverage.ToPrecision(product.QuoteIncrement)} = {fifo.BuyTotal.ToPrecision(product.QuoteIncrement)}");
                        Console.WriteLine($" ==> Sells: {fifo.SellQty.ToPrecision(product.BaseIncrement)} @ {fifo.SellAverage.ToPrecision(product.QuoteIncrement)} = {fifo.SellTotal.ToPrecision(product.QuoteIncrement)}");
                        Console.WriteLine($" ==> Settled: {fifo.SettledQty.ToPrecision(product.BaseIncrement)} @ {fifo.SetttledAverage.ToPrecision(product.QuoteIncrement)} = {fifo.SettledTotal.ToPrecision(product.QuoteIncrement)} - Net {fifo.SettledNet.ToPrecision(product.QuoteIncrement)}");
                    };
                    act();
                    result.Add(() => { });

                }

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
            bool nolog = false;
            if (arguments.Length == 1)
            {
                addBuys = true;
                addSells = true;
            }
            else if (arguments.Length == 2)
            {

                switch (arguments.arg1)
                {

                    case "buy":
                        addBuys = true;
                        break;
                    case "sell":
                        addSells = true;
                        break;
                    case "nolog":
                        addBuys = true;
                        addSells = true;
                        nolog = true;
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
                var svc = new AccountService();
                var orderTotal = svc.GetBalance(ProductType, OrderSide.Buy);
                var coinTotal = svc.GetBalance(ProductType, OrderSide.Sell);
                var totalCoinValue = coinTotal * LastTicker.Price;
                var totalValue = orderTotal + totalCoinValue;
                result.Add(() => Console.WriteLine($"[{DateTime.Now}] {OrderManager.AllOrders.Count} Orders:"));
                if (addBuys)
                {
                    var orders = OrderManager.BuyOrders.Values.OrderBy(x => x.Price).ThenBy(x => x.Price * x.Size).ToList();
                    if (orders.Count > 0) { result.Add(() => Console.WriteLine()); }
                    var totalSize = orders.Sum(x => x.Size) - orders.Sum(x => x.FilledSize);
                    var totalAmount = orders.Sum(x => (x.Size - x.FilledSize) * x.Price);
                    orderTotal += totalAmount;
                    totalValue += totalAmount; //TODO: ProductType is bug
                    var avg = totalSize > 0 ? totalAmount / totalSize : 0m;
                    result.Add(() => Console.WriteLine($"Buys {orders.Count} ({totalSize} {ProductType} @ {(avg).ToPrecision(Product.QuoteIncrement)} {totalAmount.ToCurrency()})"));
                    result.AddRange(Enumerable.Range(0, orders.Count).Select(i =>
                        {
                            var x = orders[i];
                            var product = OrdersService.GetProduct(x.ProductId);
                            var size = x.Size.ToPrecision(product.BaseIncrement);
                            var price = x.Price.ToPrecision(product.QuoteIncrement);
                            var fee = x.FillFees.ToPrecision(0.00000001m);
                            var remainingSize = x.Size - x.FilledSize;
                            var net = (remainingSize * x.Price).ToPrecision(product.QuoteIncrement);
                            return (Action)(() => Console.WriteLine($" [{i}] {x.ProductId} {x.Side} {remainingSize.ToPrecision(product.BaseIncrement)} @ ${price} = ${net}"));
                        }
                    ));
                    if (orders.Count > 0) { result.Add(() => Console.WriteLine()); }

                }
                if (addSells)
                {

                    if (OrderManager.SellOrders.Count > 0)
                    {
                        var orders = OrderManager.SellOrders.Values.OrderBy(x => x.ProductId.ToString()).ThenBy(x => x.Price).ThenBy(x => x.Price * x.Size).ToList();
                        //var ticker = Client.Instance.ProductsService.GetProductTickerAsync().Result.
                        var sellGroups = OrderManager.SellOrders.ToLookup(x => x.Value.ProductId);

                        var groupLines = sellGroups
                            .Select(x =>
                            {
                                var ticker = Client.Instance.ProductsService.GetProductTickerAsync(x.Key).Result;
                                var totalSize = x.Sum(y => y.Value.Size);
                                var filledSize = x.Sum(y => y.Value.FilledSize);
                                var coinValue = ((totalSize - filledSize) * ticker.Price);
                                totalValue += coinValue;
                                //.ToPrecision(0.01m).ToString("C");
                                var product = OrdersService.GetProduct(x.Key);
                                var sellOrdersQty = (x.Sum(y => y.Value.Size) - x.Sum(y => y.Value.FilledSize));
                                var sellOrdersValue = x.Sum(y => (y.Value.Size - y.Value.FilledSize) * y.Value.Price);
                                var sellOrdersAveragePrice = sellOrdersValue / sellOrdersQty;
                                var coinsRemaining = (x.Sum(y => y.Value.Size) - x.Sum(y => y.Value.FilledSize));
                                var tickerFormatted = ticker.Price.ToPrecision(product.QuoteIncrement);
                                var qtyFormatted = sellOrdersQty.ToPrecision(product.BaseIncrement);
                                return $" {x.Key} {ticker.Price} ({x.Count()} Sells) ({qtyFormatted} @ {sellOrdersAveragePrice.ToPrecision(product.QuoteIncrement)} = ${sellOrdersValue.ToCurrency(Currency.USD)}) ({qtyFormatted} @ {tickerFormatted} = {coinValue.ToPrecision(product.QuoteIncrement)})";
                            })
                            .ToList();

                        var totalAmount = sellGroups.Sum(x => x.Sum(y => (y.Value.Size - y.Value.FilledSize) * y.Value.Price));
                        orderTotal += totalAmount;
                        if (orders.Count > 0) { result.Add(() => Console.WriteLine()); }
                        result.Add(() => Console.WriteLine($"Sells {orders.Count} (${totalAmount.ToCurrency(Currency.USD)})"));
                        result.AddRange(groupLines.Select(x => (Action)(() => Console.WriteLine(x))));

                        result.AddRange(Enumerable.Range(0, orders.Count).Select(i =>
                        {
                            var x = orders[i];
                            var product = OrdersService.GetProduct(x.ProductId);
                            var size = x.Size.ToPrecision(product.BaseIncrement);
                            var price = x.Price.ToPrecision(product.QuoteIncrement);
                            var fee = x.FillFees.ToPrecision(0.00000001m);

                            var remainingSize = x.Size - x.FilledSize;
                            var net = (remainingSize * x.Price).ToPrecision(product.QuoteIncrement);
                            return (Action)(() => Console.WriteLine($" [{i}] {x.ProductId} {x.Side} {remainingSize.ToPrecision(product.BaseIncrement)} @ ${price} = ${net}"));
                        }
                        ));
                        if (orders.Count > 0) { result.Add(() => Console.WriteLine()); }
                    }

                }
                //File.AppendAllText(v)

                result.Insert(0, () => Console.WriteLine($"Total: {orderTotal.ToString("C")} ({totalValue.ToString("C")})"));
                if (!nolog)
                    LogBalance($"Total: {orderTotal.ToString("C")} ({totalValue.ToString("C")}) - Last: {LastTicker.Price}");
            }
            return result;
        }

        private IEnumerable<Action> ParseBalanceOptions(ArgumentList arguments)
        {
            List<Action> result = new List<Action>();//TODO: Balance for entire portfolio
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



        public LastOrder GetLast(ProductType ProductType, LastSide wantedSide = LastSide.Any)
        {
            return FillsManager.GetLast(ProductType, wantedSide);
        }

        public IEnumerable<Action> ParseChangeProductType(ArgumentList arguments)
        {

            List<Action> result = new List<Action>();

            if (arguments.arg1 != null && arguments.arg1.StartsWith("producttype."))
            {
                arguments.arg1 = arguments.arg1.Substring("producttype.".Length);
            }
            if (string.IsNullOrEmpty(arguments.arg1))
            {
                result.Add(() => Console.WriteLine($"{nameof(ProductType)}. {ProductType}"));
            }
            else if (arguments.arg1.ParseEnum<ProductType>(out ProductType productType))
            {
                result.Add(() => SetProductType(productType));
            }
            else
            {
                result.Add(arguments.InvalidAction($"Unable to parse {nameof(ProductType)}. Valid values: {string.Join(",", Enum.GetNames(typeof(ProductType)))}"));
            }
            return result;
        }
        public IEnumerable<Action> ParseSpreadOptions(ArgumentList arguments)
        {
            var result = new List<Action>();
            const string usage = "\tUsage: \tspread [list] || (buy|sell) startPrice orderSize increment [totalamount] [exp expPCT]\n\t\tspread [buy|sell] [startPrice] SpreadPct OrderPCT [TotalAmount] [exp expPCT]";
            var options = new SpreadOptions();
            int idx = 1;
            if (idx > arguments.Length)
            {
                result.Add(arguments.InvalidAction(usage));
            }
            else
            {
                if (arguments.arg1 == "list" || arguments.arg1 == "options")
                {
                    if (spreadOptions != null)
                    {
                        result.Add(() => Console.WriteLine($"{tabbracket} {spreadOptions}"));
                        return result;
                    }
                    else
                    {
                        result.Add(() => Console.WriteLine("No spread options"));
                    }
                }
                else if (idx + 1 >= arguments.Length)
                {
                    result.Add(arguments.InvalidAction(usage));
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

                    //TODO: Bug - Only parse strings not numbers: eg, 55 gets parsed as ProductType.AlgoGBP
                    if (bool.Parse(bool.FalseString) && idx < arguments.Length && Enum.TryParse<ProductType>(arguments.parts[idx], out ProductType productType))
                    {
                        idx++;
                        options.ProductType = productType;
                    }
                    else
                    {
                        productType = options.ProductType = ProductType;
                    }

                    if (idx < arguments.Length && arguments.line.Contains("%"))
                    {
                        // see if there is a start order size

                        if (!arguments[idx].Contains("%"))
                        {
                            if (arguments[idx++].ParseDecimal(out decimal startPrice))
                            {
                                options.StartPrice = startPrice;
                            }
                            else
                            {
                                result.Add(arguments.InvalidAction($"Unable to parse {nameof(options.StartPrice)}: {arguments.line}"));
                            }
                        }
                        else// start price not specified, use ticker
                        {
                            var ticker = GetProductTicker(productType);
                            if (options.OrderSide == OrderSide.Buy)
                            {
                                options.StartPrice = ticker.Bid;
                            }
                            else
                            {
                                options.StartPrice = ticker.Ask;
                            }
                        }

                        if (!arguments[idx++].ParseDecimal(out decimal spreadPct))
                        {
                            result.Add(arguments.InvalidAction($"Unable to parse {nameof(spreadPct)}: {arguments.line}"));
                        }
                        else
                        {
                            options.SpreadTotalPCT = spreadPct;
                            if (!arguments[idx++].ParseDecimal(out decimal spreadOrderSize))
                            {
                                result.Add(arguments.InvalidAction($"Unable to parse {nameof(spreadOrderSize)}: {arguments.line}"));
                            }
                            else
                            {
                                options.OrderPctSize = spreadOrderSize;
                                // spread [buy|sell]  [startPrice] [SpreadPct] [OrderPCT]
                                if (idx == arguments.Length)
                                {
                                    options.TotalAmount = AccountService.GetAccountBalance(productType, options.OrderSide);
                                }
                                else if (idx < arguments.Length && arguments[idx].ParseDecimal(out decimal orderMax))
                                {
                                    options.TotalAmount = orderMax;
                                    idx++;
                                    if (idx < arguments.Length && arguments[idx] == "exp" && arguments[++idx].ParseDecimal(out decimal expPct))
                                    {
                                        options.Exponential = expPct;
                                    }
                                }
                                else if (idx < arguments.Length && arguments[idx] == "exp" && arguments[++idx].ParseDecimal(out decimal expPct))
                                {
                                    options.TotalAmount = AccountService.GetAccountBalance(productType, options.OrderSide);
                                    options.Exponential = expPct;
                                }
                                else
                                {
                                    result.Add(arguments.InvalidAction($"Unable to parse {nameof(orderMax)}: {arguments.line}"));
                                }
                            }
                        }

                    }
                    else if (idx < arguments.Length && arguments[idx++].ParseDecimal(out decimal startPrice))
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
            }

            if (result.Count == 0)
            {
                var orderActions = options.GetOrderActions();
                if (orderActions.ToList().Count == 0)
                {
                    result.Add(() => Console.WriteLine("Spread options resulted in no order actions. Make sure you have an available account balance"));
                }
                else
                {
                    spreadOptions = options;
                    result.AddRange(orderActions);
                }
            }
            else
            {
                result.Add(() => Console.WriteLine(usage));
            }

            return result;
        }
        private SpreadOptions spreadOptions;

        private IEnumerable<Action> ParseCancellationOptions(ArgumentList arguments)
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
                            result.Add(() => OrdersService.CancelAllBuys());
                            break;
                        case "sells": //cancel all sells
                            result.Add(() => OrdersService.CancelAllSells());
                            break;
                        case null: // cancel all
                            result.Add(() => OrdersService.CancelAllOrders());
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
                            result.Add(() => OrdersService.CancelAllBuys());
                            break;

                        default: //cancel buys index
                            if (arguments.arg2 == "consolidate" || arguments.arg2 == "c" || arguments.arg2 == "cons")
                            {
                                OrderManager.Refresh();
                                var buys = OrderManager.BuyOrders.Values.OrderBy(x => x.Price).ToList();
                                if (buys.Count == 0)
                                {
                                    result.Add(arguments.InvalidAction("No buys to consolidate."));
                                }
                                else if (buys.Count == 1)
                                {
                                    result.Add(arguments.InvalidAction("Only 1 buy, nothing to consolidate."));
                                }
                                else
                                {
                                    var productType = buys.First().ProductId;
                                    var qty = 0m;
                                    var total = 0m;
                                    for (var i = 0; i < buys.Count; i++)
                                    {
                                        var buy = buys[i];
                                        qty += (buy.Size - buy.FilledSize);
                                        total += buy.Price * (buy.Size - buy.FilledSize);
                                    }
                                    var avgPrice = total / qty;
                                    var minPrice = buys.First().Price;
                                    var maxPrice = buys.Last().Price;
                                    var product = OrdersService.GetProduct(productType);
                                    var price = avgPrice.ToPrecision(product.QuoteIncrement);
                                    if (avgPrice < minPrice || avgPrice > maxPrice)
                                    {
                                        Console.WriteLine($"Invalid average price: {price}");
                                        result.Add(() => { });

                                    }
                                    else
                                    {
                                        OrdersService.CancelAllBuys();

                                        var svc = new AccountService();
                                        var balance = svc.GetBalance(productType, OrderSide.Buy);


                                        var size = balance.ToPrecision(product.BaseIncrement);
                                        qty = qty.ToPrecision(product.BaseIncrement);
                                        var order = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Buy, price, qty);

                                        Console.WriteLine($"{tabbracket} Consolidated Buy: {order}");
                                        OrdersService.PlaceLimitBuyOrder(order, this);
                                        result.Add(() => { });
                                    }
                                }
                                return result;
                            }
                            int cancelBuyIndex = -1;
                            if (arguments.arg2 == "last")
                            {
                                OrderManager.Refresh();
                                cancelBuyIndex = OrderManager.BuyOrders.Count() - 1;
                            }
                            else if (arguments.arg2 == "first")
                            {
                                cancelBuyIndex = 0;
                            }
                            else
                            {
                                cancelBuyIndex = arguments.arg2.ToIndex();
                            }
                            if (cancelBuyIndex > -1)
                            {

                                var order = OrderManager.BuyOrders.ToList()[cancelBuyIndex].Value;
                                result.Add(() => OrdersService.CancelBuyByIndex(cancelBuyIndex));
                                result[0]();
                                result.Clear();

                                result.Add(() => { });
                                if (arguments.arg3.ParseDecimal(out decimal newPrice))
                                {
                                    var total = order.Size * order.Price;
                                    var newSize = total / newPrice;
                                    var newOrder = OrderHelper.CreatePostOnlyOrder(ProductType, OrderSide.Buy, newPrice, order.Size);
                                    OrdersService.PlaceLimitBuyOrder(newOrder);
                                }
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
                            result.Add(() => OrdersService.CancelAllSells());
                            break;
                        default: //cancel buys index
                            if (arguments.arg2 == "consolidate" || arguments.arg2 == "c" || arguments.arg2 == "cons")
                            {
                                OrderManager.Refresh();
                                var sells = OrderManager.SellOrders.Values.ToList();
                                if (sells.Count == 0)
                                {
                                    result.Add(arguments.InvalidAction("No sells to consolidate."));
                                }
                                else if (sells.Count == 1)
                                {
                                    result.Add(arguments.InvalidAction("Only 1 sell, nothing to consolidate."));
                                }
                                else
                                {
                                    var productType = sells.First().ProductId;
                                    var qty = 0m;
                                    var total = 0m;
                                    for (var i = 0; i < sells.Count; i++)
                                    {
                                        var sell = sells[i];
                                        qty += sell.Size;
                                        total += sell.Price * (sell.Size - sell.FilledSize);
                                    }
                                    var avgPrice = total / qty;
                                    OrdersService.CancelAllSells();

                                    var svc = new AccountService();
                                    var balance = svc.GetBalance(productType, OrderSide.Sell);
                                    var product = OrdersService.GetProduct(productType);
                                    var price = avgPrice.ToPrecision(product.QuoteIncrement);
                                    var size = balance.ToPrecision(product.BaseIncrement);
                                    var order = OrderHelper.CreatePostOnlyOrder(productType, OrderSide.Sell, price, size);
                                    Console.WriteLine($"{tabbracket} Consolidated Sell: {order}");
                                    OrdersService.PlaceLimitSellOrder(order, this);
                                    Console.WriteLine($"{tabbracket} Consolidated Sell: {order}");
                                    result.Add(() => Console.WriteLine("Consolidated sells."));
                                }
                                return result;
                            }

                            int cancelSellIndex = -1;
                            if (arguments.arg2 == "last")
                            {
                                OrderManager.Refresh();
                                cancelSellIndex = OrderManager.SellOrders.Count() - 1;
                            }
                            else if (arguments.arg2 == "first")
                            {
                                cancelSellIndex = 0;
                            }
                            else
                            {
                                cancelSellIndex = arguments.arg2.ToIndex();
                            }
                            if (cancelSellIndex > -1)
                            {

                                var order = OrderManager.SellOrders.ToList()[cancelSellIndex].Value;
                                result.Add(() => OrdersService.CancelSellByIndex(cancelSellIndex));
                                result[0]();
                                result.Clear();

                                result.Add(() => { });
                                if (arguments.arg3.ParseDecimal(out decimal newPrice))
                                {
                                    var newOrder = OrderHelper.CreatePostOnlyOrder(ProductType, OrderSide.Sell, newPrice, order.Size);
                                    OrdersService.PlaceLimitSellOrder(newOrder);
                                }
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

        static ArgumentList arguments;
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
            arguments = new ArgumentList(line, parts, arg0, arg1, arg2, arg3, arg4, arg5, arg6);
            return arguments;

        }
    }

    public class LastOrder
    {
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Total { get; set; }
        public decimal Fee { get; set; }
        public List<FillResponse> Fills { get; set; }
        public List<Guid> OrderIds { get; set; }
        public OrderSide Side { get; set; }
        public Product Product { get; set; }
        public ProductType ProductType { get; set; }
        public override string ToString()
        {
            ProductType = Fills.First().ProductId;
            Product = Product ?? OrdersService.GetProduct(Fills.First().ProductId);
            var result = $"{ProductType} Fill Summary: {Side} {Size.ToPrecision(Product.BaseIncrement)} @ {Price.ToPrecision(Product.QuoteIncrement)} = {Total.ToPrecision(Product.QuoteIncrement)} ({OrderIds.Count} orders, {Fills.Count} fills)";
            return result;
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
        public ArgumentList Arguments;
    }


    public class TrendLineOrderOptions : SwingOrderOptions
    {
        public decimal DayDiff { get; private set; }
        public decimal BuyDiff { get; private set; }
        public decimal BuySlope { get; private set; }
        public decimal SellDiff { get; private set; }
        public decimal SellSlope { get; private set; }


        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }
        public decimal StartBuyPrice { get; private set; }
        public decimal EndBuyPrice { get; private set; }
        public decimal StartSellPrice { get; private set; }
        public decimal EndSellPrice { get; private set; }

        public TrendLineOrderOptions(
            ProductType productType,
            DateTime start,
            decimal startBuyPrice,
            decimal endBuyPrice,
            DateTime end,
            decimal startSellPrice,
            decimal endSellPrice
            ) : base(productType)
        {

            this.StartDate = start;
            this.EndDate = end;
            this.StartBuyPrice = startBuyPrice;
            this.EndBuyPrice = endBuyPrice;
            this.StartSellPrice = startSellPrice;
            this.EndSellPrice = endSellPrice;

            this.DayDiff = (decimal)end.Subtract(start).TotalDays;
            this.BuyDiff = endBuyPrice - startBuyPrice;
            this.BuySlope = BuyDiff / DayDiff;

            this.SellDiff = endSellPrice - startSellPrice;
            this.SellSlope = SellDiff / DayDiff;
        }
        public override decimal BuyPrice
        {
            get
            {
                var delta = DateTime.Now.Subtract(StartDate).TotalDays;
                return StartBuyPrice + (BuySlope * (decimal)delta).ToPrecision(Product.QuoteIncrement);
            }
        }
        public override decimal SellPrice
        {
            get
            {
                var delta = DateTime.Now.Subtract(StartDate).TotalDays;
                return StartSellPrice + (SellSlope * (decimal)delta).ToPrecision(Product.QuoteIncrement);
            }
        }
    }

    public class SwingOrderOptions
    {
        public virtual decimal BuyPrice { get; private set; }
        public virtual decimal SellPrice { get; private set; }
        public ProductType ProductType { get; private set; }
        public Product Product { get; private set; }
        //public EventHandler<WebfeedEventArgs<Done>> DoneHandler { get; internal set; }
        public EventHandler<WebfeedEventArgs<Match>> MatchHandler { get; internal set; }
        public EventHandler<WebfeedEventArgs<LastMatch>> LastMatchHandler { get; internal set; }
        public EventHandler<WebfeedEventArgs<Done>> DoneHandler { get; internal set; }

        protected SwingOrderOptions(ProductType productType)
        {
            this.ProductType = productType;
            this.Product = OrdersService.GetProduct(productType);
        }


        public SwingOrderOptions(ProductType productType, decimal buyPrice, decimal sellPrice)
            : this(productType)
        {

            this.BuyPrice = buyPrice.ToPrecision(Product.QuoteIncrement);
            this.SellPrice = sellPrice.ToPrecision(Product.QuoteIncrement);
            this.ProductType = productType;
        }

        public override string ToString()
        {
            return $"{BuyPrice} - {SellPrice}";
        }
    }
    public class SpreadOptions
    {
        public OrderSide OrderSide { get; set; }
        public ProductType ProductType { get; set; }
        public Product Product { get; set; }
        public decimal StartPrice { get; set; }
        public decimal OrderSize { get; set; }
        public decimal Increment { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? SpreadTotalPCT { get; internal set; }
        public decimal? OrderPctSize { get; internal set; }
        public decimal Exponential { get; internal set; }

        internal IEnumerable<Action> GetOrderActions()
        {
            Product = OrdersService.GetProduct(ProductType);

            if (SpreadTotalPCT != null) return GetPercentageOrderActions();
            var actions = new List<Action>();
            var currentPrice = StartPrice;
            var amountRemaining = TotalAmount;

            while (amountRemaining > 0)
            {
                var order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, OrderSize);
                amountRemaining -= (OrderSide == OrderSide.Buy) ? order.TotalAmount : OrderSize;
                if (amountRemaining >= 0m && order.IsValid())
                {
                    actions.Add(() =>
                    {
                        OrdersService.PlaceOrder(order);
                        Console.WriteLine($"{DateTime.Now}: Placed order {order}");
                        System.Threading.Thread.Sleep(100);
                    });
                }

                else if (actions.Count > 0)// need to get USD amount of order
                {
                    amountRemaining += order.TotalAmount;
                    //actions.RemoveAt(actions.Count - 1);
                    if (order.OrderSide == OrderSide.Sell)
                    {
                        order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, OrderSize + amountRemaining);
                    }
                    else
                    {
                        var newSize = (amountRemaining / currentPrice).ToPrecision(Product.BaseIncrement);
                        order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, newSize);
                        while (order.TotalAmount > amountRemaining)
                        {
                            var diffAmount = (order.TotalAmount - amountRemaining);
                            var diffSize = diffAmount / currentPrice;
                            newSize -= diffSize;
                            newSize = newSize.ToPrecision(Product.QuoteIncrement);
                            order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, newSize);
                        }

                    }
                    amountRemaining -= order.TotalAmount;
                    if (amountRemaining >= 0m && order.IsValid())
                    {
                        actions.Add(() =>
                        {
                            OrdersService.PlaceOrder(order);
                            Console.WriteLine($"{DateTime.Now}: Placed order {order}");
                            System.Threading.Thread.Sleep(100);
                        });

                    }
                    break;
                }
                currentPrice += (OrderSide == OrderSide.Buy) ? (-Increment) : Increment;
            }
            return actions;
        }

        private IEnumerable<Action> GetPercentageOrderActions()
        {
            var actions = new List<Action>();
            var currentPrice = StartPrice;
            var amountRemaining = TotalAmount;

            if (!SpreadTotalPCT.HasValue)
            {
                actions.Add(() => Console.WriteLine($"{nameof(SpreadTotalPCT)} can not be null."));
            }
            else if (!OrderPctSize.HasValue)
            {
                actions.Add(() => Console.WriteLine($"{nameof(OrderPctSize)} can not be null."));
            }
            else
            {
                //OrderPctSize = .1m;
                var totalPct = SpreadTotalPCT.Value;
                var orderPct = OrderPctSize.Value;
                int wantedOrders = (int)(1 / orderPct);

                var orderAmounts = new List<decimal>();
                if (Exponential != 0)
                {
                    var tempRemaining = amountRemaining;
                    var precision = OrderSide == OrderSide.Buy ? Product.QuoteIncrement : Product.BaseIncrement;
                    for (var i = 0; i < wantedOrders; i++)
                    {
                        var tempAmount = tempRemaining * Exponential;
                        tempAmount = tempAmount.ToPrecision(precision);
                        orderAmounts.Add(tempAmount);
                        tempRemaining -= tempAmount;
                    }
                    while (tempRemaining > Product.QuoteIncrement)
                    {
                        for (var i = orderAmounts.Count - 1; tempRemaining > Product.QuoteIncrement && i >= 0; i--)
                        {
                            var tempAmount = tempRemaining * Exponential;
                            if (tempAmount <= Product.QuoteIncrement)
                            {
                                orderAmounts[0] += tempRemaining.ToPrecision(precision);
                                tempRemaining = 0;
                                break;
                            }
                            tempAmount = tempAmount.ToPrecision(precision);
                            orderAmounts[i] += tempAmount;
                            tempRemaining -= tempAmount;
                            if (tempRemaining <= Product.QuoteIncrement)
                            {
                                orderAmounts[0] += tempRemaining.ToPrecision(precision);
                                tempRemaining = 0;
                            }
                        }
                        //orderAmounts[0] += tempRemaining.ToPrecision(precision);
                    }
                }

                var amountPerOrder = amountRemaining * orderPct;
                var startPrice = currentPrice;
                var targetPct = OrderSide == OrderSide.Buy ? 1 - totalPct : 1 + totalPct;
                var targetPrice = currentPrice * targetPct;
                var diff = Math.Abs(StartPrice - targetPrice);
                var incRaw = (diff / (wantedOrders - 1));
                Increment = incRaw.ToPrecision(Product.QuoteIncrement);

                var product = OrdersService.GetProduct(ProductType);

                while (amountRemaining > 0)
                {
                    if (Exponential != 0 && orderAmounts.Count > 0)
                    {
                        amountPerOrder = orderAmounts.Last();
                        orderAmounts.RemoveAt(orderAmounts.Count - 1);

                    }
                    var orderSize = (OrderSide == OrderSide.Buy) ?
                        (amountPerOrder / currentPrice).ToPrecision(product.BaseIncrement) :
                        amountPerOrder.ToPrecision(Product.BaseIncrement);
                    var order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, orderSize);
                    amountRemaining -= (OrderSide == OrderSide.Buy) ? order.TotalAmount : orderSize;

                    //TODO: fix last order of exponential
                    //if (amountRemaining < order.TotalAmount)
                    //{
                    //    amountPerOrder += amountRemaining;
                    //    orderSize = (OrderSide == OrderSide.Buy) ?
                    //    (amountPerOrder / currentPrice).ToPrecision(product.BaseIncrement) :
                    //    amountPerOrder.ToPrecision(Product.BaseIncrement);
                    //    order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, orderSize);
                    //    amountRemaining = 0;//-= (OrderSide == OrderSide.Buy) ? order.TotalAmount : orderSize;
                    //}
                    if (amountRemaining >= 0m && order.IsValid())
                    {
                        actions.Add(() =>
                        {
                            OrdersService.PlaceOrder(order);
                            Console.WriteLine($"{DateTime.Now}: Placed order {order}");
                            System.Threading.Thread.Sleep(100);
                        });
                    }

                    else if (actions.Count > 0)// need to get USD amount of order
                    {
                        amountRemaining += order.TotalAmount;
                        //actions.RemoveAt(actions.Count - 1);
                        if (order.OrderSide == OrderSide.Sell)
                        {
                            order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, orderSize + amountRemaining);
                        }
                        else
                        {
                            var newSize = (amountRemaining / currentPrice).ToPrecision(product.BaseIncrement);
                            order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, newSize);
                            while (order.TotalAmount > amountRemaining)
                            {
                                var diffAmount = (order.TotalAmount - amountRemaining);
                                var diffSize = (diffAmount / currentPrice);
                                newSize -= diffSize;
                                newSize = newSize.ToPrecision(product.BaseIncrement);
                                order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, newSize);
                            }

                        }
                        amountRemaining -= order.TotalAmount;
                        if (amountRemaining >= 0m && order.IsValid())
                        {
                            actions.Add(() =>
                            {
                                OrdersService.PlaceOrder(order);
                                Console.WriteLine($"{DateTime.Now}: Placed order {order}");
                                System.Threading.Thread.Sleep(100);
                            });

                        }
                        break;
                    }
                    currentPrice += (OrderSide == OrderSide.Buy) ? (-Increment) : Increment;
                }
            }
            return actions;
        }
        public override string ToString()
        {
            var result = "";

            var rangeHigh = OrderSide == OrderSide.Buy ? StartPrice : StartPrice * (1m * SpreadTotalPCT.Value);
            var rangeLow = OrderSide == OrderSide.Buy ? StartPrice * (1m - SpreadTotalPCT.Value) : StartPrice;
            rangeHigh = rangeHigh.ToPrecision(Product.QuoteIncrement);
            rangeLow = rangeLow.ToPrecision(Product.QuoteIncrement);

            if (SpreadTotalPCT.HasValue)
            {
                result = $"spread {OrderSide} {StartPrice.ToPrecision(Product.QuoteIncrement)} {SpreadTotalPCT.Value.ToString("P")} {OrderPctSize.Value.ToString("P")}";

            }
            else
            {
                result = $"spread {OrderSide} {StartPrice.ToPrecision(Product.QuoteIncrement)} {StartPrice.ToPrecision(Product.QuoteIncrement)} {OrderSize.ToPrecision(Product.BaseIncrement)}";
            }
            var strTotal = OrderSide == OrderSide.Buy ? TotalAmount.ToPrecision(Product.QuoteIncrement) : TotalAmount.ToPrecision(Product.BaseIncrement);
            result = $"{result} {strTotal}";
            if (Exponential != 0)
            {
                result = $"{result} exp {Exponential.ToString("P")}";
            }
            result = $"{result} [{rangeLow} - {rangeHigh}]";
            return result;
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

        public string this[int index] => index < this.Length ? parts[index] : null;

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




}
