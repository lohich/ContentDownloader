using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace ContentDownloader
{
    internal class Program
    {
        static ImageDownloader downloader;
        static LinksFinder linksFinder;
        static DriverFactory driverFactory;
        static bool IsExecuting => !linksFinder.IsFinished || downloader.Downloaded + downloader.Skipped != downloader.TotalLinks;

        static async Task Main(string[] args)
        {
            var result = await new Parser(x => x.CaseInsensitiveEnumValues = true).ParseArguments<CommandLineParams>(args).WithParsedAsync(DoWork);
            if (result.Tag == ParserResultType.NotParsed)
            {
                var help = HelpText.AutoBuild(result);
                Console.WriteLine(help);
            }
        }

        static async Task DoWork(CommandLineParams args)
        {
            var startTime = DateTime.Now;
            if (!Directory.Exists(args.Output))
            {
                Directory.CreateDirectory(args.Output);
            }

            AuthParams auth = null;

            if (args.AuthUrl != null)
            {
                auth = new AuthParams { AuthUrl = args.AuthUrl, SubmitSelector = args.SubmitSelector, LoginSelector = args.LoginSelector, PasswordSelector = args.PasswordSelector };
            }

            driverFactory = new DriverFactory(auth);
            downloader = new ImageDownloader(args.FileNameSegments, args.Output, args.DownloadThreadsCount, args.FileNameConflictPolicy);
            linksFinder = new LinksFinder(downloader, driverFactory);

            var threads = new List<Task>();

            threads.Add(Task.Run(() => Stats(startTime)));
            threads.Add(Task.Run(async () => await linksFinder.FindLinks(args)));

            await Task.WhenAll(threads);

            Stats(startTime);
            Console.WriteLine("Finished!");
        }

        static void Stats(DateTime startTime)
        {
            do
            {
                Console.Clear();
                Console.WriteLine($"Started {startTime}");
                Console.WriteLine($"Downloaded {downloader.Downloaded}/{downloader.TotalLinks} (Skipped {downloader.Skipped})");
                Console.WriteLine($"Time passed: {DateTime.Now - startTime}");
                if (linksFinder.IsContainersFindingFinished)
                {
                    Console.WriteLine("All containers found");
                }
                if (linksFinder.IsFinished)
                {
                    Console.WriteLine("All links found");
                }
                Thread.Sleep(1000);
            } while (IsExecuting);
        }
    }
}
