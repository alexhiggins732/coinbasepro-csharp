using CoinbasePro.Network.HttpClient;
using CoinbasePro.Network.HttpRequest;
using CoinbasePro.Services.Fees.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace CoinbasePro.Services.Fees
{
    public class FeesService : AbstractService, IFeesService
    {
        public FeesService(
            IHttpClient httpClient,
            IHttpRequestMessageService httpRequestMessageService)
                : base(httpClient, httpRequestMessageService)
        {
        }

        public async Task<IEnumerable<Fee>> GetCurrentFeesAsync()
        {
            var fee = await SendServiceCall<Fee>(HttpMethod.Get, "/fees");

            return new[] { fee };
        }
    }
}
