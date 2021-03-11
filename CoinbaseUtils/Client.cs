using CoinbasePro.Network.Authentication;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CoinbaseUtils
{
    public class Client
    {
        public static CoinbasePro.CoinbaseProClient Instance = null;
        static Client()
        {
            var authenticator = new Authenticator(Creds.ApiKey, Creds.ApiSecret, Creds.PassPhrase);

            var client = new ThrottledHttpClient();
            Instance = new CoinbasePro.CoinbaseProClient(authenticator, client);
        }

        private class ThrottledHttpClient : CoinbasePro.Network.HttpClient.IHttpClient
        {
            private static readonly System.Net.Http.HttpClient Client = new System.Net.Http.HttpClient();

            private static readonly Limiter limiter = new Limiter();
            public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequestMessage)
            {
                return await SendAsync(httpRequestMessage, CancellationToken.None);
            }

            public async Task<HttpResponseMessage> SendAsync(
               HttpRequestMessage httpRequestMessage,
               CancellationToken cancellationToken)
            {
                return await limiter.Execute(() => SendAsyncInternal(httpRequestMessage, cancellationToken));
            }
            public async Task<HttpResponseMessage> SendAsyncInternal(
                HttpRequestMessage httpRequestMessage,
                CancellationToken cancellationToken)
            {

                var messageJson = httpRequestMessage.ToString();
                var result = await Client.SendAsync(httpRequestMessage, cancellationToken);
                if (result.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    var r = await result.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"[{System.DateTime.Now}] Error: {httpRequestMessage.RequestUri} {result.ReasonPhrase}");
                }
                return result;

            }

            public async Task<string> ReadAsStringAsync(HttpResponseMessage httpRequestMessage)
            {
                var result = await httpRequestMessage.Content.ReadAsStringAsync();
                return result;
            }
        }

        public class Limiter
        {
            public class QueueRequest
            {
                public Guid RequestId;
                public DateTime RequestDate;
                public QueueRequest(int id)
                {
                    RequestId = Guid.NewGuid();// id;
                    RequestDate = DateTime.Now;
                }
            }
            private ConcurrentQueue<QueueRequest> RequestQueue = new ConcurrentQueue<QueueRequest>();
            private ConcurrentQueue<QueueRequest> ExecutionQueue = new ConcurrentQueue<QueueRequest>();

            public T Execute<T>(Func<T> action)
            {
                var queueRequest = new QueueRequest(0);
                RequestQueue.Enqueue(queueRequest);
                QueueRequest executedRequest = null;

                while (RequestQueue.TryPeek(out QueueRequest nextRequest) && nextRequest.RequestId != queueRequest.RequestId)
                {
                    System.Threading.Thread.Sleep(1);
                }

                ExecutionQueue.Enqueue(queueRequest);
                while (ExecutionQueue.Count > 5)
                {
                    if (ExecutionQueue.TryPeek(out executedRequest)
                        && DateTime.Now.Subtract(executedRequest.RequestDate).TotalSeconds > 1
                        && RequestQueue.TryPeek(out QueueRequest nextRequest)
                        && nextRequest.RequestId == queueRequest.RequestId
                        )
                    {
                        break;
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(1);
                    }
                }

                T result = action();
                queueRequest.RequestDate = DateTime.Now;
                if (executedRequest != null)
                {
                    ExecutionQueue.TryDequeue(out executedRequest);
                }
                RequestQueue.TryDequeue(out queueRequest);
                return result;
            }

        }
    }

}
