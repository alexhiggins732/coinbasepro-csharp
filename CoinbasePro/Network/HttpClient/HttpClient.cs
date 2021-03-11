using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CoinbasePro.Network.HttpClient
{
    public class HttpClient : IHttpClient
    {
        private static readonly System.Net.Http.HttpClient Client = new System.Net.Http.HttpClient();

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequestMessage)
        {
            return await SendAsync(httpRequestMessage, CancellationToken.None);
        }

        public async Task<HttpResponseMessage> SendAsync(
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
}
