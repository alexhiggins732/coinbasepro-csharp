using System;

namespace CoinbaseUtils
{
    public abstract class CommandBase : ICommand
    {
        public int CommandId { get; set; }
        public Guid CommandGuid { get; set; } = Guid.NewGuid();
        public abstract string CommandType { get;  }
    }
}
