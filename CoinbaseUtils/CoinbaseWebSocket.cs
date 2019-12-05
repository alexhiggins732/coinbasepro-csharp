using System;
using CoinbasePro.WebSocket;
using CoinbasePro.Shared.Utilities.Clock;
using CoinbasePro.WebSocket.Models.Response;
using SuperSocket.ClientEngine;

namespace CoinbaseUtils
{
    public class CoinbaseWebSocket : WebSocket
    {


        public CoinbaseWebSocket() : base(() => new WebSocketFeed(false), CredentialHelper.GetAuthenticator(), new Clock())
        {
            this.OnActivateReceived += CoinbaseWebSocket_OnActivateReceived;
            this.OnChangeReceived += CoinbaseWebSocket_OnChangeReceived;
            this.OnDoneReceived += CoinbaseWebSocket_OnDoneReceived;
            this.OnErrorReceived += CoinbaseWebSocket_OnErrorReceived;
            this.OnHeartbeatReceived += CoinbaseWebSocket_OnHeartbeatReceived;
            this.OnLastMatchReceived += CoinbaseWebSocket_OnLastMatchReceived;
            this.OnLevel2UpdateReceived += CoinbaseWebSocket_OnLevel2UpdateReceived;
            this.OnMatchReceived += CoinbaseWebSocket_OnMatchReceived;
            this.OnOpenReceived += CoinbaseWebSocket_OnOpenReceived;
            this.OnReceivedReceived += CoinbaseWebSocket_OnReceivedReceived;
            this.OnSnapShotReceived += CoinbaseWebSocket_OnSnapShotReceived;
            this.OnSubscriptionReceived += CoinbaseWebSocket_OnSubscriptionReceived;
            this.OnTickerReceived += CoinbaseWebSocket_OnTickerReceived;
            this.OnWebSocketClose += CoinbaseWebSocket_OnWebSocketClose;
            this.OnWebSocketError += CoinbaseWebSocket_OnWebSocketError;
            this.OnWebSocketOpenAndSubscribed += CoinbaseWebSocket_OnWebSocketOpenAndSubscribed;
        }

        private void CoinbaseWebSocket_OnWebSocketOpenAndSubscribed(object sender, WebfeedEventArgs<EventArgs> e)
        {
            
        }

        private void CoinbaseWebSocket_OnWebSocketError(object sender, WebfeedEventArgs<SuperSocket.ClientEngine.ErrorEventArgs> e)
        {
            
        }

        private void CoinbaseWebSocket_OnWebSocketClose(object sender, WebfeedEventArgs<EventArgs> e)
        {
            
        }

        private void CoinbaseWebSocket_OnTickerReceived(object sender, WebfeedEventArgs<Ticker> e)
        {
            
        }

        private void CoinbaseWebSocket_OnSubscriptionReceived(object sender, WebfeedEventArgs<Subscription> e)
        {
            
        }

        private void CoinbaseWebSocket_OnSnapShotReceived(object sender, WebfeedEventArgs<Snapshot> e)
        {
            
        }

        private void CoinbaseWebSocket_OnReceivedReceived(object sender, WebfeedEventArgs<Received> e)
        {
            
        }

        private void CoinbaseWebSocket_OnOpenReceived(object sender, WebfeedEventArgs<Open> e)
        {
            
        }

        private void CoinbaseWebSocket_OnMatchReceived(object sender, WebfeedEventArgs<Match> e)
        {
            
        }

        private void CoinbaseWebSocket_OnLevel2UpdateReceived(object sender, WebfeedEventArgs<Level2> e)
        {
            
        }

        private void CoinbaseWebSocket_OnLastMatchReceived(object sender, WebfeedEventArgs<LastMatch> e)
        {
            
        }

        private void CoinbaseWebSocket_OnHeartbeatReceived(object sender, WebfeedEventArgs<Heartbeat> e)
        {
            
        }

        private void CoinbaseWebSocket_OnErrorReceived(object sender, WebfeedEventArgs<Error> e)
        {
            
        }

        private void CoinbaseWebSocket_OnDoneReceived(object sender, WebfeedEventArgs<Done> e)
        {
            
        }

        private void CoinbaseWebSocket_OnChangeReceived(object sender, WebfeedEventArgs<Change> e)
        {
            
        }

        private void CoinbaseWebSocket_OnActivateReceived(object sender, WebfeedEventArgs<Activate> e)
        {
            
        }
    }
}
