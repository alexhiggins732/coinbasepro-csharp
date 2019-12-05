namespace CoinbaseUtils
{
    public interface ICoinbaseService
    {
       CoinbasePro.CoinbaseProClient client { get; }
    }
    public class CoinbaseService: ICoinbaseService
    {
        public CoinbasePro.CoinbaseProClient client => Client.Instance;
    }
}
