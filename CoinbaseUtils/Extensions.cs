using CoinbasePro.Services.Orders;
using CoinbasePro.Services.Orders.Models.Responses;
using CoinbasePro.Services.Products.Models;
using CoinbasePro.Shared.Types;
using CoinbasePro.WebSocket.Models.Response;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.Serialization;

namespace CoinbaseUtils
{
    public static class Extensions
    {
        public static DateTime ConvertToLocal(DateTime value)
        {
            var now = DateTime.Now;
            var utcNow = DateTime.UtcNow;
            var diff = now.Subtract(utcNow).TotalHours;
            return value.AddHours(diff);
        }
        public static string ToJson(this object value, Formatting formatting = Formatting.Indented)
        {
            return JsonConvert.SerializeObject(value, formatting);
        }

        public static T FromJson<T>(this string value)
        {
            return JsonConvert.DeserializeObject<T>(value);
        }

        public static int ToIndex(this string value)
        {
            var parsed = int.TryParse(value, out int result);
            if (!parsed)
                result = -1;
            return result;
        }

        public static List<string> GetEnumNames<TEnum>(this TEnum value)
            where TEnum : Enum
        {
            var result = new List<string>();
            var names = Enum.GetNames(typeof(TEnum));
            foreach (var name in names)
            {
                result.Add(name);
            }
            return result;
        }

        public static Dictionary<string, TEnum> GetEnumDictionary<TEnum>(this TEnum value)
            where TEnum : struct, Enum
        {
            var result = new Dictionary<string, TEnum>();
            var names = Enum.GetNames(typeof(TEnum));
            foreach (var name in names)
            {
                Enum.TryParse<TEnum>(name, out TEnum parsed);
                result.Add(name, parsed);
                var enumMember = parsed.ToEnumMemberAttrValue();
                if (enumMember != name)
                {
                    result.Add(enumMember, parsed);
                }
            }

            return result;
        }

        public static string ToEnumMemberAttrValue(this Enum @enum)
        {
            var attr =
                @enum.GetType().GetMember(@enum.ToString()).FirstOrDefault()?.
                    GetCustomAttributes(false).OfType<EnumMemberAttribute>().
                    FirstOrDefault();
            if (attr == null)
                return @enum.ToString();
            return attr.Value;
        }

        public static bool ParseEnum<TEnum>(this string value, out TEnum result)
            where TEnum : struct, Enum
        {
            result = default(TEnum);
            var dict = result.GetEnumDictionary<TEnum>();
            foreach (var kvp in dict)
            {
                if (string.Compare(kvp.Key, value, true) == 0)
                {
                    result = kvp.Value;
                    return true;
                }
            }
            return false;
        }

        public static ConcurrentDictionary<TKey, TValue> ToConcurrentDictionary<TSource, TKey, TValue>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TValue> elementSelector)
            => new ConcurrentDictionary<TKey, TValue>(source.Select(x => new KeyValuePair<TKey, TValue>(keySelector(x), elementSelector(x))));

        public static ConcurrentDictionary<TKey, TValue> ToConcurrent<TKey, TValue>(this Dictionary<TKey, TValue> data)
            => new ConcurrentDictionary<TKey, TValue>(data);
        public static bool ParseDecimal(this string value, out decimal result)
        {
            var pctIdx = value.IndexOf("%");
            var dollarIdx = value.IndexOf("$");
            if (pctIdx == -1 && dollarIdx == -1)
            {
                return decimal.TryParse(value, out result);
            }
            else if (pctIdx != -1)
            {
                var decimalPart = value.Split('%')[0].Trim();
                bool parsed = decimal.TryParse(decimalPart, out result);
                if (parsed)
                {
                    result /= 100.0m;
                }
                return parsed;
            }
            else if (dollarIdx != -1)
            {
                var decimalPart = (value.Substring(dollarIdx + 1) ?? "").Trim();
                bool parsed = decimal.TryParse(decimalPart, out result);
                return parsed;
            }
            else
            {
                result = 0;
                return false;
            }


        }
        public static string ToCurrency(this decimal value) => value.ToCurrency(Currency.USD);

