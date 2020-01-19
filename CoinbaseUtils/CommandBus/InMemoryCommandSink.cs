using System.Collections.Generic;

namespace CoinbaseUtils
{
    public class InMemoryCommandSink : ICommandSink
    {
        public List<string> messages = new List<string>();

        public void ProcessCommand(ICommand e, string message)
        {
            messages.Add(message);
        }
    }
}
