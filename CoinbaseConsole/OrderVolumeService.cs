using CoinbasePro.Network.Authentication;
using CoinbasePro.Network.HttpClient;
using CoinbasePro.Network.HttpRequest;
using CoinbasePro.Services;
using CoinbasePro.Services.Orders.Models.Responses;
using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Shared.Utilities.Clock;
using CoinbasePro.Shared.Utilities.Extensions;
using CoinbasePro.Shared.Utilities.Queries;
using CoinbaseUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace CoinbaseConsole
{
    public class OrderVolumeService : AbstractService
    {

        private readonly QueryBuilder queryBuilder;
        protected IHttpClient httpClient;
        public OrderVolumeService(
          IHttpClient httpClient,
          IHttpRequestMessageService httpRequestMessageService,
          QueryBuilder queryBuilder)
              : base(httpClient, httpRequestMessageService)
        {
            this.queryBuilder = queryBuilder;
            this.httpClient = httpClient;
        }
        public static OrderVolumeService GetOrderVolumeService()
        {
            var authenticator = new Authenticator(Creds.ApiKey, Creds.ApiSecret, Creds.PassPhrase);
            var client = new CoinbasePro.Network.HttpClient.HttpClient();
            var clock = new Clock();
            bool sandBox = false;
            var httpRequestMessageService = new HttpRequestMessageService(authenticator, clock, sandBox);
            var queryBuilder = new QueryBuilder();
            return new OrderVolumeService(client, httpRequestMessageService, queryBuilder);
        }

        public static decimal Get30DayVolume()
        {
            var svc = OrderVolumeService.GetOrderVolumeService();


            var page = svc.GetFirstOrderPage(new[] { CoinbasePro.Services.Orders.Types.OrderStatus.All });
            var limit = DateTime.UtcNow.AddDays(-30);
            var sum = page.Items.Where(x => x.DoneAt >= limit).Sum(x => x.ExecutedValue);
            var min = page.Items.Where(x => x.DoneAt != DateTime.MinValue).Min(x => x.DoneAt);
            while (min >= limit)
            {

                page = page.GetNextPage();
                if (page.Items.Count > 0)
                {
                    sum += page.Items.Where(x => x.DoneAt >= limit).Sum(x => x.ExecutedValue);
                    min = page.Items.Where(x => x.DoneAt != DateTime.MinValue).Min(x => x.DoneAt);
                }
                else
                {
                    //todo
                }
            }
            return sum;
        }

        public IList<IList<OrderResponse>> GetOrders(
            OrderStatus[] orderStatus,
            int limit,
            int numberOfPages)
        {
            var queryKeyValuePairs = orderStatus.Select(p => new KeyValuePair<string, string>("status", p.GetEnumMemberValue())).ToArray();
            var query = queryBuilder.BuildQuery(queryKeyValuePairs);

            //var httpResponseMessage = await SendHttpRequestMessagePagedAsync<OrderResponse>(HttpMethod.Get, $"/orders{query}&limit={limit}", numberOfPages: numberOfPages);
            var httpResponseMessageTask = SendHttpRequestMessagePagedAsync<OrderResponse>(HttpMethod.Get, $"/orders{query}&limit={limit}", numberOfPages: numberOfPages);
            var httpResponseMessage = httpResponseMessageTask.Result;
            return httpResponseMessage;
        }

        public PagedResult<OrderResponse> GetFirstOrderPage(OrderStatus[] orderStatus)
        {
            var queryKeyValuePairs = orderStatus.Select(p => new KeyValuePair<string, string>("status", p.GetEnumMemberValue())).ToArray();
            var query = queryBuilder.BuildQuery(queryKeyValuePairs);
        
            var response= SendHttpRequestMessagePaged<OrderResponse>(HttpMethod.Get, $"/orders{query}");
            return response;
        }

        public class PagedResult<T>
        {
            public IList<T> Items;
            public string CbAfter;
            public Func<PagedResult<T>> GetNextPage;
            public PagedResult(IList<T> page)
            {
                this.Items = page;
            }
        }
        protected PagedResult<T> SendHttpRequestMessagePaged<T>(
          HttpMethod httpMethod,
          string uri,
          string content = null)
        {
            var httpResponseMessage = SendHttpRequestMessageAsync(httpMethod, uri, content).Result;
            var contentBody = httpClient.ReadAsStringAsync(httpResponseMessage).ConfigureAwait(false).GetAwaiter().GetResult();

            var firstPage = JsonConfig.DeserializeObject<IList<T>>(contentBody);
            var result = new PagedResult<T>(firstPage);


            if (!httpResponseMessage.Headers.TryGetValues("cb-after", out var firstPageAfterCursorId))
            {
                result.GetNextPage = () => null;
            }
            else
            {
                result.CbAfter = firstPageAfterCursorId.First();
                result.GetNextPage = () => GetNextPage(result, uri);
            }
            return result;

        }

        private PagedResult<T> GetNextPage<T>(PagedResult<T> current, string uri)
        {

            if (current.CbAfter == null)
            {
                return null;
            }
            else
            {
                var subsequentHttpResponseMessage = SendHttpRequestMessageAsync(HttpMethod.Get, uri + $"&after={current.CbAfter}").ConfigureAwait(false).GetAwaiter().GetResult();
                if (subsequentHttpResponseMessage.Headers.TryGetValues("cb-after", out var cursorHeaders))
                {
                    //break;
                }

                //subsequentPageAfterHeaderId = cursorHeaders.First();

                var subsequentContentBody = httpClient.ReadAsStringAsync(subsequentHttpResponseMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                var page = JsonConfig.DeserializeObject<IList<T>>(subsequentContentBody);
                var result = new PagedResult<T>(page);
                result.CbAfter = cursorHeaders.FirstOrDefault();
                result.GetNextPage = () => GetNextPage(result, uri);
                return result;

            }

        }
    }

}