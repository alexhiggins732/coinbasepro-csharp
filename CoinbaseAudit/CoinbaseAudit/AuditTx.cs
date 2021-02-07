using CoinbaseData;
using System;

namespace CoinbaseAudit
{
    public class AuditTx
    {
        //shared

        public string Portfolio;
        public string Product;

        public decimal Size;
        //purchase fields

        public decimal PurchasePrice;
        public decimal PurchaseFee;
        public decimal Cost;
        public decimal CostUsd;
        public decimal CostFees;
        public decimal CostFeesUsd;


        public int? PurchaseTradeId;
        public Guid? PurchaseOrderId;
        public DateTime PurchaseTime;

        // sale fields
        public decimal? SellSize;
        public decimal? SellPrice;
        public decimal? SellFee;
        public decimal? SaleTotalProceeds;
        public decimal? SaleNetProceeds;
        public decimal? SaleTotalProceedsUsd;
        public decimal? SaleNetProceedsUsd;
        public int? SaleTradeId;
        public Guid? SaleOrderId;
        public DateTime? SaleTime;

        //Calculated
        public decimal CostBasis;
        public decimal CostBasisUsd;
        public decimal CostBasisFees;
        public decimal CostBasisFeesUsd;

        public decimal? NetProfit;
        public decimal? NetProfitUsd;


        public decimal Undisposed;



        public AuditTx()
        {

        }
        public AuditTx(DbFill fill)
        {
            Portfolio = Constants.Portfolio.CoinbasePro;
            Product = fill.ProductId;
            Size = fill.Size;

            PurchaseFee = fill.Fee;
            PurchasePrice = fill.Price;
            //this is cost when usd, but not 
            var pair = CurrencyPair.GetCurrencyPair(Product);
            if (fill.Side == Constants.CoinbaseProTxnSide.Buy)
            {
                if (pair.BuyCurrency == "Usd")
                {
                    Cost = (fill.Size * fill.Price) + fill.Fee;
                    CostFees = fill.Fee;
                    CostUsd = Cost;
                    CostFeesUsd = CostFees;
                }
                   
                //else
                // throw new NotImplementedException();
            }


            PurchaseTradeId = fill.TradeId;
            PurchaseOrderId = fill.OrderId;
            PurchaseTime = fill.CreatedAt;
            Undisposed = fill.Size;
        }



        public static AuditTx CreateCredit(string productId, decimal size, DateTime txnDate)
        {
            return new AuditTx()
            {
                Portfolio = Constants.Portfolio.CoinbasePro,
                Product = productId,
                Size = size,

                PurchaseFee = 0,
                PurchasePrice = 0,
                PurchaseTradeId = null,
                PurchaseOrderId = null,
                PurchaseTime = txnDate,
                Undisposed = size,
                Cost = 0.000000001m,
                CostUsd = 0.000000001m,
            };
        }
        public AuditTx Clone()
        {
            var result = new AuditTx
            {
                Portfolio = Portfolio,
                Product = Product,
                Size = Size,
                SellSize = SellSize,
                SellPrice = SellPrice,
                SellFee = SellFee,
                SaleTotalProceeds = SaleTotalProceeds,
                SaleNetProceeds = SaleNetProceeds,
                SaleTotalProceedsUsd = SaleTotalProceedsUsd,
                SaleNetProceedsUsd = SaleNetProceedsUsd,
                PurchasePrice = PurchasePrice,
                PurchaseFee = PurchaseFee,
                Cost = Cost,
                CostUsd = CostUsd,
                CostFees = CostFees,
                CostFeesUsd = CostFeesUsd,

                CostBasis = CostBasis,
                CostBasisUsd = CostBasisUsd,
                CostBasisFees= CostBasisFees,
                CostBasisFeesUsd = CostBasisFeesUsd,

                NetProfit = NetProfit,
                NetProfitUsd = NetProfitUsd,
                SaleTradeId = SaleTradeId,
                SaleOrderId = SaleOrderId,
                SaleTime = SaleTime,
                PurchaseTradeId = PurchaseTradeId,
                PurchaseOrderId = PurchaseOrderId,
                PurchaseTime = PurchaseTime,
                Undisposed = Undisposed,
            };

            return result;
        }
        public string HeaderCsv(string delimiter = "\t")
        {
            var parts = new string[]
            {
                nameof(Portfolio),
                nameof(Product),
                nameof(Size),
                nameof(SellSize),
                nameof(SellPrice),
                nameof(SellFee),
                nameof(SaleTotalProceeds),
                nameof(SaleNetProceeds),
                nameof(SaleTotalProceedsUsd),
                nameof(SaleNetProceedsUsd),
                nameof(PurchasePrice),
                nameof(PurchaseFee),
                nameof(Cost),
                nameof(CostUsd),
                nameof(CostFees),
                nameof(CostFeesUsd),
                nameof(CostBasis),
                nameof(CostBasisUsd),
                nameof(CostBasisFees),
                nameof(CostBasisFeesUsd),
                nameof(NetProfit),
                nameof(NetProfitUsd),
                nameof(SaleTradeId),
                nameof(SaleOrderId),
                nameof(SaleTime),
                nameof(PurchaseTradeId),
                nameof(PurchaseOrderId),
                nameof(PurchaseTime),
                nameof(Undisposed),
            };
            return string.Join(delimiter, parts);
        }



        public string ToCsv(string delimiter = "\t")
        {
            var parts = new string[]
            {
                Portfolio,
                Product,
                Size.ToString(),
                SellSize?.ToString(),
                SellPrice?.ToString(),
                SellFee?.ToString(),
                SaleTotalProceeds?.ToString(),
                SaleNetProceeds?.ToString(),
                SaleTotalProceedsUsd?.ToString(),
                SaleNetProceedsUsd?.ToString(),
                PurchasePrice.ToString(),
                PurchaseFee.ToString(),
                Cost.ToString(),
                CostUsd.ToString(),
                CostFees.ToString(),
                CostFeesUsd.ToString(),
                CostBasis.ToString(),
                CostBasisUsd.ToString(),
                CostBasisFees.ToString(),
                CostBasisFeesUsd.ToString(),
                NetProfit?.ToString(),
                NetProfitUsd?.ToString(),
                SaleTradeId?.ToString(),
                SaleOrderId?.ToString(),
                SaleTime?.ToString(),
                PurchaseTradeId?.ToString(),
                PurchaseOrderId?.ToString(),
                PurchaseTime.ToString(),
                Undisposed.ToString(),
            };
            return string.Join(delimiter, parts);
        }

        internal bool IsDisposed()
        {
            return Undisposed == 0 || Undisposed < 0.00000001m;
        }
        internal bool IsNotDisposed()
        {
            return Undisposed > 0m;
        }
        internal void SetDisposed()
        {
            Undisposed = 0;
        }
    }
}
