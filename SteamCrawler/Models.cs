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

    public class TempGameData
    {
        public int GameIndex { get; set; }    // 우리 DB용 고유 번호
        public int SteamID { get; set; }      // 스팀 ID
        public string Title { get; set; }     // 게임 제목
        public string ShortName { get; set; } // 생성된 8자리 고유 이름 (키)
    }
}