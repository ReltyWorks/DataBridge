namespace SteamCrawler
{
    public class SteamApiRoot
    {
        public SteamApiResponse Response { get; set; }
    }

    public class SteamApiResponse
    {
        public List<SteamApp> Apps { get; set; }
        public uint? Last_appid { get; set; }
        public bool Have_more_results { get; set; }
    }

    public class SteamApp
    {
        public int Appid { get; set; }
        public string Name { get; set; }
    }
}