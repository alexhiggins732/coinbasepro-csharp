using Microsoft.VisualStudio.TestTools.UnitTesting;
using CoinbaseAudit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseAudit.Tests
{
    [TestClass()]
    public class AccountAuditTests
    {
        [TestMethod()]
        public void AccountAuditTest()
        {
            DateTime startDate = DateTime.Parse("10/1/2017");
            DateTime endDate = DateTime.Parse("1/1/2019");
            var repo = new AccountRepo();
            var txns = repo.GetAccountTxns(startDate, endDate);
            repo.SaveToCsv(txns);
         
            var audit = new AccountAudit();
            for (var i = 0; i < txns.Count; i++)
            {
                var txn = txns[i];
                audit.AddTxn(txn);
            }
        }
    }
}