using CoinbaseData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CoinbaseAudit
{
    public class AuditFill
    {
        private DbFill fill;
        public DbFill Fill => fill.Clone();

        private List<Account> accountTxns;

        public Account BuyTxn { get; private set; }
        public Account SellTxn { get; private set; }
        public Account FeeTxn { get; private set; }

        //calculated
        public string BuyCurrency => BuyTxn.amount_balance_unit;
        public string SellCurrency => SellTxn.amount_balance_unit;
        public List<Account> AccountTxs => accountTxns.ToList();
        public string ProductId => fill.ProductId;
        public string Side => fill.Side;
        public AuditFill(DbFill fill, List<Account> accountTxns)
        {
            this.fill = fill;
            this.accountTxns = accountTxns;
            if (fill != null)
            {
                BuyTxn = accountTxns.First(x => x.type == "match" && x.amount < 0);
                SellTxn = accountTxns.First(x => x.type == "match" && x.amount > 0);
                FeeTxn = accountTxns.FirstOrDefault(x => x.type == "fee" && x.amount < 0) ?? new Account();
            }
        }
        public decimal SellFee => Side == "Sell" ? Math.Abs(FeeTxn.amount) : 0;
        public decimal BuyFee => Side == "Buy" ? Math.Abs(FeeTxn.amount) : 0;
        public decimal SellAmountGross => SellTxn.amount;
        public decimal SellAmountNet => SellTxn.amount - SellFee;
        public decimal BuyAmountGross => BuyTxn.amount - BuyFee;
        public decimal BuyAmountNet => BuyTxn.amount;
    }
}
