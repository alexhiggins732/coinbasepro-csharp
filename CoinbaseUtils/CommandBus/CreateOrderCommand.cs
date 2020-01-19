using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Shared.Types;

namespace CoinbaseUtils
{
    public class CreateOrderCommand : OrderCommand
    {
        public CreateOrderCommand(ProductType productType, OrderSide orderSide, decimal size, decimal price)
            : base(nameof(CreateOrderCommand), productType, orderSide, size, price)
        {

        }

    }
}
