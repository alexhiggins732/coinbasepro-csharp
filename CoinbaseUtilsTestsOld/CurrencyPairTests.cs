using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CoinbaseUtils;
using CoinbasePro.Shared.Types;
namespace CoinbaseUtilsTests
{
    /// <summary>
    /// Summary description for CurrencyPairTests
    /// </summary>
    [TestClass]
    public class CurrencyPairTests
    {
        public CurrencyPairTests()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestEmptyConstructor()
        {
            var pair = new CurrencyPair();
            Assert.IsTrue(pair.SellCurrency == Currency.Unknown);
            Assert.IsTrue(pair.BuyCurrency == Currency.Unknown);
            Assert.IsTrue(pair.ProductType == ProductType.Unknown);
        }



        [TestMethod]
        public void TestCurrencyConstructor()
        {
            var pairLtcUsd = new CurrencyPair(Currency.USD, Currency.LTC);
            Assert.IsTrue(pairLtcUsd.SellCurrency == Currency.LTC);
            Assert.IsTrue(pairLtcUsd.BuyCurrency == Currency.USD);
            Assert.IsTrue(pairLtcUsd.ProductType == ProductType.LtcUsd);


            var pairBtcUsd = new CurrencyPair(Currency.USD, Currency.BTC);
            Assert.IsTrue(pairBtcUsd.SellCurrency == Currency.BTC);
            Assert.IsTrue(pairBtcUsd.BuyCurrency == Currency.USD);
            Assert.IsTrue(pairBtcUsd.ProductType == ProductType.BtcUsd);

            var pairLtcBtc = new CurrencyPair(Currency.BTC, Currency.LTC);
            Assert.IsTrue(pairLtcBtc.SellCurrency == Currency.LTC);
            Assert.IsTrue(pairLtcBtc.BuyCurrency == Currency.BTC);
            Assert.IsTrue(pairLtcBtc.ProductType == ProductType.LtcBtc);
        }

        [TestMethod]
        public void TestProductTypeConstructor()
        {
            var pairLtcUsd = new CurrencyPair(ProductType.LtcUsd);
            Assert.IsTrue(pairLtcUsd.SellCurrency == Currency.LTC);
            Assert.IsTrue(pairLtcUsd.BuyCurrency == Currency.USD);
            Assert.IsTrue(pairLtcUsd.ProductType == ProductType.LtcUsd);


            var pairBtcUsd = new CurrencyPair(ProductType.BtcUsd);
            Assert.IsTrue(pairBtcUsd.SellCurrency == Currency.BTC);
            Assert.IsTrue(pairBtcUsd.BuyCurrency == Currency.USD);
            Assert.IsTrue(pairBtcUsd.ProductType == ProductType.BtcUsd);

            var pairLtcBtc = new CurrencyPair(ProductType.LtcBtc);
            Assert.IsTrue(pairLtcBtc.SellCurrency == Currency.LTC);
            Assert.IsTrue(pairLtcBtc.BuyCurrency == Currency.BTC);
            Assert.IsTrue(pairLtcBtc.ProductType == ProductType.LtcBtc);
        }

        [TestMethod]
        public void TestCurrenciesForAllProductTypes()
        {
            var productTypes = ProductType.Unknown.GetEnumDictionary();
            foreach (var kvp in productTypes)
            {
                var productType = kvp.Value;

                var pair = new CurrencyPair(productType);
                Assert.IsTrue(pair.ProductType == productType);
                if (productType != ProductType.Unknown)
                {
                    Assert.IsTrue(pair.BuyCurrency != Currency.Unknown);
                    Assert.IsTrue(pair.SellCurrency != Currency.Unknown);
                }
            }
        }
    }
}
