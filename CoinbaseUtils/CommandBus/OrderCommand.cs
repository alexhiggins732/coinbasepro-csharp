using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Shared.Types;

namespace CoinbaseUtils
{
    public class OrderCommand : CommandBase
    {
        public ProductType ProductType;
        public OrderSide OrderSide;
        public decimal Size;
        public decimal Price;
        public override string CommandType { get;  }
        public OrderCommand(string commandType, ProductType productType, OrderSide orderSide, decimal size, decimal price)
        {
            CommandType = commandType;
            ProductType = productType;
            OrderSide = orderSide;
            Size = size;
            Price = price;
        }
    }
}
