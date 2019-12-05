using CoinbasePro.Shared.Types;
using CoinbaseUtils;
using SuperSocket.SocketBase;
using SuperWebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseConsole
{
    public class TickerServer : SocketServerBase, IDisposable
    {
        private ConcurrentDictionary<string, WebSocketSession> sessions;
        private CoinbaseTicker ticker;

        public TickerServer(WebSocketServer appServer, CoinbaseTicker ticker) : base(appServer)
        {
            this.ticker = ticker;
            sessions = new ConcurrentDictionary<string, WebSocketSession>();
            bindEvents();
        }

        void bindEvents()
        {
            OnSessionOpened += AppServer_NewSessionConnected;
            OnSessionClosed += AppServer_SessionClosed;
            ticker.OnTickerReceived += Ticker_OnTickerReceived;
        }
        void unbindEvents()
        {
            OnSessionOpened -= AppServer_NewSessionConnected;
            OnSessionClosed -= AppServer_SessionClosed;
            ticker.OnTickerReceived -= Ticker_OnTickerReceived;
        }

        private void AppServer_SessionClosed(WebSocketSession session, CloseReason closeReason)
        {
            sessions?.TryRemove(session.SessionID, out WebSocketSession value);
        }

        private void Ticker_OnTickerReceived(object sender, CoinbasePro.WebSocket.Models.Response.WebfeedEventArgs<CoinbasePro.WebSocket.Models.Response.Ticker> e)
        {
            sessions?.ToList()
                .ForEach(session =>
                {
                    try
                    {
                        session.Value.Send(e.ToJson());
                    }
                    catch { };
                });
        }

        private void AppServer_NewSessionConnected(WebSocketSession session)
        {
            sessions?.TryAdd(session.SessionID, session);

        }


        public override void Stop()
        {
            unbindEvents();
            ticker = null;
            if (sessions != null)
            {
                try { sessions.Clear(); } catch { }
                sessions = null;
            }
            base.Stop();

        }
        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }


        public static TickerServer Create(int port, ProductType productType)
            => Create(port, CoinbaseTicker.Create(productType));

        public static TickerServer Create(int port, CoinbaseTicker ticker)
        {
            var appServer = new WebSocketServer();
            if (!appServer.Setup(port)) //Setup with listening port
            {
                Console.WriteLine("Failed to setup!");
                return null;
            }
            var result = new TickerServer(appServer, ticker);
            return result;
        }
    }
    public class SocketServerBase : IDisposable
    {
        //
        // Summary:
        //     Occurs when [new message received].
        public event SessionHandler<WebSocketSession, string> OnMessageReceived;
        //
        // Summary:
        //     Occurs when [new data received].
        public event SessionHandler<WebSocketSession, byte[]> OnDataReceived;
        public event SessionHandler<WebSocketSession> OnSessionOpened;
        protected WebSocketServer AppServer;
        public event SessionHandler<WebSocketSession, CloseReason> OnSessionClosed;

        public static SocketServerBase Create(int port)
        {
            var appServer = new WebSocketServer();
            if (!appServer.Setup(port)) //Setup with listening port
            {
                Console.WriteLine("Failed to setup!");
                return null;
            }
            return new SocketServerBase(appServer);
        }

        public SocketServerBase(WebSocketServer appServer)
        {
            this.AppServer = appServer;

            AppServer.NewMessageReceived += new SessionHandler<WebSocketSession, string>(AppServer_NewMessageReceived);
            AppServer.NewDataReceived += new SessionHandler<WebSocketSession, byte[]>(AppServer_NewDataReceived);
            AppServer.NewSessionConnected += AppServer_NewSessionConnected;
            AppServer.SessionClosed += AppServer_SessionClosed;
        }

        public bool Start() => AppServer.Start();
        private void AppServer_SessionClosed(WebSocketSession session, CloseReason value)
        {
            OnSessionClosed?.Invoke(session, value);
        }

        private void AppServer_NewSessionConnected(WebSocketSession session)
        {
            OnSessionOpened?.Invoke(session);
        }

        private void AppServer_NewDataReceived(WebSocketSession session, byte[] value)
        {
            OnDataReceived?.Invoke(session, value);
        }

        private void AppServer_NewMessageReceived(WebSocketSession session, string value)
        {
            OnMessageReceived?.Invoke(session, value);
        }

        public virtual void Stop()
        {
            if (AppServer != null)
            {
                try
                {
                    AppServer.NewMessageReceived -= new SessionHandler<WebSocketSession, string>(AppServer_NewMessageReceived);
                    AppServer.NewDataReceived -= new SessionHandler<WebSocketSession, byte[]>(AppServer_NewDataReceived);
                    AppServer.NewSessionConnected -= AppServer_NewSessionConnected;
                    AppServer.SessionClosed -= AppServer_SessionClosed;
                }
                catch { }
                AppServer.Stop();
                AppServer = null;
            }
        }

        public virtual void Dispose()
        {
            Stop();
        }


    }
}
