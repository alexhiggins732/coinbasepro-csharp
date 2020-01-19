using System;

namespace CoinbaseUtils
{
    public interface ICommandBusLogger : IDisposable
    {
        void AddSink(InMemoryCommandSink memorySink);
        void RegisterBus(CommandBus commandBus);
    }
}
