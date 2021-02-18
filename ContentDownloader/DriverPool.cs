using System;
using System.Collections.Concurrent;
using System.Threading;
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
        private readonly AuthParams authParams;

        private bool disposed;

        public DriverPool(int capacity, AuthParams authParams)
        {
            this.capacity = capacity;
            this.authParams = authParams;
            semaphore = new SemaphoreSlim(capacity, capacity);
        }

        public int InUse => capacity - semaphore.CurrentCount;

        public void Dispose()
        {
            if (!disposed)
            {
                foreach (var item in all)
                {
                    item.Close();
                    item.Quit();
                    item.Dispose();
                }

                GC.SuppressFinalize(this);
                disposed = true;
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
                driverParams.PageLoadStrategy = PageLoadStrategy.Eager;
                var service = ChromeDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;
                result = new ChromeDriver(service, driverParams);

                all.Add(result);

                if (authParams != null)
                {
                    result.Navigate().GoToUrl(authParams.AuthUrl);

                    var tmp = authParams.LoginSelector.Split(';');
                    result.FindElement(By.XPath(tmp[0])).SendKeys(tmp[1]);

                    tmp = authParams.PasswordSelector.Split(';');
                    result.FindElement(By.XPath(tmp[0])).SendKeys(tmp[1]);

                    result.FindElement(By.XPath(authParams.SubmitSelector)).Click();

                    Thread.Sleep(1000);
                }
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
