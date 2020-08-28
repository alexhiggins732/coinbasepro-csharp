using CoinbasePro.Services.Orders.Types;
using CoinbasePro.Shared.Types;
using CoinbaseUtils;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace CoinbaseConsole
{
    public class OrderSyncer : IDisposable
    {

        private Timer SyncTimer;
        private bool isRunning;
        private double syncInterval;
        public double SyncInterval
        {
            get
            {
                return syncInterval;
            }
            set
            {
                syncInterval =  value;
                if (SyncTimer != null) SyncTimer.Interval = syncInterval;
            }
        }

        DateTime LastSync;
        private ConsoleDriver consoleDriver;
        public OrderSyncer( int syncIntervalSeconds = 60)
        {
            SyncInterval = syncIntervalSeconds * 1000;
        }

        public OrderSyncer(ConsoleDriver consoleDriver)
        {
            this.consoleDriver = consoleDriver;
        }



        public List<ProductType> ProductTypes => OrderManager.ProductTypes;
        private void SyncOrders()
        {
            OrderManager.Refresh();
        }
        private void SyncTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var current = ProductTypes.ToList();
            SyncOrders();
            if (!ProductTypes.SequenceEqual(current))
            {
                var added = ProductTypes.Except(current).ToList();
                var removed = current.Except(ProductTypes).ToList();
            }
            consoleDriver.SetProductFeeds(ProductTypes);
        }
        public override string ToString()
        {
            return $"=> OrderSync: Running: {isRunning}({(int)(syncInterval / 1000)}s): {string.Join(",", ProductTypes.Select(x => x))}";
        }

        public void Start()
        {
            Stop();
           
            this.SyncTimer = new Timer();
            SyncTimer.Interval = syncInterval;
            SyncTimer.Elapsed += SyncTimer_Elapsed;
            SyncTimer.Start();

            isRunning = true;
        }

        private void Stop()
        {
            if (SyncTimer != null)
            {
                SyncTimer.Stop();
                SyncTimer.Elapsed -= SyncTimer_Elapsed;
                SyncTimer = null;
            }
            isRunning = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
