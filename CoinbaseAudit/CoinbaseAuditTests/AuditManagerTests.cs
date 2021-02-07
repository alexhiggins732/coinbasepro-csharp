using Microsoft.VisualStudio.TestTools.UnitTesting;
using CoinbaseAudit;
using CoinbaseAudit.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoinbaseData;
using System.IO;
using Newtonsoft.Json;
using System.Data.SqlClient;
using Dapper;
namespace CoinbaseAudit.Tests
{

    internal class TradeSide
    {
        public const string Buy = nameof(Buy);
        public const string Sell = nameof(Sell);
    }
    [TestClass()]
    public class AuditManagerTests
    {
        List<DbFill> Mar18Fills;
        List<DbFill> Aug18Fills;

        private int TradeCounter;
        private DateTime TradeDate = DateTime.Parse("1/1/2018");
        private DateTime NextDate() => (TradeDate = TradeDate.AddSeconds(1));
        DbFill CreateFill(string productId, string side, decimal size, decimal price, decimal fee = 0m)
        {
            return new DbFill()
            {
                ProductId = productId,
                CreatedAt = NextDate(),
                Fee = fee,
                Liquidity = "Maker",
                OrderId = Guid.NewGuid(),
                Price = price,
                Settled = true,
                Side = side,
                Size = size,
                TradeId = ++TradeCounter
            };
        }
        public AuditManagerTests()
        {
            var json = File.ReadAllText("dbfills_3-1-2018_4-1-2018.json");
            Mar18Fills = JsonConvert.DeserializeObject<List<DbFill>>(json);
            json = File.ReadAllText("dbfills_8-1-2018_9-1-2018.json");
            Aug18Fills = JsonConvert.DeserializeObject<List<DbFill>>(json);

        }

        public class SettledSummary
        {
            List<AuditTx> settled;
            public SettledSummary(Queue<AuditTx> txns)
            {
                settled = txns.ToList();
            }
            public SettledSummary(List<AuditTx> txns)
            {
                settled = txns.ToList();
            }

            public decimal TotalCost => settled.Sum(x => x.CostBasis);
            public decimal TotalCostFees => settled.Sum(x => x.CostBasisFees);
            public decimal TotalCostNet => TotalCost - TotalCostFees;

            public decimal TotalCostUsd => settled.Sum(x => x.CostBasisUsd);
            public decimal TotalCostFeesUsd => settled.Sum(x => x.CostBasisFeesUsd);
            public decimal TotalCostNetUsd => TotalCostUsd - TotalCostFeesUsd;


            public string TotalCostCurrency => TotalCost.ToString("C");
            public string TotalCostFeesCurrency => TotalCostFees.ToString("C");
            public string TotalCostNetCurrency => TotalCostNet.ToString("C");


            public string TotalCostUsdCurrency => TotalCostUsd.ToString("C");
            public string TotalCostFeesUsdCurrency => TotalCostFeesUsd.ToString("C");
            public string TotalCostNetUsdCurrency => TotalCostNetUsd.ToString("C");




            public decimal TotalDisposed => settled.Sum(x => x.SaleTotalProceeds ?? 0m);
            public decimal TotalNetDisposed => settled.Sum(x => x.SaleNetProceeds ?? 0m);
            public decimal TotalDisposedFees => TotalDisposed - TotalNetDisposed;

            public decimal TotalDisposedUsd => settled.Sum(x => x.SaleTotalProceedsUsd ?? 0m);
            public decimal TotalNetDisposedUsd
            {
                get
                {
                    var linqResult = settled.Sum(x => x.SaleNetProceedsUsd ?? 0m);
                    var result = 0m;
                    for (var i = 0; i < settled.Count; i++)
                    {
                        var settle = settled[i];
                        var settleNetUsd = settle.SaleNetProceedsUsd ?? 0m;
                        result += settleNetUsd;

                    }
                    return result;
                }
            }
            public decimal TotalDisposedFeesUsd => TotalDisposedUsd - TotalNetDisposedUsd;


            public string TotalDisposedCurrency => TotalDisposed.ToString("C");
            public string TotalNetDisposedCurrency => TotalNetDisposed.ToString("C");
            public string TotalDisposedFeesCurrency => TotalDisposedFees.ToString("C");


            public string TotalDisposedUsdCurrency => TotalDisposedUsd.ToString("C");
            public string TotalNetDisposedUsdCurrency => TotalNetDisposedUsd.ToString("C");
            public string TotalDisposedFeesUsdCurrency => TotalDisposedFeesUsd.ToString("C");

            internal void VerifyUsd(int minId, int maxId, bool checkCost)
            {
                var settledUsd = settled.Where(x => x.Product.EndsWith("Usd")).ToList();
                if (settledUsd.Count > 0)
                {
                    var usdSummary = new SettledSummary(settled);

                    /*
                        select format(abs(sum(amount)), 'C') as CostFee from account where 
                           type='fee' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018' 
                           and order_id in (select order_id from account where amount<0 and type='match' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018')

                    */
                    Func<string, object, string> Get = (query, dbparams) =>
                    {
                        var connString = "server=localhost;Initial Catalog=coinbase;Integrated Security=true;";
                        using (var conn = new SqlConnection(connString))
                        {
                            var result = conn.QueryFirst<string>(query, dbparams);
                            return result;
                        }
                    };
                    var minBuyDate = settledUsd.Min(x => x.PurchaseTime);
                    var maxBuyDate = settledUsd.Max(x => x.PurchaseTime);
                    var minSellDate = settledUsd.Min(x => x.SaleTime ?? DateTime.MaxValue);
                    var maxSellDate = settledUsd.Max(x => x.SaleTime ?? DateTime.MinValue);
                    var startDate = minBuyDate < minSellDate ? minBuyDate : minSellDate;
                    var endDate = maxBuyDate > maxSellDate ? maxBuyDate : maxSellDate;

                    var startDateString = startDate.ToString("yyyy-MM-dd HH:mm:ss.fff");//2018-08-01 01:23:43.000
                    var endDateString = endDate.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var dbParams = new { minId, maxId };
                    string dbRaw = "";

                    bool verifyCost = checkCost;

                    if (verifyCost)
                    {


                        var DbTotalCostFeesUsdCurrencyQuery = $@"select format(abs(sum(amount)), 'C') as CostFee from account where 
                           type='fee' and amount_balance_unit='usd' and id between @minId and @maxId
                           and order_id in (select order_id from account where amount<0 and type='match' and amount_balance_unit='usd' and id between @minId and @maxId)";
                        dbRaw = DbTotalCostFeesUsdCurrencyQuery.Replace("@minId", $"{minId}").Replace("@maxId", $"{maxId}");
                        var DbTotalCostFeesUsdCurrency = Get(dbRaw, dbParams);
                        Assert.IsTrue(usdSummary.TotalCostFeesUsdCurrency == DbTotalCostFeesUsdCurrency);


                        var TotalCostUsdCurrencyQuery = $@" select format(abs(sum(amount)), 'C') as CostUsd from account where 
                           amount_balance_unit='usd' and id between @minId and @maxId
                           and order_id in (select order_id from account where amount<0 and type='match' and amount_balance_unit='usd' and id between @minId and @maxId)";
                        dbRaw = TotalCostUsdCurrencyQuery.Replace("@minId", $"{minId}").Replace("@maxId", $"{maxId}");
                        var DbTotalCostUsdCurrency = Get(dbRaw, dbParams);//issue tx[0] bought and sold but tx[1] only bought not sold (still unsettled). DbQuery doesnt not distinquish unsettled.

                        Assert.IsTrue(usdSummary.TotalCostUsdCurrency == DbTotalCostUsdCurrency);


                        var TotalCostNetCurrencyQuery = $@" select format(abs(sum(amount)), 'C') as CostNet from account where 
                           type='match' and amount_balance_unit='usd' and id between @minId and @maxId
                           and order_id in (select order_id from account where amount<0 and type='match' and amount_balance_unit='usd' and id between @minId and @maxId)";
                        dbRaw = TotalCostNetCurrencyQuery.Replace("@minId", $"{minId}").Replace("@maxId", $"{maxId}");
                        var DbTotalCostNetCurrency = Get(dbRaw, dbParams);
                        Assert.IsTrue(usdSummary.TotalCostNetCurrency == DbTotalCostNetCurrency);
                    }

                    var TotalDisposedFeesUsdCurrencyQuery = $@" select format(abs(isnull(sum(amount), 0)), 'C') as DisposeFee from account where 
                           type='fee' and amount_balance_unit='usd' and id between @minId and @maxId
                           and order_id in (select order_id from account where amount>0 and type='match' and amount_balance_unit='usd' and id between @minId and @maxId)";
                    dbRaw = TotalDisposedFeesUsdCurrencyQuery.Replace("@minId", $"{minId}").Replace("@maxId", $"{maxId}");
                    var DbTotalDisposedFeesUsdCurrency = Get(dbRaw, dbParams);
                    Assert.IsTrue(usdSummary.TotalDisposedFeesUsdCurrency == DbTotalDisposedFeesUsdCurrency);


                    var TotalDisposedUsdCurrencyQuery = $@" select format(abs(isnull(sum(amount), 0)), 'C') as DisposedUsd from account where 
                            type='match' and amount_balance_unit='usd' and id between @minId and @maxId
                          and order_id in (select order_id from account where amount>0 and type='match' and amount_balance_unit='usd' and id between @minId and @maxId)";
                    dbRaw = TotalDisposedUsdCurrencyQuery.Replace("@minId", $"{minId}").Replace("@maxId", $"{maxId}");
                    var DbTotalDisposedUsdCurrency = Get(dbRaw, dbParams);
                    if (usdSummary.TotalDisposedUsdCurrency != DbTotalDisposedUsdCurrency)
                    {
                        var auditDisposed = usdSummary.TotalDisposedUsd;
                        var dbDisposed = decimal.Parse(DbTotalDisposedUsdCurrency, System.Globalization.NumberStyles.AllowCurrencySymbol | System.Globalization.NumberStyles.AllowThousands | System.Globalization.NumberStyles.AllowDecimalPoint);
                        var diff = Math.Abs(auditDisposed - dbDisposed);
                        if (diff > 0.01m)
                        {
                            Assert.IsTrue(usdSummary.TotalDisposedUsdCurrency == DbTotalDisposedUsdCurrency);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Rounding error for TotalDisposedUsdCurrency at MaxId {maxId}");
                        }
                    }



                    var TotalNetDisposedUsdCurrencyQuery = $@" select format(abs(isnull(sum(amount), 0)), 'C') as DisposedUsd from account where 
                             amount_balance_unit='usd' and id between @minId and @maxId
                          and order_id in (select order_id from account where amount>0 and type='match' and amount_balance_unit='usd' and id between @minId and @maxId)";
                    dbRaw = TotalNetDisposedUsdCurrencyQuery.Replace("@minId", $"{minId}").Replace("@maxId", $"{maxId}");
                    var DbTotalNetDisposedUsdCurrency = Get(dbRaw, dbParams);
                    if (usdSummary.TotalNetDisposedUsdCurrency != DbTotalNetDisposedUsdCurrency)
                    {
                        var auditDisposed = usdSummary.TotalNetDisposedUsd;
                        var dbDisposed = decimal.Parse(DbTotalNetDisposedUsdCurrency, System.Globalization.NumberStyles.AllowCurrencySymbol | System.Globalization.NumberStyles.AllowThousands | System.Globalization.NumberStyles.AllowDecimalPoint);
                        var diff = Math.Abs(auditDisposed - dbDisposed);
                        if (diff > 0.01m)
                        {
                            Assert.IsTrue(usdSummary.TotalNetDisposedUsdCurrency == DbTotalNetDisposedUsdCurrency);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Rounding error for TotalDisposedUsdCurrency at MaxId {maxId}");
                        }
                    }


                }
            }
        }


