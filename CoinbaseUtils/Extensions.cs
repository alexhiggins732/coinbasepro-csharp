using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            }
            return result;
        }
        public static bool ParseEnum<TEnum>(this string value, out TEnum result)
            where TEnum : struct, Enum
        {
            result = default(TEnum);
            var dict = result.GetEnumDictionary<TEnum>();
            foreach(var kvp in dict)
            {
                if (string.Compare(kvp.Key, value, true) == 0)
                {
                    result = kvp.Value;
                    return true;
                }
            }
            return false;
        }

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
                var decimalPart = (value.Substring(dollarIdx+1) ?? "").Trim();
                bool parsed = decimal.TryParse(decimalPart, out result);
                return parsed;
            }
            else
            {
                result = 0;
                return false;
            }


        }
    }
}
