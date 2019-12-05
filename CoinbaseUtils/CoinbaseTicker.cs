using CoinbasePro.Shared.Types;
using CoinbasePro.WebSocket;
using CoinbasePro.Shared.Utilities.Clock;
using CoinbasePro.WebSocket.Models.Response;
using System;
using CoinbasePro.WebSocket.Types;
using System.Linq;

namespace CoinbaseUtils
{
    public class CoinbaseTicker :IDisposable
    {
        public event EventHandler<WebfeedEventArgs<Ticker>> OnTickerReceived;
        private CoinbaseWebSocket Feed;
        public ProductType ProductType { get; }
        public CoinbaseTicker(ProductType productType)
        {
            this.ProductType = ProductType;
            this.Feed = new CoinbaseWebSocket();
            Feed.OnTickerReceived += Feed_OnTickerReceived;
            var productTypes = new[] { productType };
            var channelTypes = new[] { ChannelType.Ticker };
            Feed.Start(productTypes.ToList(), channelTypes.ToList());
        }

        private void Feed_OnTickerReceived(object sender, CoinbasePro.WebSocket.Models.Response.WebfeedEventArgs<CoinbasePro.WebSocket.Models.Response.Ticker> e)
        {
            OnTickerReceived?.Invoke(sender, e);
            
        }

        public static CoinbaseTicker Create(ProductType productType) => new CoinbaseTicker(productType);

        public void Dispose()
        {
            Stop();
        }

        public void Stop()
        {
            if (Feed != null)
            {
                Feed.Stop();
                Feed.OnTickerReceived -= Feed_OnTickerReceived;
                Feed = null;
            }
        }
    }
}
