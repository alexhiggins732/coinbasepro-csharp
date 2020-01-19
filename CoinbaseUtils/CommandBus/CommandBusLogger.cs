using System.Collections.Generic;

namespace CoinbaseUtils
{
    public class CommandBusLogger : ICommandBusLogger
    {
        CommandBus bus = null;
        private List<ICommandSink> sinks = new List<ICommandSink>();
        public CommandBusLogger()
        {

        }

        public void AddSink(InMemoryCommandSink memorySink)
        {
            sinks.Add(memorySink);
        }

        public void Dispose()
        {
            if (bus != null)
            {
                bus.CommandReceived -= CommandBus_CommandRecieved;
                bus = null;
            }
        }

        public void RegisterBus(CommandBus commandBus)
        {
            this.bus = commandBus;
            bus.CommandReceived += CommandBus_CommandRecieved;
        }

        private void CommandBus_CommandRecieved(object sender, ICommand e)
        {
            string message = $"Command {e.GetType()} received: {e.ToJson()}";
            sinks.ForEach(sink =>
            {
                sink.ProcessCommand(e, message);
            });
        }
    }
}
