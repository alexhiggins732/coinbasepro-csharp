using CoinbasePro.Network.Authentication;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using System;
using System.Text;
using System.Threading.Tasks;
using CoinbaseData;
using System.IO;
using CoinbaseUtils;
using CoinbasePro.WebSocket.Types;
using System.Linq;
using SuperWebSocket;

namespace CoinbaseConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var productType = OrderService.GetProduct(ProductType.DaiUsdc);

            var c = OrderService.service.client;
            

            var o = c.AccountsService.GetAllAccountsAsync().Result;

            RunManager();

        }
        static void LogError(string message)
        {
            var sw = new StreamWriter("error.log", true);
            sw.WriteLine($"[{DateTime.Now}] [ERROR] {message}");
            sw.Close();
        }

        static void RunManager()
        {
            var driver = new ConsoleDriver();
            driver.Run();
        }
    }
    public class DevelopmentTests
    {

        static void RunTestTickerServer()
        {
            //TestWebSocketServer();
            var server = DevelopmentTests.TestTickerServer();
            if (server == null)
            {
                Console.WriteLine("Failed To create server. Press any key to exit");
                var k = Console.ReadKey();
            }
            else
            {
                Console.WriteLine("Server Started. Press any key to exit");
                var k = Console.ReadKey();

            }
            server?.Stop();
        }
        static CoinbaseWebSocket TestWebSocket()
        {
            var socket = new CoinbaseWebSocket();
            var productTypes = new[] { ProductType.LtcUsd };
            var channelTypes = new[] { ChannelType.Ticker };
            socket.OnTickerReceived += (sender, e) => Socket_OnTickerReceived(socket, e);
            socket.Start(productTypes.ToList(), channelTypes.ToList());
            Console.WriteLine("Started Socket");
            return socket;

        }
        static CoinbaseTicker TestTicker()
        {
            var ticker = CoinbaseTicker.Create(ProductType.LtcUsd);
            ticker.OnTickerReceived += (sender, e) => Socket_OnTickerReceived(ticker, e);
            Console.WriteLine("Started ticker");
            return ticker;


        }

        private static void Socket_OnTickerReceived(object sender, CoinbasePro.WebSocket.Models.Response.WebfeedEventArgs<CoinbasePro.WebSocket.Models.Response.Ticker> e)
        {
            var ticker = e.LastOrder;
            Console.WriteLine($"{sender.GetType().Name} - Ask:{ticker.BestAsk} - Bid:{ ticker.BestBid}");
        }

        public static TickerServer TestTickerServer()
        {
            var server = TickerServer.Create(2012, ProductType.LtcUsd);
            if (server.Start())
            {
                Console.WriteLine("Started");
                Console.ReadKey();
                return null;
            }
            else
            {
                Console.WriteLine("Failed to start server");
                return null;
            }
        }
        public static void TestWebSocketServer()
        {
            var appServer = new WebSocketServer();

            //Setup the appServer
            if (!appServer.Setup(2012)) //Setup with listening port
            {
                Console.WriteLine("Failed to setup!");
                Console.ReadKey();
                return;
            }

            appServer.NewMessageReceived += (session, message) =>
            {
                Console.WriteLine($"Recieved {message}");
                session.Send(message);
            };


            appServer.NewSessionConnected += (session) =>
            {
                Console.WriteLine($"Connected {session.SessionID}");
            };

            Console.WriteLine();

            //Try to start the appServer
            if (!appServer.Start())
            {
                Console.WriteLine("Failed to start!. Press any key to exit!");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Started. Press any key to exit!");
            Console.ReadKey();
        }

        public static void TestTickers()
        {
            TestWebSocketServer();
            var socket = TestWebSocket();
            var ticker = TestTicker();
            Console.WriteLine("Press any key to exit");
            var k = Console.ReadKey();
            socket.Stop();
            ticker.Stop();
        }

        static void SeedCandles()
        {
            var products = new[] { ProductType.LtcUsd, ProductType.BtcUsd };
            var grans = new[] { CandleGranularity.Minutes1, CandleGranularity.Minutes5, CandleGranularity.Minutes15, CandleGranularity.Hour1, CandleGranularity.Hour6, CandleGranularity.Hour24 };
            Candles.SaveCandles(products, grans);
            //SaveCandles(ProductType.BtcUsd, CandleGranularity.Minutes5);
            TableHelper.AssureCandlesTables(ProductType.LtcUsd);
            //create an authenticator with your apiKey, apiSecret and passphrase
            var authenticator = new Authenticator(Creds.ApiKey, Creds.ApiSecret, Creds.PassPhrase);

            var client = Client.Instance = new CoinbasePro.CoinbaseProClient(authenticator);
            //create the CoinbasePro client

            //use one of the services 
            var allAccounts = client.AccountsService.GetAllAccountsAsync().GetAwaiter().GetResult();

            var candleService = new CandleService();
            //var now = DateTime.UtcNow;
            //var start = now.AddMinutes(-(300 * 15));
            var now = DateTime.Now;
            var start = now.AddMinutes(-(300 * 15));
            var candles = candleService
                .GetCandles(ProductType.LtcUsd, start, now, CandleGranularity.Minutes15);

        }


    }
}
