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
    internal class DriverPool : IDisposable
    {
        private readonly ConcurrentQueue<IWebDriver> free = new ConcurrentQueue<IWebDriver>();

        private readonly ConcurrentBag<IWebDriver> all = new ConcurrentBag<IWebDriver>();

        private readonly SemaphoreSlim semaphore;

        private readonly int capacity;

        public DriverPool(int capacity)
        {
            this.capacity = capacity;
            semaphore = new SemaphoreSlim(capacity, capacity);
        }

        public int InUse => capacity - semaphore.CurrentCount;

        public void Dispose()
        {
            foreach (var item in all)
            {
                item.Close();
            }
        }

        public IWebDriver GetDriver()
        {
            semaphore.Wait();

            IWebDriver result;
            if (!free.TryDequeue(out result))
            {
                var driverParams = new ChromeOptions();
                driverParams.AddArgument("headless");
                driverParams.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
                var service = ChromeDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;
                result = new ChromeDriver(service, driverParams);
                all.Add(result);
            }
            
            return result;
        }

        public void Release(IWebDriver item)
        {
            free.Enqueue(item);
            semaphore.Release();            
        }
    }
}
