using DataBridge.Search;
using Microsoft.Extensions.Caching.Memory;
using MySql.Data.MySqlClient;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json.Nodes;

namespace DataBridge.Manager
{
    public class SearchManager
    {
        private ConcurrentDictionary<string, GameLabel> _labelCache = new();
        private readonly PruningTrie _trieEngine = new();
        private readonly IMemoryCache _infoCache;
        private string connectionString = Definition.CONNECTION_STRING;
        private static readonly HttpClient _httpClient = new HttpClient();

        public SearchManager(IMemoryCache memoryCache)
        {
            _infoCache = memoryCache;
        }

        /// <summary> 데이터 초기 로딩 (리스트 + 트라이 동시 구축) </summary>
        public async Task LoadGameData()
        {
            Console.WriteLine("[Manager] 데이터 로딩 시작...");

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // 환경변수 테이블 이름 사용
                string queryLabel = $"SELECT SearchName, Title, GameIndex, SteamAppID, Weight FROM {Definition.LABEL_TABLE_NAME}";

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
                            SteamAppID = reader.GetInt32("SteamAppID"),
                            Weight = reader.GetInt32("Weight")
                        };

                        // 1. 리스트 캐시에 추가
                        _labelCache.TryAdd(key, label);

                        // 2. 트라이 엔진에 추가
                        _trieEngine.Insert(label);
                    }
                }
            }
            Console.WriteLine($"[Manager] 로딩 완료! (Total: {_labelCache.Count}개)");
        }

        /// <summary> 얕은 탐색 (Autocomplete) -> 트라이 사용 </summary>
        public List<GameLabel> Autocomplete(string query)
        {
            return _trieEngine.Autocomplete(query);
        }

        /// <summary> 깊은 탐색 (Full Search) -> 리스트 전수조사 </summary>
        public List<GameLabel> FullSearch(string query)
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

            // 가중치 순 정렬
            results.Sort((a, b) => b.Weight.CompareTo(a.Weight));

            return results;
        }

        /// <summary> 인덱스 조회 (라벨 캐시에서 찾기) </summary>
        public GameLabel? GetLabelByIndex(int index)
        {
            return _labelCache.Values.FirstOrDefault(x => x.GameIndex == index);
        }

        /// <summary> 게임 정보 가져오기 (캐시 -> DB -> 스팀(필요시)) </summary>
        public async Task<GameInfo?> GetGameInfoAsync(int gameIndex)
        {
            string cacheKey = $"GameInfo_{gameIndex}";

            if (_infoCache.TryGetValue(cacheKey, out GameInfo? info))
                return info;

            info = await FetchGameInfoFromDbOrSteam(gameIndex);

            if (info != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions()
                                                    .SetSlidingExpiration(TimeSpan.FromMinutes(10));
                _infoCache.Set(cacheKey, info, cacheOptions);
            }

            return info;
        }

        // DB 조회 및 스팀 업데이트
        private async Task<GameInfo?> FetchGameInfoFromDbOrSteam(int index)
        {
            GameInfo? info = null;

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();

                string query = $"SELECT * FROM {Definition.INFO_TABLE_NAME} WHERE GameIndex = @idx LIMIT 1";
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

                if (info == null) return null;

                // 스팀 검증이 안 된 경우 -> 스팀 API 호출
                if (!info.IsSteamVerified && !info.IsManuallyModified)
                {
                    Console.WriteLine($"[Steam] 정보 갱신 시도 (AppID: {info.SteamAppID})");

                    // 업데이트 성공 여부뿐만 아니라 '가중치(리뷰수)'도 받아옴
                    var result = await UpdateInfoFromSteamApi(info);

                    if (result.Success)
                    {
                        // 1. 상세 정보 테이블(tb_GameInfo) 업데이트
                        string updateInfoQuery = $@"
                            UPDATE {Definition.INFO_TABLE_NAME} 
                            SET Developer = @dev, Publisher = @pub, Genre = @genre, ReleaseDate = @date, IsSteamVerified = 1 
                            WHERE GameIndex = @idx";

                        using (var cmd = new MySqlCommand(updateInfoQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@dev", info.Developer);
                            cmd.Parameters.AddWithValue("@pub", info.Publisher);
                            cmd.Parameters.AddWithValue("@genre", info.Genre);
                            cmd.Parameters.AddWithValue("@date", info.ReleaseDate);
                            cmd.Parameters.AddWithValue("@idx", info.GameIndex);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 2. 라벨 테이블(tb_GameLabel)의 가중치(Weight) 업데이트
                        // 스팀 리뷰 수(recommendations)를 가중치로 사용
                        if (result.NewWeight > 0)
                        {
                            string updateLabelQuery = $"UPDATE {Definition.LABEL_TABLE_NAME} SET Weight = @weight WHERE GameIndex = @idx";
                            using (var cmd = new MySqlCommand(updateLabelQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@weight", result.NewWeight);
                                cmd.Parameters.AddWithValue("@idx", info.GameIndex);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            Console.WriteLine($"[DB] 가중치 업데이트 완료: {result.NewWeight}점");

                            // 3. 메모리 캐시(_labelCache)도 즉시 갱신 (서버 재시작 없이 반영되게)
                            var label = GetLabelByIndex(info.GameIndex);
                            if (label != null)
                            {
                                label.Weight = result.NewWeight;
                            }
                        }

                        Console.WriteLine($"[DB] 스팀 최신 정보로 업데이트 완료! ({info.Title})");
                    }
                }
            }

            return info;
        }

        // 스팀 상점 API 호출 로직 (리턴타입 변경: bool -> (bool, int))
        private async Task<(bool Success, int NewWeight)> UpdateInfoFromSteamApi(GameInfo info)
        {
            try
            {
                string url = $"https://store.steampowered.com/api/appdetails?appids={info.SteamAppID}&l=koreana&key={Definition.STEAM_API_KEY}";
                string jsonString = await _httpClient.GetStringAsync(url);

                var root = JsonNode.Parse(jsonString);
                var appData = root?[info.SteamAppID.ToString()];

                if (appData != null && (bool?)appData["success"] == true)
                {
                    var data = appData["data"];

                    if (data?["developers"] is JsonArray devs)
                        info.Developer = string.Join(", ", devs);

                    if (data?["publishers"] is JsonArray pubs)
                        info.Publisher = string.Join(", ", pubs);

                    if (data?["genres"] is JsonArray genres)
                    {
                        var genreList = genres.Select(g => g?["description"]?.ToString()).Where(s => s != null);
                        info.Genre = string.Join(", ", genreList);
                    }

                    info.ReleaseDate = 0;
                    info.IsSteamVerified = true;

                    // 추천 수(recommendations) 파싱 -> 가중치로 사용
                    int weight = 0;
                    if (data?["recommendations"]?["total"] != null)
                    {
                        weight = (int)data["recommendations"]["total"];
                    }

                    return (true, weight); // 성공 + 가중치 반환
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Steam Error] API 호출 실패: {ex.Message}");
            }
            return (false, 0);
        }
    }
}