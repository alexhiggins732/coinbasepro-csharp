using CoinbasePro.Shared.Types;
using CoinbasePro.WebSocket.Models.Response;
using CoinbaseUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoinbasePro.Services.Orders.Types;
namespace CoinbaseConsole
{
    public class ProfitMeter
    {
        public OrderSide Side { get; set; }
        public decimal TakerFeeRate = 0.0020m;
        public ProfitMeter(ConsoleDriver driver)
        {
            this.Driver = driver;
            this.ProductType = driver.ProductType;

            var svc = new AccountService();
            TakerFeeRate = svc.TakerFeeRate;
            var balance = svc.GetBalance(ProductType, OrderSide.Sell);
            var product = OrdersService.GetProduct(ProductType);
            balance = balance.ToPrecision(product.BaseIncrement);
            if (balance > 0 || OrderManager.SellOrders.Any(x => x.Value.ProductId == ProductType))
            {
                Side = OrderSide.Sell;
            }
            else
            {
                Side = OrderSide.Buy;
            }
            this.Ticker = CoinbaseTicker.Create(ProductType);
            Ticker.OnTickerReceived += On_TickerReceived;
        }

        private void On_TickerReceived(object sender, WebfeedEventArgs<Ticker> e)
        {
            if (LastPrice == 0)
            {
                LastPrice = e.LastOrder.Price;
            }
            else
            {
                var diff = e.LastOrder.Price / LastPrice;
                var ticker = e.LastOrder;
                var net = diff;
                var diffMinusFee = (diff - TakerFeeRate);
                if (Side == OrderSide.Sell)
                {
                    net = diffMinusFee - 1m;
                }
                else
                {
                    net = 1m - (diff + TakerFeeRate);
                }
                var side = this.Side;
                //System.Threading.Thread.Sleep(100);
                Console.Title = $"{DateTime.Now}: {ProductType} ({Side}): {ticker.Price} ({ticker.BestBid}-{ticker.BestAsk}) Profit: {diff.ToString("P")} Net: {net.ToString("P")}";
            }
        }
        public void Stop()
        {
            this.Ticker.Stop();
            Ticker.OnTickerReceived -= On_TickerReceived;
        }

        public decimal LastPrice = 0m;
        public ConsoleDriver Driver { get; private set; }
        public ProductType ProductType { get; private set; }
        public CoinbaseTicker Ticker { get; private set; }
    }
}
