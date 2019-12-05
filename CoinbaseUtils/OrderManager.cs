using CoinbasePro.Services.Orders.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseUtils
{
   

    public class OrderManager
    {
    }
    public class OrderClient : CoinbaseService
    {

    }
    public class Trade
    {
        public OrderSide Side;
        public decimal Amount;

        public decimal UsdAmount;
        public decimal CoinAmount;
    }

}
