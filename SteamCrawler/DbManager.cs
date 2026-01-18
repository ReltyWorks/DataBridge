using MySql.Data.MySqlClient;

namespace SteamCrawler
{
    public static class DbManager
    {
        private static string connectionString = $"Server=localhost;Database={Definition.DB_SCHEMA_NAME};Uid={Definition.DB_USERNAME};Pwd={Definition.DB_PASSWORD};";

        // 1. 마지막 GameIndex 가져오기 (없으면 0 리턴)
        public static int GetLastGameIndex()
        {
            int maxIndex = 0;

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    string query = $"SELECT MAX({Definition.DB_FIELD_GAMEINDEX}) FROM {Definition.INFO_TABLE_NAME}";

                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    object result = cmd.ExecuteScalar();

                    // DB가 비어있으면 DBNull이 나옴 -> 0으로 처리
                    if (result != DBNull.Value && result != null)
                        maxIndex = Convert.ToInt32(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DB Error] 인덱스 조회 실패: {ex.Message}");
                }
            }
            return maxIndex;
        }

        // 2. 이미 존재하는 ShortName 싹 긁어오기 (HashSet으로 리턴)
        public static HashSet<string> GetAllSearchNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    string query = $"SELECT {Definition.DB_FIELD_SEARCHNAME} FROM {Definition.LABEL_TABLE_NAME}";

                    MySqlCommand cmd = new MySqlCommand(query, conn);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            names.Add(reader.GetString(0));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DB Error] 숏네임 로딩 실패: {ex.Message}");
                }
            }
            return names; // 텅 비어있으면 0개짜리 HashSet 리턴
        }

        // 3. 데이터 대량 삽입
        public static void BulkInsertGameData(List<TempGameData> dataList)
        {
            if (dataList.Count == 0)
            {
                Console.WriteLine("[Info] 저장할 데이터가 없습니다.");
                return;
            }

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // 트랜잭션 시작
                MySqlTransaction tr = conn.BeginTransaction();

                try
                {
                    // 명령 객체 미리 생성
                    MySqlCommand cmdInfo = new MySqlCommand();
                    cmdInfo.Connection = conn;
                    cmdInfo.Transaction = tr;

                    MySqlCommand cmdLabel = new MySqlCommand();
                    cmdLabel.Connection = conn;
                    cmdLabel.Transaction = tr;

                    Console.WriteLine($"[DB] 데이터 저장 시작... ({dataList.Count}개)");

                    foreach (var data in dataList)
                    {
                        // 쿼리 1: tb_GameInfo (상세 정보) - 일단 기본값(False)으로 저장
                        cmdInfo.CommandText = $@"
                            INSERT INTO {Definition.INFO_TABLE_NAME} 
                            ({Definition.DB_FIELD_GAMEINDEX}, {Definition.DB_FIELD_STEAMID}, {Definition.DB_FIELD_TITLE}, IsSteamVerified, IsManuallyModified) 
                            VALUES 
                            (@idx, @steamId, @title, 0, 0)";

                        cmdInfo.Parameters.Clear();
                        cmdInfo.Parameters.AddWithValue("@idx", data.GameIndex);
                        cmdInfo.Parameters.AddWithValue("@steamId", data.SteamID);
                        cmdInfo.Parameters.AddWithValue("@title", data.Title);
                        cmdInfo.ExecuteNonQuery();

                        // 쿼리 2: tb_GameLabel (검색용 라벨)
                        cmdLabel.CommandText = $@"
                            INSERT INTO {Definition.LABEL_TABLE_NAME} 
                            ({Definition.DB_FIELD_SEARCHNAME}, {Definition.DB_FIELD_TITLE}, {Definition.DB_FIELD_GAMEINDEX}, {Definition.DB_FIELD_STEAMID}) 
                            VALUES 
                            (@searchName, @title, @idx, @steamId)";

                        cmdLabel.Parameters.Clear();
                        cmdLabel.Parameters.AddWithValue("@searchName", data.SearchName);
                        cmdLabel.Parameters.AddWithValue("@title", data.Title);
                        cmdLabel.Parameters.AddWithValue("@idx", data.GameIndex);
                        cmdLabel.Parameters.AddWithValue("@steamId", data.SteamID);
                        cmdLabel.ExecuteNonQuery();
                    }

                    // 모든 루프가 문제없이 끝나면 진짜 저장!
                    tr.Commit();
                    Console.WriteLine("[Success] 모든 데이터가 DB에 저장되었습니다!");
                }
                catch (Exception ex)
                {
                    // 에러 나면 없던 일로 되돌리기
                    tr.Rollback();
                    Console.WriteLine($"[Error] 저장 중 문제 발생! 롤백되었습니다. : {ex.Message}");
                }
            }
        }
    }
}