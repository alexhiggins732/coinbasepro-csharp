using CoinbasePro.Network.Authentication;

namespace CoinbaseUtils
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
}
