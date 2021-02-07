using CoinbasePro.Shared.Types;
using System;
using CoinbasePro.Services.Fills.Types;
using CoinbasePro.Services.Orders.Types;

namespace CoinbaseData
{
    public class DbFill
    {
        public DbFill() { }
        public DbFill(int tradeId, ProductType productId, decimal price, decimal size, Guid orderId, DateTime createdAt, FillLiquidity liquidity, decimal fee, bool settled, OrderSide side)
        {
            TradeId = tradeId;
            ProductId = productId.ToString();
            Price = price;
            Size = size;
            OrderId = orderId;
            CreatedAt = createdAt;
            Liquidity = liquidity.ToString();
            Fee = fee;
            Settled = settled;
            Side = side.ToString();
        }
        public DbFill(int tradeId, string productId, decimal price, decimal size, Guid orderId, DateTime createdAt, string liquidity, decimal fee, bool settled, string side)
        {
            TradeId = tradeId;
            ProductId = productId.ToString();
            Price = price;
            Size = size;
            OrderId = orderId;
            CreatedAt = createdAt;
            Liquidity = liquidity.ToString();
            Fee = fee;
            Settled = settled;
            Side = side.ToString();
        }

        public DbFill Clone()
        {
            var result = new DbFill(TradeId, ProductId, Price, Size, OrderId, CreatedAt, Liquidity, Fee, Settled, Side);
            return result;
        }

        public int TradeId { get; set; }
        public string ProductId { get; set; }
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public Guid OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Liquidity { get; set; }
        public decimal Fee { get; set; }
        public bool Settled { get; set; }
        public string Side { get; set; }

    }
}


