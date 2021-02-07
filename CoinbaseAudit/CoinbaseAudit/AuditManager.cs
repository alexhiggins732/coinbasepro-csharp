using CoinbaseData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CoinbaseAudit
{
    public class AuditManager
    {
        public Dictionary<string, Queue<AuditTx>> Transactions;
        public Queue<AuditTx> Settled;
        public AuditManager()
        {
            Transactions = new Dictionary<string, Queue<AuditTx>>();
            Settled = new Queue<AuditTx>();

        }

        public void AddTx(DbFill fill)
        {

            if (fill.Side == Constants.CoinbaseProTxnSide.Buy)
            {
                AddBuyTx(fill);
            }
            else
            {
                AddSellTx(fill);
            }
        }

        private void AddBuyTx(DbFill fill)
        {
            System.Diagnostics.Debug.Assert(fill.Side == Constants.CoinbaseTxnSide.Buy);
            var buyTx = new AuditTx(fill);
            var currencies = CurrencyPair.GetCurrencyPair(fill.ProductId);
            bool isUsd = currencies.BuyCurrency == "Usd";

            if (isUsd)
            {

                var queue = GetQueue(currencies.BuyCurrency);
                var balance = queue.ToList().Sum(x => x.Undisposed);
                bool completed = false;
                while (!completed)
                {
                    if (queue.Count > 0)
                    {
                        var tx = queue.Peek();
                        System.Diagnostics.Debug.Assert(tx.Size > 0);
                        AssureBalance(tx);

                        var disposed = tx.Clone();
                        var fillFee = fill.Fee;
                        var fillSellSize = fill.Size * fill.Price + fillFee;

                        // fil.size = 
                        // fillSellSize =   0.0135497324
                        // undisposed =     0.0015049269;
                        var disposedSellSize = fillSellSize < disposed.Undisposed ? fillSellSize : disposed.Undisposed;
                        var disposedPct = disposedSellSize / fillSellSize;
                        var disposedFeePct = fillSellSize < disposed.Undisposed ? 1m : disposedPct;

                        disposed.Undisposed -= disposedSellSize;
                        //disposed.Undisposed -= (fill.Fee * disposedFeePct);
                        if (disposed.IsDisposed())
                            queue.Dequeue();
                        else
                            tx.Undisposed = disposed.Undisposed;


                        var disposedByPct = (fillSellSize * disposedPct);
                        var disposedCoin = (fill.Size * disposedPct);
                        fill.Size -= (fill.Size * disposedPct);
                        fill.Fee -= (fill.Fee * disposedFeePct);
                        completed = fill.Size < .00000001m;


                    }
                    else
                    {
                        completed = true;
                        var mult = (decimal)System.Math.Pow(10, 8);//compare to a constant 0.000000001
                        var roundingErrorSize = fill.Size * mult;
                        roundingErrorSize = System.Math.Truncate(roundingErrorSize);
                        if (roundingErrorSize != 0)
                        {
                            Console.WriteLine($"{fill.CreatedAt}: {fill.Side} {fill.ProductId} TradeId {fill.TradeId} AddBuyTx missing {fill.Size} in {currencies.BuyCurrency} funds. ");
                            //throw new System.InvalidOperationException($"{currencies.BuyCurrency} Queue is empty.");
                        }
                    }
                }
                // doesn't appear this would track the costs correcty? and what about the fee.
                buyTx.CostUsd = buyTx.Cost;
                buyTx.CostFeesUsd = buyTx.CostFees;
                QueueBuy(currencies.SellCurrency, buyTx);
            }
            else
            {
                // buying the sell currency. Size is always the sell currency size.
                //  DbFill: LtcBtc 20M @.01 BTC
                //      Buy 20 LTC
                //      Sell 0.2 BTC


                // in addtion to adding the buy to the queue, need to also process a sell.
                var queue = GetQueue(currencies.BuyCurrency);
                var balance = queue.ToList().Sum(x => x.Undisposed);
                bool completed = false;

                while (!completed)
                {
                    var originalFee = fill.Fee;
                    var feeRate = fill.Fee / (fill.Size + fill.Fee);
                    if (queue.Count > 0)
                    {
                        var tx = queue.Peek();
                        System.Diagnostics.Debug.Assert(tx.Size > 0);
                        System.Diagnostics.Debug.Assert(tx.IsNotDisposed());

                        // 0.04696784208
                        //-0.0468273600000000
                        var netFillSize = fill.Size * fill.Price;
                        var fillSellSize = fill.Size * fill.Price + fill.Fee;

                        // without fee:-0.0060842000
                        // from tx[0]     0.0046186139552 -> 
                        var disposed = tx.Clone();
                        var disposedSellSize = fillSellSize < disposed.Undisposed ? fillSellSize : disposed.Undisposed;
                        var disposedPct = disposedSellSize / disposed.Size;
                        var disposedFeePct = fillSellSize < disposed.Undisposed ? 1m : disposedPct;
                        var disposedFee = originalFee * disposedFeePct;



                        disposed.SaleTradeId = fill.TradeId;
                        disposed.SaleOrderId = fill.OrderId;
                        disposed.SaleTime = fill.CreatedAt;


                        // set sell values
                        disposed.SellSize = disposedSellSize; //fill.Size;
                        disposed.SellPrice = fill.Price;
                        disposed.SellFee = 0; // fee counts as buy fee fill.Fee * disposedFeePct;

                        //do we need both units for non-usd?
                        var buyFee = fill.Fee * disposedFeePct;
                        disposed.SaleTotalProceeds = (disposed.SellSize / disposed.SellPrice).Value;
                        disposed.SaleNetProceeds = ((disposed.SellSize - buyFee) / disposed.SellPrice).Value; ; // disposed.SaleTotalProceeds - fill.Fee * disposedFeePct;
                        // disposed.SaleTotalProceeds+ = disposed.SellFee; // not tracking non-usd fee and fee would need conversion;
                        disposed.CostBasis = disposed.Cost * disposedPct;
                        disposed.CostBasisUsd = disposed.CostUsd * disposedPct;
                        disposed.CostFees = 0; // disposed.PurchaseFee * disposedFeePct;
                        disposed.CostFeesUsd = 0; // disposed.CostFees * disposedFeePct;
                        // carry the usd cost basis without a fee for non-usd transactions.

                        var proceedPct = disposed.SellSize / disposed.CostBasis;
                        disposed.SaleTotalProceedsUsd = disposed.CostBasisUsd * proceedPct;
                        disposed.SaleNetProceedsUsd = disposed.SaleTotalProceedsUsd - (disposed.SaleTotalProceedsUsd * feeRate);
                        disposed.NetProfitUsd = disposed.NetProfit = 0;



                        // USD buy 20 ltc * 100 = 2000, sell 20 ltc @ .01 = .2 BTC, with costbasis of 2,000
                        //todo: should this go in the branch? it will always be zero for non-usd
                        tx.Undisposed -= disposedSellSize;
                        //tx.Undisposed -= disposed.SellFee.Value;
                        disposed.SetDisposed();// disposedSellSize;
                        //disposed.Undisposed -= disposed.SellFee.Value;

                        SettleTransaction(disposed);



                        fill.Size -= disposed.SaleNetProceeds.Value;  // disposed.SaleTotalProceeds.Value; // disposed.SaleNetProceeds.Value;
                        fill.Fee -= buyFee;


                        if (tx.IsDisposed())
                            queue.Dequeue();


                        //need to add sell transaction
                        var dbBuy = fill.Clone();
                        dbBuy.Size = disposed.SaleNetProceeds.Value;
                        dbBuy.Side = Constants.CoinbaseProTxnSide.Buy;
                        dbBuy.Fee = disposedFee; //no fee tracking for non-usd or autobuys

                        var dbBuyTx = new AuditTx(dbBuy);
                        dbBuyTx.Cost = disposed.SaleTotalProceeds.Value;
                        dbBuyTx.CostUsd = disposed.SaleTotalProceedsUsd.Value;
                        var buyFeeRate = buyFee / disposed.CostBasisUsd;

                        dbBuyTx.CostFees = buyFee; // disposed.SaleTotalProceeds.Value - disposed.SaleNetProceeds.Value;
                        dbBuyTx.CostFeesUsd = dbBuyTx.CostUsd * feeRate; //disposed.SaleTotalProceedsUsd.Value - disposed.SaleTotalProceedsUsd.Value;
                        QueueBuy(currencies.SellCurrency, dbBuyTx);
                        completed = fill.Size < 0.00000001m;
                    }
                    else
                    {
                        completed = true;
                        var mult = (decimal)System.Math.Pow(10, 7);//compare to a constant 0.000000001
                        var roundingErrorSize = fill.Size * mult;
                        roundingErrorSize = System.Math.Truncate(roundingErrorSize);
                        if (roundingErrorSize != 0)
                            throw new System.InvalidOperationException($"{currencies.BuyCurrency} Queue is empty.");
                    }
                }



                //{90520fbd-080b-4c81-bc76-386aebab9ce8}: Buy 19.1946448600 LtcBtc @ 0.010260000M:
                // LTCBTCSell: 0.0000357752220957120000000000 @ 0.0102800000M  &  0.19344544887200000000 @ 0.0102700000M
                // => USD: Buy 19.1756252100 @ 75.17 ($1441.4317470357)
                // => LTC: amount: +19.1946448600 balance: 19.1946448600
                // => BTC: amount: -=-0.1969370562 balance: 0.00000

            }

        }

        private void AssureBalance(AuditTx tx)
        {
            System.Diagnostics.Debug.Assert(tx.IsNotDisposed());
        }

        private void QueueBuy(string sellCurrency, AuditTx buyTx)
        {
            //buyTx.Truncate();
            System.Diagnostics.Debug.Assert(buyTx.CostUsd != 0);
            var queue = GetQueue(sellCurrency);

            if (queue.Count == 1)
            {
                var head = queue.Peek();
                //merge if possible.
                if (head.PurchasePrice == buyTx.PurchasePrice)
                {
                    head.Undisposed += buyTx.Undisposed;
                    head.Cost += buyTx.Cost;
                    head.CostUsd += buyTx.CostUsd;
                    head.CostFees += buyTx.CostFees;
                    head.CostFeesUsd += buyTx.CostFeesUsd;
                    head.Size += buyTx.Size;
                    return;
                }
            }
            AssureBalance(buyTx);
            queue.Enqueue(buyTx);
            var balance = queue.ToList().Sum(x => x.Undisposed);
        }

        private void AddSellTx(DbFill fill, bool autoQueueBuy = true)
        {
            System.Diagnostics.Debug.Assert(fill.Side == Constants.CoinbaseProTxnSide.Sell);
            var currencies = CurrencyPair.GetCurrencyPair(fill.ProductId);

            bool isUsd = currencies.BuyCurrency == "Usd";
            decimal buyCostUsd = 0;


            var queue = GetQueue(currencies.SellCurrency);
            bool completed = false;

            var balance = queue.ToList().Sum(x => x.Undisposed);
            if (balance < fill.Size)
            {
                var tradeId = fill.TradeId;
                var shortage = balance - fill.Size;
                string bp = $"{fill.CreatedAt} Insufficient fund: Sell {fill.ProductId} TradeId {tradeId}  ({fill.CreatedAt}) Account {currencies.SellCurrency} is short {shortage}";
                Console.WriteLine(bp);
            }
            //2e6e14e7-1597-44a1-a4e8-01f174ec7da7: Sell 0.3385291800M LtcBtc @ 0.0102800000M:
            // => USD: Buy 19.1756252100 @ 75.17 ($1441.4317470357)
            // => LTC: amount: -0.3385291800 balance: 18.8370960300
            // => BTC: amount: +=0.0034800799 balance: 0.0034800799

            //sell side: credit the fill.size to the sell currency. Deduct fee from buy currency 
            while (!completed)
            {
                var feeRate = fill.Fee / fill.Size;
                if (queue.Count > 0)
                {
                    //TODO: Apply fee based on trade side? 
                    var tx = queue.Peek();
                    System.Diagnostics.Debug.Assert(tx.Size > 0);
                    AssureBalance(tx);


                    bool useBranching = bool.Parse(bool.FalseString);


                    var disposed = tx.Clone();

                    var disposeSize = fill.Size < disposed.Undisposed ? fill.Size : disposed.Undisposed;


                    var disposedSellSize = fill.Size < disposed.Undisposed ? fill.Size : disposed.Undisposed;
                    var disposedPct = disposedSellSize / disposed.Size;
                    var disposedFeePct = fill.Size == disposedSellSize ? 1m : disposedSellSize / fill.Size;
                    //disposed.Size = 5.7790745900, disposed.Undisposed = 2.2934702300
                    //fill.Size = 2.2934702300 

                    disposed.SaleTradeId = fill.TradeId;
                    disposed.SaleOrderId = fill.OrderId;
                    disposed.SaleTime = fill.CreatedAt;

                    // set sell values
                    disposed.SellSize = disposedSellSize; //fill.Size;
                    disposed.SellPrice = fill.Price;
                    disposed.SellFee = fill.Fee * disposedFeePct;

                    //do we need both units for non-usd?

                    disposed.SaleTotalProceeds = ((disposed.SellSize * disposed.SellPrice)).Value;
                    disposed.SaleNetProceeds = disposed.SaleTotalProceeds - disposed.SellFee;
                    //tx 19.17562521 LTC with USD cost of 1441.4317470357
                    // selling -0.3385291800000000 for 0.0034800801196347 BTC
                    // selling 1.765% (0.01765414041485638736052455418219)
                    disposed.CostBasis = disposed.Cost * disposedPct;
                    disposed.CostBasisUsd = disposed.CostUsd * disposedPct;
                    disposed.CostBasisFees = disposed.CostFees * disposedPct;
                    disposed.CostBasisFeesUsd = disposed.CostFeesUsd * disposedPct;


                    if (isUsd)
                    {
                        disposed.SaleTotalProceedsUsd = disposed.SaleTotalProceeds;
                        disposed.SaleNetProceedsUsd = disposed.SaleNetProceeds;
                        disposed.NetProfit = disposed.SaleNetProceeds - disposed.CostBasisUsd;
                        disposed.NetProfitUsd = disposed.SaleNetProceedsUsd - disposed.CostBasisUsd;
                    }
                    else
                    {
                        // carry the usd cost basis without a fee for non-usd transactions.
                        // point here was to get the ratio of sale:cost, if switching transaction.s
                        var proceedPct = 0m;
                        if (bool.Parse(bool.FalseString) && tx.Product == fill.ProductId)
                        {
                            proceedPct = disposed.SaleTotalProceeds.Value / disposed.CostBasis;
                            disposed.SaleTotalProceedsUsd = disposed.CostBasisUsd * proceedPct;
                            disposed.SaleNetProceedsUsd = disposed.SaleTotalProceedsUsd - (disposed.SaleTotalProceedsUsd * feeRate);
                        }
                        else
                        {
                            //pricePerCost =75.17
                            //pricePerDisposed = 0.01028 btc
                            // 1/0.01028 = 97.276264591439688715953307392996
                            // diposedPerCost= 
                            var pricePerCost = tx.PurchasePrice;
                            var pricePerDisposed = fill.Price;
                            var unitCost = pricePerCost * (1 / pricePerDisposed);
                            var disposedUnitValue = disposed.SaleTotalProceeds.Value * unitCost;
                            proceedPct = disposedUnitValue / disposed.CostBasisUsd;
                            disposed.SaleTotalProceedsUsd = disposed.CostBasisUsd * proceedPct;
                            disposed.SaleNetProceedsUsd = disposed.SaleTotalProceedsUsd - (disposed.SaleTotalProceedsUsd * feeRate);
                        }

                     
                        disposed.NetProfitUsd = disposed.NetProfit = 0;
                    }


                    // USD buy 20 ltc * 100 = 2000, sell 20 ltc @ .01 = .2 BTC, with costbasis of 2,000
                    //todo: should this go in the branch? it will always be zero for non-usd

                    disposed.SetDisposed();
                    tx.Undisposed -= disposed.SellSize.Value;
                    //tx.Undisposed -= disposed.SellFee.Value;

                    SettleTransaction(disposed);
                    buyCostUsd += disposed.CostBasisUsd;


                    fill.Size -= disposed.SellSize.Value;
                    fill.Fee -= disposed.SellFee.Value;

                    if (tx.IsDisposed())
                        queue.Dequeue();
                    //selling 5*100 = 500; fee= .50, net 499.5
                    if (!isUsd)
                    {
                        //need to add buy transaction
                        var dbBuy = fill.Clone();
                        dbBuy.Size = disposed.SaleNetProceeds.Value;
                        dbBuy.Side = Constants.CoinbaseProTxnSide.Buy;
                        dbBuy.Fee = 0; //no fee tracking for non-usd or autobuys
                        var buyTx = new AuditTx(dbBuy);
                        buyTx.Cost = disposed.SaleTotalProceeds.Value;
                        buyTx.CostFees = disposed.SaleTotalProceeds.Value - disposed.SaleNetProceeds.Value;
                        buyTx.CostUsd = disposed.SaleTotalProceedsUsd.Value;
                        buyTx.CostFeesUsd = disposed.SaleTotalProceedsUsd.Value - disposed.SaleNetProceedsUsd.Value;
                        QueueBuy(currencies.BuyCurrency, buyTx);
                    }
                    else
                    {
                        var dbBuy = fill.Clone();
                        dbBuy.Size = disposed.SaleNetProceeds.Value;
                        dbBuy.Side = Constants.CoinbaseProTxnSide.Buy;
                        dbBuy.Price = 1m;
                        dbBuy.Fee = disposed.SellFee.Value; //no fee tracking for non-usd or autobuys
                        var buyTx = new AuditTx(dbBuy);
                        //buyTx.CostUsd = buyTx.Cost = buyTx.Size + disposed.SellFee.Value;
                        //buyTx.CostFees = buyTx.CostFees = disposed.SellFee.Value;
                        QueueBuy(currencies.BuyCurrency, buyTx);
                    }

                    completed = completed = fill.Size < 0.00000001m;


                }
                else
                {
                    //todo: handled mismatched balance;

                    completed = true;
                    var mult = (decimal)System.Math.Pow(10, 7);
                    var roundingErrorSize = System.Math.Truncate(fill.Size * mult);
                    if (roundingErrorSize != 0)
                    {
                        Console.WriteLine($"{fill.CreatedAt}: {fill.Side} {fill.ProductId} {fill.TradeId} Missing {fill.Size} in funds");
                        //throw new System.InvalidOperationException($"{currencies.BuyCurrency} Queue is empty.");
                    }

                }
            }
            //while have buy tx, pop off stack and complete tx

            //if (!isUsd && autoQueueBuy)
            //{
            //    string bp = "";
            //    var buySize = fill.Price * fill.Size;
            //    if (fill.Fee != 0)
            //    {
            //        string fillBp = "";
            //    }
            //    var dbBuy = fill.Clone();
            //    dbBuy.Size = buySize;
            //    dbBuy.Side = Constants.CoinbaseProTxnSide.Buy;
            //    dbBuy.Fee = fill.Fee;
            //    fill.Fee = 0;
            //    var buyTx = new AuditTx(dbBuy);
            //    buyTx.CostUsd = buyCostUsd;
            //    QueueBuy(currencies.BuyCurrency, buyTx);
            //}

        }

        public decimal Balance(string productId)
        {
            if (Transactions.ContainsKey(productId))
                return Transactions[productId].Sum(x => x.Undisposed);
            return 0;
        }

        private void SettleTransaction(AuditTx tx)
        {
            //tx.Truncate();
            System.Diagnostics.Debug.Assert(tx.SaleTotalProceeds != null, "SaleTotalProceeds not set");
            System.Diagnostics.Debug.Assert(tx.SaleTotalProceeds >= 0, "SaleTotalProceeds not set");
            System.Diagnostics.Debug.Assert(tx.SaleNetProceeds != null, "SaleNetProceeds not set");
            System.Diagnostics.Debug.Assert(tx.NetProfit != null, "NetProfit not set");
            System.Diagnostics.Debug.Assert(tx.SellSize != null && tx.SellSize > 0, "SellSize not set");
            System.Diagnostics.Debug.Assert(tx.IsDisposed());

            Settled.Enqueue(tx);
        }

        public void Credit(string currency, decimal size, DateTime txnDate)
        {
            if (AccountDebits.ContainsKey(currency))
            {
                var debits = AccountDebits[currency];
                var matched = debits.FirstOrDefault(x => x.Undisposed >= size);
                if (matched != null)
                {
                    debits.Remove(matched);
                    matched.Undisposed = size;
                    var matchQueue = GetQueue(currency);
                    AssureBalance(matched);
                    matchQueue.Enqueue(matched);
                }
                else
                {
                    var remaining = size;
                    while (remaining > 0 && debits.Count > 0)
                    {
                        var tx = debits[0];
                        var undisposed = tx.Undisposed;
                        if (undisposed <= remaining)
                        {
                            remaining -= undisposed;
                            debits.Remove(matched);
                            var matchQueue = GetQueue(currency);
                            AssureBalance(tx);
                            matchQueue.Enqueue(tx);
                        }
                        else
                        {
                            var clone = tx.Clone();

                            clone.Undisposed = remaining;
                            tx.Undisposed -= remaining;
                            remaining -= clone.Undisposed;
                            var matchQueue = GetQueue(currency);
                            AssureBalance(clone);
                            if (tx.IsDisposed())
                                debits.Remove(tx);
                            matchQueue.Enqueue(clone);

                        }
                    }
                }
                return;
            }

            //string bp = "";

            var queue = GetQueue(currency);
            var creditTx = AuditTx.CreateCredit(currency, size, txnDate);
            AssureBalance(creditTx);
            queue.Enqueue(creditTx);
        }
        private Dictionary<string, List<AuditTx>> AccountDebits = new Dictionary<string, List<AuditTx>>();

        public void Debit(Account txn)
        {
            var productId = Constants.AccountCurrency.MapToCurrency[txn.amount_balance_unit];

            if (!AccountDebits.ContainsKey(productId))
                AccountDebits.Add(productId, new List<AuditTx>());



            var debits = AccountDebits[productId];
            var queue = GetQueue(productId);
            var amount = txn.amount;
            //var debitTx = new AuditTx();
            //Settled.Enqueue(debitTx);
            if (queue.Count > 0)
            {
                bool finished = false;
                decimal total = Math.Abs(amount);
                while (!finished)
                {
                    if (queue.Count > 0)
                    {
                        var tx = queue.Peek();
                        if (tx.Undisposed >= total)
                        {
                            var copy = tx.Clone();
                            tx.Undisposed -= (copy.Undisposed = total);
                            if (tx.IsDisposed())
                            {
                                queue.Dequeue();
                            }
                            debits.Add(copy);
                            finished = true;
                        }
                        else
                        {
                            AssureBalance(tx);
                            queue.Dequeue();
                            total -= tx.Undisposed;
                            debits.Add(tx);
                        }
                    }

                }

            }
            else
            {
                throw new NotImplementedException("Need to implement fifo matching to create a debit");
            }
        }

        Queue<AuditTx> GetQueue(string product)
        {
            if (!Transactions.ContainsKey(product))
                Transactions.Add(product, new Queue<AuditTx>());
            //if (!Settled.ContainsKey(product))
            //    Settled.Add(product, new Queue<AuditTx>());
            return Transactions[product];
        }
    }

    public class CurrencyPair
    {
        public string BuyCurrency { get; set; }
        public string SellCurrency { get; set; }
        public CurrencyPair() { }

        public CurrencyPair(string buyCurrency, string sellCurrency)
        {
            BuyCurrency = buyCurrency;
            SellCurrency = sellCurrency;
        }

        public static CurrencyPair GetCurrencyPair(string productId)
        {
            int i = 0;
            var sellCurrency = productId[i++].ToString();
            for (; !char.IsUpper(productId[i]); i++)
            {
                sellCurrency += productId[i];
            }
            var buyCurrency = productId.Substring(i);
            return new CurrencyPair(buyCurrency, sellCurrency);
        }
    }
}
