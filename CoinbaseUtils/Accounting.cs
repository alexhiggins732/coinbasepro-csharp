using CoinbasePro.Services.Fills.Models.Responses;
using CoinbasePro.Services.Orders.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseUtils
{
    public class Accounting
    {

    }

    public class Fifo
    {

        public List<FillResponse> Fills;

        public decimal BalanceQty;
        public decimal BreakEvenPrice;
        public decimal BreakEvenTotal;

        public decimal SettledTotal;
        public decimal SetttledAverage;
        public decimal SettledQty;
        public decimal SettledNet;


        public decimal BuyTotal;
        public decimal BuyQty;
        public decimal BuyAverage;

        public decimal SellTotal;
        public decimal SellQty;
        public decimal SellAverage;


        public Queue<FillResponse> Buys;
        public Queue<FillResponse> Sells;
        public Fifo()
        {
            Fills = new List<FillResponse>();
            Buys = new Queue<FillResponse>();
            Sells = new Queue<FillResponse>();
        }
        public void Add(FillResponse fill)
        {
            Fills.Add(fill);
            if (fill.Side == OrderSide.Buy)
                Buys.Enqueue(fill);
            else
                Sells.Enqueue(fill);
        }
        public void Update()
        {
            BuyTotal = Buys.Sum(x => x.Price * x.Size + x.Fee);
            BuyQty = Buys.Sum(x => x.Size);
            if (BuyTotal == 0) return;
            BuyAverage = BuyTotal / BuyQty;

            SellTotal = Sells.Sum(x => x.Price * x.Size + x.Fee);
            SellQty = Sells.Sum(x => x.Size);
            SellAverage = SellTotal == 0 ? 0 : SellTotal / SellQty;
            BalanceQty = BuyQty - SellQty;
            //(Total Sells USD - Total Buys USD)

            // NET: TotalInvested - PL (amount remaining>
            // buy 20 @ 50 = 1000, sell 5 @ 45 = 225.  Balance= 15. B/E Price = (1000-225)= 775/(balance)15 = 51.66 
            BreakEvenPrice = BalanceQty == 0 ? 0 : (BuyTotal - SellTotal) / BalanceQty;
            BreakEvenTotal = BreakEvenPrice * BalanceQty;

            var remainingBuys = SellQty;

            // calculate current PL
            var settledBuys = new List<FillResponse>();
            for (var i = Fills.Count - 1; remainingBuys > 0 && i > 0; i--)
            {
                var fill = Fills[i];
                if (fill.Side == OrderSide.Buy)
                {
                    if (fill.Size < remainingBuys)
                    {
                        settledBuys.Add(fill);
                        remainingBuys -= fill.Size;
                    }
                    else
                    {
                        var sizePct = remainingBuys / fill.Size;
                        var fee = fill.Fee * sizePct;
                        //var total = fill.Price * fill.Size + fill.Fee;
                        //var feeRate = fill.Fee / total;
                        var tempFill = new FillResponse
                        {
                            Size = remainingBuys,
                            Fee = fill.Fee * sizePct,
                            CreatedAt = fill.CreatedAt,
                            OrderId = fill.OrderId,
                            Liquidity = fill.Liquidity,
                            ProductId = fill.ProductId,
                            Settled = fill.Settled,
                            Side = fill.Side,
                            TradeId = fill.TradeId,
                            Price = fill.Price

                        };
                        remainingBuys -= tempFill.Size;
                        settledBuys.Add(tempFill);
                    }
                }
            }

            SettledTotal = settledBuys.Sum(x => x.Price * x.Size + x.Fee);
            SettledQty = settledBuys.Sum(x => x.Size);
            SetttledAverage = SettledTotal == 0 ? 0 : SettledTotal / SettledQty;
            SettledNet = SellTotal - SettledTotal;


        }
    }
}
