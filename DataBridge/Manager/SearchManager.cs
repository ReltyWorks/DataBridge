using MySql.Data.MySqlClient;
using System.Collections.Concurrent;
using System.Data;

namespace DataBridge.Manager
{
    public class SearchManager
    {
        // 1. 검색용 라벨 캐시 (서버 켜지면 전부 로딩)
        // Key: search_name (ex: pubg00) / Value: 나머지 정보
        private ConcurrentDictionary<string, GameLabel> _labelCache = new();

        // 2. 상세 정보 캐시 (필요할 때 로딩, 일단은 딕셔너리로 구현)
        // Key: game_index / Value: 상세 정보
        private ConcurrentDictionary<int, GameInfo> _infoCache = new();

        // DB 접속 문자열 (프로그램 시작 시 한 번만 로딩하므로 직접 사용)
        private string connectionString = $"Server=localhost;Database={Definition.DB_SCHEMA_NAME};Uid={Definition.DB_USERNAME};Pwd={Definition.DB_PASSWORD};";

        public SearchManager()
        {
        }

        // [기능 1] 데이터 초기화 (서버 켜질 때 호출)
        public async Task LoadGameData()
        {
            Console.WriteLine("[Manager] 데이터 로딩 시작...");

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // 1. GameLabel 로딩 (전체)
                string queryLabel = $"SELECT SearchName, Title, GameIndex, SteamAppID FROM tb_GameLabel";
                using (var cmd = new MySqlCommand(queryLabel, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string key = reader.GetString("SearchName");
                        var label = new GameLabel
                        {
                            Title = reader.GetString("Title"),
                            GameIndex = reader.GetInt32("GameIndex"),
                            SteamAppID = reader.GetInt32("SteamAppID")
                        };
                        _labelCache.TryAdd(key, label);
                    }
                }

                // 2. GameInfo 로딩 (일단 전체 로딩 / 나중에 LRU 등으로 교체 가능)
                // 주의: MARS(Multiple Active Result Sets) 설정이 없다면 Reader를 닫고 다시 열어야 함.
                // 여기서는 안전하게 별도 커맨드로 진행
                string queryInfo = $"SELECT GameIndex, SteamAppID, Title, Developer, Publisher, Genre, ReleaseDate, IsSteamVerified, IsManuallyModified FROM tb_GameInfo";
                using (var cmd = new MySqlCommand(queryInfo, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int key = reader.GetInt32("GameIndex");
                        var info = new GameInfo
                        {
                            SteamAppID = reader.GetInt32("SteamAppID"),
                            Title = reader.GetString("Title"),
                            // Null 체크 필요 (DB에 Null 허용 컬럼들)
                            Developer = reader.IsDBNull(reader.GetOrdinal("Developer")) ? "" : reader.GetString("Developer"),
                            Publisher = reader.IsDBNull(reader.GetOrdinal("Publisher")) ? "" : reader.GetString("Publisher"),
                            Genre = reader.IsDBNull(reader.GetOrdinal("Genre")) ? "" : reader.GetString("Genre"),
                            ReleaseDate = reader.IsDBNull(reader.GetOrdinal("ReleaseDate")) ? 0 : reader.GetInt32("ReleaseDate"),
                            IsSteamVerified = reader.GetBoolean("IsSteamVerified"),
                            IsManuallyModified = reader.GetBoolean("IsManuallyModified")
                        };
                        _infoCache.TryAdd(key, info);
                    }
                }
            }

            Console.WriteLine($"[Manager] 로딩 완료! (Label: {_labelCache.Count}개, Info: {_infoCache.Count}개)");
        }

        // [기능 2] 정밀 검색 로직 (공백=AND, 콤마=OR)
        // 리턴: 검색된 GameLabel 리스트
        public List<GameLabel> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<GameLabel>();

            // 1. 소문자 변환
            string rawQuery = query.ToLower().Trim();

            // 2. 콤마(,)로 1차 분리 -> OR 조건
            // "battle ground, pubg" => ["battle ground", "pubg"]
            string[] orGroups = rawQuery.Split(',');

            // 결과 담을 리스트 (중복 방지를 위해 HashSet 사용 고려 가능하나, 데이터 적으면 List도 OK)
            // 여기서는 순서 유지를 위해 List 사용
            List<GameLabel> results = new List<GameLabel>();

            // 전체 데이터 스캔 (메모리라 빠름)
            // _labelCache는 <SearchName, GameLabel>
            foreach (var kvp in _labelCache)
            {
                string key = kvp.Key;
                GameLabel data = kvp.Value;

                bool isMatch = false;

                // OR 그룹 중 하나라도 만족하면 합격
                foreach (string group in orGroups)
                {
                    if (string.IsNullOrWhiteSpace(group)) continue;

                    // 3. 공백( )으로 2차 분리 -> AND 조건
                    // "battle ground" => ["battle", "ground"]
                    string[] andKeywords = group.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (andKeywords.Length == 0) continue;

                    bool allKeywordsMatched = true;

                    // 모든 키워드가 Key(SearchName)에 포함되어야 함
                    foreach (string keyword in andKeywords)
                    {
                        // 단순 포함 여부 확인 (Contains)
                        if (!key.Contains(keyword))
                        {
                            allKeywordsMatched = false;
                            break;
                        }
                    }

                    // AND 조건을 모두 통과했다면? -> 이 게임은 검색 결과
                    if (allKeywordsMatched)
                    {
                        isMatch = true;
                        break; // 더 이상 다른 OR 그룹 볼 필요 없음
                    }
                }

                if (isMatch)
                {
                    results.Add(data);
                }
            }

            // TODO : 정렬, 짧은 이름 우선, 혹은 정확도순? 일단은 그냥 리턴
            return results;
        }

        // [기능 3] 인덱스로 직접 찾기 (상세 정보 조회용 등)
        public GameLabel? GetLabelByIndex(int index)
        {
            // Values를 뒤지는 건 느리지만, Label 캐시에서 Index 검색은 가끔 쓰일 테니 LINQ 사용
            return _labelCache.Values.FirstOrDefault(x => x.GameIndex == index);
        }

        public GameInfo? GetInfoByIndex(int index)
        {
            _infoCache.TryGetValue(index, out var info);
            return info;
        }
    }
}