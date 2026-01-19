using Microsoft.Extensions.Caching.Memory;
using MySql.Data.MySqlClient;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json.Nodes;

namespace DataBridge.Manager
{
    public class SearchManager
    {
        // 1. 검색용 라벨 캐시 (서버 켜지면 전부 로딩)
        private ConcurrentDictionary<string, GameLabel> _labelCache = new();

        // 2. 상세 정보 캐시 (슬라이딩 만료 적용)
        private readonly IMemoryCache _infoCache;

        // DB 접속 문자열
        private string connectionString = $"Server=localhost;Database={Definition.DB_SCHEMA_NAME};Uid={Definition.DB_USERNAME};Pwd={Definition.DB_PASSWORD};";

        // 스팀 API 호출용 클라이언트 (재사용)
        private static readonly HttpClient _httpClient = new HttpClient();

        public SearchManager(IMemoryCache memoryCache)
        {
            _infoCache = memoryCache;
        }

        // --------------------------------------------------------------------------
        // [핵심 기능] 게임 정보 가져오기 (캐시 -> DB -> 스팀(필요시))
        // --------------------------------------------------------------------------
        public async Task<GameInfo?> GetGameInfoAsync(int gameIndex)
        {
            string cacheKey = $"GameInfo_{gameIndex}";

            // 1. 캐시 확인 (슬라이딩 만료 자동 적용)
            if (_infoCache.TryGetValue(cacheKey, out GameInfo? info))
            {
                return info;
            }

            // 2. 캐시에 없으면 DB/스팀 조회
            info = await FetchGameInfoFromDbOrSteam(gameIndex);

            // 3. 가져온 정보 캐시에 등록 (10분 슬라이딩)
            if (info != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(10));
                _infoCache.Set(cacheKey, info, cacheOptions);
            }

            return info;
        }

