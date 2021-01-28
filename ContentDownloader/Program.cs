using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using System.Collections.Generic;
using System.Threading;

namespace ContentDownloader
{
    static class Program
    {
        static ImageDownloader downloader;
        static LinksFinder linksFinder;
        static DriverPool driverPool;
        static bool IsExecuting => !linksFinder.IsFinished || downloader.Downloaded != linksFinder.TotalLinks;

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

            AuthParams auth = null;

            if(args.AuthUrl != null)
            {
                auth = new AuthParams { AuthUrl = args.AuthUrl, SubmitSelector = args.SubmitSelector, LoginSelector = args.LoginSelector, PasswordSelector = args.PasswordSelector };
            }

            driverPool = new DriverPool(args.DownloadThreadsCount, auth);
            downloader = new ImageDownloader(args.FileNameSegments, args.Output);
            linksFinder = new LinksFinder(downloader, driverPool);

            var threads = new List<Task>();

            threads.Add(Task.Run(Stats));
            threads.Add(Task.Run(async () => await linksFinder.FindLinks(args)));

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
                Console.WriteLine($"Downloaded {downloader.Downloaded}/{linksFinder.TotalLinks}");
                Console.WriteLine($"Drivers in use: {driverPool.InUse}");
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
            }
        }
    }
}
