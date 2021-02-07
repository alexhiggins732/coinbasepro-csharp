using CoinbaseData;
using System;

namespace CoinbaseAudit
{
    public class Fill2 : DbFill
    {
        public decimal CostUsd;

        public Fill2(DbFill dbFill)
            : this(dbFill.TradeId, dbFill.ProductId, dbFill.Price, dbFill.Size, dbFill.OrderId, dbFill.CreatedAt, dbFill.Liquidity, dbFill.Fee, dbFill.Settled, dbFill.Side, dbFill.Price * dbFill.Size)
        {

        }
        public Fill2(int tradeId, string productId, decimal price, decimal size, Guid orderId, DateTime createdAt, string liquidity, decimal fee, bool settled, string side)

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
        public Fill2(int tradeId, string productId, decimal price, decimal size, Guid orderId, DateTime createdAt, string liquidity, decimal fee, bool settled, string side, decimal costUsd)

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
            CostUsd = costUsd;
        }

        public new Fill2 Clone()
        {
            var result = new Fill2(TradeId, ProductId, Price, Size, OrderId, CreatedAt, Liquidity, Fee, Settled, Side, CostUsd);
            return result;
        }
    }
}