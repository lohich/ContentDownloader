using System;
using CommandLine;

namespace ContentDownloader
{
    internal class CommandLineParams
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

        [Option("containerPath", HelpText = "Path in container that shoud be passed before finding links")]
        public string PathInContainer { get; set; }

        [Option("authUrl", HelpText = "Url for auth page")]
        public string AuthUrl { get; set; }

        [Option("authLogin", HelpText = "Login selector and login in format \"selector;login\"")]
        public string LoginSelector { get; set; }

        [Option("authPassword", HelpText = "Password selector and password in format \"selector;password\"")]
        public string PasswordSelector { get; set; }

        [Option("authSubmit", HelpText = "Submit button selector")]
        public string SubmitSelector { get; set; }
    }
}
