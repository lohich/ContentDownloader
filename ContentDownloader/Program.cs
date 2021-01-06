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

namespace ContentDownloader
{
    static class Program
    {
        static bool isFinished;
        static ConcurrentQueue<(string link, string fileName)> downloadLinks = new ConcurrentQueue<(string,string)>();

        static async Task Main(string[] args)
        {
            CommandLineParams clp = null;

            Parser.Default.ParseArguments<CommandLineParams>(args)
                .WithParsed(p => clp = p);

            DriverPool.Capacity = clp.DownloadThreadsCount;
            var threads = new Task[clp.DownloadThreadsCount];
            for(int i = 0; i < clp.DownloadThreadsCount; i++)
            {
                threads[i] = Task.Run(async () => await Download(clp.Output));
            }

            Task.Run(() => FindLinks(clp));

            await Task.WhenAll(threads);

            Console.WriteLine("Finished!");
            Console.ReadKey();
        }

        static async Task Download(string path)
        {
            while (!isFinished || !downloadLinks.IsEmpty)
            {
                if (downloadLinks.TryDequeue(out var info))
                {
                    var fileName = info.fileName;
                    var url = info.link;

                    using var client = new HttpClient();
                    var bmp = new Bitmap(await client.GetStreamAsync(url));
                    if (fileName == null)
                    {
                        fileName = Path.Combine(path, url.Substring(url.LastIndexOf('/') + 1));
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
                }
            }
        }

        static IEnumerable<string> GetElementsAttributesByXpath(string xpath, string attribute, ISearchContext element)
        {
            if (xpath == null)
            {
                return null;
            }

            var result = element.FindElements(By.XPath(xpath)).Select(x => x.GetAttribute(attribute));

            return result;
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

        static void FindLinks(CommandLineParams args)
        {
            var currentPage = GetNextPage(args.URI.ToString());

            while (currentPage != null)
            {
                var containerLinks = GetElementsAttributesByXpath(args.ContainerSelector, "href", currentPage);

                containerLinks?.AsParallel().ForAll(containerLink =>
                {
                    var currentContainerPage = GetNextPage(containerLink);

                    while (currentContainerPage != null)
                    {
                        var links = GetElementsAttributesByXpath(args.LinkSelector, "href", currentContainerPage);

                        links?.AsParallel().ForAll(link =>
                        {
                            var fileName = args.FileNameSelector == null ? null : currentContainerPage.FindElement(By.XPath(args.FileNameSelector)).Text; 
                            downloadLinks.Enqueue((link, fileName));
                        });

                        currentContainerPage = currentContainerPage.GetNextPage(args.NextPageInContainerSeclector);
                    }
                });

                currentPage = currentPage.GetNextPage(args.NextPageContainerSelector);
            }

            isFinished = true;
        }
    }
}
