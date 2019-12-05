using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CoinbaseUtils;
using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Shared.Types;
namespace CoinbaseUtilsTests
{
    [TestClass]
    public class AccuntServiceTests
    {
        [TestMethod]
        public void TestMakerFeeRate()
        {
            var service = new AccountService();
            var rate = service.MakerFeeRate;
            Assert.IsTrue(rate > 0 && rate < .5m);

        }
        [TestMethod]
        public void TestTakerFeeRate()
        {
            var service = new AccountService();
            var makerRate = service.TakerFeeRate;
        }



        [TestMethod]
        public void TestBuyAvailableByCurrency()
        {
            var productType = ProductType.LtcUsd;
            var pair = new CurrencyPair(productType);
            var service = new AccountService();

            var available = service.GetBalance(pair.BuyCurrency);
            Assert.IsTrue(available >= 0m);
        }
        [TestMethod]
        public void TestBuyAvailableByProductType()
        {
            var productType = ProductType.LtcUsd;
            var pair = new CurrencyPair(productType);
            var service = new AccountService();

            var available = service.GetBalance(productType, OrderSide.Buy);
            Assert.IsTrue(available >= 0m);
        }


        [TestMethod]
        public void TestSellAvailableByCurrency()
        {
            var productType = ProductType.LtcUsd;
            var pair = new CurrencyPair(productType);
            var service = new AccountService();

            var available = service.GetBalance(pair.SellCurrency);
            Assert.IsTrue(available >= 0m);
        }

        [TestMethod]
        public void TestSellAvailableByProductType()
        {
            var productType = ProductType.LtcUsd;
            var pair = new CurrencyPair(productType);
            var service = new AccountService();

            var available = service.GetBalance(productType, OrderSide.Sell);
            Assert.IsTrue(available >= 0m);
        }




    }
}
