﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace ContentDownloader
{
    internal class ImageDownloader
    {
        static readonly string invalidRegStr;
        private int downloaded;
        private readonly int fileNameSegments;
        private readonly string outputPath;
        private readonly ObservableConcurrentQueue<Uri> downloadLinks = new ObservableConcurrentQueue<Uri>();
        private readonly SemaphoreSlim semaphore;
        private readonly FileNameConflictPolicy fileNameConflictPolicy;

        public ImageDownloader(int fileNameSegments, string outputPath, int threads, FileNameConflictPolicy fileNameConflictPolicy)
        {
            this.fileNameSegments = fileNameSegments;
            this.outputPath = outputPath;
            downloadLinks.ContentChanged += EventHadler;
            semaphore = new SemaphoreSlim(threads, threads);
            this.fileNameConflictPolicy = fileNameConflictPolicy;
        }

        static ImageDownloader()
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
        }

        public int Downloaded => downloaded;

        public void Download(Uri url)
        {
            downloadLinks.Enqueue(url);
        }

        private static string GetValidFileName(string fileName)
        {
            return Regex.Replace(fileName, invalidRegStr, "_");
        }

        private async Task DownloadNext()
        {
            semaphore.Wait();

            if (downloadLinks.TryDequeue(out var url))
            {
                var fileName = GetValidFileName(HttpUtility.UrlDecode(string.Concat(url.Segments.TakeLast(fileNameSegments))));
                fileName = Path.Combine(outputPath, fileName);

                if (fileNameConflictPolicy == FileNameConflictPolicy.Ignore && File.Exists(fileName))
                {
                    return;
                }

                using var client = new HttpClient();
                using var stream = await client.GetStreamAsync(url);

                var tmp = Path.Combine(outputPath, Path.GetRandomFileName());

                using var file = File.Create(tmp);

                stream.CopyTo(file);
                file.Close();

                if (fileNameConflictPolicy == FileNameConflictPolicy.Rename && File.Exists(fileName))
                {
                    lock (this)
                    {
                        var imageExtension = Path.GetExtension(fileName);
                        var mask = fileName.Insert(fileName.IndexOf(imageExtension), "({0})");
                        int index = 1;
                        while (File.Exists(fileName))
                        {
                            fileName = string.Format(mask, index);
                            index++;
                        }
                    }
                }

                File.Move(tmp, fileName, true);
                Interlocked.Increment(ref downloaded);
            }

            semaphore.Release();
        }

        private void EventHadler(object sender, NotifyConcurrentQueueChangedEventArgs<Uri> args)
        {
            if (args.Action == NotifyConcurrentQueueChangedAction.Enqueue)
            {
                Task.Run(DownloadNext);
            }
        }
    }
}
