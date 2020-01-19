using System;

namespace CoinbaseUtils
{
    public class CommandBus : IDisposable
    {
        ICommandBusLogger busLogger;
        public CommandBus(ICommandBusLogger busLogger)
        {
            this.busLogger = busLogger;
            busLogger.RegisterBus(this);
        }
        public void Send(ICommand command)
        {
            CommandReceived?.Invoke(this, command);
            CommandExecuting?.Invoke(this, command);
            CommandExecute?.Invoke(this, command);
            CommandExecuted?.Invoke(this, command);
            RaisingEvents?.Invoke(this, command);
            RaisedEvents?.Invoke(this, command);
        }

        public void Dispose()
        {
            if (busLogger != null)
            {
                busLogger.Dispose();
                busLogger = null;
            }
        }

        public EventHandler<ICommand> CommandReceived;
        public EventHandler<ICommand> CommandExecuting;
        public EventHandler<ICommand> CommandExecute;
        public EventHandler<ICommand> CommandExecuted;
        public EventHandler<ICommand> RaisingEvents;
        public EventHandler<ICommand> RaisedEvents;
    }
}
