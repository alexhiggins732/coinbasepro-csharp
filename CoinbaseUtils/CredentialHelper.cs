using CoinbasePro.Network.Authentication;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace CoinbaseUtils
{
    internal class CredentialHelper
    {

        public static IAuthenticator GetAuthenticator() => new Authenticator(Creds.ApiKey, Creds.ApiSecret, Creds.PassPhrase);
        private const string ApiKeyPlaceHolder = "<apiKey>";
        private const string ApiSecretPlaceHolder = "<apiSecret>";
        private const string PassPhrasePlaceHolder = "<passphrase>";

        public static readonly CredentialHelper Instance;
        static CredentialHelper()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var drive = Path.GetPathRoot(assemblyPath);
            var credsFolderPath = Path.Combine(drive, "CredsFolder");
            var credsFolder = Directory.CreateDirectory(credsFolderPath);
            var credsFilePath = Path.Combine(credsFolder.FullName, "creds.json");

            var creds = new CredentialHelper()
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
                    creds = File.ReadAllText(credsFilePath).FromJson<CredentialHelper>();
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
}