        [TestMethod()]
        public void AuditManagerTest_Usd_DaiUsd_DaiUsdc_DaiUsd_WithFees()
        {
            /*

            --buy DaiUsd 100 @ 1 with .1% fee
            match   usd -100    0.1
            match   dai 100     100                   
            fee     usd -.1     0
            cost: 100.1

            --sell DaiUsdc 100 @ 1 with .1% fee
            match   dai     -100    0
            match   usdc     100    100                   
            fee     usdc    -.1     99.9

            cost:       100.1   100
            disposed:   100

            --buy DaiUsdc 99.9 @ 1 with .1% fee
            match   usdc    -99.8001m   0.0999
            match   dia     99.8001     100                   
            fee     usdc    -0.0999     0

            cost:       100.1   100     99.9
            disposed:   100     99.9

            --sell DaiUsdc 99.8001 @ 1 with .1 fee
            match   usd    +99.8001m    99.8001
            match   dia    -99.8001     0                   
            fee     usd    -0.0998001   99.7002999

            cost:       100.1   100     99.9        = 300
            disposed:   100     99.9    99.7002999  = 299.6002999

             */

            var manager = new AuditManager();


            const decimal feePct = 0.001m;

            var initialGross = 100m;
            var net = initialGross;
            var gross = 0m;
            var fee = 0m;

            manager.Credit(Currency.Usd, net, DateTime.UtcNow);
            Assert.IsTrue(manager.Balance("Usd") == net);
            var priceHistory = new List<decimal[]>();
            Action CalcShares = () =>
            {
                gross = net;
                fee = gross * feePct;
                net = gross - fee;
                priceHistory.Add(new[] { gross, fee, net });
            };

            CalcShares();//100m = 99.900M + 0.100M
            var buy0 = CreateFill(ProductIds.DaiUsd, TradeSide.Buy, net, 1, fee);
            manager.AddTx(buy0);
            Assert.IsTrue(manager.Balance("Usd") == 0);
            Assert.IsTrue(manager.Balance("Dai") == net);


            CalcShares(); //99.800100M + 0.099900M = 99.900M
            var sell0 = CreateFill(ProductIds.DaiUsdc, TradeSide.Sell, gross, 1, fee);
            manager.AddTx(sell0);
            Assert.IsTrue(manager.Balance("Dai") == 0);
            Assert.IsTrue(manager.Balance("Usdc") == net);

            var summary = new SettledSummary(manager.Settled);

            var expectedDisposed = gross;
            Assert.IsTrue(summary.TotalDisposedUsd == expectedDisposed);

            var expectedDisposedFees = fee;
            Assert.IsTrue(summary.TotalDisposedFeesUsd == expectedDisposedFees);

            var expectedNetDisposed = expectedDisposed - expectedDisposedFees;
            Assert.IsTrue(summary.TotalNetDisposedUsd == expectedNetDisposed);

            var expectedCostFees = initialGross * feePct;
            Assert.IsTrue(summary.TotalCostFeesUsd == expectedCostFees);

            var expectedCost = initialGross;
            Assert.IsTrue(summary.TotalCostUsd == expectedCost);

            var expectedCostNet = expectedCost - expectedCostFees;
            Assert.IsTrue(summary.TotalCostNet == expectedCostNet);

            //--buy DaiUsdc 99.9 @ 1 with .1 % fee
            //match usdc    -99.8001m   0.0999
            //match dia     99.8001     100
            //fee usdc    -0.0999     0

            CalcShares(); //net = 99.700299900 + net = 99.700299900 = 99.800100M
            var buy1 = CreateFill(ProductIds.DaiUsdc, TradeSide.Buy, net, 1, fee);
            manager.AddTx(buy1);
            Assert.IsTrue(manager.Balance("Dai") == net);// 99.8001;
            Assert.IsTrue(manager.Balance("Usdc") == 0);


            summary = new SettledSummary(manager.Settled);

            var expectedDisposed1 = gross;
            Assert.IsTrue(summary.TotalDisposedUsd == expectedDisposed + expectedDisposed1);
            // 0.099900
            var expectedDisposedFees1 = expectedDisposed1 * feePct;
            var t = expectedDisposedFees + fee;
            Assert.IsTrue(summary.TotalDisposedFeesUsd == expectedDisposedFees + expectedDisposedFees1);

            var expectedNetDisposed1 = expectedDisposed1 - expectedDisposedFees1;
            Assert.IsTrue(summary.TotalNetDisposedUsd == expectedNetDisposed + expectedNetDisposed1);

            var expectedCostFees1 = 0m; // 1 * 99.9m * feePct;
            Assert.IsTrue(summary.TotalCostFeesUsd == expectedCostFees + expectedCostFees1);

            var expectedCost1 = 1 * expectedCostNet + expectedCostFees1;
            Assert.IsTrue(summary.TotalCostUsd == expectedCost + expectedCost1);

            var expectedCostNet1 = expectedDisposed - expectedCostFees1;
            Assert.IsTrue(summary.TotalCostNet == expectedCostNet + expectedCostNet1);


            CalcShares(); //99.600599600100 + 0.099700299900M =  99.700299900M;
            var sell1 = CreateFill(ProductIds.DaiUsd, TradeSide.Sell, gross, 1, fee);
            manager.AddTx(sell1);
            Assert.IsTrue(manager.Balance("Dai") == 0);// 99.8001;
            Assert.IsTrue(manager.Balance("Usd") == net);//99.7002999


            summary = new SettledSummary(manager.Settled);

            var expectedDisposed2 = gross;
            Assert.IsTrue(summary.TotalDisposedUsd == expectedDisposed + expectedDisposed1 + expectedDisposed2);

            var expectedDisposedFees2 = expectedDisposed2 * feePct;
            Assert.IsTrue(summary.TotalDisposedFeesUsd == expectedDisposedFees + expectedDisposedFees1 + expectedDisposedFees2);

            var expectedNetDisposed2 = expectedDisposed2 - expectedDisposedFees2;
            Assert.IsTrue(summary.TotalNetDisposedUsd == expectedNetDisposed + expectedNetDisposed1 + expectedNetDisposed2);

            var expectedCostFees2 = expectedDisposed1 * feePct;//should be expectedDisposed1;
            Assert.IsTrue(summary.TotalCostFeesUsd == expectedCostFees + expectedCostFees1 + expectedCostFees2);

            var expectedCost2 = expectedDisposed1;
            Assert.IsTrue(summary.TotalCostUsd == expectedCost + expectedCost1 + expectedCost2);

            var expectedCostNet2 = expectedCost2 - expectedCostFees2;
            Assert.IsTrue(summary.TotalCostNet == expectedCostNet + expectedCostNet1 + expectedCostNet2);

        }

