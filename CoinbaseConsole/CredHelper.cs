using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CoinbasePro.Network.Authentication;
using Newtonsoft.Json;
namespace CoinbaseConsole
{
    public class Client
    {
        public static CoinbasePro.CoinbaseProClient Instance = null;
        static Client()
        {
            var authenticator = new Authenticator(Creds.ApiKey, Creds.ApiSecret, Creds.PassPhrase);

            Instance = new CoinbasePro.CoinbaseProClient(authenticator);
        }
    }
    public static class Creds
    {
        public static readonly string ApiKey;
        public static string ApiSecret;
        public static string PassPhrase;
        static Creds()
        {
            ApiKey = CredHelper.Instance.ApiKey;
            ApiSecret = CredHelper.Instance.ApiSecret;
            PassPhrase = CredHelper.Instance.PassPhrase;
        }
    }
    internal class CredHelper
    {

        private const string ApiKeyPlaceHolder = "<apiKey>";
        private const string ApiSecretPlaceHolder = "<apiSecret>";
        private const string PassPhrasePlaceHolder = "<passphrase>";

        public static readonly CredHelper Instance;
        static CredHelper()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var drive = Path.GetPathRoot(assemblyPath);
            var credsFolderPath = Path.Combine(drive, "CredsFolder");
            var credsFolder = Directory.CreateDirectory(credsFolderPath);
            var credsFilePath = Path.Combine(credsFolder.FullName, "creds.json");

            var creds = new CredHelper()
            {
                ApiKey = ApiKeyPlaceHolder,
                ApiSecret = ApiSecretPlaceHolder,
                PassPhrase = PassPhrasePlaceHolder
            };

            if (!File.Exists(credsFilePath))
            {
                File.WriteAllText(credsFilePath, creds.ToJson());
            }

            bool read = false;
            while (!read)
            {
                try
                {
                    creds = File.ReadAllText(credsFilePath).FromJson<CredHelper>();
                }

                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing credentials: {ex.Message}");
                    continue;
                }

                if (creds.ApiKey == ApiKeyPlaceHolder
                    || creds.ApiSecret == ApiSecretPlaceHolder
                    || creds.PassPhrase == PassPhrasePlaceHolder)
                {
                    Process.Start("notepad.exe", $"\"{credsFilePath}\"");
                    Console.WriteLine($"Please update the {ApiKeyPlaceHolder}, {ApiSecretPlaceHolder}, {PassPhrasePlaceHolder}.");
                    Console.ReadKey();
                }
                else
                {
                    read = true;
                }
            }
            Instance = creds;


        }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }

        public string PassPhrase { get; set; }
    }

    public static class Ext
    {
        public static string ToJson(this object value, Formatting formatting = Formatting.Indented)
        {
            return JsonConvert.SerializeObject(value, formatting);
        }

        public static T FromJson<T>(this string value)
        {
            return JsonConvert.DeserializeObject<T>(value);
        }
    }
}
