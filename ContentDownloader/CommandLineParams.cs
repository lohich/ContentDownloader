using System;
using System.Collections.Generic;
using System.Text;
using CommandLine;

namespace ContentDownloader
{
    class CommandLineParams
    {
        [Option("link", HelpText = "Selector for link with file to be downloaded", Required = true)]
        public string LinkSelector { get; set; }

        [Option("container", HelpText = "Selector for container that contains links")]
        public string ContainerSelector { get; set; }

        [Option("nextContainer", HelpText = "Selector for achieving next page with containers")]
        public string NextPageContainerSelector { get; set; }

        [Option("nextLinks", HelpText = "Selector for achieving next page with links inside container")]
        public string NextPageInContainerSeclector { get; set; }

        [Option("names", HelpText = "Count of url segments that used in output filename", Default = 1)]
        public int FileNameSegments { get; set; }

        [Option("output", HelpText = "Output directory", Required = true)]
        public string Output { get; set; }

        [Option("url", Required = true, HelpText = "Start url")]
        public Uri URI { get; set; }

        [Option("threads", HelpText = "Count of threads for work", Default = 5)]
        public int DownloadThreadsCount { get; set; }
    }
}
