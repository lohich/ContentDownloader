using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace ContentDownloader
{
    internal class DriverFactory
    {
        private readonly ChromeDriverFactoryParams _params;

        public DriverFactory(ChromeDriverFactoryParams opt)
        {
            _params = opt;
        }

        public IWebDriver GetDriver()
        {
            var driverParams = new ChromeOptions();
            driverParams.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
            driverParams.PageLoadStrategy = PageLoadStrategy.Normal;

            if (!_params.IsChromeWindowsRequired)
            {
                driverParams.AddArgument("headless");
            }

            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            var result = new ChromeDriver(service, driverParams);

            if (_params != null)
            {
                result.Navigate().GoToUrl(_params.AuthUrl);

                var tmp = _params.LoginSelector.Split(';');
                result.FindElement(By.XPath(tmp[0])).SendKeys(tmp[1]);

                tmp = _params.PasswordSelector.Split(';');
                result.FindElement(By.XPath(tmp[0])).SendKeys(tmp[1]);

                result.FindElement(By.XPath(_params.SubmitSelector)).Click();

                Thread.Sleep(1000);
            }

            return result;
        }

        public void Destroy(IWebDriver item)
        {
            item.Close();
            item.Quit();
            item.Dispose();
        }
    }
}