        [TestMethod()]
        public void AuditManagerTest_Usd_LtcUsdWithFees()
        {
            var manager = new AuditManager();

            const decimal feePct = 0.001m;
            var buy0 = CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 10, 100, 10 * 100 * feePct);
            var buy1 = CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 10, 100, 10 * 100 * feePct);
            var sell0 = CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 5, 100, 5 * 100 * feePct);
            var sell1 = CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 10, 100, 10 * 100 * feePct);
            var sell2 = CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 5, 100, 5 * 100 * feePct);

            manager.AddTx(buy0);
            manager.AddTx(buy1);

            manager.AddTx(sell0);


            var summary = new SettledSummary(manager.Settled);

            var expectedDisposed = 5 * 100m;
            Assert.IsTrue(summary.TotalDisposedUsd == expectedDisposed);

            var expectedDisposedFees = 5 * 100m * feePct;
            Assert.IsTrue(summary.TotalDisposedFeesUsd == expectedDisposedFees);

            var expectedNetDisposed = expectedDisposed - expectedDisposedFees;
            Assert.IsTrue(summary.TotalNetDisposedUsd == expectedNetDisposed);

            var expectedCostFees = 5 * 100m * feePct;
            Assert.IsTrue(summary.TotalCostFeesUsd == expectedCostFees);

            var expectedCost = 5 * 100m + expectedCostFees;
            Assert.IsTrue(summary.TotalCostUsd == expectedCost);

            var expectedCostNet = expectedCost - expectedCostFees;
            Assert.IsTrue(summary.TotalCostNet == expectedCostNet);




            manager.AddTx(sell1);

            var summary2 = new SettledSummary(manager.Settled);

            var expectedDisposed2 = 10 * 100m;
            Assert.IsTrue(summary2.TotalDisposedUsd == expectedDisposed + expectedDisposed2);

            var expectedDisposedFees2 = 10 * 100m * feePct;
            Assert.IsTrue(summary2.TotalDisposedFeesUsd == expectedDisposedFees + expectedDisposedFees2);

            var expectedNetDisposed2 = expectedDisposed2 - expectedDisposedFees2;
            Assert.IsTrue(summary2.TotalNetDisposedUsd == expectedNetDisposed + expectedNetDisposed2);

            var expectedCostFees2 = 10 * 100m * feePct;
            Assert.IsTrue(summary2.TotalCostFeesUsd == expectedCostFees + expectedCostFees2);

            var expectedCost2 = 10 * 100m + expectedCostFees2;
            Assert.IsTrue(summary2.TotalCostUsd == expectedCost + expectedCost2);

            var expectedCostNet2 = expectedCost2 - expectedCostFees2;
            Assert.IsTrue(summary2.TotalCostNet == expectedCostNet + expectedCostNet2);


            manager.AddTx(sell2);


            var summary3 = new SettledSummary(manager.Settled);

            var expectedDisposed3 = 5 * 100m;
            Assert.IsTrue(summary3.TotalDisposedUsd == expectedDisposed3 + expectedDisposed2 + expectedDisposed);

            var expectedDisposedFees3 = 5 * 100m * feePct;
            Assert.IsTrue(summary3.TotalDisposedFeesUsd == expectedDisposedFees3 + expectedDisposedFees2 + expectedDisposedFees);

            var expectedNetDisposed3 = expectedDisposed3 - expectedDisposedFees3;
            Assert.IsTrue(summary3.TotalNetDisposedUsd == expectedNetDisposed3 + expectedNetDisposed2 + expectedNetDisposed);

            var expectedCostFees3 = 5 * 100m * feePct;
            Assert.IsTrue(summary3.TotalCostFeesUsd == expectedCostFees3 + expectedCostFees2 + expectedCostFees);

            var expectedCost3 = 5 * 100m + expectedCostFees3;
            Assert.IsTrue(summary3.TotalCostUsd == expectedCost3 + expectedCost2 + expectedCost);

            var expectedCostNet3 = expectedCost3 - expectedCostFees3;
            Assert.IsTrue(summary3.TotalCostNet == expectedCostNet3 + expectedCostNet2 + expectedCostNet);


        }

        [TestMethod()]
        public void AuditManagerTest_Usd_LtcUsd()
        {

            var manager = new AuditManager();
            var fills = new List<DbFill>();

            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 20, 100));//2000 ltc
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 20, 100)); //sell 2000 ltc 

            manager.Credit(Currency.Usd, 2000, fills.First().CreatedAt.AddMinutes(-1));
            foreach (var fill in fills)
            {
                manager.AddTx(fill);
            }
            var settled = manager.Settled.ToList();


            //cost: purchase $2,000 LTC [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = 2000m;
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] sell $2,000 ltc for usd

            var totalDisposed = settled.Sum(x => x.SaleNetProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = 2000m;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);
        }


        [TestMethod()]
        public void AuditManagerTest_Usd_LtcUsdWithFee()
        {

            var manager = new AuditManager();
            var fills = new List<DbFill>();
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 78.91m, 18.4684825200m, 4.3720438670m));
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 78.91m, 18.4684825200m, 4.3720438670m));

            var initialBalance = 78.91m * 18.4684825200m + 4.3720438670m;
            manager.Credit(Currency.Usd, initialBalance, fills.First().CreatedAt.AddMinutes(-1));
            foreach (var fill in fills)
            {
                manager.AddTx(fill);
            }
            var settled = manager.Settled.ToList();


            //cost: purchase $2,000 LTC [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = 1457.3479556532m + 4.3720438670m;
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] sell $2,000 ltc for usd

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = 78.91m * 18.4684825200m;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);
        }

        [TestMethod()]
        public void AuditManagerTest_Usd_BtcUsd_LtcUsdWithFee()
        {

            var manager = new AuditManager();
            var fills = new List<DbFill>();
            fills.Add(CreateFill(ProductIds.BtcUsd, TradeSide.Buy, 10m, 10000m));
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 2287.46448474m, 0.004334m, 0.05m));
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 2287.46448474m, 100, 0));

            var initialBalance = 10m * 10000M;
            manager.Credit(Currency.Usd, initialBalance, fills.First().CreatedAt.AddMinutes(-1));
            foreach (var fill in fills)
            {
                manager.AddTx(fill);
                var btcBalance = manager.Transactions[Currency.Btc].Sum(x => x.Undisposed);
            }
            var settled = manager.Settled.ToList();


            //cost: purchase $2,000 LTC [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = 1457.3479556532m + 4.3720438670m;
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] sell $2,000 ltc for usd

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = 78.91m * 18.4684825200m;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);
        }

        [TestMethod()]
        public void AuditManagerTest_Usd_LtcBtc_BtcUsd()
        {

            var manager = new AuditManager();
            var fills = new List<DbFill>();
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 20, 100));//2000 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); //sell 2000 ltc 
            fills.Add(CreateFill(ProductIds.BtcUsd, TradeSide.Sell, .2m, 10000));

            var initialBalance = 20m * 100m;
            manager.Credit(Currency.Usd, initialBalance, fills.First().CreatedAt.AddMinutes(-1));
            foreach (var fill in fills)
            {
                manager.AddTx(fill);
            }
            var settled = manager.Settled.ToList();


            //cost: purchase $2,000 LTC, purchase $2,000 ltc btc [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = 2000m * 2;
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] sell $2,000 ltc for btc, sell $2,000 btc for usd

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = 2000m * 2;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);
        }

        [TestMethod()]
        public void AuditManagerTest_Usd_LtcBtc_LtcBtc_LtcUsd()
        {

            var manager = new AuditManager();
            var fills = new List<DbFill>();
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 20, 100));   //2000 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); //sell 2000 ltc for btc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 20, .01m));  //sell 2000 btc for ltc
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 20, 100));  //sell 2000 ltc for usd

            var initialBalance = 20m * 100;
            manager.Credit(Currency.Usd, initialBalance, fills.First().CreatedAt.AddMinutes(-1));
            foreach (var fill in fills)
            {
                manager.AddTx(fill);
            }
            var settled = manager.Settled.ToList();


            //cost: purchase $2000 ltc, purchase $2,000 btc, purchase $2,000 ltc  [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = 2000m * 3;
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] sell $2,000 ltc for btc, sell $2,000 btc for ltc, sell $2,000 ltc for usd

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = 2000m * 3;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);
        }


        [TestMethod()]
        public void AuditManagerTest_2Usd_LtcBtc_BtcUsd()
        {

            var manager = new AuditManager();
            List<DbFill> fills = new List<DbFill>();
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 10, 100));   //buy 1000 ltc
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 10, 101));   //buy 1010 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); //sell 2010 ltc and buy 2010 btc
            fills.Add(CreateFill(ProductIds.BtcUsd, TradeSide.Sell, .2m, 10000));  //sell 2010 btc and buy 2000 usd

            var initialBalance = 10 * 100 + 10 * 101;
            manager.Credit(Currency.Usd, initialBalance, fills.First().CreatedAt.AddMinutes(-1));


            foreach (var fill in fills)
            {
                manager.AddTx(fill);
            }
            var settled = manager.Settled.ToList();

            //cost: purchase $2,010 LTC, purchase $2,010 btc [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = 2010m * 2;
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] sell $2,010 ltc for btc, sell 2,000 btc for usd

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = 2010m + 2000m;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);
        }


        [TestMethod()]
        public void AuditManagerTest_2Usd_LtcBtc_LtcBtc_LtcUsd()
        {

            var manager = new AuditManager();
            List<DbFill> fills = new List<DbFill>();
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 10, 100));   //buy 1000 ltc
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 10, 101));   //buy 1010 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); //sell 2010 ltc and buy 2010 btc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 20, .01m));   //sell 2010 btc and buy 2010 ltc
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 20, 100));  //sell 2010 ltc and buy 2000 usd

            var initialBalance = 10m * 100 + 10 * 101;
            manager.Credit(Currency.Usd, initialBalance, fills.First().CreatedAt.AddMinutes(-1));
            foreach (var fill in fills)
            {
                manager.AddTx(fill);
            }
            var settled = manager.Settled.ToList();

            //cost: purchase $2,010 LTC, purchase $2,010 btc, purchase $2,010 ltc, [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = 2010m * 3;
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] sell $2,010 ltc for btc, sell $2,010 btc for ltc, sell 2,000 ltc for usd

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = 2010m + 2010m + 2000m;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);
        }


        [TestMethod()]
        public void AuditManagerTest_Usd_LtcBtc_LtcBtc_LtcBtc_LtcBtc_LtcBtc_BtcUsd()
        {

            var manager = new AuditManager();
            List<DbFill> fills = new List<DbFill>();

            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 20, 100));   // buy 2000 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); // sell 2000 ltc for 2000 btc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 20, .01m));  // sell 2000 btc for 2000 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); // sell 2000 ltc for 2000 btc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 20, .01m));  // sell 2000 btc for 2000 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); // sell 2000 ltc for 2000 btc
            fills.Add(CreateFill(ProductIds.BtcUsd, TradeSide.Sell, .2m, 10000));   // sell 2000 btc for 2000 usd

            var initialBalance = 20m * 100;
            manager.Credit(Currency.Usd, initialBalance, fills.First().CreatedAt.AddMinutes(-1));

            foreach (var fill in fills)
            {
                manager.AddTx(fill);
            }
            var settled = manager.Settled.ToList();

            //cost: purchase 6 $2000 purchases [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = 2000m * 6;
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] 6 $2000 sells

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = 2000m * 6;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);


        }


        [TestMethod()]
        public void AuditManagerTest_Usd_LtcBtc_LtcBtc_LtcBtc_LtcBtc_LtcBtc_LtcBtc_LtcUsd()
        {

            var manager = new AuditManager();
            List<DbFill> fills = new List<DbFill>();

            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 20, 100));   // buy 2000 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); // sell 2000 ltc for 2000 btc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 20, .01m));  // sell 2000 btc for 2000 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); // sell 2000 ltc for 2000 btc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 20, .01m));  // sell 2000 btc for 2000 ltc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 20, .01m)); // sell 2000 ltc for 2000 btc
            fills.Add(CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 20, .01m));  // sell 2000 btc for 2000 ltc
            fills.Add(CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 20, 100));  // sell 2000 ltc for 2000 usd

            var initialBalance = 20m * 100;
            manager.Credit(Currency.Usd, initialBalance, fills.First().CreatedAt.AddMinutes(-1));

            foreach (var fill in fills)
            {
                manager.AddTx(fill);
            }
            var settled = manager.Settled.ToList();

            //cost: purchase 6 $2000 purchases [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = 2000m * 7;
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] 6 $2000 sells

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = 2000m * 7;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);


        }

        [TestMethod()]
        public void AuditManagerTest_2Usd_3Ltc_6Btc_2LtcUsd()
        {

            var manager = new AuditManager();

            var initialBalance = 3 * 100 + 3 * 102;


            var fill0 = CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 3, 100);
            manager.Credit(Currency.Usd, initialBalance, fill0.CreatedAt.AddMinutes(-1));
            decimal usdBalance = manager.Balance("Usd");
            Assert.IsTrue(usdBalance == initialBalance);
            manager.AddTx(fill0);
            decimal ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 3);
            usdBalance = manager.Balance("Usd");
            Assert.IsTrue(usdBalance == 306);

            var fill1 = CreateFill(ProductIds.LtcUsd, TradeSide.Buy, 3, 102);
            manager.AddTx(fill1);
            usdBalance = manager.Balance("Usd");
            Assert.IsTrue(usdBalance == 0);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 6);

            var fill2 = CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 2, .01m);
            manager.AddTx(fill2);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 4);
            var btcBalance = manager.Balance("Btc");
            Assert.IsTrue(btcBalance == .02m);

            var fill3 = CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 2, .01m);
            manager.AddTx(fill3);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 2);
            btcBalance = manager.Balance("Btc");
            Assert.IsTrue(btcBalance == .04m);

            var fill4 = CreateFill(ProductIds.LtcBtc, TradeSide.Sell, 2, .01m);
            manager.AddTx(fill4);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 0);
            btcBalance = manager.Balance("Btc");
            Assert.IsTrue(btcBalance == .06m);



            var fill5 = CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 1, .01m);
            manager.AddTx(fill5);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 1);
            btcBalance = manager.Balance("Btc");
            Assert.IsTrue(btcBalance == .05m);

            var fill6 = CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 1, .01m);
            manager.AddTx(fill6);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 2);
            btcBalance = manager.Balance("Btc");
            Assert.IsTrue(btcBalance == .04m);


            var fill7 = CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 1, .01m);
            manager.AddTx(fill7);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 3);
            btcBalance = manager.Balance("Btc");
            Assert.IsTrue(btcBalance == .03m);

            var fill8 = CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 1, .01m);
            manager.AddTx(fill8);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 4);
            btcBalance = manager.Balance("Btc");
            Assert.IsTrue(btcBalance == .02m);

            var fill9 = CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 1, .01m);
            manager.AddTx(fill9);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 5);
            btcBalance = manager.Balance("Btc");
            Assert.IsTrue(btcBalance == .01m);

            var fill10 = CreateFill(ProductIds.LtcBtc, TradeSide.Buy, 1, .01m);
            manager.AddTx(fill10);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 6);
            btcBalance = manager.Balance("Btc");
            Assert.IsTrue(btcBalance == .0m);

            var fill11 = CreateFill(ProductIds.LtcUsd, TradeSide.Sell, 6, 100);
            manager.AddTx(fill11);
            ltcBalance = manager.Balance("Ltc");
            Assert.IsTrue(ltcBalance == 0);
            usdBalance = manager.Balance("Usd");
            Assert.IsTrue(usdBalance == 600m);






            var settled = manager.Settled.ToList();

            //cost: purchase $2,010 LTC, purchase $2,010 ltc btc, purchase $2,010 ltc, [sell]
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            var expectedCost = (300 + 306) + (202 * 3) + (101 * 6);
            var expectedCostCurrency = expectedCost.ToString("C");
            Assert.IsTrue(costCurrency == expectedCostCurrency);

            //settled: [purchase] sell $2,010 ltc for btc, sell $2,010 btc for ltc, sell 2,000 ltc for usd

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            var expectedDisposed = (202 * 3) + (101 * 6) + 600;
            var expectedDisposedCurrency = expectedDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == expectedDisposedCurrency);


        }


        [TestMethod()]
        public void AuditManagerTestWithCryptoUsdOnly()
        {

            var manager = new AuditManager();
            var initialBalance = 1214.6099995357m + 0.0096319012m;
            manager.Credit(Currency.Usd, initialBalance, Mar18Fills.First().CreatedAt.AddMinutes(-1));

            foreach (var fill in Mar18Fills)
            {
                manager.AddTx(fill);
            }

            var settled = manager.Settled.ToList();

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == "$109,838.38");

            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            Assert.IsTrue(costCurrency == "$109,975.40");
        }

        [TestMethod()]
        public void AuditManagerTestMarchWithAccountAndCryptoUsdOnly()
        {

            var repo = new AuditRepo();
            var txns = repo.GetAuditFills("3/1/2018", "4/1/2018");
            var altTxns = repo.GetAuditAltTxns("3/1/2018", "4/1/2018");



            var manager = new AuditManager();
            manager.Credit(Currency.Usd, 1214.6099995357m + 0.0096319012m, txns.First().BuyTxn.time.AddMinutes(-1));



            int altIndex = 0;
            var lastUsdBalance = 0m;
            var lastTradeId = 0;
            for (var i = 0; i < txns.Count; i++)
            {

                var txn = txns[i];
                for (; altIndex < altTxns.Count && altTxns[altIndex].AccountTxs.First().time < txn.BuyTxn.time; altIndex++)
                {
                    var altTx = altTxns[altIndex].AccountTxs.First();
                    switch (altTx.type)
                    {
                        case "withdrawal":
                            break;
                        case "deposit":
                            break;
                        case "conversion":
                            break;

                    }
                }
                var src = txn.Fill;
                var fill = src.Clone();
                manager.AddTx(fill);

                decimal ltcBalance = 0;
                decimal usdBalance = 0;
                if (manager.Transactions.ContainsKey("Ltc"))
                {
                    ltcBalance = manager.Transactions["Ltc"].Sum(x => x.Undisposed);
                }
                if (manager.Transactions.ContainsKey("Usd"))
                {
                    usdBalance = manager.Transactions["Usd"].Sum(x => x.Undisposed);
                }
                var accountTxns = txn.AccountTxs;
                var buyTx = accountTxns.First(x => x.type == "match" && x.amount < 0);
                var sellTx = accountTxns.First(x => x.type == "match" && x.amount > 0);
                var feeTx = accountTxns.FirstOrDefault(x => x.type == "fee" && x.amount < 0);

                var buyTotal = buyTx.amount;
                var sellTotal = sellTx.amount;
                var fee = feeTx?.amount ?? 0m;
                var ltcTx = buyTx.amount_balance_unit == AccountCurrency.LTC ? buyTx : sellTx;
                var usdTx = buyTx.amount_balance_unit == AccountCurrency.USD ? buyTx : sellTx;
                var usdBalanceWithFee = usdTx.balance + fee;
                var expectedUsdBalance = feeTx?.balance ?? usdBalanceWithFee;
                if (ltcBalance != ltcTx.balance)
                {
                    string bp = "";
                }
                var balanceDiff = (decimal)Math.Abs(usdBalance - usdBalanceWithFee);
                if (balanceDiff >= 0.001m)
                {
                    string bp = "";
                }
                Assert.IsTrue(ltcBalance == ltcTx.balance);
                Assert.IsTrue(balanceDiff < 0.01m);
                lastUsdBalance = expectedUsdBalance;
                lastTradeId = fill.TradeId;
            }

            var settled = manager.Settled.ToList();



            /*
             * Notes:
                dbo. Fills: (exported from Coinbase as csv) contains total column without fee. To get total need to add fee seperately.
                                Total: total + fee
                                Sub:   total
                dbo DbFills: Total is Price * Size and includes fee.
                                Total: price*size
                                Sub:   price*size- fee
                dbo Account: Contains Total without fee in amount with type='match' for group keyed on trade_id + order_id
                                Total = amount where amount >0
                                Sub total  = sum(amount)
                                Fee:  = amount where amount < 0 



            // actual from dbo.fills: select sum(total) from fills where created_at between '3/1/2018' and '4/1/2018' and side='sell'
            //      ->Sub-Total:     109736.7855872993
            // actual fee from dbo.fills: select sum(fee) from fills where created_at between '3/1/2018' and '4/1/2018' and side='sell'
            //      ->Total Fees:     101.5977928860
            // actual total+ fee =  select sum(total+fee) from fills where created_at between '3/1/2018' and '4/1/2018' and side='sell'
            //      ->GroosTotal:     109838.3833801853

            // actual from dbo.dbfills:   select sum (price*size) from dbfills where createdat between '3/1/2018' and '4/1/2018' and side='sell'l'
            //      ->Gross Total:     109838.38338019090000000000
            // actual fee from dbo.fills: select sum (fee) from dbfills where createdat between '3/1/2018' and '4/1/2018' and side='sell'
            //      ->fee:    101.5977928890
            // actual total+ fee =  select sum (price*size - fee) from dbfills where createdat between '3/1/2018' and '4/1/2018' and side='sell'
            //      ->Subtotal:    109736.78558730190000000000

            // actual sell total from dbo.account:  select sum(amount) from account where type='match' and amount>0 
            //              and amount_balance_unit = 'usd' and time between '3/1/2018' and '4/1/2018'
            //      ->Gross Total:     109838.3833801909
            // actual total Fees from dbo.account: 	select sum(amount) from account where type='fee' and amount < 0 
            //      and amount_balance_unit = 'usd' and time between '3/1/2018' and '4/1/2018'
            //      ->all fees:    -262.5745923585 (= -101.5977928860 sell fees + -160.9767994725 buy fees)
            // actual sell fees from dbo.account: 	select sum(amount) from account a join dbo.dbfills f on a.trade_id= f.tradeid and a.order_id= f.orderid
            //          where type = 'fee' and amount< 0 and amount_balance_unit = 'usd' and time between '3/1/2018' and '4/1/2018' and f.side = 'sell'
            //      -> fees:-101.5977928860    

            // actual total from: dbo.account: select sum(amount) from account a join dbo.dbfills f on a.trade_id = f.tradeid and a.order_id = f.orderid
            //      where amount>0 and amount_balance_unit = 'usd' and time between '3/1/2018' and '4/1/2018' and f.side = 'sell'
            //      -> 109838.3833801909
            // actual sub-total from: dbo.account: select sum(amount) from account a join dbo.dbfills f on a.trade_id = f.tradeid and a.order_id = f.orderid
            //      where amount_balance_unit = 'usd' and time between '3/1/2018' and '4/1/2018' and f.side = 'sell'
            //      -> 109736.7855873049

            // actual buy total from dbo.account: select sum(amount) from account a join dbo.dbfills f on a.trade_id= f.tradeid and a.order_id= f.orderid
            //          where type ='match' and amount< 0
            //          and amount_balance_unit = 'usd'
            //          and time between '3/1/2018' and '4/1/2018' and f.side = 'buy'
            //      -> -109814.4281314957
            // actual buy fees from dbo.account: 	select sum(amount) from account a join dbo.dbfills f on a.trade_id= f.tradeid and a.order_id= f.orderid
            //          where type = 'fee' and amount< 0 and amount_balance_unit = 'usd' and time between '3/1/2018' and '4/1/2018' and f.side = 'buy'
            //      -> buy fees:-160.9767994725
            // actual buy total from dbo.account:select sum(abs(amount)) from account a join dbo.dbfills f on a.trade_id= f.tradeid and a.order_id= f.orderid
            //         where type in ('match', 'fee') and amount< 0 
            //		 and amount_balance_unit = 'usd' 
            //		 and time between '3/1/2018' and '4/1/2018' and f.side = 'buy'
            //      -> 109975.4049309682m


            Total Gross Disposed: $109,838.38 
            Total Fee Disposed: $101.5977928860
            Total Net Disposed= 109736.7855872993

            Total Gross Bought: $109,975.40 
            Total Fee Bought: $160.9767994725
            Total Net Bought: $109,814.4281314957
            */
            var totalGrossDisposedExpected = 109838.3833801909m;
            var totalFeeDisposedExpected = 101.5977928860m;
            var totalNetDisposedExpected = 109736.7855872993m;

            var totalGrossBoughtExpected = 109975.4049309682m;
            var totalFeeBoughtExpected = 160.9767994725m;
            var totalNetBoughtExpected = 109814.42813149573m;


            var totalGrossDisposedUsd = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var totalGrossDisposed = settled.Sum(x => x.SaleTotalProceeds.Value);
            var totalNetDisposed = settled.Sum(x => x.SaleNetProceeds.Value);
            var totalNetDisposedUsd = settled.Sum(x => x.SaleNetProceedsUsd.Value);
            var totalFeeDisposed = settled.Sum(x => x.SellFee.Value);


            var totalCostBasisUsd = settled.Sum(x => x.CostBasisUsd);
            var totalCostBasis = settled.Sum(x => x.CostBasis);




            Assert.IsTrue(totalGrossDisposedUsd.ToString("C") == totalGrossDisposedExpected.ToString("C"));
            Assert.IsTrue(totalGrossDisposed.ToString("C") == totalGrossDisposedExpected.ToString("C"));

            Assert.IsTrue(totalFeeDisposed.ToString("C") == totalFeeDisposedExpected.ToString("C"));

            Assert.IsTrue(totalNetDisposed.ToString("C") == totalNetDisposedExpected.ToString("C"));
            Assert.IsTrue(totalNetDisposedUsd.ToString("C") == totalNetDisposedExpected.ToString("C"));


            Assert.IsTrue(totalCostBasisUsd.ToString("C") == totalGrossBoughtExpected.ToString("C"));
            Assert.IsTrue(totalCostBasis.ToString("C") == totalGrossBoughtExpected.ToString("C"));


            var amountUsdDisposed = txns.Where(x => x.Side == TradeSide.Sell)
                .Sum(x => Math.Abs(x.SellTxn.amount));

            var amountUsdGrossDisposed = txns.Where(x => x.Side == TradeSide.Sell)
               .Sum(x => Math.Abs(x.SellAmountGross));
            var amountUsdNetDisposed = txns.Where(x => x.Side == TradeSide.Sell)
                .Sum(x => Math.Abs(x.SellAmountNet));
            //     //101.5977928860
            var amountUsdDisposedFee = txns.Where(x => x.Side == TradeSide.Sell)
                .Sum(x => Math.Abs(x.SellFee));


            //160.9767994725
            var amountUsdGrossBought = txns.Where(x => x.Side == TradeSide.Buy)
             .Sum(x => Math.Abs(x.BuyAmountGross));
            //160.9767994725
            var amountUsdFeeBought = txns.Where(x => x.Side == TradeSide.Buy)
                .Sum(x => Math.Abs(x.BuyFee));
            var amountUsdNetBought = txns.Where(x => x.Side == TradeSide.Buy)
            .Sum(x => Math.Abs(x.BuyAmountNet));

            Assert.IsTrue(amountUsdGrossBought.ToString("C") == totalGrossBoughtExpected.ToString("C"));
            Assert.IsTrue(amountUsdFeeBought.ToString("C") == totalFeeBoughtExpected.ToString("C"));
            Assert.IsTrue(amountUsdNetBought.ToString("C") == totalNetBoughtExpected.ToString("C"));



        }
        [TestMethod()]
        public void AuditManagerTestJanuary()
        {
            var manager = new AuditManager();
            manager.Credit(Currency.Usd, 1483.5641493089624500m, DateTime.Parse("2018-12-31 23:58:56.0000000"));
            //manager.Credit(Currency.Eth, 1.1637130900m, DateTime.Parse("2018-01-31 23:58:56.0000000"));
            var repo = new AuditRepo();
            var txns = repo.GetAuditFills("1/1/2018", "2/1/2018");
            var altTxns = repo.GetAuditAltTxns("1/1/2018", "2/1/2018").Select(x => x.AccountTxs.First()).ToList();
            var altIndex = 0;
            for (var i = 0; i < txns.Count; i++)
            {
                var txn = txns[i];
                for (; altIndex < altTxns.Count && altTxns[altIndex].time < txn.BuyTxn.time; altIndex++)
                {
                    var altTx = altTxns[altIndex];
                    switch (altTx.type)
                    {
                        case "withdrawal":
                            manager.Debit(altTx);
                            break;
                        case "deposit":
                            manager.Credit(AccountCurrency.MapToCurrency[altTx.amount_balance_unit], altTx.amount, altTx.time);
                            break;
                        case "conversion":
                            break;
                        default:
                            break;

                    }
                }
                var src = txn.Fill;
                var fill = src.Clone();
                manager.AddTx(fill);

            }
            var settled = manager.Settled.ToList();
            var settledProducts = settled.Select(x => x.Product).Distinct().ToList();
            var usdSettled = settled.Where(x => x.Product.EndsWith("Usd")).ToList();

            //usdSettledTotal = 252098.63785211800000000000000
            var usdSettledTotal = usdSettled.Sum(x => x.SaleTotalProceedsUsd.Value);
            //usdSettledNet = 251810.97533570880160000000000
            var usdSettledNet = usdSettled.Sum(x => x.SaleNetProceedsUsd.Value);
            var usdSettledFees = usdSettled.Sum(x => x.SellFee.Value);
            //usdSettledFees = 287.66331159764953620000000000
            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var netDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);



            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
        }


        [TestMethod()]
        public void AuditManagerTestFebruary()
        {
            var manager = new AuditManager();
            manager.Credit(Currency.Usd, 252.5132973182m, DateTime.Parse("2018-01-31 23:58:56.0000000"));
            manager.Credit(Currency.Eth, 1.1637130900m, DateTime.Parse("2018-01-31 23:58:56.0000000"));
            var repo = new AuditRepo();
            var txns = repo.GetAuditFills("2/1/2018", "3/1/2018");
            var altTxns = repo.GetAuditAltTxns("2/1/2018", "3/1/2018").Select(x => x.AccountTxs.First()).ToList();
            var altIndex = 0;
            for (var i = 0; i < txns.Count; i++)
            {
                var txn = txns[i];
                for (; altIndex < altTxns.Count && altTxns[altIndex].time < txn.BuyTxn.time; altIndex++)
                {
                    var altTx = altTxns[altIndex];
                    switch (altTx.type)
                    {
                        case "withdrawal":
                            manager.Debit(altTx);
                            break;
                        case "deposit":
                            manager.Credit(AccountCurrency.MapToCurrency[altTx.amount_balance_unit], altTx.amount, altTx.time);
                            break;
                        case "conversion":
                            break;
                        default:
                            break;

                    }
                }
                var src = txn.Fill;
                var fill = src.Clone();
                manager.AddTx(fill);

            }
            var settled = manager.Settled.ToList();
            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var netDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);

            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
        }

        [TestMethod()]
        public void AuditManagerTestMarch()
        {
            var manager = new AuditManager();
            //manager.Credit(Currency.Usd, 252.5132973182m, DateTime.Parse("2018-01-31 23:58:56.0000000"));
            //manager.Credit(Currency.Eth, 1.1637130900m, DateTime.Parse("2018-01-31 23:58:56.0000000"));
            var repo = new AuditRepo();
            var txns = repo.GetAuditFills("3/1/2018", "4/1/2018");
            var altTxns = repo.GetAuditAltTxns("3/1/2018", "4/1/2018").Select(x => x.AccountTxs.First()).ToList();
            var altIndex = 0;


            decimal lastUsdBalance = 0;
            decimal lastLtcBalance = 0;

            decimal totalBuyFees = 0;
            decimal totalSellFees = 0;
            decimal unclearedFees = 0;
            for (var i = 0; i < txns.Count; i++)
            {
                var txn = txns[i];
                for (; altIndex < altTxns.Count && altTxns[altIndex].time < txn.BuyTxn.time; altIndex++)
                {
                    var altTx = altTxns[altIndex];
                    switch (altTx.type)
                    {
                        case "withdrawal":
                            manager.Debit(altTx);
                            break;
                        case "deposit":
                            manager.Credit(AccountCurrency.MapToCurrency[altTx.amount_balance_unit], altTx.amount, altTx.time);
                            break;
                        case "conversion":
                            break;
                        default:
                            break;

                    }
                }
                var src = txn.Fill;
                var fill = src.Clone();
                manager.AddTx(fill);
                unclearedFees += fill.Fee;
                if (fill.Side == "Buy")
                {
                    totalBuyFees += src.Fee;
                    //var currSettled = manager.Settled.ToList();
                    //var currPurchaseFees = curr.Sum(x => x.PurchaseFee);
                    //var diff = Math.Abs(totalBuyFees - currPurchaseFees);
                    //Assert.IsTrue(diff < 0.00000001m);
                }
                else
                {
                    totalSellFees += src.Fee;
                    //var curr = manager.Settled.ToList();
                    //var currPurchaseFees = (decimal)curr.Sum(x => x.SellFee ?? 0m);
                    //var diff = Math.Abs(currPurchaseFees - totalSellFees);
                    //Assert.IsTrue(diff < 0.00000001m);
                }


                var ltcBalance = manager.Balance("Ltc");
                var usdBalance = manager.Balance("Usd");



                lastUsdBalance = usdBalance;
                lastLtcBalance = ltcBalance;


            }
            var settled = manager.Settled.ToList();
            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == "$109,838.38");
            var netDisposed = settled.Sum(x => x.SaleNetProceedsUsd.Value);
            var netDisposedCurrency = netDisposed.ToString("C");
            Assert.IsTrue(netDisposedCurrency == "$109,736.79");

            var sellFees = settled.Sum(x => x.SellFee.Value);
            var sellFeesCurrency = sellFees.ToString("C");
            Assert.IsTrue(sellFeesCurrency == "$101.60");

            //totalCost = 109975.40493097254520000000000
            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            Assert.IsTrue(costCurrency == "$109,975.40");


            //costFees = 211.3332295817391000
            var costFees = settled.Sum(x => x.CostBasisFeesUsd);
            var costFeesCurrency = costFees.ToString("C");
            Assert.IsTrue(costFeesCurrency == "$160.98");

        }

        [TestMethod()]
        public void AuditManagerTest_August_WithCrypto()
        {
            var manager = new AuditManager();
            manager.Credit(Currency.Usd, 1461.7280153692m, DateTime.Parse("7/30/2018"));
            //manager.Credit(Currency.Btc, 0.000000001184663125397852505m, DateTime.Parse("7/30/2018"));
            manager.Credit(Currency.Btc, 0.0000000015m, DateTime.Parse("7/30/2018"));

            var repo = new AuditRepo();
            var txns = repo.GetAuditFills("8/1/2018", "9/1/2018");

            //todo: GetAccount Transactions and make sure fills are sorted.
            int fillNumber = 0;
            var balances = new Dictionary<int, Dictionary<string, decimal>>();

            decimal lastUsdBalance = 0;
            decimal lastLtcBalance = 0;
            decimal lastBtcBalance = 0;
            var accountIndex = 0;
            int lastTradeId = 0;
            var currmap = new Dictionary<string, string>
            {
                { AccountCurrency.USD,Currency.Usd },
                { AccountCurrency.LTC,Currency.Ltc },
                { AccountCurrency.BTC,Currency.Btc },
                { AccountCurrency.ETC,Currency.Etc },

            };

            int minId = int.MaxValue;
            int maxId = int.MinValue;
            decimal lastUsdDisposed = 0m;
            decimal lastUsdCost = 0m;
            bool verify = bool.Parse(bool.FalseString);
            foreach (var txn in txns)
            {
                var ids = (new[] { txn.BuyTxn?.id, txn.SellTxn?.id, txn.FeeTxn?.id }).Where(x => x != null && x != 0).Cast<int>().ToList();
                minId = (new[] { minId, ids.Min() }).Min();
                maxId = (new[] { maxId, ids.Max() }).Max();
                if (maxId == 52911)
                {
                    string bp = "";
                }
                var src = txn.Fill;
                var fill = src.Clone();
                decimal usdBalance = 0;
                decimal ltcBalance = 0;
                decimal btcBalance = 0;
                bool isBtc = txn.BuyTxn.amount_balance_unit == "BTC" || txn.SellTxn.amount_balance_unit == "BTC";
                if (isBtc & 0 == 1)
                {
                    if (manager.Transactions.ContainsKey("Usd"))
                    {
                        usdBalance = manager.Transactions["Usd"].Sum(x => x.Undisposed);
                    }
                    if (manager.Transactions.ContainsKey("Ltc"))
                    {
                        ltcBalance = manager.Transactions["Ltc"].Sum(x => x.Undisposed);
                    }
                    if (manager.Transactions.ContainsKey("Btc"))
                    {
                        btcBalance = manager.Transactions["Btc"].Sum(x => x.Undisposed);
                    }
                    Console.WriteLine($"Process fillNumber {fillNumber} - TradeId {src.TradeId} - {fill.Side} {fill.Size * fill.Price} ");
                    Console.WriteLine($" Balances Pre: USD {usdBalance} - Ltc {ltcBalance} - Btc {btcBalance}");
                    string bp = "";
                }
                // lastUsdCost = 10537.017892534159600000000000
                // lastUsdDisposed = 10523.992586016300000000000000
                // Size = 0.3385291800000000
                // Price = 0.0102800000000000
                // sell -0.3385291800000000 ltc
                // buy 0.0034800801196347 btc
                manager.AddTx(fill);

                if (verify && manager.Settled.Any(x => x.Product.EndsWith("Usd")))
                {
                    var checkCost = manager.Transactions.Where(x => x.Key != "Usd").Any(x => x.Value.All(y => y.Undisposed == 0));
                    var fillSummary = new SettledSummary(manager.Settled);
                    //if (!src.ProductId.EndsWith("Usd") && lastUsdCost > 0 && lastUsdDisposed > 0)
                    //{
                    //    var TotalCostUsdDiff = Math.Abs(fillSummary.TotalCostUsd - lastUsdCost);
                    //    var TotalDisposedUsdDiff = Math.Abs(fillSummary.TotalDisposedUsd - lastUsdDisposed);
                    //    Assert.IsTrue(lastUsdCost == fillSummary.TotalCostUsd);
                    //    Assert.IsTrue(lastUsdDisposed == fillSummary.TotalDisposedUsd);
                    //}
                    //else
                    //{
                    //    lastUsdCost = fillSummary.TotalCostUsd;
                    //    lastUsdDisposed = fillSummary.TotalDisposedUsd;
                    //}

                    Console.WriteLine($"VerifyUsd({minId}, {maxId}, {checkCost})");
                    fillSummary.VerifyUsd(minId, maxId, checkCost);
                }



                if (manager.Transactions.ContainsKey("Usd"))
                {
                    usdBalance = manager.Transactions["Usd"].Sum(x => x.Undisposed);
                }
                if (manager.Transactions.ContainsKey("Ltc"))
                {
                    ltcBalance = manager.Transactions["Ltc"].Sum(x => x.Undisposed);
                }
                if (manager.Transactions.ContainsKey("Btc"))
                {
                    btcBalance = manager.Transactions["Btc"].Sum(x => x.Undisposed);
                }

                if (isBtc & 0 == 1)
                {
                    if (manager.Transactions.ContainsKey("Usd"))
                    {
                        usdBalance = manager.Transactions["Usd"].Sum(x => x.Undisposed);
                    }
                    if (manager.Transactions.ContainsKey("Ltc"))
                    {
                        ltcBalance = manager.Transactions["Ltc"].Sum(x => x.Undisposed);
                    }
                    if (manager.Transactions.ContainsKey("Btc"))
                    {
                        btcBalance = manager.Transactions["Btc"].Sum(x => x.Undisposed);
                    }
                    Console.WriteLine($" Balances Post: USD {usdBalance} - Ltc {ltcBalance} - Btc {btcBalance}");
                    string bp = "";
                }

                var buyBalance = manager.Transactions[currmap[txn.BuyTxn.amount_balance_unit]].Sum(x => x.Undisposed);
                var sellBalance = manager.Transactions[currmap[txn.SellTxn.amount_balance_unit]].Sum(x => x.Undisposed);

                var buyTx = txn.FeeTxn.amount_balance_unit != txn.BuyTxn.amount_balance_unit ? txn.BuyTxn : txn.FeeTxn;
                var sellTx = txn.FeeTxn.amount_balance_unit != txn.SellTxn.amount_balance_unit ? txn.SellTxn : txn.FeeTxn;

                var buyPrecision = buyTx.amount_balance_unit != AccountCurrency.USD ? 0.00000001m : .01m;
                var buyDiff = (decimal)Math.Abs(buyBalance - buyTx.balance);
                if (buyDiff >= buyPrecision)
                {
                    string bp = "";
                }
                Assert.IsTrue(buyDiff < buyPrecision);

                var sellPrecision = sellTx.amount_balance_unit != AccountCurrency.USD ? 0.00000001m : .01m;
                var sellDiff = (decimal)Math.Abs(sellBalance - sellTx.balance);
                // fee: -0.0001404820M
                // dif: -0.0001404820M
                //18.4684825200 -4.3800597160
                if (sellDiff > sellPrecision)
                {
                    string bp = "";
                }
                Assert.IsTrue(sellDiff < sellPrecision);


                lastUsdBalance = usdBalance;
                lastLtcBalance = ltcBalance;
                lastBtcBalance = btcBalance;
                lastTradeId = fill.TradeId;
                fillNumber++;

            }
            var settled = manager.Settled.ToList();
            var usdSettled = settled.Where(x => x.Product.EndsWith("Usd")).ToList();
            var dispOrders = usdSettled.Where(x => x.Product.EndsWith("Usd")).GroupBy(x => x.SaleOrderId)
                   .Select(x => new
                   {
                       x.First().SaleOrderId,
                       SaleNetProceedsUsd = x.Sum(b => b.SaleNetProceedsUsd),
                       SaleTotalProceedsUsd = x.Sum(b => b.SaleTotalProceedsUsd),
                       CostBasisUsd = x.Sum(b => b.CostBasisUsd),
                   })
                   .ToList();
            var usdSummary = new SettledSummary(usdSettled);


            /*
                select format(abs(sum(amount)), 'C') as CostFee from account where 
                   type='fee' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018' 
                   and order_id in (select order_id from account where amount<0 and type='match' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018')

            */
            Assert.IsTrue(usdSummary.TotalCostFeesUsdCurrency == "$73.29");


            /*
                select format(abs(sum(amount)), 'C') as CostUsd from account where 
                   amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018' 
                   and order_id in (select order_id from account where amount<0 and type='match' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018')

            */
            Assert.IsTrue(usdSummary.TotalCostUsdCurrency == "$126,879.79");

            /*
                select format(abs(sum(amount)), 'C') as CostNet from account where 
                   type='match' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018' 
                   and order_id in (select order_id from account where amount<0 and type='match' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018')
            */
            Assert.IsTrue(usdSummary.TotalCostNetCurrency == "$126,806.49");


            /*
                select format(abs(sum(amount)), 'C') as DisposeFee from account where 
                   type='fee' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018' 
                   and order_id in (select order_id from account where amount>0 and type='match' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018')
            */
            Assert.IsTrue(usdSummary.TotalDisposedFeesUsdCurrency == "$97.35");
            var checkit = usdSummary.TotalDisposedFeesUsd + usdSummary.TotalNetDisposedUsd;
            /*
               select format(abs(sum(amount)), 'C') as DisposedUsd from account where 
                    type='match' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018' 
                  and order_id in (select order_id from account where amount>0 and type='match' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018')
           */

            //calculating: usdSummary.TotalDisposedUsdCurrency = "$126,500.44" vs expected "$126,404.30" (diff 96.14)
            Assert.IsTrue(usdSummary.TotalDisposedUsdCurrency == "$126,404.30"); //<=sum usd match where amount > 0


            /*
               select format(abs(sum(amount)), 'C') as DisposedUsd from account where 
                    amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018' 
                  and order_id in (select order_id from account where amount>0 and type='match' and amount_balance_unit='usd' and time between '8/1/2018' and '9/1/2018')
            */
            // calculating: usdSummary.TotalNetDisposedUsdCurrency = "$126,403.09" vs expected "$126,306.95" (96.14)
            Assert.IsTrue(usdSummary.TotalNetDisposedUsdCurrency == "$126,306.95");

            var am = usdSettled.Where(x => x.Product.EndsWith("Usd")).Where(x => x.SaleOrderId == Guid.Parse("08be7444-67a9-476e-8fe7-d8645f14a36d")).ToList();
            var dispOrderSummary = string.Join("\r\n", dispOrders.Select(x => $"{x.SaleOrderId}\t{x.SaleNetProceedsUsd}\t{x.SaleTotalProceedsUsd}"));
            var dispTotalUsdDisposed = usdSettled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var dispTotalUsdNetDisposed = usdSettled.Sum(x => x.SaleNetProceedsUsd.Value);
            var dispBuyOrderSummary = string.Join("\r\n", dispOrders.OrderBy(x => x.SaleOrderId).Select(x => $"{x.SaleOrderId}\t{x.CostBasisUsd}"));
            var dispTotalUsdCost = usdSettled.Sum(x => x.CostBasisUsd);
            var dispNetUsdCostFees = usdSettled.Sum(x => x.CostFeesUsd);
            var dispNetUsdCost = dispTotalUsdCost - dispNetUsdCostFees;
            var buyCostUsdWithBuyFeesAndSellFees = "$126,879.79";

            var buyCostUsdWithOnlyBuyFeesFees = "$126,977.14";

            if (bool.Parse(bool.FalseString))
            {
                var fileName = "audit_08-2018.csv";
                string delim = ",";
                using (var sw = new StreamWriter(fileName))
                {
                    sw.WriteLine(settled[0].HeaderCsv(delim));
                    for (var i = 0; i < settled.Count; i++)
                    {
                        sw.WriteLine(settled[i].ToCsv(delim));
                    }
                }

            }

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var netDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            //returning actual: totalDisposed = 135235.40427676928119347310405
            // diff: 26.97
            Assert.IsTrue(disposedCurrency == "$135,208.03");

            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            Assert.IsTrue(costCurrency == "$126,879.79");



            var totalUsdDiposed = usdSettled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var netUsdDiposed = usdSettled.Sum(x => x.SaleTotalProceedsUsd.Value - x.SellFee.Value);

            //Expected: 126404.3 returns: 126500.44278219190000000000000M
            // netUsdDiposed: 126403.09392114599999999999999M
            var totalUsdCost = usdSettled.Sum(x => x.CostBasisUsd);
            //usdCose: 126,879.78635988880114242137595 - expected:
            if (bool.Parse(bool.FalseString))
            {
                var tsvFile = "dbfills_8-1-2018_9-1-2018.txt";
                using (var sw = new StreamWriter(tsvFile))
                {

                    var tx = settled[0];
                    sw.WriteLine(tx.HeaderCsv());
                    for (var i = 0; i < settled.Count; i++)
                    {
                        tx = settled[i];
                        sw.WriteLine(tx.ToCsv());
                    }
                    foreach (var unsettledProduct in manager.Transactions)
                    {
                        var unsettled = unsettledProduct.Value.ToList();
                        for (var i = 0; i < unsettled.Count; i++)
                        {
                            tx = unsettled[i];
                            sw.WriteLine(tx.ToCsv());
                        }
                    }
                }
            }
        }


        [TestMethod()]
        public void AuditManagerTest()
        {
            var manager = new AuditManagerUSD();
            foreach (var fill in Mar18Fills)
            {
                manager.AddTx(fill);
            }

            var settled = manager.Settled.ToList();

            var totalDisposed = settled.Sum(x => x.SaleTotalProceedsUsd.Value);
            var disposedCurrency = totalDisposed.ToString("C");
            Assert.IsTrue(disposedCurrency == "$109,838.38");

            var totalCost = settled.Sum(x => x.CostBasisUsd);
            var costCurrency = totalCost.ToString("C");
            Assert.IsTrue(costCurrency == "$109,975.40");

            if (bool.Parse(bool.FalseString))
            {
                var tsvFile = "dbfills_3-1-2018_4-1-2018.txt";
                using (var sw = new StreamWriter(tsvFile))
                {

                    var tx = settled[0];
                    sw.WriteLine(tx.HeaderCsv());
                    for (var i = 0; i < settled.Count; i++)
                    {
                        tx = settled[i];
                        sw.WriteLine(tx.ToCsv());
                    }
                    foreach (var unsettledProduct in manager.Transactions)
                    {
                        var unsettled = unsettledProduct.Value.ToList();
                        for (var i = 0; i < unsettled.Count; i++)
                        {
                            tx = unsettled[i];
                            sw.WriteLine(tx.ToCsv());
                        }
                    }
                }
            }
        }

        [TestMethod()]
        public void AddTxTest()
        {
            Assert.Fail();
        }
    }
}


