using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace ContentDownloader
{
    static class DriverPool
    {
        private static ConcurrentQueue<IWebDriver> pool = new ConcurrentQueue<IWebDriver>();

        private static int inUse;

        public static int Capacity { get; set; } = 5;

        public static IWebDriver GetDriver()
        {
            while(inUse >= Capacity)
            {
                Thread.Sleep(50);
            }

            IWebDriver result;
            if (!pool.TryDequeue(out result))
            {
                var driverParams = new ChromeOptions();
                driverParams.AddArgument("headless");
                result = new ChromeDriver(driverParams);
            }

            Interlocked.Increment(ref inUse);

            return result;
        }

        public static void Release(IWebDriver item)
        {
            pool.Enqueue(item);
            Interlocked.Decrement(ref inUse);
        }
    }
}
