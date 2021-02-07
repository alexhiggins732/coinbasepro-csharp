using CoinbaseAudit.Constants;
using CoinbaseData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CoinbaseAudit
{

    public class AccountAuditTxn
    {
        public AccountTxn AccountTxn { get; set; }
        public decimal Size;
        public decimal Undisposed;
        public decimal Cost;
        public string Currency;

        public AccountAuditTxn(AccountTxn accountTxn)
        {
            AccountTxn = accountTxn;
            Size = accountTxn.BuyTxn.amount;
            if (accountTxn.FeeTxn != null)
                Size -= accountTxn.FeeTxn.amount;
            Undisposed = Size;
            Currency = accountTxn.BuyCurrency;
        }
        public AccountAuditTxn()
        {

        }

        public AccountAuditTxn(AccountTxn accountTxn, decimal size, decimal undisposed, decimal cost)
        {
            AccountTxn = accountTxn;
            Size = size;
            Undisposed = undisposed;
            Cost = cost;
            Currency = accountTxn.BuyCurrency;
        }

        public AccountAuditTxn Clone()
        {
            return new AccountAuditTxn(AccountTxn.Clone(), Size, Undisposed, Cost);
        }
    }

    public class AccountWallet
    {
        public Queue<AccountAuditTxn> Transactions;
        public Queue<AccountAuditTxn> Withdrawals;

        public AccountWallet()
        {
            Transactions = new Queue<AccountAuditTxn>();
            Withdrawals = new Queue<AccountAuditTxn>();
        }


        public decimal Balance => Transactions.Sum(x => x.Undisposed);
        public decimal WithdrawalBalance => Transactions.Sum(x => x.Undisposed);
    }
    public class AccountAudit
    {
        public Dictionary<string, AccountWallet> Wallets;
        public List<AccountAuditTxn> Disposals;

        public AccountAudit()
        {
            Wallets = new Dictionary<string, AccountWallet>();
            Disposals = new List<AccountAuditTxn>();
        }

        public AccountWallet GetWallet(string currency)
        {
            if (!Wallets.ContainsKey(currency))
                Wallets.Add(currency, new AccountWallet());
            return Wallets[currency];
        }


        public void AddTxn(AccountTxn txn)
        {

            switch (txn.type)
            {
                case AccountTxnType.conversion:
                    ProcessConversion(txn);
                    break;
                case AccountTxnType.deposit:
                    ProcessDeposit(txn);
                    break;
                case AccountTxnType.withdrawal:
                    ProcessWidthrawal(txn);
                    break;
                case AccountTxnType.match:
                    ProcessMatch(txn);
                    break;
                case AccountTxnType.fee:
                    throw new NotImplementedException("Fee must be part of a match transaction");
                default:
                    throw new NotImplementedException($"Transaction Type '{txn.type}' is not implemented");
            }
        }
        private void ProcessDeposit(AccountTxn depositTxn, decimal cost = 0m)
        {
            //TODO: Match Withdrawals;
            var wallet = GetWallet(depositTxn.BuyCurrency);
            var undisposed = depositTxn.NetBuyAmount;
            var remaingingCost = cost;
            while (wallet.WithdrawalBalance > 0 && undisposed > 0)
            {
                var src = wallet.Withdrawals.Peek();
                var disposed = src.Clone();
                if (disposed.Undisposed == undisposed)
                {
                    if (disposed.Cost == 0 && remaingingCost != 0)
                        disposed.Cost = remaingingCost;


                    wallet.Transactions.Enqueue(disposed);
                    undisposed = 0;
                    wallet.Withdrawals.Dequeue();
                }
                else if (disposed.Undisposed > undisposed)
                {

                    string bp = "need to take a partial from disposed";

                }
                else
                {
                    string bp = "need to take a partial from depositTxn";
                }
            }
            if (undisposed == 0)
                return;

            var auditTxn = new AccountAuditTxn(depositTxn);
            auditTxn.Cost = cost;
            wallet.Transactions.Enqueue(auditTxn);
        }

        private void ProcessWidthrawal(AccountTxn withdrawalTxn)
        {
            //TODO: Dequeue transactions for the withdrawal
            string bp = "";
            var wallet = GetWallet(withdrawalTxn.SellCurrency);
            var undisposed = withdrawalTxn.NetBuyAmount;

            var auditTxn = new AccountAuditTxn(withdrawalTxn);
            wallet.Withdrawals.Enqueue(auditTxn);
        }

        private void ProcessConversion(AccountTxn txn)
        {

            var sellWallet = GetWallet(txn.SellCurrency);
            var buyWallet = GetWallet(txn.BuyCurrency);

            //todo: deduct sell amount from feeWallet;

            var tx = new AccountAuditTxn(txn);




        }

        private void ProcessMatch(AccountTxn txn)
        {

            var sellWallet = GetWallet(txn.SellCurrency);
            var transactions = sellWallet.Transactions;
            var buyWallet = GetWallet(txn.BuyCurrency);
            var credits = buyWallet.Transactions;
            var undisposed = txn.GrossSellAmount;

            while (undisposed > 0)
            {
                if (sellWallet.Transactions.Count > 0)
                {
                    var src = transactions.Peek();
                    var disposed = src.Clone();
                    if (disposed.Undisposed == undisposed)
                    {
                        disposed.Undisposed -= undisposed;
                        var buyTxn = new AccountAuditTxn()
                        {
                            AccountTxn = src.AccountTxn,
                            Cost = txn.SellCurrency == "USD" ? txn.GrossSellAmount : disposed.Cost,
                            Currency = txn.BuyCurrency,
                            Undisposed = txn.NetBuyAmount,
                            Size = txn.NetBuyAmount
                        };



                        credits.Enqueue(buyTxn);
                    }
                    else if (disposed.Undisposed > undisposed)
                    {

                    }
                    else  //if (disposed.Undisposed < undisposed)
                    {

                    }


                }
                else
                {
                    throw new InvalidOperationException($"The {txn.SellCurrency} {nameof(sellWallet)} is empty");
                }
            }

            string bp = "";
        }

    }

    public class AccountTxnType
    {
        public const string fee = nameof(fee);
        public const string withdrawal = nameof(withdrawal);
        public const string deposit = nameof(deposit);
        public const string conversion = nameof(conversion);
        public const string match = nameof(match);
    }

    public class AccountTxn
    {
        public int sequence { get; set; }
        public string type { get; set; }

        public DateTime time { get; set; }

        public Guid? transfer_id { get; set; }
        public int? trade_id { get; set; }
        public Guid? order_id { get; set; }

        public Account BuyTxn { get; set; }
        public Account SellTxn { get; set; }
        public Account FeeTxn { get; set; }

        public AccountTxn()
        {

        }

        public AccountTxn(int sequence, string type, DateTime time, Guid? transfer_id, int? trade_id, Guid? order_id, Account buyTxn, Account sellTxn, Account feeTxn)
        {
            this.sequence = sequence;
            this.type = type;
            this.time = time;
            this.transfer_id = transfer_id;
            this.trade_id = trade_id;
            this.order_id = order_id;
            BuyTxn = buyTxn;
            SellTxn = sellTxn;
            FeeTxn = feeTxn;
        }

        public AccountTxn Clone()
        {
            return new AccountTxn(sequence, type, time, transfer_id, trade_id, order_id, BuyTxn?.Clone(), SellTxn?.Clone(), FeeTxn?.Clone());
        }

        public decimal BuyFee => FeeTxn != null && FeeTxn.amount_balance_unit == BuyTxn.amount_balance_unit ? Math.Abs(FeeTxn.amount) : 0;
        public decimal SellFee => FeeTxn != null && FeeTxn.amount_balance_unit == SellTxn.amount_balance_unit ? Math.Abs(FeeTxn.amount) : 0;

        public decimal Fee => Math.Abs(FeeTxn?.amount ?? 0);

        public string BuyCurrency => BuyTxn?.amount_balance_unit;
        public string SellCurrency => SellTxn?.amount_balance_unit;
        public string FeeCurrency => FeeTxn?.amount_balance_unit;

        public decimal GrossSellAmount => Math.Abs(SellTxn?.amount ?? 0);
        public decimal NetSellAmount => GrossSellAmount - SellFee;

        public decimal GrossBuyAmount => Math.Abs(BuyTxn?.amount ?? 0);
        public decimal NetBuyAmount => GrossBuyAmount - BuyFee;

        public decimal? FinalSellBalance => SellTxn != null ? SellTxn.balance - SellFee : (decimal?)null;
        public decimal? FinalBuyBalance => BuyTxn != null ? BuyTxn.balance - BuyFee : (decimal?)null;

    }
    public class AccountRepo
    {
        public List<AccountTxn> GetAccountTxns(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate = startDate ?? DateTime.Parse("1/1/2010");
            endDate = endDate ?? DateTime.UtcNow.AddDays(1);
            if (startDate >= endDate)
                throw new ArgumentException($"{nameof(startDate)} '{startDate}' must be before {nameof(endDate)} '{endDate}'");
            var result = new List<AccountTxn>();
            var txns = TableHelper.GetByQuery<Account>("select * from account where [time] between @startDate and @endDate order by id", new { startDate, endDate });

            var cbTxns = TableHelper.GetByQuery<CbTxns>("select * from CbTxns where [time] between @startDate and @endDate order by time", new { startDate, endDate });

            var cbConverter = new CbTxnsToAccountTxnConverter();
            int cbi = 0;
            int sequence = 0;
            for (var i = 0; i < txns.Count; i++)
            {
                var tx = txns[i];
                if (cbi < cbTxns.Count && cbTxns[cbi].time < tx.time)
                {
                    i--;
                    var cb = cbTxns[cbi++];
                    AccountTxn converted = null;
                    switch (cb.side)
                    {
                        case CoinbaseTxnSide.Buy:
                            var deposit = cbConverter.ConvertCbUsdDeposit(cb);
                            deposit.sequence = ++sequence;
                            result.Add(deposit);
                            converted = cbConverter.ConvertCbBuy(cb);
                            break;
                        case CoinbaseTxnSide.CoinbaseEarn:
                            converted = cbConverter.ConvertCbEarn(cb);
                            break;
                        case CoinbaseTxnSide.Convert:
                            converted = cbConverter.ConvertCbConvert(cb);
                            break;
                        case CoinbaseTxnSide.Receive:
                            converted = cbConverter.ConvertCbRecieve(cb);
                            break;
                        case CoinbaseTxnSide.RewardsIncome:
                            converted = cbConverter.ConvertCbRewards(cb);
                            break;
                        case CoinbaseTxnSide.Sell:
                            converted = cbConverter.ConvertCbSell(cb);
                            break;
                        case CoinbaseTxnSide.Send:
                            converted = cbConverter.ConvertCbSend(cb);
                            break;
                        default:
                            throw new InvalidCastException($"Coinbase TX side '{cb.side}' cannot be converted to a {nameof(AccountTxn)}");
                    }
                    converted.sequence = ++sequence;
                    result.Add(converted);
                    continue;
                }
                else
                {
                    switch (tx.type)
                    {
                        case AccountTxnType.conversion:
                            {
                                var conversionTx = new AccountTxn
                                {
                                    sequence = ++sequence,
                                    time = tx.time,
                                    SellTxn = tx,
                                    BuyTxn = txns[++i],
                                    type = AccountTxnType.conversion
                                };

                                System.Diagnostics.Debug.Assert(conversionTx.SellTxn.type == AccountTxnType.conversion);
                                System.Diagnostics.Debug.Assert(conversionTx.SellTxn.amount < 0);
                                System.Diagnostics.Debug.Assert(conversionTx.BuyTxn.type == AccountTxnType.conversion);
                                System.Diagnostics.Debug.Assert(conversionTx.BuyTxn.amount > 0);
                                System.Diagnostics.Debug.Assert(conversionTx.FeeTxn == null);
                                result.Add(conversionTx);
                            }
                            break;
                        case AccountTxnType.deposit:
                            {
                                var depositTx = new AccountTxn
                                {
 
                                    time = tx.time,
                                    transfer_id = tx.transfer_id,
                                    BuyTxn = tx,
                                    type = AccountTxnType.deposit
                                };
                                if (depositTx.BuyCurrency!= "USD")
                                {
                                    var withdrawal = cbConverter.ConvertCbNonUsdWithdrawal(depositTx);
                                    withdrawal.sequence = ++sequence;
                                    result.Add(withdrawal);
                                }
                           

                                System.Diagnostics.Debug.Assert(depositTx.BuyTxn.type == AccountTxnType.deposit);
                                System.Diagnostics.Debug.Assert(depositTx.BuyTxn.amount > 0);
                                System.Diagnostics.Debug.Assert(depositTx.SellTxn == null);
                                System.Diagnostics.Debug.Assert(depositTx.FeeTxn == null);
                                sequence = ++sequence;
                                result.Add(depositTx);

                             
                            }
                            break;
                        case AccountTxnType.withdrawal:
                            {
                                var withdrawalTx = new AccountTxn
                                {
                                    sequence = ++sequence,
                                    time = tx.time,
                                    transfer_id = tx.transfer_id,
                                    SellTxn = tx,
                                    type = AccountTxnType.withdrawal
                                };
                                result.Add(withdrawalTx);
                                System.Diagnostics.Debug.Assert(withdrawalTx.BuyTxn == null);
                                System.Diagnostics.Debug.Assert(withdrawalTx.FeeTxn == null);

                                var deposit = cbConverter.ConvertCbNonUsdDeposit(withdrawalTx);
                                deposit.sequence = ++sequence;
                                result.Add(deposit);

                               
                            }
                            break;
                        case AccountTxnType.match:
                        case AccountTxnType.fee:
                            {
                                Account sellTx = null;
                                Account buyTx = null;
                                if (tx.amount < 0)
                                {
                                    sellTx = tx;
                                    buyTx = txns[++i];
                                }
                                else
                                {
                                    buyTx = tx;
                                    sellTx = txns[++i];
                                }
                                var matchTx = new AccountTxn
                                {
                                    sequence = ++sequence,
                                    time = sellTx.time,
                                    order_id = tx.order_id,
                                    trade_id = tx.trade_id,
                                    SellTxn = sellTx,
                                    BuyTxn = buyTx,
                                    type = AccountTxnType.match
                                };
                                System.Diagnostics.Debug.Assert(matchTx.SellTxn.order_id == matchTx.order_id);
                                System.Diagnostics.Debug.Assert(matchTx.SellTxn.trade_id == matchTx.trade_id);
                                System.Diagnostics.Debug.Assert(matchTx.SellTxn.type == AccountTxnType.match);
                                System.Diagnostics.Debug.Assert(matchTx.SellTxn.amount < 0);


                                System.Diagnostics.Debug.Assert(matchTx.BuyTxn.type == AccountTxnType.match);
                                System.Diagnostics.Debug.Assert(matchTx.BuyTxn.amount > 0);
                                System.Diagnostics.Debug.Assert(matchTx.BuyTxn.order_id == matchTx.order_id);
                                System.Diagnostics.Debug.Assert(matchTx.BuyTxn.trade_id == matchTx.trade_id);
                                int j = i + 1;
                                if (j < txns.Count && txns[j].trade_id == matchTx.BuyTxn.trade_id && txns[j].order_id == matchTx.BuyTxn.order_id)
                                {
                                    matchTx.FeeTxn = txns[++i];
                                    System.Diagnostics.Debug.Assert(matchTx.FeeTxn.type == AccountTxnType.fee);
                                    System.Diagnostics.Debug.Assert(matchTx.FeeTxn.amount < 0);
                                    System.Diagnostics.Debug.Assert(matchTx.FeeTxn.order_id == matchTx.order_id);
                                    System.Diagnostics.Debug.Assert(matchTx.FeeTxn.trade_id == matchTx.trade_id);
                                }
                                result.Add(matchTx);
                            }
                            break;
                    }
                }

            }

            return result;
        }

        public void SaveToCsv(List<AccountTxn> txns)
        {
            var minBuyDate = txns.Min(x => x.BuyTxn?.time ?? DateTime.MaxValue);
            var minSellDate = txns.Min(x => x.SellTxn?.time ?? DateTime.MaxValue);
            var maxBuyDate = txns.Max(x => x.BuyTxn?.time ?? DateTime.MinValue);
            var maxSellDate = txns.Max(x => x.SellTxn?.time ?? DateTime.MinValue);
            var startDate = (new[] { minBuyDate, minSellDate }).Min();
            var endDate = (new[] { maxBuyDate, maxSellDate }).Max();
            endDate = endDate.AddDays(1);
            var startTs = startDate.ToString("MM-dd-yyyy");
            var endTs = endDate.ToString("MM-dd-yyyy");
            var fileName = $"cb_account_merged_txns_{startTs}_{endTs}.csv";
            const string delimiter = ",";
            var tx = txns[0];
            const string buy = nameof(buy) + "_";
            const string sell = nameof(sell) + "_";
            const string fee = nameof(fee) + "_";
            var headers = new string[]
            {

                nameof(tx.BuyTxn.portfolio),
                nameof(tx.BuyTxn.type),
                nameof(tx.BuyTxn.time),
                nameof(tx.BuyTxn.transfer_id),
                nameof(tx.BuyTxn.order_id),
                nameof(tx.BuyTxn.trade_id),

                "Cost",
                "Disposed",
                "Net",
                buy + "final_balance",
                buy + nameof(tx.BuyTxn.amount),
                buy + "gross_amount",
                buy + nameof(tx.BuyTxn.balance),
                buy + nameof(tx.BuyTxn.amount_balance_unit),
                buy + nameof(tx.BuyTxn.id),

                sell + "final_balance",
                sell + nameof(tx.SellTxn.amount),
                sell + "gross_amount",
                sell + nameof(tx.SellTxn.balance),
                sell + nameof(tx.SellTxn.amount_balance_unit),
                sell + nameof(tx.SellTxn.id),

                fee + nameof(tx.FeeTxn.amount),
                fee + nameof(tx.FeeTxn.balance),
                fee + nameof(tx.FeeTxn.amount_balance_unit),
                fee + nameof(tx.FeeTxn.id),
            };

            using (var sw = new StreamWriter(fileName))
            {
                sw.WriteLine(string.Join(delimiter, headers.Select(x => x.Replace("_", ""))));
                for (var i = 0; i < txns.Count; i++)
                {
                    tx = txns[i];
                    decimal? buyFinalBalance = tx.FinalBuyBalance;
                    decimal? sellFinalBalance = tx.FinalSellBalance;

                    decimal? cost = tx.BuyCurrency == "USD" ? tx.GrossBuyAmount : (decimal?)null;
                    decimal? disposed = tx.SellCurrency == "USD" ? tx.GrossSellAmount : (decimal?)null;
                    decimal? net = null;
                    decimal? grossBuyAmount = tx.BuyTxn != null ? tx.GrossBuyAmount : (decimal?)null;
                    decimal? grossSellAmount = tx.SellTxn != null ? tx.GrossBuyAmount : (decimal?)null;
                    var data = new string[]
                    {
                        (tx.BuyTxn?.portfolio ??  tx.SellTxn?.portfolio ?? tx.FeeTxn?.portfolio).ToString(),
                        (tx.BuyTxn?.type ??  tx.SellTxn?.type).ToString(),
                        (tx.BuyTxn?.time ?? tx.SellTxn?.time ?? tx.FeeTxn?.time).ToString(),
                        (tx.BuyTxn?.transfer_id ??  tx.SellTxn?.transfer_id ?? tx.FeeTxn?.transfer_id).ToString(),
                        (tx.BuyTxn?.order_id ?? tx.SellTxn?.order_id ?? tx.FeeTxn?.order_id).ToString(),
                        (tx.BuyTxn?.trade_id ?? tx.SellTxn?.trade_id ?? tx.FeeTxn?.trade_id).ToString(),

                        cost?.ToString(),
                        disposed?.ToString(),
                        net?.ToString(),


                        buyFinalBalance?.ToString(),
                        tx.BuyTxn?.amount.ToString(),
                        grossBuyAmount?.ToString(),
                        tx.BuyTxn?.balance.ToString(),
                        tx.BuyTxn?.amount_balance_unit.ToString(),
                        tx.BuyTxn?.id.ToString(),

                        sellFinalBalance?.ToString(),
                        tx.SellTxn?.amount.ToString(),
                        grossSellAmount?.ToString(),
                        tx.SellTxn?.balance.ToString(),
                        tx.SellTxn?.amount_balance_unit.ToString(),
                        tx.SellTxn?.id.ToString(),


                        tx.FeeTxn?.amount.ToString(),
                        tx.FeeTxn?.balance.ToString(),
                        tx.FeeTxn?.amount_balance_unit.ToString(),
                        tx.FeeTxn?.id.ToString()
                    };
                    if (data.Any(x => (x ?? "").IndexOf(delimiter) > -1))
                    {
                        string bp = "Invalid data";
                    }
                    sw.WriteLine(string.Join(delimiter, data));
                }
            }
            Console.WriteLine($"Saved data to {fileName}");
        }
    }

    internal class CbTxnsToAccountTxnConverter
    {
        private Dictionary<string, decimal> balances;
        public CbTxnsToAccountTxnConverter()
        {
            balances = new Dictionary<string, decimal>();
            balances["BTC"] = 0.01m;
            balances["LTC"] = 0.1m;
            balances["USD"] = 1m;
        }
        public const string Usd = nameof(Usd);

        internal AccountTxn ConvertCbUsdDeposit(CbTxns cb)
        {
            if (!balances.ContainsKey(Usd))
                balances.Add(Usd, 0);
            balances[Usd] += cb.usdTotal;
            var buyTx = new Account("Coinbase", "deposit", cb.time, cb.usdTotal, balances[Usd], Usd, null, null, 0);
            var tx = new AccountTxn
            {
                time = cb.time,
                BuyTxn = buyTx,
                type = AccountTxnType.deposit
            };
            return tx;
        }

        internal AccountTxn ConvertCbNonUsdDeposit(AccountTxn withdrawalTx)
        {
            var asset = withdrawalTx.SellCurrency.ToUpper();
            if (!balances.ContainsKey(asset))
                balances.Add(asset, 0);
            var amount = withdrawalTx.NetSellAmount;
            balances[asset] += amount;
            var buyTx = new Account("Coinbase", AccountTxnType.deposit, withdrawalTx.time, amount, balances[asset], asset, null, null, 0);
            var tx = new AccountTxn
            {
                time = withdrawalTx.time,
                BuyTxn = buyTx,
                type = AccountTxnType.deposit
            };
            return tx;
        }

        internal AccountTxn ConvertCbNonUsdWithdrawal(AccountTxn depositTx)
        {
            var asset = depositTx.BuyCurrency.ToUpper();
            if (!balances.ContainsKey(asset))
                balances.Add(asset, 0);

            var amount = depositTx.NetBuyAmount;
            if (balances[asset] < amount)
                throw new ArgumentException($"Coinbase wallet {asset} has insufficent funds '{balances[asset]}' to withdrawal '{amount}'");
            balances[asset] -= amount;

            var sellTx = new Account("Coinbase", AccountTxnType.withdrawal, depositTx.time, amount, balances[asset], asset, null, null, 0);
            var tx = new AccountTxn
            {
                time = depositTx.time,
                SellTxn = sellTx,
                type = AccountTxnType.deposit
            };
            return tx;

        }
        internal AccountTxn ConvertCbBuy(CbTxns cb)
        {
            if (!balances.ContainsKey(cb.asset))
                balances.Add(cb.asset, 0);
            if (!balances.ContainsKey(Usd))
                balances.Add(Usd, 0);
            balances[cb.asset] += cb.quantity;
         
            var buyTx = new Account("Coinbase", "match", cb.time, cb.quantity, balances[cb.asset], cb.asset, null, null, 0);
            balances[Usd] -= cb.usdSubTotal;
            var sellTx = new Account("Coinbase", "match", cb.time, cb.usdSubTotal, balances[Usd], Usd, null, null, 0);
            balances[Usd] -= cb.usdFees;
            var feeTx = new Account("Coinbase", "fee", cb.time, cb.usdFees, balances[Usd], Usd, null, null, 0);
            var tx = new AccountTxn
            {
                time = cb.time,
                BuyTxn = buyTx,
                SellTxn = sellTx,
                FeeTxn = feeTx,
                type = AccountTxnType.match
            };
            return tx;
        }

        internal AccountTxn ConvertCbRecieve(CbTxns cb)
        {
            if (!balances.ContainsKey(cb.asset))
                balances.Add(cb.asset, 0);
            balances[cb.asset] += cb.quantity;
            var buyTx = new Account("Coinbase", "recieve", cb.time, cb.quantity, balances[cb.asset], cb.asset, null, null, 0);
            var tx = new AccountTxn
            {
                time = cb.time,
                BuyTxn = buyTx,
                SellTxn = null,
                FeeTxn = null,
                type = AccountTxnType.deposit
            };
            return tx;
        }

        internal AccountTxn ConvertCbSell(CbTxns cb)
        {
            if (!balances.ContainsKey(cb.asset))
                balances.Add(cb.asset, 0);
            if (!balances.ContainsKey(Usd))
                balances.Add(Usd, 0);
            if (balances[cb.asset] < cb.quantity)
                throw new ArgumentException($"Coinbase wallet {cb.asset} has insufficent funds '{balances[cb.asset]}' to sell '{cb.quantity}'");
            balances[Usd] += cb.usdTotal;
            balances[cb.asset] -= cb.quantity;
            var buyTx = new Account("Coinbase", "match", cb.time, cb.usdTotal, balances[Usd], Usd, null, null, 0);
            var sellTx = new Account("Coinbase", "match", cb.time, cb.quantity, balances[cb.asset], cb.asset, null, null, 0);
            balances[Usd] -= cb.usdFees;
            var feeTx = new Account("Coinbase", "fee", cb.time, cb.usdFees, balances[Usd], Usd, null, null, 0);
            var tx = new AccountTxn
            {
                time = cb.time,
                BuyTxn = buyTx,
                SellTxn = sellTx,
                FeeTxn = feeTx,
                type = AccountTxnType.match
            };
            return tx;
        }

        internal AccountTxn ConvertCbSend(CbTxns cb)
        {
            if (!balances.ContainsKey(cb.asset))
                balances.Add(cb.asset, 0);
            if (balances[cb.asset] < cb.quantity)
            {
                throw new ArgumentException($"Coinbase wallet {cb.asset} has insufficent funds '{balances[cb.asset]}' to sell '{cb.quantity}'");
                //Console.WriteLine("Warning: Coinbase wallet {cb.asset} has insufficent funds '{balances[cb.asset]}' to sell '{cb.quantity}'");
            }

            balances[cb.asset] -= cb.quantity;
            balances[cb.asset] = Math.Max(0, balances[cb.asset]);
            var sellTx = new Account("Coinbase", "match", cb.time, cb.quantity, balances[cb.asset], cb.asset, null, null, 0);
            var tx = new AccountTxn
            {
                time = cb.time,
                BuyTxn = null,
                SellTxn = sellTx,
                FeeTxn = null,
                type = AccountTxnType.withdrawal
            };
            return tx;
        }

        internal AccountTxn ConvertCbConvert(CbTxns cb)
        {
            if (!balances.ContainsKey(cb.asset))
                balances.Add(cb.asset, 0);
            if (balances[cb.asset] < cb.quantity)
                throw new ArgumentException($"Coinbase wallet {cb.asset} has insufficent funds '{balances[cb.asset]}' to sell '{cb.quantity}'");
            var noteParts = cb.notes.Trim().Split(' ');
            var convertToAsset = noteParts[noteParts.Length - 1];
            if (!balances.ContainsKey(convertToAsset))
                balances.Add(convertToAsset, 0);

            var convertToAmount = Decimal.Parse(noteParts[noteParts.Length - 2]);
            balances[convertToAsset] += convertToAmount;
            balances[cb.asset] -= cb.quantity;
            var buyTx = new Account("Coinbase", "match", cb.time, convertToAmount, balances[convertToAsset], convertToAsset, null, null, 0);
            var sellTx = new Account("Coinbase", "match", cb.time, cb.quantity, balances[cb.asset], cb.asset, null, null, 0);
            balances[cb.asset] -= cb.usdFees;
            var feeTx = new Account("Coinbase", "fee", cb.time, cb.usdFees, balances[cb.asset], convertToAsset, null, null, 0);
            var tx = new AccountTxn
            {
                time = cb.time,
                BuyTxn = buyTx,
                SellTxn = sellTx,
                FeeTxn = feeTx,
                type = AccountTxnType.conversion
            };
            return tx;
        }

        internal AccountTxn ConvertCbEarn(CbTxns cb)
        {
            if (!balances.ContainsKey(cb.asset))
                balances.Add(cb.asset, 0);
            balances[cb.asset] += cb.quantity;
            var buyTx = new Account("Coinbase", "earn", cb.time, cb.quantity, balances[cb.asset], cb.asset, null, null, 0);

            var tx = new AccountTxn
            {
                time = cb.time,
                BuyTxn = buyTx,
                SellTxn = null,
                FeeTxn = null,
                type = AccountTxnType.deposit
            };
            return tx;
        }

        internal AccountTxn ConvertCbRewards(CbTxns cb)
        {
            if (!balances.ContainsKey(cb.asset))
                balances.Add(cb.asset, 0);
            balances[cb.asset] += cb.quantity;
            var buyTx = new Account("Coinbase", "rewards", cb.time, cb.quantity, balances[cb.asset], cb.asset, null, null, 0);
            var tx = new AccountTxn
            {
                time = cb.time,
                BuyTxn = buyTx,
                SellTxn = null,
                FeeTxn = null,
                type = AccountTxnType.deposit
            };
            return tx;
        }

    
    }
}
