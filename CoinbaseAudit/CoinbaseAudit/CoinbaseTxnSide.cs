using System.Collections.Generic;

namespace CoinbaseAudit.Constants
{

    public class Currency
    {
        public const string Bsv = nameof(Bsv);
        public const string Btc = nameof(Btc);
        public const string Comp = nameof(Comp);
        public const string Dai = nameof(Dai);
        public const string Etc = nameof(Etc);
        public const string Eth = nameof(Eth);
        public const string Ltc = nameof(Ltc);
        public const string Usd = nameof(Usd);
        public const string Usdc = nameof(Usdc);
        public const string Xlm = nameof(Xlm);
    }
    public class AccountCurrency
    {
        public const string BSV = nameof(BSV);
        public const string BTC = nameof(BTC);
        public const string COMP = nameof(COMP);
        public const string DAI = nameof(DAI);
        public const string ETC = nameof(ETC);
        public const string ETH = nameof(ETH);
        public const string LTC = nameof(LTC);
        public const string USD = nameof(USD);
        public const string USDC = nameof(USDC);
        public const string XLM = nameof(XLM);
        public static Dictionary<string, string> MapToCurrency = new Dictionary<string, string>
            {
                { AccountCurrency.BSV,Currency.Bsv },
                { AccountCurrency.BTC,Currency.Btc },
                { AccountCurrency.COMP,Currency.Comp },
                { AccountCurrency.DAI,Currency.Dai },
                { AccountCurrency.ETC,Currency.Etc },
                { AccountCurrency.ETH,Currency.Eth },
                { AccountCurrency.LTC,Currency.Ltc },
                { AccountCurrency.USD,Currency.Usd },
                { AccountCurrency.USDC,Currency.Usdc },
                { AccountCurrency.XLM,Currency.Xlm },



            };
    }

    public class Product
    {
        public const string ALGOUSD = "ALGO-USD";
        public const string ATOMUSD = "ATOM-USD";
        public const string BANDUSD = "BAND-USD";
        public const string BTCUSD = "BTC-USD";
        public const string COMPUSD = "COMP-USD";
        public const string DAIUSDC = "DAI-USDC";
        public const string EOSUSD = "EOS-USD";
        public const string ETCUSD = "ETC-USD";
        public const string ETHBTC = "ETH-BTC";
        public const string ETHDAI = "ETH-DAI";
        public const string ETHUSD = "ETH-USD";
        public const string ETHUSDC = "ETH-USDC";
        public const string KNCUSD = "KNC-USD";
        public const string LINKUSD = "LINK-USD";
        public const string LTCBTC = "LTC-BTC";
        public const string LTCUSD = "LTC-USD";
        public const string NMRUSD = "NMR-USD";
        public const string REPUSD = "REP-USD";
        public const string UMAUSD = "UMA-USD";
        public const string XLMUSD = "XLM-USD";
        public const string XRPUSD = "XRP-USD";

    }
    public class ProductIds
    {
 
        public const string AlgoUsd = nameof(AlgoUsd);
        public const string AtomUsd = nameof(AtomUsd);
        public const string BandUsd = nameof(BandUsd);
        public const string BtcUsd = nameof(BtcUsd);
        public const string CompUsd = nameof(CompUsd);
        public const string DaiUsd = nameof(DaiUsd);
        public const string DaiUsdc = nameof(DaiUsdc);
        public const string EosUsd = nameof(EosUsd);
        public const string EtcUsd = nameof(EtcUsd);
        public const string EthBtc = nameof(EthBtc);
        public const string EthDai = nameof(EthDai);
        public const string EthUsd = nameof(EthUsd);
        public const string EthUsdc = nameof(EthUsdc);
        public const string KncUsd = nameof(KncUsd);
        public const string LinkUsd = nameof(LinkUsd);
        public const string LtcBtc = nameof(LtcBtc);
        public const string LtcUsd = nameof(LtcUsd);
        public const string NmrUsd = nameof(NmrUsd);
        public const string RepUsd = nameof(RepUsd);
        public const string UmaUsd = nameof(UmaUsd);
        public const string XlmUsd = nameof(XlmUsd);
        public const string XrpUsd = nameof(XrpUsd);


    }
    public class CoinbaseProTxnSide
    {
        public const string Buy = nameof(Buy);
        public const string Sell = nameof(Sell);

        public const string Convert = nameof(Convert);
        public const string Receive = nameof(Receive);
        public const string Send = nameof(Send);
    }
    public class CoinbaseTxnSide
    {
        public const string Buy = nameof(Buy);
        public const string CoinbaseEarn = "Coinbase Earn";
        public const string Convert = nameof(Convert);
        public const string Receive = nameof(Receive);
        public const string RewardsIncome = "Rewards Income";
        public const string Sell = nameof(Sell);
        public const string Send = nameof(Send);
    }
    public class Portfolio
    {
        public const string Coinbase = nameof(Coinbase);
        public const string CoinbasePro = nameof(CoinbasePro);
        public const string External = nameof(External);
    }

}
