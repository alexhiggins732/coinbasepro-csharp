using System;
using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Shared.Types;
using CoinbaseUtils;
using CoinbaseUtilsTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace CoinbaseUtilsTests
{
    [TestClass]
    public class CommandBusTests
    {
        [TestMethod]
        public void TestCommandBus()
        {
            var memorySink = new InMemoryCommandSink();
            ICommandBusLogger logger = new CommandBusLogger();
            logger.AddSink(memorySink);
            var bus = new CommandBus(logger);

            ProductType productType = ProductType.LtcUsd;
            OrderSide orderSide = OrderSide.Buy;
            decimal size = 1m;
            decimal price = 1m;
            var command = new CreateOrderCommand(productType, orderSide, size, price);
            Assert.AreNotEqual(command.CommandGuid, Guid.Empty);
            bus.Send(command);

            var json = command.ToJson();
            var expected = $"Command {command.GetType()} received: {command.ToJson()}";
            var sinkMessages = memorySink.messages;
            Assert.IsTrue(sinkMessages.Contains(expected));
            Assert.IsTrue(sinkMessages.Contains(expected));

            var processor = new OrderProcessor(bus);
            var newcommand = new CreateOrderCommand(productType, orderSide, size, price);
            Assert.AreNotEqual(newcommand.CommandGuid, Guid.Empty);
            Assert.AreNotEqual(newcommand.CommandGuid, command.CommandGuid);
            var newjson = newcommand.ToJson();
            var newexpected = $"Command {newcommand.GetType()} received: {newcommand.ToJson()}";
            bus.Send(newcommand);
            Assert.IsTrue(sinkMessages.Contains(newexpected));
            Assert.IsTrue(sinkMessages.Contains(newexpected));
        }
    }

    
}
