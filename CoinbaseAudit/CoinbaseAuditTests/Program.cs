using CoinbaseAudit.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
namespace CoinbaseAuditTests
{
    class Program
    {
        public static void Main(string[] args)
        {
            //var t = new AccountAuditTests();
            //t.AccountAuditTest();

            //var repo = new CoinbaseAudit.AccountRepo();
            //var txList = repo.GetAccountTxns();

            var test = new CoinbaseAudit.Tests.AuditManagerTests();
       
            test.AuditManagerTest_Usd_DaiUsd_DaiUsdc_DaiUsd_WithFees();
            test.AuditManagerTest_Usd_LtcUsdWithFees();

            test.AuditManagerTestMarch();
            test.AuditManagerTest_August_WithCrypto();
            test.AuditManagerTestJanuary();
            test.AuditManagerTestFebruary();
            test.AuditManagerTest_Usd_LtcUsd();
            //test.AuditManagerTest_Usd_BtcUsd_LtcUsdWithFee();
            test.AuditManagerTestMarchWithAccountAndCryptoUsdOnly();
            test.AuditManagerTest_Usd_LtcUsdWithFee();
            test.AuditManagerTest_Usd_LtcBtc_BtcUsd();
            test.AuditManagerTest_Usd_LtcBtc_LtcBtc_LtcUsd();
            test.AuditManagerTest_2Usd_LtcBtc_BtcUsd();
            test.AuditManagerTest_2Usd_LtcBtc_LtcBtc_LtcUsd();
            test.AuditManagerTest_Usd_LtcBtc_LtcBtc_LtcBtc_LtcBtc_LtcBtc_BtcUsd();
            test.AuditManagerTest_Usd_LtcBtc_LtcBtc_LtcBtc_LtcBtc_LtcBtc_LtcBtc_LtcUsd();
            test.AuditManagerTest_2Usd_3Ltc_6Btc_2LtcUsd();
        
            test.AuditManagerTestWithCryptoUsdOnly();
          
        }
    }
}
