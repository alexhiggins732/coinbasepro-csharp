using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoinbasePro.Services.Orders;
using CoinbasePro.Services.Products.Models;
using CoinbasePro.Shared.Types;
namespace CoinbaseUtils
{
    public class CurrencyPair
    {
        public Currency BuyCurrency { get; private set; } = Currency.Unknown;
        public Currency SellCurrency { get; private set; } = Currency.Unknown;
        public ProductType ProductType { get; private set; } = ProductType.Unknown;
        public Product Product { get; private set; } = null;

        private static Dictionary<string, Currency> currencies = Currency.Unknown.GetEnumDictionary();
        private static Dictionary<string, ProductType> productTypes = ProductType.Unknown.GetEnumDictionary();
        public static Dictionary<ProductType, Product> products { get; set; } = new Dictionary<ProductType, Product>();
        public CurrencyPair()
        {

        }
        public CurrencyPair(Currency buyCurrency, Currency sellCurrency)
        {
            BuyCurrency = buyCurrency;
            SellCurrency = sellCurrency;
            ProductType = GetProductType(buyCurrency, sellCurrency);
            Product = OrdersService.GetProduct(ProductType);
        }

        private ProductType GetProductType(Currency buyCurrency, Currency sellCurrency)
        {
            var pairName = $"{SellCurrency}{BuyCurrency}".ToUpper();
            foreach (var kvp in productTypes)
            {
                if (string.Compare(pairName, kvp.Key, true) == 0)
                {
                    return kvp.Value;
                }
            }
            throw new ArgumentOutOfRangeException($"Unable to find {nameof(ProductType)} for {nameof(buyCurrency)} = {buyCurrency}, {nameof(sellCurrency)} = {sellCurrency}, ");
        }

        public CurrencyPair(ProductType productType)
        {

            BuyCurrency = GetBuyCurrency(productType);
            SellCurrency = GetSellCurrency(productType);
            ProductType = productType;
        }

        private Currency GetSellCurrency(CoinbasePro.Shared.Types.ProductType productType)
        {

            var productTypeName = productType.ToString();
            foreach (var currency in currencies)
            {
                if (productTypeName.StartsWith(currency.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return currency.Value;
                }
            }
            throw new ArgumentOutOfRangeException($"Unable to find {nameof(Currency)} for {nameof(ProductType)} = {productType}");

        }

        private Currency GetBuyCurrency(ProductType productType)
        {
            var productTypeName = productType.ToString();
            foreach (var currency in currencies)
            {
                if (productTypeName.EndsWith(currency.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return currency.Value;
                }
            }
            throw new ArgumentOutOfRangeException($"Unable to find {nameof(Currency)} for {nameof(ProductType)} = {productType}");
        }
    }
}
