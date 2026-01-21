namespace SteamCrawler
{
    public static class Definition
    {
        // 실행 파일 위치(bin\Debug\net8.0\)를 기준으로 3단계 위로 올라가 Data 폴더 지정
        // Path.GetFullPath를 사용해 깔끔한 절대 경로로 변환
        public static readonly string SAVE_DIRECTORY = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Data\"));

        public static readonly string HISTORY_FILE = Path.Combine(SAVE_DIRECTORY, "History.txt");
        public static readonly string NEW_ARRIVAL_FILE = Path.Combine(SAVE_DIRECTORY, "NewArrival.txt");

        public const string STEAM_API_BASE_URL = "https://api.steampowered.com/IStoreService/GetAppList/v1/";
        public static readonly string STEAM_API_KEY = DotNetEnvHelper.GetEnv("STEAM_API_KEY");

        public static readonly string DB_SCHEMA_NAME = DotNetEnvHelper.GetEnv("DB_SCHEMA_NAME");
        public static readonly string DB_USERNAME = DotNetEnvHelper.GetEnv("DB_USERNAME");
        public static readonly string DB_PASSWORD = DotNetEnvHelper.GetEnv("DB_PASSWORD");

        public static readonly string INFO_TABLE_NAME = DotNetEnvHelper.GetEnv("INFO_TABLE_NAME");
        public static readonly string LABEL_TABLE_NAME = DotNetEnvHelper.GetEnv("LABEL_TABLE_NAME");

        public static readonly string CONNECTION_STRING = $"Server=127.0.0.1;Database={DB_SCHEMA_NAME};Uid={DB_USERNAME};Pwd={DB_PASSWORD};";

        public const string DB_FIELD_GAMEINDEX = "GameIndex";
        public const string DB_FIELD_STEAMID = "SteamAppID";
        public const string DB_FIELD_TITLE = "Title";
        public const string DB_FIELD_SEARCHNAME = "SearchName";
    }
}