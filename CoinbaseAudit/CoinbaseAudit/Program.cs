using CoinbaseAudit.Constants;
using CoinbaseData;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseAudit
{
    class Program
    {
        static void Main(string[] args)
        {
  
            var audit = new Audit();
            audit.Run();
        }
    }
    public class TxTest
    {
        public void Run()
        {
            var tx = new Tx();
            tx.Id = 1;
            tx.Portfolio = Portfolio.CoinbasePro;
            tx.Time = DateTime.Parse("1/1/2018");
            tx.Unit = "LTC";
            tx.Size = 0.999m;
            tx.Price = 200;
            tx.PriceUnit = "USD";
            tx.Fee = 0.2m;
            tx.FeeUnit = "USD";
            tx.UsdSpotPrice = 200;
            tx.UsdSubTotal = 199.8m;
            tx.UsdFees = 0.2m;
            tx.UsdTotal = 200;
            tx.OrderId = Guid.NewGuid();
            tx.TransferId = null;
            tx.TradeId = 1;

            var sell1 = new Tx();
            sell1.Portfolio = Portfolio.CoinbasePro;
            sell1.Time = DateTime.Parse("1/1/2018 01:00");
            sell1.Unit = "LTC";
            sell1.Size = 0.25m;
            sell1.Price = 200;
            sell1.PriceUnit = "USD";
            sell1.Fee = 0.05m;
            sell1.FeeUnit = "USD";
            sell1.UsdSpotPrice = 200;
            sell1.UsdSubTotal = 49.95m;
            sell1.UsdFees = 0.05m;
            sell1.UsdTotal = 50;
            sell1.OrderId = Guid.NewGuid();
            sell1.TransferId = null;
            sell1.TradeId = 2;

            var sell2 = new Tx();
            sell2.Portfolio = Portfolio.CoinbasePro;
            sell2.Time = DateTime.Parse("1/1/2018 01:00");
            sell2.Unit = "LTC";
            sell2.Size = 0.25m;
            sell2.Price = 200;
            sell2.PriceUnit = "USD";
            sell2.Fee = 0.05m;
            sell2.FeeUnit = "USD";
            sell2.UsdSpotPrice = 200;
            sell2.UsdSubTotal = 49.95m;
            sell2.UsdFees = 0.05m;
            sell2.UsdTotal = 50;
            sell2.OrderId = Guid.NewGuid();
            sell2.TransferId = null;
            sell2.TradeId = 3;

            var sell3 = new Tx();
            sell3.Portfolio = Portfolio.CoinbasePro;
            sell3.Time = DateTime.Parse("1/1/2018 01:00");
            sell3.Unit = "LTC";
            sell3.Size = 0.25m;
            sell3.Price = 200;
            sell3.PriceUnit = "USD";
            sell3.Fee = 0.05m;
            sell3.FeeUnit = "USD";
            sell3.UsdSpotPrice = 200;
            sell3.UsdSubTotal = 49.95m;
            sell3.UsdFees = 0.05m;
            sell3.UsdTotal = 50;
            sell3.OrderId = Guid.NewGuid();
            sell3.TransferId = null;
            sell3.TradeId = 4;

            var sell4 = new Tx();
            sell4.Portfolio = Portfolio.CoinbasePro;
            sell4.Time = DateTime.Parse("1/1/2018 01:00");
            sell4.Unit = "LTC";
            sell4.Size = 0.249m;
            sell4.Price = 200;
            sell4.PriceUnit = "USD";
            sell4.Fee = 0.0498m;
            sell4.FeeUnit = "USD";
            sell4.UsdSpotPrice = 200;
            sell4.UsdSubTotal = 49.7502m;
            sell4.UsdFees = 0.0498m;
            sell4.UsdTotal = 49.8m;
            sell4.OrderId = Guid.NewGuid();
            sell4.TransferId = null;
            sell4.TradeId = 5;

            tx.AddDispistions(sell1, sell2, sell3, sell4);
            if (tx.Balance != 0)
            {
                throw new Exception();
            }
        }
    }

    public class Tx
    {
        public int Id;
        public string Portfolio;
        public DateTime Time;
        public string Side;
        public string Unit;
        public decimal Size;
        public decimal Price;
        public string PriceUnit;
        public decimal Fee;
        public string FeeUnit;
        public decimal UsdSpotPrice;
        public decimal UsdSubTotal;
        public decimal UsdFees;
        public decimal UsdTotal;
        public Guid? OrderId;
        public Guid? TransferId;
        public int? TradeId;
        public List<Tx> Dispositions = new List<Tx>();
        public decimal Balance => Size - Dispositions.Sum(x => x.Size);
        public decimal DisposedUsd => Dispositions.Sum(x => x.UsdTotal);
        public decimal DisposedUsdFees => Dispositions.Sum(x => x.UsdFees);
        public decimal DisposedUsdSubTotal => Dispositions.Sum(x => x.UsdSubTotal);

        internal void AddDispistions(params Tx[] sells)
        {

            decimal total = 0;
            for (var i = 0; i < sells.Length; i++)
            {
                var disposition = sells[i];
                if (disposition.Unit != Unit)
                {
                    throw new ArgumentException($"Invalid unit [{i}]: {disposition.Unit } for TX {Id} {disposition.Unit}");
                }
                if (total + disposition.Size > Size)
                {
                    //Todo: Split and return partial;
                    throw new ArgumentException($"Invalid unit: {disposition.Unit } for TX {Id} {disposition.Unit}");
                }

            }

            Dispositions.AddRange(sells);
        }
    }

    public class CbTxns
    {
        public DateTime time { get; set; }
        public string side { get; set; }
        public string asset { get; set; }
        public decimal quantity { get; set; }
        public decimal usdSpotPrice { get; set; }
        public decimal usdSubTotal { get; set; }
        public decimal usdFees { get; set; }
        public decimal usdTotal { get; set; }
        public string notes { get; set; }
        public int id { get; set; }
    }


    public class Audit
    {
        public void Run()
        {
            var cbTxns = TableHelper.Get<CbTxns>();
            var fills = TableHelper.Get<CoinbaseData.DbFill>("dbfills");
            var startDate = DateTime.Parse("8/1/2018");
            var endDate = DateTime.Parse("9/1/2018");
            var marchFills = fills.Where(x => x.CreatedAt >= startDate && x.CreatedAt <= endDate).ToList();
            var json = JsonConvert.SerializeObject(marchFills);
            File.WriteAllText("dbfills_8-1-2018_9-1-2018.json", json);
            var account = TableHelper.Get<Account>();
            var m = new Dictionary<string, Wallet>();
            foreach (var tx in cbTxns)
            {


                var wallet = new Wallet()
                {
                    Portfolio = "Coinbase",
                    Unit = tx.asset
                };

                if (!m.ContainsKey(wallet.WalletKey))
                {

                    m.Add(wallet.WalletKey, wallet);
                }
                else
                {
                    wallet = m[wallet.WalletKey];
                }
                wallet.AddCoinbaseTxn(tx);
            }
        }
    }
    public class Wallet
    {
        public string Portfolio { get; set; }
        public string Unit { get; set; }
        public string WalletKey => $"{Portfolio}-{Unit}";

        public void AddCoinbaseTxn(CbTxns tx)
        {
            switch (tx.side)
            {
                case CoinbaseTxnSide.Buy:
                    // add to wallet
                    // remove from USD?
                    break;
                case CoinbaseTxnSide.CoinbaseEarn:
                    // add to wallet
                    break;
                case CoinbaseTxnSide.Convert:
                    // remove from to wallet
                    // add to wallet
                    break;
                case CoinbaseTxnSide.Receive:
                    // add to target wallet
                    // remove from source wallet
                    break;
                case CoinbaseTxnSide.RewardsIncome:
                    //add to wallet
                    break;
                case CoinbaseTxnSide.Sell:
                    // remove from to wallet
                    // add to USD?
                    break;
                case CoinbaseTxnSide.Send:
                    // remove from source wallet
                    // Add to target wallet
                    break;
            }
        }
    }
}
