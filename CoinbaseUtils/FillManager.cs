using CoinbasePro.Shared.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseUtils
{
    public class FillManager
    {
        public ConcurrentDictionary<ProductType, FillQueue> ProductTypeQueues = new ConcurrentDictionary<ProductType, FillQueue>();
    }

    public class FillQueue
    {
        public ConcurrentDictionary<Guid, Fill> BuyFills = new ConcurrentDictionary<Guid, Fill>();
        public ConcurrentDictionary<Guid, Fill> SellFills = new ConcurrentDictionary<Guid, Fill>();
    }
    public class Fill
    {

    }
}
