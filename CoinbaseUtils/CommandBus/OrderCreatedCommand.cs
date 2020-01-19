using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Shared.Types;

namespace CoinbaseUtils
{
    public class OrderCreatedCommand : OrderCommand
    {
        public OrderCreatedCommand(CreateOrderCommand createOrderCommand)
            : this(createOrderCommand.ProductType, createOrderCommand.OrderSide, createOrderCommand.Size, createOrderCommand.Price)
        {
        }

        public OrderCreatedCommand(ProductType productType, OrderSide orderSide, decimal size, decimal price)
                : base(nameof(OrderCreatedCommand), productType, orderSide, size, price)
        {

        }

    }
}
