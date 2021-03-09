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
            Driver.LastTicker = e.LastOrder;
            if (Driver.profitMeter != null && this != Driver.profitMeter)
            {
                this.Stop();
                return;
            }
            if (LastPrice == 0)
            {
                LastPrice = e.LastOrder.Price;
            }
            else
            {
                var profitPct = e.LastOrder.Price / LastPrice;
                var ticker = e.LastOrder;
                var net = profitPct;
                var diffMinusFee = (profitPct - TakerFeeRate);
                if (Side == OrderSide.Sell)
                {
                    net = diffMinusFee - 1m;
                }
                else
                {
                    net = 1m - (profitPct + TakerFeeRate);
                }
                var side = this.Side;
                //System.Threading.Thread.Sleep(100);
                var priceString = LastPrice.ToPrecision(0.0001m);
                Console.Title = $"{DateTime.Now}: {ProductType} ({Side}): {ticker.Price} ({ticker.BestBid}-{ticker.BestAsk}) Profit: {profitPct.ToString("P")} Net: {net.ToString("P")} - Last: {priceString}";

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
