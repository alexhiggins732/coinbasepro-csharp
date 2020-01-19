namespace CoinbaseUtils
{
    public interface ICommandSink
    {
        void ProcessCommand(ICommand e, string message);
    }
}
