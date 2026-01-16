using Grpc.Core;
using MySql.Data.MySqlClient;
using DataBridge.Search;


namespace DataBridge.Services
{
    public class SearchServiceImpl : SearchService.SearchServiceBase
    {
        // DB 접속 정보
        private static string connectionString = $"Server=localhost;Database={Definition.DB_SCHEMA_NAME};Uid={Definition.DB_USERNAME};Pwd={Definition.DB_PASSWORD};";


        // 1. 얕은 탐색 (ShortName -> Label)
        public override async Task<GameLabelResponse> GetGameByShortName(ShortNameRequest request, ServerCallContext context)
        {
            return await SearchDB("ShortName", request.ShortName);
        }

        // 2. 깊은 탐색 (GameIndex -> Label)
        public override async Task<GameLabelResponse> GetGameByIndex(GameIndexRequest request, ServerCallContext context)
        {
            return await SearchDB("GameIndex", request.GameIndex);
        }

        // DB 조회 공통 함수
        private async Task<GameLabelResponse> SearchDB(string column, object value)
        {
            var response = new GameLabelResponse { IsFound = false };

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();

                string query = $"SELECT ShortName, Title, GameIndex, SteamAppID FROM tb_GameLabel WHERE {column} = @val LIMIT 1";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@val", value);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // 찾았다!
                            response.IsFound = true;
                            response.ShortName = reader.GetString(reader.GetOrdinal("ShortName"));
                            response.Title = reader.GetString(reader.GetOrdinal("Title"));
                            response.GameIndex = reader.GetInt32(reader.GetOrdinal("GameIndex"));
                            response.SteamAppId = reader.GetInt32(reader.GetOrdinal("SteamAppID"));
                        }
                    }
                }
            }
            return response;
        }
    }
}