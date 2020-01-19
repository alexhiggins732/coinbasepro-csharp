using System;

namespace CoinbaseUtils
{
    public abstract class CommandProcessor : IDisposable
    {
        protected CommandBus bus;

        public CommandProcessor(CommandBus bus)
        {
            this.bus = bus;
            AddHandlers();
        }
        private void AddHandlers()
        {
            bus.CommandExecute += Bus_CommandExecute;
        }
        public void Dispose()
        {
            if (bus != null)
            {
                bus.CommandExecute -= Bus_CommandExecute;
            }
        }

        protected abstract void Bus_CommandExecute(object sender, ICommand e);
    }
    public class OrderProcessor : IDisposable
    {
        CommandBus bus;
        public OrderProcessor(CommandBus bus)
        {
            this.bus = bus;
            AddHandlers();
        }

        private void AddHandlers()
        {
            bus.CommandExecute += Bus_CommandExecute;
        }

        public void Dispose()
        {
            if (bus != null)
            {
                bus.CommandExecute -= Bus_CommandExecute;
            }
        }

        private void Bus_CommandExecute(object sender, ICommand e)
        {
            if (e is CreateOrderCommand createOrderCommand)
            {
                var orderCreatedCommand = new OrderCreatedCommand(createOrderCommand);
                bus.Send(orderCreatedCommand);

            }
        }
    }


    public class GetAccountBalanceCommand : CommandBase
    {
        public override string CommandType => nameof(GetAccountBalanceCommand);
    }
    public class GetMakeFeeRateCommand: CommandBase
    {
        public override string CommandType => nameof(GetMakeFeeRateCommand);
    }
    public class MakerFeeRateCommand : CommandBase
    {
        public override string CommandType => nameof(GetMakeFeeRateCommand);
        public decimal MakerFeeRate;

        public MakerFeeRateCommand(decimal makerFeeRate)
        {
            MakerFeeRate = makerFeeRate;
        }
    }
    public class TakerFeeRateCommand : CommandBase
    {
        public override string CommandType => nameof(TakerFeeRateCommand);
        public decimal TakerFeeRate;

        public TakerFeeRateCommand(decimal makerFeeRate)
        {
            TakerFeeRate = makerFeeRate;
        }
    }

    public class AccountCommandProcessor : CommandProcessor
    {
        private AccountService service = new AccountService();
        public AccountCommandProcessor(CommandBus bus)
            : base(bus)
        {
        }
        protected override void Bus_CommandExecute(object sender, ICommand e)
        {
            if (e is GetMakeFeeRateCommand makerFeeRateCommand)
            { 
                bus.Send(new MakerFeeRateCommand(service.MakerFeeRate));
            }
            else if (e is GetMakeFeeRateCommand takerFeeRateCommand)
            {
                bus.Send(new TakerFeeRateCommand(service.MakerFeeRate));
            }
        }
    }


}
