using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CommandLine;
using OpenQA.Selenium;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Web;

namespace ContentDownloader
{
    static class Program
    {
        static bool isFinished;
        static bool isContainersFindingFinished;
        static ConcurrentQueue<(Uri link, string fileName)> downloadLinks = new ConcurrentQueue<(Uri, string)>();
        static ConcurrentQueue<Uri> containerLinks = new ConcurrentQueue<Uri>();
        static int totalLinks;
        static int downloaded;
        static DriverPool driverPool;
        static bool IsExecuting => !isFinished || !downloadLinks.IsEmpty;

        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<CommandLineParams>(args).WithNotParsed(x => Console.ReadKey()).WithParsedAsync(DoWork);
        }

        static async Task DoWork(CommandLineParams args)
        {
            if (!Directory.Exists(args.Output))
            {
                Directory.CreateDirectory(args.Output);
            }

            var threads = new List<Task>();
            threads.Add(Task.Run(Stats));

            driverPool = new DriverPool(args.DownloadThreadsCount);

            for (int i = 0; i < args.DownloadThreadsCount; i++)
            {
                threads.Add(Task.Run(async () => await Download(args.Output)));
            }

            threads.Add(Task.Run(async () => await FindLinks(args)));

            await Task.WhenAll(threads);

            Console.WriteLine("Finished!");
            Console.ReadKey();
        }

        static void Stats()
        {
            var startTime = DateTime.Now;
            while (IsExecuting)
            {
                Console.Clear();
                Console.WriteLine($"Started {startTime}");
                Console.WriteLine($"Downloaded {downloaded}/{totalLinks}");
                Console.WriteLine($"Drivers in use: {driverPool.InUse}");
                Console.WriteLine($"Time passed: {DateTime.Now - startTime}");
                if (isContainersFindingFinished)
                {
                    Console.WriteLine("All containers found");
                }
                if (isFinished)
                {
                    Console.WriteLine("All links found");
                }
                Thread.Sleep(1000);
            }
        }

        static async Task Download(string path)
        {
            while (IsExecuting)
            {
                if (downloadLinks.TryDequeue(out var info))
                {
                    var fileName = info.fileName;
                    var url = info.link;

                    using var client = new HttpClient();
                    var bmp = new Bitmap(await client.GetStreamAsync(url));
                    if (fileName == null)
                    {
                        fileName = Path.Combine(path, HttpUtility.UrlDecode(url.Segments.Last()));
                    }
                    else
                    {
                        if (!fileName.Contains(".jpg"))
                        {
                            fileName += ".jpg";
                        }
                        fileName = Path.Combine(path, fileName);
                    }

                    var tmp = Path.Combine(path, Path.GetRandomFileName());

                    bmp.Save(tmp, ImageFormat.Jpeg);

                    if (File.Exists(fileName))
                    {
                        var mask = fileName.Insert(fileName.IndexOf(".jpg"), "({0})");
                        int index = 1;
                        while (File.Exists(fileName))
                        {
                            fileName = string.Format(mask, index);
                            index++;
                        }
                    }

                    File.Move(tmp, fileName);
                    Interlocked.Increment(ref downloaded);
                }
            }
        }

        static IEnumerable<string> GetElementsAttributesByXpath(string xpath, string attribute, ISearchContext element)
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

        static IWebDriver GetPage(string url)
        {
            var driver = driverPool.GetDriver();
            driver.Navigate().GoToUrl(url);

            return driver;
        }

        static IWebDriver GetNextPage(this IWebDriver driver, string xpath)
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

        static void DoWithLinks(string selector, ISearchContext element, string attributes, Action<string> whatToDo)
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

        static void EnqueueLinks(string selector, ISearchContext element, string fileNameSelector)
        {
            DoWithLinks(selector, element, "href;src", link =>
            {
                var fileName = fileNameSelector == null ? null : element.FindElement(By.XPath(fileNameSelector)).Text;
                downloadLinks.Enqueue((new Uri(link), fileName));
                Interlocked.Increment(ref totalLinks);
            });
        }

        static void ListPages(string firstPageUrl, string nextPageSelector, string linksSelector, string filenameSelector, Action<ISearchContext> actionOnPage = null)
        {
            var currentPage = GetPage(firstPageUrl);

            while (currentPage != null)
            {
                actionOnPage?.Invoke(currentPage);

                EnqueueLinks(linksSelector, currentPage, filenameSelector);
                currentPage = currentPage.GetNextPage(nextPageSelector);
            }
        }

        static void ListPagesInContainer(string nextPageSelector, string linksSelector, string filenameSelector)
        {
            while (!isContainersFindingFinished || !containerLinks.IsEmpty)
            {
                if (containerLinks.TryDequeue(out var firstPageUrl))
                {
                    ListPages(firstPageUrl.ToString(), nextPageSelector, linksSelector, filenameSelector);
                }
            }
        }

        static async Task FindLinks(CommandLineParams args)
        {
            var threads = new List<Task>();

            for (int i = 0; i < args.DownloadThreadsCount; i++)
            {
                threads.Add(Task.Run(() => ListPagesInContainer(args.NextPageInContainerSeclector, args.LinkSelector, args.FileNameSelector)));
            }

            ListPages(args.URI.ToString(), args.NextPageContainerSelector, args.LinkSelector, args.FileNameSelector,
                page => DoWithLinks(args.ContainerSelector, page, "href",
                containerLink => containerLinks.Enqueue(new Uri(containerLink))));

            isContainersFindingFinished = true;

            await Task.WhenAll(threads);

            isFinished = true;
        }
    }
}
