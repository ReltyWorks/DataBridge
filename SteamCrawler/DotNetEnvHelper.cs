namespace SteamCrawler
{
    public static class DotNetEnvHelper
    {
        public static string GetEnv(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);

            if (string.IsNullOrWhiteSpace(value))
                throw new Exception($"[.env Error] 환경변수 '{key}'를 찾을 수 없습니다.");

            return value;
        }
    }
}