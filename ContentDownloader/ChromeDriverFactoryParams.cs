namespace ContentDownloader
{
    internal class ChromeDriverFactoryParams
    {
        public string AuthUrl { get; set; }
        public string LoginSelector { get; set; }
        public string PasswordSelector { get; set; }
        public string SubmitSelector { get; set; }
        public bool IsChromeWindowsRequired { get; set; }
    }
}
