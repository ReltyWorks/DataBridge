using DataBridge.Utils;

namespace DataBridge
{
    public static class Definition
    {
        public static readonly string DB_SCHEMA_NAME = DotNetEnvHelper.GetEnv("DB_SCHEMA_NAME");
        public static readonly string DB_USERNAME = DotNetEnvHelper.GetEnv("DB_USERNAME");
        public static readonly string DB_PASSWORD = DotNetEnvHelper.GetEnv("DB_PASSWORD");

        public static readonly string CONNECTION_STRING = $"Server=127.0.0.1;Database={DB_SCHEMA_NAME};Uid={DB_USERNAME};Pwd={DB_PASSWORD};";

        public const int TRIE_MAX_DEPTH = 8;  // 8글자 넘어가면 더 이상 트리 안 탐 (메모리 절약)
        public const int TRIE_MAX_ITEMS = 5; // 자동완성 갯수
    }
}
