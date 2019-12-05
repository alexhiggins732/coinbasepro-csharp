using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoinbasePro.Shared.Types;
using CoinbasePro.Services.Accounts.Models;
using CoinbasePro.Services.Orders.Types;
namespace CoinbaseUtils
{
    public interface IAccountService : ICoinbaseService
    {
        Dictionary<Currency, Account> AllAccounts { get; }
        decimal MakerFeeRate { get; }
        decimal TakerFeeRate { get; }
        decimal GetBalance(ProductType productType, OrderSide orderSide);
        decimal GetBalance(Currency currency);
    }

    public class AccountService : CoinbaseService, IAccountService
    {
        #region static
        public static IAccountService Instance;
        public static Dictionary<string, Currency> CurrencyDictionary;
        public static decimal GetAccountBalance(ProductType productType, OrderSide orderSide)
            => Instance.GetBalance(productType, orderSide);

        static AccountService()
        {
            var d = new Dictionary<string, Currency>();
            foreach (var currencyName in Enum.GetNames(typeof(Currency)))
            {
                d.Add(currencyName, (Currency)Enum.Parse(typeof(Currency), currencyName));
            }
            CurrencyDictionary = d;
            var svc = new CoinbaseService();
            var fees = svc.client.FeesService.GetCurrentFeesAsync().Result.First();
            CachedMakerFeeRate = fees.MakerFeeRate;
            CachedTakerFeeRate = fees.TakerFeeRate;
            AllAccountsCache = svc.client.
                AccountsService.GetAllAccountsAsync().Result
                .ToDictionary(x => x.Currency, x => x);
            Instance = new AccountService();
        }
        public static decimal CachedMakerFeeRate { get; private set; }
        public static decimal CachedTakerFeeRate { get; private set; }
        public static Dictionary<Currency, Account> AllAccountsCache { get; }

        #endregion


        public Dictionary<Currency, Account> AllAccounts { get; }

        public decimal MakerFeeRate { get; }
        public decimal TakerFeeRate { get; }



        public AccountService()
        {
            this.AllAccounts = AllAccountsCache;
            this.MakerFeeRate = CachedMakerFeeRate;
            this.TakerFeeRate = CachedTakerFeeRate;
        }

        public decimal GetBalance(Currency currency)
        {
            Account account = Instance.AllAccounts[currency];
            return account.Available;
        }
        public decimal GetBalance(ProductType productType, OrderSide orderSide)
        {
            var pair = new CurrencyPair(productType);
            switch (orderSide)
            {
                case OrderSide.Buy:
                    return GetBalance(pair.BuyCurrency);

                case OrderSide.Sell:
                    return GetBalance(pair.SellCurrency);
                default:
                    throw new ArgumentOutOfRangeException(nameof(orderSide));
            }
        }
    }

}
