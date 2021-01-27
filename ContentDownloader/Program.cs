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
using System.Reflection;

namespace ContentDownloader
{
    static class Program
    {
        static bool isFinished;
        static bool isContainersFindingFinished;
        static ConcurrentQueue<Uri> downloadLinks = new ConcurrentQueue<Uri>();
        static ConcurrentQueue<Uri> containerLinks = new ConcurrentQueue<Uri>();
        static int totalLinks;
        static int downloaded;
        static DriverPool driverPool;
        static bool IsExecuting => !isFinished || !downloadLinks.IsEmpty;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            await Parser.Default.ParseArguments<CommandLineParams>(args).WithNotParsed(x => Console.ReadKey()).WithParsedAsync(DoWork);
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            driverPool.Dispose();
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
                threads.Add(Task.Run(async () => await Download(args.Output, args.FileNameSegments)));
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

        public static ImageFormat ParseImageFormat(string str)
        {
            if (str.ToLower() == ".jpg")
            {
                return ImageFormat.Jpeg;
            }

            str = str.Remove(0, 1);
            var result = typeof(ImageFormat)
                    .GetProperty(str, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase)
                    .GetValue(str, null) as ImageFormat;

            return result;
        }

        static async Task Download(string path, int fileNameSegments)
        {
            while (IsExecuting)
            {
                if (downloadLinks.TryDequeue(out var url))
                {
                    using var client = new HttpClient();
                    var bmp = new Bitmap(await client.GetStreamAsync(url));

                    var fileName = Path.Combine(path, HttpUtility.UrlDecode(string.Concat(url.Segments.TakeLast(fileNameSegments)).Replace('/', ' ')));

                    var tmp = Path.Combine(path, Path.GetRandomFileName());

                    var imageExtension = Path.GetExtension(fileName);

                    bmp.Save(tmp, ParseImageFormat(imageExtension));

                    if (File.Exists(fileName))
                    {
                        var mask = fileName.Insert(fileName.IndexOf(imageExtension), "({0})");
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

        static void EnqueueLinks(string selector, ISearchContext element)
        {
            DoWithLinks(selector, element, "href;src", link =>
            {
                downloadLinks.Enqueue(new Uri(link));
                Interlocked.Increment(ref totalLinks);
            });
        }

        static void ListPages(string firstPageUrl, string nextPageSelector, string linksSelector, Action<ISearchContext> actionOnPage = null)
        {
            var currentPage = GetPage(firstPageUrl);

            while (currentPage != null)
            {
                actionOnPage?.Invoke(currentPage);

                EnqueueLinks(linksSelector, currentPage);
                currentPage = currentPage.GetNextPage(nextPageSelector);
            }
        }

        static void ListPagesInContainer(string nextPageSelector, string linksSelector)
        {
            while (!isContainersFindingFinished || !containerLinks.IsEmpty)
            {
                if (containerLinks.TryDequeue(out var firstPageUrl))
                {
                    ListPages(firstPageUrl.ToString(), nextPageSelector, linksSelector);
                }
            }
        }

        static async Task FindLinks(CommandLineParams args)
        {
            var threads = new List<Task>();

            for (int i = 0; i < args.DownloadThreadsCount; i++)
            {
                threads.Add(Task.Run(() => ListPagesInContainer(args.NextPageInContainerSeclector, args.LinkSelector)));
            }

            ListPages(args.URI.ToString(), args.NextPageContainerSelector, args.LinkSelector,
                page => DoWithLinks(args.ContainerSelector, page, "href",
                containerLink => containerLinks.Enqueue(new Uri(containerLink))));

            isContainersFindingFinished = true;

            await Task.WhenAll(threads);

            isFinished = true;
        }
    }
}
