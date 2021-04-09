using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace ContentDownloader
{
    internal class DriverFactory
    {
        private readonly AuthParams authParams;

        public DriverFactory(AuthParams authParams)
        {
            this.authParams = authParams;
        }

        public IWebDriver GetDriver()
        {
            var driverParams = new ChromeOptions();
            //driverParams.AddArgument("headless");
            driverParams.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
            driverParams.PageLoadStrategy = PageLoadStrategy.Normal;
            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;
            var result = new ChromeDriver(service, driverParams);

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
