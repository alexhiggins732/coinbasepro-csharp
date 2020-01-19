using System;

namespace CoinbaseUtils
{
    public interface ICommand
    {
        int CommandId { get; set; }
        Guid CommandGuid { get; set; }
        string CommandType { get; }
    }
}
