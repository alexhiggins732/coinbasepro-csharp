using CoinbasePro.Shared.Types;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoinbasePro;
using CoinbasePro.WebSocket;
using CoinbasePro.Network.Authentication;

namespace CoinbaseUtils
{
    public class SocketFeedManager
    {
        public Dictionary<ProductType, WebSocketFeed> Feeds;
        public SocketFeedManager()
        {

        }
    }
    class Orderbook
    {
    }
}
