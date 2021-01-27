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
    internal class DriverPool
    {
        private readonly ConcurrentQueue<IWebDriver> pool = new ConcurrentQueue<IWebDriver>();

        private readonly Semaphore semaphore;

        public DriverPool(int capacity)
        {
            semaphore = new Semaphore(capacity, capacity);
        }

        public int InUse { get; private set; }

        public IWebDriver GetDriver()
        {
            semaphore.WaitOne();

            InUse++;

            IWebDriver result;
            if (!pool.TryDequeue(out result))
            {
                var driverParams = new ChromeOptions();
                driverParams.AddArgument("headless");
                driverParams.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
                var service = ChromeDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;
                result = new ChromeDriver(service, driverParams);
            }
            
            return result;
        }

        public void Release(IWebDriver item)
        {
            pool.Enqueue(item);
            InUse--;
            semaphore.Release();            
        }
    }
}
