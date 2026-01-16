namespace SteamCrawler
{
    public static class Definition
    {
        // 실행 파일 위치(bin\Debug\net8.0\)를 기준으로 3단계 위로 올라가 Data 폴더 지정
        // Path.GetFullPath를 사용해 깔끔한 절대 경로로 변환
        public static readonly string SAVE_DIRECTORY = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Data\"));

        public static readonly string HISTORY_FILE = Path.Combine(SAVE_DIRECTORY, "History.txt");
        public static readonly string NEW_ARRIVAL_FILE = Path.Combine(SAVE_DIRECTORY, "NewArrival.txt");

        public const string STEAM_API_KEY = "STEAM_API_KEY";
        public const string STEAM_API_BASE_URL = "https://api.steampowered.com/IStoreService/GetAppList/v1/";
    }
}