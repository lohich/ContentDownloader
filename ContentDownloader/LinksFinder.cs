using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;

namespace ContentDownloader
{
    internal class LinksFinder
    {
        private readonly IBrowsingContext context;
        private readonly ImageDownloader downloader;

        private readonly ConcurrentQueue<string> containerLinks = new ConcurrentQueue<string>();
        private string host;

        public LinksFinder(ImageDownloader downloader)
        {
            this.downloader = downloader;

            var config = Configuration.Default.WithDefaultLoader().WithDefaultCookies().WithXPath();
            context = BrowsingContext.New(config);
        }

        public bool IsFinished { get; private set; }
        public bool IsContainersFindingFinished { get; private set; }

        private IEnumerable<string> GetElementsAttributesByXpath(string xpath, string attribute, IParentNode element)
        {
            if (xpath == null)
            {
                return null;
            }

            var attibutes = attribute.Split(';');

            var result = new List<string>();
            foreach (var a in attibutes)
            {
                var values = element.QuerySelectorAll($"*[xpath>'{xpath}']").Select(x => x.GetAttribute(a));
                result.AddRange(values);
            }

            return result.Distinct();
        }

        private Task<IDocument> GetPage(string url)
        {
            return context.OpenAsync(url);
        }

        private string GetEndLink(string link)
        {
            if(!link.Contains("://"))
            {
                return host + link;
            }

            return link;
        }

        private Task<IDocument> GetNextPage(IDocument driver, string xpath)
        {
            var nextPageLink = driver.QuerySelector($"*[xpath>'{xpath}']")?.GetAttribute("href");

            if (nextPageLink == null)
            {
                return Task.FromResult<IDocument>(null);
            }

            return GetPage(GetEndLink( nextPageLink));
        }

        private void DoWithLinks(string selector, IParentNode element, string attributes, Action<string> whatToDo)
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

        private void EnqueueLinks(string selector, IParentNode element)
        {
            DoWithLinks(selector, element, "href;src", link =>
            {
                downloader.Download(new Uri(GetEndLink(link)));
            });
        }

        private async Task ListPages(string firstPageUrl, string nextPageSelector, string linksSelector, Action<IDocument> actionOnPage = null)
        {
            var page = await GetPage(firstPageUrl);

            await ListPages(page, nextPageSelector, linksSelector, actionOnPage);
        }

        private async Task ListPages(IDocument currentPage, string nextPageSelector, string linksSelector, Action<IDocument> actionOnPage = null)
        {
            while (currentPage != null)
            {
                actionOnPage?.Invoke(currentPage);

                EnqueueLinks(linksSelector, currentPage);
                currentPage = await GetNextPage(currentPage, nextPageSelector);
            }
        }

        private async Task ListPagesInContainer(string nextPageSelector, string pathSelector, string linksSelector)
        {
            while (!IsContainersFindingFinished || !containerLinks.IsEmpty)
            {
                if (containerLinks.TryDequeue(out var firstPageUrl))
                {
                    var page = await GetPage(firstPageUrl);

                    if (pathSelector != null)
                    {
                        var path = pathSelector.Split(";");
                        foreach (var item in path)
                        {
                            page = await GetNextPage(page, item);
                        }
                    }
                    await ListPages(page, nextPageSelector, linksSelector);
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
        }

        public async Task FindLinks(CommandLineParams args)
        {
            var threads = new List<Task>();

            if (args.ContainerSelector != null)
            {
                for (int i = 0; i < args.DownloadThreadsCount; i++)
                {
                    threads.Add(Task.Run(async () => await ListPagesInContainer(args.NextPageInContainerSeclector, args.PathInContainer, args.LinkSelector)));
                }
            }

            host = $"{args.URI.Scheme}://{args.URI.Host}";

            await ListPages(args.URI.ToString(), args.NextPageContainerSelector, args.LinkSelector,
                page => DoWithLinks(args.ContainerSelector, page, "href",
                containerLink => containerLinks.Enqueue(GetEndLink(containerLink))));

            IsContainersFindingFinished = true;

            await Task.WhenAll(threads);

            IsFinished = true;
        }
    }
}
