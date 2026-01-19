using DataBridge.Utils;

namespace DataBridge
{
    public static class Definition
    {
        public const string DB_SCHEMA_NAME = "DataBridgeDB";
        public static readonly string DB_USERNAME = DotNetEnvHelper.GetEnv("DB_USERNAME");
        public static readonly string DB_PASSWORD = DotNetEnvHelper.GetEnv("DB_PASSWORD");
    }
}
