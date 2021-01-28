using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;

namespace ContentDownloader
{
    internal class ImageDownloader
    {
        static readonly string invalidRegStr;
        private int downloaded;
        private readonly int fileNameSegments;
        private readonly string path;
        private readonly ObservableConcurrentQueue<Uri> downloadLinks = new ObservableConcurrentQueue<Uri>();

        public ImageDownloader(int fileNameSegments, string path)
        {
            this.fileNameSegments = fileNameSegments;
            this.path = path;
            downloadLinks.ContentChanged += EventHadler;
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

        static string GetValidFileName(string fileName)
        {
            return Regex.Replace(fileName, invalidRegStr, "_");
        }

        private async void Download()
        {
            if (downloadLinks.TryDequeue(out var url))
            {
                using var client = new HttpClient();
                var bmp = new Bitmap(await client.GetStreamAsync(url));

                var fileName = GetValidFileName(HttpUtility.UrlDecode(string.Concat(url.Segments.TakeLast(fileNameSegments))));
                fileName = Path.Combine(path, fileName);

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

        private void EventHadler(object sender, NotifyConcurrentQueueChangedEventArgs<Uri> args)
        {
            if (args.Action == NotifyConcurrentQueueChangedAction.Enqueue)
            {
                Download();
            }
        }
    }
}
