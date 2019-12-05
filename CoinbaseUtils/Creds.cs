using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseUtils
{
    public static class Creds
    {
        public static readonly string ApiKey;
        public static string ApiSecret;
        public static string PassPhrase;
        static Creds()
        {
            ApiKey = CredentialHelper.Instance.ApiKey;
            ApiSecret = CredentialHelper.Instance.ApiSecret;
            PassPhrase = CredentialHelper.Instance.PassPhrase;
        }
    }
}
