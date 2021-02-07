using CoinbaseData;
using System.Collections.Generic;

namespace CoinbaseAudit
{
    public class AuditManagerUSD
    {
        public Dictionary<string, Queue<AuditTx>> Transactions;
        public Queue<AuditTx> Settled;
        public AuditManagerUSD()
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
            var queue = GetQueue(fill.ProductId);
            queue.Enqueue(buyTx);
        }

        private void AddSellTx(DbFill fill)
        {
            System.Diagnostics.Debug.Assert(fill.Side == Constants.CoinbaseTxnSide.Sell);
            var queue = GetQueue(fill.ProductId);
            bool completed = false;
            // todo: Get USD Cost basis.
            while (!completed)
            {
                if (queue.Count > 0)
                {
                    var tx = queue.Peek();
                    System.Diagnostics.Debug.Assert(tx.Size > 0);
                    System.Diagnostics.Debug.Assert(tx.Undisposed > 0);
                    var costBasisPct = tx.Undisposed / tx.Size;
                    if (tx.Undisposed == fill.Size)
                    {
                        tx = queue.Dequeue();// if settled remove it from the tx queue
                        tx.SellSize = fill.Size;
                        tx.SellPrice = fill.Price;
                        tx.SellFee = fill.Fee;
                        tx.SaleTotalProceeds = tx.SellPrice * tx.SellSize;
                        tx.SaleNetProceeds = tx.SaleTotalProceeds - tx.SellFee;
                        tx.SaleTradeId = fill.TradeId;
                        tx.SaleOrderId = fill.OrderId;
                        tx.SaleTime = fill.CreatedAt;
                        tx.CostBasis = tx.Cost * costBasisPct;
                        tx.NetProfit = tx.SaleNetProceeds - tx.CostBasis;
                        tx.Undisposed -= fill.Size;
                      
                        System.Diagnostics.Debug.Assert(tx.Undisposed == 0, "Tx has  not been disposed");                   
                        SettleTransaction(tx);
                        completed = true;
                    }
                    else if (tx.Undisposed > fill.Size)
                    {
                        // not going to settle, so 
                        //  deduect the disposed balance from the tx queue and leave it there
                        //  Create a partial settled tx from the purcahse settled it and put it in the settled queue.
                        var settled = tx.Clone();
                        //settled.Size = fill.Size;
                        settled.SellSize = fill.Size;
                        settled.SellPrice = fill.Price;
                        settled.SellFee = fill.Fee;
                        settled.SaleTotalProceeds = settled.SellSize * settled.SellPrice;
                        settled.SaleNetProceeds = settled.SaleTotalProceeds - settled.SellFee;
                        settled.SaleTradeId = fill.TradeId;
                        settled.SaleOrderId = fill.OrderId;
                        settled.SaleTime = fill.CreatedAt;

                        //calculate the partial net partial for just this portion of the disposition of the total cost basis
                        settled.CostBasis = tx.Cost * (settled.SellSize.Value / settled.Size);
                        settled.NetProfit = settled.SaleNetProceeds - settled.CostBasis;
                        settled.Undisposed = 0;
                        tx.Undisposed -= fill.Size;
                        SettleTransaction(settled);
                        completed = true;
                    }
                    else
                    {
                        // sell size is greater than the existing transaction.
                        // So settle out the tx in the queue and move it to the settled queue.
                        // Continue to apply the remainder of the sell to successive txns in the tx queue.

                        tx = queue.Dequeue();
                        System.Diagnostics.Debug.Assert(tx.Undisposed > 0, "Tx has been disposed");
                        //tx.Size = tx.Undisposed;
                        tx.SellSize = tx.Undisposed;
                        tx.SellPrice = fill.Price;
                        var txFeePct = (tx.SellSize ?? 0m) / fill.Size;
                        tx.SellFee = fill.Fee * txFeePct;
                        tx.SaleTotalProceeds = tx.SellPrice * tx.SellSize;
                        tx.SaleNetProceeds = tx.SaleTotalProceeds - tx.SellFee;
                        tx.SaleTradeId = fill.TradeId;
                        tx.SaleOrderId = fill.OrderId;
                        tx.SaleTime = fill.CreatedAt;
                        tx.CostBasis = tx.Cost * (tx.SellSize.Value / tx.Size);
                        tx.NetProfit = tx.SaleNetProceeds - tx.CostBasis;
                        var fillFeePct = ((fill.Size - tx.SellSize) ?? 0m) / fill.Size;
                        tx.Undisposed = 0;
                        // fill.Size -= txPct; // just subract to get an exact number.
                        System.Diagnostics.Debug.Assert(fill.Size > tx.SellSize);
                        fill.Size -= tx.SellSize.Value;
                        fill.Fee *= fillFeePct;
                        SettleTransaction(tx);

                    }
                }
                else
                {
                    //todo: handled mismatched balance;
                    completed = true;
                }
            }
            //while have buy tx, pop off stack and complete tx


        }

        private void SettleTransaction(AuditTx tx)
        {
            System.Diagnostics.Debug.Assert(tx.SaleTotalProceeds > 0, "SaleTotalProceeds not set");
            System.Diagnostics.Debug.Assert(tx.SaleNetProceeds > 0, "SaleNetProceeds not set");
            System.Diagnostics.Debug.Assert(tx.NetProfit != null, "NetProfit not set");
            System.Diagnostics.Debug.Assert(tx.SellSize > 0, "SellSize not set");
            Settled.Enqueue(tx);
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
}
