using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;

namespace ContentDownloader
{
    internal class LinksFinder
    {
        private readonly DriverFactory driverFactory;
        private readonly ImageDownloader downloader;

        private readonly ConcurrentQueue<Uri> containerLinks = new ConcurrentQueue<Uri>();
        private int totalLinks;

        public LinksFinder(ImageDownloader downloader, DriverFactory driverFactory)
        {
            this.downloader = downloader;
            this.driverFactory = driverFactory;
        }

        public int TotalLinks => totalLinks;
        public bool IsFinished { get; private set; }
        public bool IsContainersFindingFinished { get; private set; }

        private IEnumerable<string> GetElementsAttributesByXpath(string xpath, string attribute, ISearchContext element)
        {
            if (xpath == null)
            {
                return null;
            }

            var attibutes = attribute.Split(';');

            var result = new List<string>();
            foreach (var a in attibutes)
            {
                try
                {
                    result.AddRange(element.FindElements(By.XPath(xpath)).Select(x => x.GetAttribute(a)).ToArray());
                }
                catch (NoSuchElementException) { }
            }

            return result.Distinct();
        }

        private void GetPage(IWebDriver driver, Uri url)
        {
            driver.Navigate().GoToUrl(url);
        }

        private IWebDriver GetNextPage(IWebDriver driver, string xpath)
        {
            try
            {
                var nextPageLink = driver.FindElement(By.XPath(xpath)).GetAttribute("href");
                driver.Navigate().GoToUrl(nextPageLink);
                return driver;
            }
            catch (Exception e) when (e is ArgumentNullException || e is NoSuchElementException)
            {
                return null;
            }
        }

        private void DoWithLinks(string selector, ISearchContext element, string attributes, Action<string> whatToDo)
        {
            var links = GetElementsAttributesByXpath(selector, attributes, element);

            if (links != null)
            {
                foreach (var link in links.Where(x => x != null))
                {
                    whatToDo(link);
                }
            }
        }

        private void EnqueueLinks(string selector, ISearchContext element)
        {
            DoWithLinks(selector, element, "href;src", link =>
            {
                downloader.Download(new Uri(link));
                Interlocked.Increment(ref totalLinks);
            });
        }

        private void ListPages(Uri firstPageUrl, string nextPageSelector, string linksSelector, Action<ISearchContext> actionOnPage = null)
        {
            var driver = driverFactory.GetDriver();
            GetPage(driver, firstPageUrl);

            ListPages(driver, nextPageSelector, linksSelector, actionOnPage);

            driverFactory.Destroy(driver);
        }

        private void ListPages(IWebDriver currentPage, string nextPageSelector, string linksSelector, Action<ISearchContext> actionOnPage = null)
        {
            while (currentPage != null)
            {
                actionOnPage?.Invoke(currentPage);

                EnqueueLinks(linksSelector, currentPage);
                currentPage = GetNextPage(currentPage, nextPageSelector);
            }
        }

        private void ListPagesInContainer(string nextPageSelector, string pathSelector, string linksSelector)
        {
            var driver = driverFactory.GetDriver();

            while (!IsContainersFindingFinished || !containerLinks.IsEmpty)
            {
                if (containerLinks.TryDequeue(out var firstPageUrl))
                {
                    GetPage(driver, firstPageUrl);

                    if (pathSelector != null)
                    {
                        var path = pathSelector.Split(";");
                        foreach (var item in path)
                        {
                            driver = GetNextPage(driver, item);
                        }
                    }
                    ListPages(driver, nextPageSelector, linksSelector);
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            driverFactory.Destroy(driver);
        }

        public async Task FindLinks(CommandLineParams args)
        {
            var threads = new List<Task>();

            for (int i = 0; i < args.DownloadThreadsCount; i++)
            {
                threads.Add(Task.Run(() => ListPagesInContainer(args.NextPageInContainerSeclector, args.PathInContainer, args.LinkSelector)));
            }

            ListPages(args.URI, args.NextPageContainerSelector, args.LinkSelector,
                page => DoWithLinks(args.ContainerSelector, page, "href",
                containerLink => containerLinks.Enqueue(new Uri(containerLink))));

            IsContainersFindingFinished = true;

            await Task.WhenAll(threads);

            IsFinished = true;
        }
    }
}
