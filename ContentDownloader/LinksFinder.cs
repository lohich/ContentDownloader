using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;

namespace ContentDownloader
{
    internal class LinksFinder
    {
        private readonly DriverPool driverPool;
        private readonly ImageDownloader downloader;

        private readonly ConcurrentQueue<Uri> containerLinks = new ConcurrentQueue<Uri>();
        private int totalLinks;
        
        public LinksFinder(ImageDownloader downloader, DriverPool driverPool)
        {
            this.downloader = downloader;
            this.driverPool = driverPool;
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
                    result.AddRange(element.FindElements(By.XPath(xpath)).Select(x => x.GetAttribute(a)));
                }
                catch (NoSuchElementException) { }
            }

            return result.Distinct();
        }

        private IWebDriver GetPage(string url)
        {
            var driver = driverPool.GetDriver();
            driver.Navigate().GoToUrl(url);

            return driver;
        }

        private IWebDriver GetNextPage(IWebDriver driver, string xpath)
        {
            try
            {
                var nextPageLink = driver.FindElement(By.XPath(xpath)).GetAttribute("href");
                driverPool.Release(driver);
                return GetPage(nextPageLink);
            }
            catch (Exception e) when (e is ArgumentNullException || e is NoSuchElementException)
            {
                driverPool.Release(driver);
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

        private void ListPages(string firstPageUrl, string nextPageSelector, string linksSelector, Action<ISearchContext> actionOnPage = null)
        {
            var currentPage = GetPage(firstPageUrl);

            while (currentPage != null)
            {
                actionOnPage?.Invoke(currentPage);

                EnqueueLinks(linksSelector, currentPage);
                currentPage = GetNextPage(currentPage,nextPageSelector);
            }
        }

        private void ListPagesInContainer(string nextPageSelector, string linksSelector)
        {
            if (nextPageSelector != null)
            {
                while (!IsContainersFindingFinished || !containerLinks.IsEmpty)
                {
                    if (containerLinks.TryDequeue(out var firstPageUrl))
                    {
                        ListPages(firstPageUrl.ToString(), nextPageSelector, linksSelector);
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        public async Task FindLinks(CommandLineParams args)
        {
            var threads = new List<Task>();

            for (int i = 0; i < args.DownloadThreadsCount; i++)
            {
                threads.Add(Task.Run(() => ListPagesInContainer(args.NextPageInContainerSeclector, args.LinkSelector)));
            }

            ListPages(args.URI.ToString(), args.NextPageContainerSelector, args.LinkSelector,
                page => DoWithLinks(args.ContainerSelector, page, "href",
                containerLink => containerLinks.Enqueue(new Uri(containerLink))));

            IsContainersFindingFinished = true;

            await Task.WhenAll(threads);

            IsFinished = true;
        }
    }
}
