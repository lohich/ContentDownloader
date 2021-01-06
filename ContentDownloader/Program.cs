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
        static ConcurrentQueue<(Uri link, string fileName)> downloadLinks = new ConcurrentQueue<(Uri, string)>();
        static int totalLinks;
        static int downloaded;
        static bool IsFinished  => !isFinished || !downloadLinks.IsEmpty;

        static async Task Main(string[] args)
        {
            CommandLineParams clp = null;

            Parser.Default.ParseArguments<CommandLineParams>(args)
                .WithParsed(p => clp = p).WithNotParsed(x => Console.ReadKey());

            var threads = new List<Task>();
            threads.Add(Task.Run(Stats));

            DriverPool.Capacity = clp.DownloadThreadsCount;
            
            for (int i = 0; i < clp.DownloadThreadsCount; i++)
            {
                threads.Add(Task.Run(async () => await Download(clp.Output)));
            }

            threads.Add(Task.Run(() => FindLinks(clp)));  

            await Task.WhenAll(threads);

            Console.WriteLine("Finished!");
            Console.ReadKey();
        }

        static void Stats()
        {
            var startTime = DateTime.Now;
            while (IsFinished)
            {
                Console.Clear();
                Console.WriteLine($"Started {startTime}");
                Console.WriteLine($"Downloaded {downloaded}/{totalLinks}");
                Console.WriteLine($"Drivers in use: {DriverPool.InUse}");
                Console.WriteLine($"Time passed: {DateTime.Now - startTime}");
                Thread.Sleep(1000);
            }
        }

        static async Task Download(string path)
        {
            while (IsFinished)
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

        static IWebDriver GetNextPage(string url)
        {
            var driver = DriverPool.GetDriver();
            driver.Navigate().GoToUrl(url);

            return driver;
        }

        static IWebDriver GetNextPage(this IWebDriver driver, string xpath)
        {
            try
            {
                var nextPageLink = driver.FindElement(By.XPath(xpath)).GetAttribute("href");
                DriverPool.Release(driver);
                return GetNextPage(nextPageLink);
            }
            catch (Exception e) when (e is ArgumentNullException || e is NoSuchElementException)
            {
                DriverPool.Release(driver);
                return null;
            }
        }

        static void DoWithLinks(string selector, ISearchContext element, Action<string> whatToDo)
        {
            var links = GetElementsAttributesByXpath(selector, "href;src", element);

            if (links != null)
            {
                Parallel.ForEach(links.Where(x => x != null), whatToDo);
            }
        }

        static void EnqueueLinks(string selector, ISearchContext element, string fileNameSelector)
        {
            DoWithLinks(selector, element, link =>
            {
                var fileName = fileNameSelector == null ? null : element.FindElement(By.XPath(fileNameSelector)).Text;
                downloadLinks.Enqueue((new Uri(link), fileName));
                Interlocked.Increment(ref totalLinks);
            });
        }

        static void FindLinks(CommandLineParams args)
        {
            var currentPage = GetNextPage(args.URI.ToString());

            while (currentPage != null)
            {
                DoWithLinks(args.ContainerSelector, currentPage, containerLink =>
                {
                    var currentContainerPage = GetNextPage(containerLink);

                    while (currentContainerPage != null)
                    {
                        EnqueueLinks(args.LinkSelector, currentContainerPage, args.FileNameSelector);

                        currentContainerPage = currentContainerPage.GetNextPage(args.NextPageInContainerSeclector);
                    }
                });

                EnqueueLinks(args.LinkSelector, currentPage, args.FileNameSelector);
                currentPage = currentPage.GetNextPage(args.NextPageContainerSelector);
            }

            isFinished = true;
        }
    }
}
