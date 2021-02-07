using System;

namespace CoinbaseAudit
{
    public class Account
    {
        public string portfolio { get; set; }
        public string type { get; set; }
        public DateTime time { get; set; }
        public decimal amount { get; set; }
        public decimal balance { get; set; }
        public string amount_balance_unit { get; set; }
        public Guid? transfer_id { get; set; }
        public int? trade_id { get; set; }
        public Guid? order_id { get; set; }
        public int id { get; set; }

        public Account() { }

        public Account(string portfolio, string type, DateTime time, decimal amount, decimal balance, string amount_balance_unit, Guid? transfer_id, int? trade_id, int id)
        {
            this.portfolio = portfolio;
            this.type = type;
            this.time = time;
            this.amount = amount;
            this.balance = balance;
            this.amount_balance_unit = amount_balance_unit;
            this.transfer_id = transfer_id;
            this.trade_id = trade_id;
            this.id = id;
        }

        public Account Clone()
        {
            return new Account(portfolio, type, time, amount, balance, amount_balance_unit, transfer_id, trade_id, id);
        }
    }
}