        public static string ToCurrency(this decimal value, Product product)
            => ToPrecision(value, product.QuoteIncrement).ToString();

        public static string ToCurrency(this decimal value, Currency currency)
        {


            var defaultPrecision = 2;
            int precision = defaultPrecision;
            switch (currency)
            {
                default:
                    break;
            }
            return Math.Round(value, precision).ToString();
        }
        public static string ToCurrency(this decimal value, ProductType productType)
        {
            var pair = new CurrencyPair(productType);
    
            var defaultPrecision = 2;
            int precision = defaultPrecision;
            switch (productType)
            {
                case ProductType.BtcUsd:
                case ProductType.BtcUsdc:
                case ProductType.LtcUsd:
                case ProductType.EthUsd:
                case ProductType.EthUsdc:
                case ProductType.DaiUsdc:
                    precision = 8;
                    break;
                default:
                    precision = 8;
                    //throw new NotImplementedException("ToCurrency({productType})");
                    break;
            }
            return Math.Round(value, precision).ToString();
        }
        public static Decimal ToPrecision(this decimal value, decimal precision)
        {
            var mult = 1m / precision;
            decimal temp = value * mult;
            var truncated = Math.Truncate(temp);
            var result = truncated / mult;
            return result;
        }
        public static int Precision(this decimal value)
        {
            return value.ToString().Trim('0').Split('.')[1].Length;
        }
        public static int GetPrecision(this decimal value)
        {
            return value.ToString().Trim('0').Split('.')[1].TrimEnd(new[] { '0' }).Length;
        }
        public static string ToDebugString(this CoinbasePro.Services.Orders.Models.Responses.OrderResponse x)
        {
            if (x == null)
                return "Invalid order resposne;";
            return $"{x.Id}: {x.Size} @ {x.Price.ToCurrency(x.ProductId)} ({x.FillFees.ToCurrency(x.ProductId)}) = ({(x.Price * x.Size).ToCurrency(x.ProductId)})";
        }
        public static string ToDebugString(this Open x)
        {
            return $"Open: {x.ProductId} {x.Side} {x.RemainingSize} @ {x.Price.ToCurrency(x.ProductId)} = ({(x.Price * x.RemainingSize).ToCurrency(x.ProductId)})";
        }

        public static string ToDebugString(this Received x)
        {
            return $"Received: {x.OrderId} {x.ProductId} {x.Side} {x.Size} @ {x.Price.ToCurrency(x.ProductId)} = ({(x.Price * x.Size).ToCurrency(x.ProductId)})";
        }

        public static string ToDebugString(this Done x)
        {
            var total = (x.Price * x.RemainingSize);
            var totalCurr = total.ToCurrency(x.ProductId);
            var result = $"Done: {x.OrderId} {x.ProductId} {x.Side} {x.Reason} @ {x.Price.ToCurrency(x.ProductId)} = ({totalCurr})";
            if (total == 0m || totalCurr=="0" || totalCurr== "0.0000")
            {
                string bp = "WTF";
            }
            Console.WriteLine($" internal=>  {result}");
            return result;
        }

        public static string ToDebugString(this Match x)
        {
            return $"Match: {x.ToJson()}";
        }

        public static string ToDebugString(this LastMatch x)
        {
            return $"LastMatch: {x.ToJson()}";
        }


        public static string ToDebugString(this Ticker x)
        {
            return $"Ticker: {x.ProductId} Buy: {x.BestBid.ToCurrency(x.ProductId)} Ask: {x.BestAsk.ToCurrency(x.ProductId)} Last: {x.LastSize} @ {x.Price.ToCurrency(x.ProductId)} = {(x.LastSize * x.Price).ToCurrency(x.ProductId)}";
        }

        public static bool TryParsePercent(this string value, out decimal result)
        {
            bool parsed = false;
            if (string.IsNullOrEmpty(value))
            {
                result = 0;
            }
            else
            {
                var cleaned = value.Trim().TrimEnd('%');
                parsed = decimal.TryParse(cleaned, out result);
                if (parsed)
                {
                    result /= 100;
                }
            }
            return parsed;
        }
    }
}
