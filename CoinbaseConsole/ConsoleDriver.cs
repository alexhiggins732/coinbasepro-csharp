using CoinbasePro.Services.Accounts.Models;
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
        public ConsoleDriver(ProductType productType)
        {
            SetProductType(productType);
            log = new StreamWriter("ConsoleDriver.log", true);
            log.AutoFlush = true;
            OrderSyncer = new OrderSyncer(this);
        }

        public Func<Ticker, String> FormatTicker => (ticker) =>
            $"{DateTime.Now}: {ticker.ProductId}: {ticker.BestBid}-{ticker.BestAsk}";


        public CoinbaseTicker ticker;
        private CoinbaseWebSocket userFeed;

        void SetProductType(ProductType productType)
        {
            this.ProductType = productType;
            Console.WriteLine($"{tabbracket}Set Product: {productType}");
            SetTicker(productType);
        }

        public Ticker LastTicker { get; private set; } = null;
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
            Console.Title = FormatTicker(LastTicker = e.LastOrder);
        }

        public WebSocketFeedLogger SocketLogger;
        private void CreateCoinbaseWebSocket()
        {
            this.SocketLogger = new WebSocketFeedLogger();
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

            userFeed.OnOpenReceived += (sender, e) =>
            {
                string message = $"[{DateTime.Now}] {e.LastOrder.ToDebugString()}";
                Console.WriteLine(message);
                log.WriteLine(message);
                OrderManager.AddOrUpdate(e.LastOrder);
            };
            userFeed.OnReceivedReceived += (sender, e) =>
            {
                string message = $"[{DateTime.Now}] {e.LastOrder.ToDebugString()}";
                log.WriteLine(message);
                Console.WriteLine(message);
                OrderManager.AddOrUpdate(e.LastOrder);
            };
            userFeed.OnMatchReceived += (sender, e) =>
            {
                var myOrder = OrderManager.TryGetByFirstIds(e.LastOrder.MakerOrderId, e.LastOrder.TakerOrderId);
                string message = $"[{DateTime.Now}] {e.LastOrder.ToDebugString()}";
                log.WriteLine(message);
                Console.WriteLine(message);
            };
            userFeed.OnLastMatchReceived += (sender, e) =>
            {
                var myOrder = OrderManager.TryGetByFirstIds(e.LastOrder.MakerOrderId, e.LastOrder.TakerOrderId);
                string message = $"[{DateTime.Now}] {e.LastOrder.ToDebugString()}";
                log.WriteLine(message);
                Console.WriteLine(message);
            };
            userFeed.OnDoneReceived += (sender, e) =>
            {
                string message = $"[{DateTime.Now}] {e.LastOrder.ToDebugString()}";
                log.WriteLine(message);
                Console.WriteLine(message);
                OrderManager.AddOrUpdate(e.LastOrder);
            };

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

            ProductType productType = DefaultProductType;

            List<Action> commandActions = new List<Action>();
            if (arg0 == "cancel")
            {
                commandActions.AddRange(ParseCancellationOptions(arguments));
                //cancel all, cancel buys, cancel sells, cancel all buys, cancel all sells
            }
            else if (arg0 == "cls")
            {
                commandActions.Add(() => Console.Clear());
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
            else if (arg0 == "swing")
            {
                commandActions.AddRange(ParseSwingOptions(arguments));
            }
            else if (arg0 == "feeds")
            {
                commandActions.Add((Action)(() => Console.WriteLine($"=> {string.Join(",", ProductSubscriptions)}")));
            }
            else
            {
                commandActions.Add(arguments.InvalidAction());
            }

            result.Actions = commandActions;
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
                if (OrderSide == OrderSide.Buy)
                {
                    var target = StartPrice * (1 - TrailingStopPct * TargetMult);
                    var trigger = StartPrice * (1 - TrailingStopPct * TriggerMult);
                    var targetRounded = target.ToPrecision(Product.QuoteIncrement);
                    var triggerRounded = trigger.ToPrecision(Product.QuoteIncrement);
                    return $"{ProductType} {OrderSide} {Price} Trail: {TrailingStopPct.ToString("P")} (next {targetRounded} @ {triggerRounded})";
                }
                else
                {
                    var target = StartPrice * (1 + TrailingStopPct * TargetMult);
                    var trigger = StartPrice * (1 + TrailingStopPct * TriggerMult);
                    var targetRounded = target.ToPrecision(Product.QuoteIncrement);
                    var triggerRounded = trigger.ToPrecision(Product.QuoteIncrement);
                    return $"{ProductType} {OrderSide} {Price} Trail: {TrailingStopPct.ToString("P")} (next {targetRounded} @ {triggerRounded})";
                }
            }

            internal void On_BuyTickerReceived(object sender, WebfeedEventArgs<Ticker> e)
            {
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
                    if (doCallBack)
                    {
                        decimal callBackAmount = e.LastOrder.Price * (1 - (TrailingStopPct * 2));
                        var roundedCallBackAmount = callBackAmount.ToPrecision(Product.QuoteIncrement);
                        string callBack = $"stop {ProductType} sell {roundedCallBackAmount} {TrailingStopPct}";
                        Console.WriteLine($"Stop callback: {callBack}");
                        Thread.Sleep(500);
                        Driver.ParseArguments(callBack);
                    }


                }
                else if (TrailingStopPct > 0m)
                {
                    var current = Price;
                    var currStop = StartPrice * (1 - (TrailingStopPct * (TargetMult + 1)));
                    var target = StartPrice * (1 - TrailingStopPct * TargetMult);
                    var trigger = StartPrice * (1 - TrailingStopPct * TriggerMult);
                    var targetRounded = target.ToPrecision(Product.QuoteIncrement);
                    var triggerRounded = trigger.ToPrecision(Product.QuoteIncrement);
                    while (lastPrice < triggerRounded)
                    {
                        Price = targetRounded;
                        TargetMult++;
                        TriggerMult++;
                        target = StartPrice * (1 - TrailingStopPct * TargetMult);
                        trigger = StartPrice * (1 - TrailingStopPct * TriggerMult);
                        targetRounded = target.ToPrecision(Product.QuoteIncrement);
                        triggerRounded = trigger.ToPrecision(Product.QuoteIncrement);
                        Console.WriteLine($"[{DateTime.Now}] Decreasing trailing buy to {Price} (next {targetRounded} @ {triggerRounded}): {this}");
                        //Console.WriteLine($"[{DateTime.Now}]  => Next to { StartPrice *(1 + TrailingStopPct * targetMult)} (Trigger:{StartPrice * (1 + TrailingStopPct * triggerMult)}): {this}");

                    }
                }
            }
            public int TriggerMult = 2;
            public int TargetMult = 1;
            private decimal _startPrice;
            bool doCallBack = true;
            public decimal StartPrice
            {
                get => _startPrice;
                set => Price = _startPrice = value;
            }

            internal void On_SellTickerReceived(object sender, WebfeedEventArgs<Ticker> e)
            {
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

                    if (doCallBack)
                    {
                        decimal callBackAmount = e.LastOrder.Price * (1 + (TrailingStopPct*2));
                        var roundedCallBackAmount = callBackAmount.ToPrecision(Product.QuoteIncrement);
                        string callBack = $"stop {ProductType} buy {roundedCallBackAmount} {TrailingStopPct}";
                        Console.WriteLine($"Stop callback: {callBack}");
                        Thread.Sleep(500);
                        Driver.ParseArguments(callBack);
                    }


                }
                else if (TrailingStopPct > 0m)
                {
                    var current = Price;
                    var currStop = StartPrice * (1 + (TrailingStopPct * (TargetMult - 1)));
                    var target = StartPrice * (1 + TrailingStopPct * TargetMult);
                    var trigger = StartPrice * (1 + TrailingStopPct * TriggerMult);
                    var targetRounded = target.ToPrecision(Product.QuoteIncrement);
                    var triggerRounded = trigger.ToPrecision(Product.QuoteIncrement);
                    while (lastPrice > triggerRounded)
                    {
                        Price = targetRounded;
                        TargetMult++;
                        TriggerMult++;
                        target = StartPrice * (1 + TrailingStopPct * TargetMult);
                        trigger = StartPrice * (1 + TrailingStopPct * TriggerMult);
                        targetRounded = target.ToPrecision(Product.QuoteIncrement);
                        triggerRounded = trigger.ToPrecision(Product.QuoteIncrement);
                        Console.WriteLine($"[{DateTime.Now}] Increasing trailing sell to {Price} (next {targetRounded} @ {triggerRounded}): {this}");
                        //Console.WriteLine($"[{DateTime.Now}]  => Next to { StartPrice *(1 + TrailingStopPct * targetMult)} (Trigger:{StartPrice * (1 + TrailingStopPct * triggerMult)}): {this}");

                    }
                }
            }
            internal void Complete()
            {
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
            if (arguments.Length > 1)
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
                        if (parsedProduct)
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
        private IEnumerable<Action> ParseSwingOptions(ArgumentList arguments)
        {
            List<Action> result = new List<Action>();

            if (arguments.line.IndexOf("cancel") > -1)
            {
                result.AddRange(ParseSwingCancelOptions(arguments));
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
            orderOptions.MatchHandler = UserFeed_OnMatchReceived;
            orderOptions.LastMatchHandler = UserFeed_OnLastMatchReceived;
            orderOptions.DoneHandler = UserFeed_OnDoneReceived;


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
            orderOptions.MatchHandler = UserFeed_OnMatchReceived;
            orderOptions.LastMatchHandler = UserFeed_OnLastMatchReceived;
            orderOptions.DoneHandler = UserFeed_OnDoneReceived;


            this.userFeed.OnMatchReceived += orderOptions.MatchHandler;
            this.userFeed.OnLastMatchReceived += orderOptions.LastMatchHandler;
            this.userFeed.OnDoneReceived += orderOptions.DoneHandler;
            Console.WriteLine($"{tabbracket}Swing:{orderOptions.BuyPrice}-{orderOptions.SellPrice}");

        }


        private ConcurrentDictionary<string, Task> MatchCache = new ConcurrentDictionary<string, Task>();
        private void UserFeed_OnDoneReceived(object sender, WebfeedEventArgs<Done> e)
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
                    UserFeed_OnMatchReceived(sender, args);
                });
            }

        }

        private void UserFeed_OnLastMatchReceived(object sender, WebfeedEventArgs<LastMatch> e)
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
            UserFeed_OnMatchReceived(sender, args);



        }
        private void UserFeed_OnMatchReceived(object sender, WebfeedEventArgs<Match> e)
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
                    OrdersService.PlaceLimitSellOrder(newOrder);
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
                    var result = OrdersService.PlaceLimitBuyOrder(newOrder);
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
            if (arguments.Length == 1)
            {
                var svc = new CoinbaseService();
                var fillsLists = svc.client.FillsService.GetFillsByProductIdAsync(ProductType, 30, 1).Result;
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
                var svc = new AccountService();
                var orderTotal = svc.GetBalance(ProductType.LtcUsd, OrderSide.Buy);
                var totalValue = orderTotal;
                result.Add(() => Console.WriteLine($"[{DateTime.Now}] {OrderManager.AllOrders.Count} Orders:"));
                if (addBuys)
                {
                    var orders = OrderManager.BuyOrders.Values.OrderBy(x => x.Price).ThenBy(x => x.Price * x.Size).ToList();
                    if (orders.Count > 0) { result.Add(() => Console.WriteLine()); }
                    var totalSize = orders.Sum(x => x.Size) - orders.Sum(x => x.FilledSize);
                    var totalAmount = orders.Sum(x => (x.Size - x.FilledSize) * x.Price);
                    orderTotal += totalAmount;
                    totalValue += totalAmount; //TODO: ProductType is bug
                    result.Add(() => Console.WriteLine($"Buys {orders.Count} ({totalSize} {ProductType} {totalAmount.ToCurrency()})"));
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

                                return $" {x.Key} {ticker.Price} ({x.Count()} Sells) ({sellOrdersQty.ToPrecision(product.BaseIncrement)} @ {sellOrdersAveragePrice.ToPrecision(product.QuoteIncrement)} = ${sellOrdersValue.ToCurrency(Currency.USD)}) ({coinValue.ToPrecision(product.QuoteIncrement)})";
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
                result.Insert(0, () => Console.WriteLine($"Total: {orderTotal.ToString("C")} ({totalValue.ToString("C")})"));
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

            var options = new SpreadOptions();
            int idx = 1;
            if (idx > arguments.Length)
            {
                result.Add(arguments.InvalidAction());
            }
            else
            {
                if (idx + 1 >= arguments.Length)
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
                                else if (idx < arguments.Length && arguments[idx++].ParseDecimal(out decimal orderMax))
                                {
                                    options.TotalAmount = orderMax;
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
                result.AddRange(options.GetOrderActions());
            }
            else
            {
                result.Add(() => Console.WriteLine("\tUsage: \tspread (buy|sell) startPrice orderSize increment [totalamount]\n\t\tspread [buy|sell] [startPrice] SpreadPct OrderPCT [TotalAmount] "));
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
                            int cancelBuyIndex = arguments.arg2.ToIndex();
                            if (cancelBuyIndex > -1)
                            {
                                result.Add(() => OrdersService.CancelBuyByIndex(cancelBuyIndex));
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
                            int cancelSellIndex = arguments.arg2.ToIndex();
                            if (cancelSellIndex > -1)
                            {
                                result.Add(() => OrdersService.CancelSellByIndex(cancelSellIndex));
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
                            newSize = newSize.ToPrecision(Product.BaseIncrement);
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
                var totalPct = SpreadTotalPCT.Value;
                var orderPct = OrderPctSize.Value;
                int wantedOrders = (int)(1 / orderPct);
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
                    var orderSize = (OrderSide == OrderSide.Buy) ?
                        (amountPerOrder / currentPrice).ToPrecision(product.BaseIncrement) :
                        amountPerOrder.ToPrecision(Product.BaseIncrement);
                    var order = OrdersService.CreatePostOnlyOrder(ProductType, OrderSide, currentPrice, orderSize);
                    amountRemaining -= (OrderSide == OrderSide.Buy) ? order.TotalAmount : orderSize;
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




}
