using CoinbaseData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseAudit
{
    public class AuditRepo
    {

        public List<AuditFill> GetAuditFills(string startDate, string endDate)
        {
            if (!DateTime.TryParse(startDate, out DateTime start))
                throw new Exception($"Invalid start date: {startDate}");
            if (!DateTime.TryParse(endDate, out DateTime end))
                throw new Exception($"Invalid start date: {startDate}");
            return GetAuditFills(start, end);
        }

        public List<AuditFill> GetAuditFills(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate = startDate ?? DateTime.Parse("1/1/1900");
            endDate = endDate ?? DateTime.UtcNow.AddDays(1);

            var result = new List<AuditFill>();
            var fills = TableHelper.GetByQuery<DbFill>(@"
                select f.* from dbfills f join DbFillSerialIds s on f.id=s.id
                        where f.createdat between @startDate and @endDate
                        order by s.SerialId", new { startDate, endDate });
            var txns = TableHelper.GetByQuery<Account>("select * from account where type in ('match', 'fee') and [time] between @startDate and @endDate order by id", new { startDate, endDate });

            var i = 0;
            foreach (var fill in fills)
            {
                var tradeTxns = new List<Account>();
                for (; i < txns.Count && txns[i].trade_id == fill.TradeId && txns[i].order_id == fill.OrderId; i++)
                {
                    tradeTxns.Add(txns[i]);
                }
                result.Add(new AuditFill(fill, tradeTxns));
            }

            return result;
        }

        public List<AuditFill> GetAuditAltTxns(string startDate, string endDate)
        {
            if (!DateTime.TryParse(startDate, out DateTime start))
                throw new Exception($"Invalid start date: {startDate}");
            if (!DateTime.TryParse(endDate, out DateTime end))
                throw new Exception($"Invalid start date: {startDate}");
            return GetAuditAltTxns(start, end);
        }
        public List<AuditFill> GetAuditAltTxns(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate = startDate ?? DateTime.Parse("1/1/1900");
            endDate = endDate ?? DateTime.UtcNow.AddDays(1);

            var result = new List<AuditFill>();

            var txns = TableHelper.GetByQuery<Account>("select * from account where type not in ('match', 'fee') and [time] between @startDate and @endDate order by id", new { startDate, endDate });

            foreach (var txn in txns)
            {
                result.Add(new AuditFill(null, (new[] { txn }).ToList()));
            }

            return result;
        }
    }
}
