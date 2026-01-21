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

        public async Task LoadGameData()
        {
            Console.WriteLine("[Manager] 데이터 로딩 시작...");

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // 변수 사용 (LABEL_TABLE_NAME)
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

                        _labelCache.TryAdd(key, label);
                        _trieEngine.Insert(label);
                    }
                }
            }
            Console.WriteLine($"[Manager] 로딩 완료! (Total: {_labelCache.Count}개)");
        }

        public List<GameLabel> Autocomplete(string query)
        {
            return _trieEngine.Autocomplete(query);
        }

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

            results.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            return results;
        }

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

        private async Task<GameInfo?> FetchGameInfoFromDbOrSteam(int index)
        {
            GameInfo? info = null;

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // 변수 사용 (INFO_TABLE_NAME)
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

                if (!info.IsSteamVerified && !info.IsManuallyModified)
                {
                    Console.WriteLine($"[Steam] 정보 갱신 시도 (AppID: {info.SteamAppID})");

                    bool isUpdated = await UpdateInfoFromSteamApi(info);

                    if (isUpdated)
                    {
                        // 변수 사용 (INFO_TABLE_NAME)
                        string updateQuery = $@"
                            UPDATE {Definition.INFO_TABLE_NAME} 
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

        private async Task<bool> UpdateInfoFromSteamApi(GameInfo info)
        {
            try
            {
                // 스팀 API 키 적용 (STEAM_API_KEY)
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
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Steam Error] API 호출 실패: {ex.Message}");
            }
            return false;
        }
    }
}