        // --------------------------------------------------------------------------
        // [내부 로직] DB 조회 및 스팀 업데이트
        // --------------------------------------------------------------------------
        private async Task<GameInfo?> FetchGameInfoFromDbOrSteam(int index)
        {
            GameInfo? info = null;

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // 1. 일단 DB에서 가져오기
                string query = "SELECT * FROM tb_GameInfo WHERE GameIndex = @idx LIMIT 1";
                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@idx", index);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            info = new GameInfo
                            {
                                GameIndex = reader.GetInt32("GameIndex"),
                                SteamAppID = reader.GetInt32("SteamAppID"),
                                Title = reader.GetString("Title"),
                                Developer = reader.IsDBNull(reader.GetOrdinal("Developer")) ? "" : reader.GetString("Developer"),
                                Publisher = reader.IsDBNull(reader.GetOrdinal("Publisher")) ? "" : reader.GetString("Publisher"),
                                Genre = reader.IsDBNull(reader.GetOrdinal("Genre")) ? "" : reader.GetString("Genre"),
                                ReleaseDate = reader.IsDBNull(reader.GetOrdinal("ReleaseDate")) ? 0 : reader.GetInt32("ReleaseDate"),
                                IsSteamVerified = reader.GetBoolean("IsSteamVerified"),
                                IsManuallyModified = reader.GetBoolean("IsManuallyModified")
                            };
                        }
                    }
                }

                // 2. DB에 데이터가 없으면? -> NULL 리턴 (라벨은 있는데 인포가 없는 경우라 거의 없음)
                if (info == null) return null;

                // 3. 데이터는 있는데 [스팀 검증]이 안 된 상태라면? (IsSteamVerified == false)
                // -> 그리고 [수동 수정]도 안 된 상태라면? (수동 수정은 덮어쓰면 안 되니까)
                if (!info.IsSteamVerified && !info.IsManuallyModified)
                {
                    Console.WriteLine($"[Steam] 정보 갱신 시도 (AppID: {info.SteamAppID})");

                    // 스팀 API 호출해서 정보 채워넣기
                    bool isUpdated = await UpdateInfoFromSteamApi(info);

                    if (isUpdated)
                    {
                        // 4. 업데이트된 정보를 DB에 다시 저장 (UPDATE)
                        string updateQuery = @"
                            UPDATE tb_GameInfo 
                            SET Developer = @dev, Publisher = @pub, Genre = @genre, ReleaseDate = @date, IsSteamVerified = 1 
                            WHERE GameIndex = @idx";

                        using (var cmd = new MySqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@dev", info.Developer);
                            cmd.Parameters.AddWithValue("@pub", info.Publisher);
                            cmd.Parameters.AddWithValue("@genre", info.Genre);
                            cmd.Parameters.AddWithValue("@date", info.ReleaseDate);
                            cmd.Parameters.AddWithValue("@idx", info.GameIndex);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        Console.WriteLine($"[DB] 스팀 최신 정보로 업데이트 완료! ({info.Title})");
                    }
                }
            }

            return info;
        }

        // 스팀 상점 API 호출 로직 (Store API)
        private async Task<bool> UpdateInfoFromSteamApi(GameInfo info)
        {
            try
            {
                // 한국어(koreana)로 요청
                string url = $"https://store.steampowered.com/api/appdetails?appids={info.SteamAppID}&l=koreana";
                string jsonString = await _httpClient.GetStringAsync(url);

                // JSON 파싱 (동적 구조라 JsonNode 사용)
                var root = JsonNode.Parse(jsonString);
                var appData = root?[info.SteamAppID.ToString()]; // Root["12345"]

                if (appData != null && (bool?)appData["success"] == true)
                {
                    var data = appData["data"];

                    // 개발사 (배열 -> 콤마 문자열)
                    if (data?["developers"] is JsonArray devs)
                        info.Developer = string.Join(", ", devs);

                    // 배급사 (배열 -> 콤마 문자열)
                    if (data?["publishers"] is JsonArray pubs)
                        info.Publisher = string.Join(", ", pubs);

                    // 장르 (배열 -> 콤마 문자열)
                    if (data?["genres"] is JsonArray genres)
                    {
                        var genreList = genres.Select(g => g?["description"]?.ToString()).Where(s => s != null);
                        info.Genre = string.Join(", ", genreList);
                    }

                    // 출시일 (복잡함: "release_date": { "date": "2024년 1월 15일" })
                    // 파싱 귀찮으니 일단 0으로 두거나, 간단한 변환 로직 필요. 
                    // 여기선 스킵하거나 간단히 처리
                    info.ReleaseDate = 0; // 날짜 파싱은 추후 정교화 필요

                    info.IsSteamVerified = true;
                    return true; // 업데이트 성공
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Steam Error] API 호출 실패: {ex.Message}");
            }
            return false;
        }

        // --------------------------------------------------------------------------
        // [기능 1] 라벨 데이터만 초기 로딩 (GameInfo 로딩 삭제됨)
        // --------------------------------------------------------------------------
        public async Task LoadGameData()
        {
            Console.WriteLine("[Manager] 라벨 데이터 로딩 시작...");

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();

                string queryLabel = $"SELECT SearchName, Title, GameIndex, SteamAppID FROM tb_GameLabel";
                using (var cmd = new MySqlCommand(queryLabel, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string key = reader.GetString("SearchName");
                        var label = new GameLabel
                        {
                            SearchName = key,
                            Title = reader.GetString("Title"),
                            GameIndex = reader.GetInt32("GameIndex"),
                            SteamAppID = reader.GetInt32("SteamAppID")
                        };
                        _labelCache.TryAdd(key, label);
                    }
                }
            }
            Console.WriteLine($"[Manager] 로딩 완료! (Label: {_labelCache.Count}개)");
        }

        // [기능 2] 검색 (AND/OR 로직 유지)
        public List<GameLabel> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<GameLabel>();
            string rawQuery = query.ToLower().Trim();
            string[] orGroups = rawQuery.Split(',');

            List<GameLabel> results = new List<GameLabel>();

            foreach (var kvp in _labelCache)
            {
                string key = kvp.Key;
                GameLabel data = kvp.Value;
                bool isMatch = false;

                foreach (string group in orGroups)
                {
                    if (string.IsNullOrWhiteSpace(group)) continue;
                    string[] andKeywords = group.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (andKeywords.Length == 0) continue;

                    bool allKeywordsMatched = true;
                    foreach (string keyword in andKeywords)
                    {
                        if (!key.Contains(keyword))
                        {
                            allKeywordsMatched = false;
                            break;
                        }
                    }

                    if (allKeywordsMatched)
                    {
                        isMatch = true;
                        break;
                    }
                }

                if (isMatch) results.Add(data);
            }
            return results;
        }

        // [기능 3] 인덱스 조회 (라벨 캐시에서 찾기)
        public GameLabel? GetLabelByIndex(int index)
        {
            return _labelCache.Values.FirstOrDefault(x => x.GameIndex == index);
        }

    }
}