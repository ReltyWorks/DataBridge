using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SteamCrawler
{
    public static class SteamApiLoader
    {
        // Program.cs에 있던 로직을 그대로 캡슐화
        public static async Task Run()
        {
            Console.WriteLine("[SteamCrawler] 작업을 시작합니다... (IStoreService Ver)");

            // 1. 디렉토리 초기화
            if (!Directory.Exists(Definition.SAVE_DIRECTORY))
            {
                Directory.CreateDirectory(Definition.SAVE_DIRECTORY);
                Console.WriteLine($"[Init] 폴더 생성 완료: {Definition.SAVE_DIRECTORY}");
            }
            else
            {
                Console.WriteLine($"[Init] 저장 경로 확인: {Definition.SAVE_DIRECTORY}");
            }

            Console.WriteLine($"[Mode] Authenticated Mode (Key: {Definition.STEAM_API_KEY.Substring(0, 4)}***)");

            // 2. 기존 히스토리 로드 (중복 방지 핵심 로직)
            HashSet<int> historyIds = new HashSet<int>();
            if (File.Exists(Definition.HISTORY_FILE))
            {
                var lines = File.ReadAllLines(Definition.HISTORY_FILE);
                foreach (var line in lines)
                {
                    int separatorIdx = line.IndexOf('_');
                    if (separatorIdx > 0 && int.TryParse(line.Substring(0, separatorIdx), out int id))
                        historyIds.Add(id);
                }
                Console.WriteLine($"[Load] 기존 히스토리 로드 완료: {historyIds.Count}개");
            }
            else
            {
                Console.WriteLine("[Load] 기존 히스토리 없음 (최초 실행)");
            }

            // 3. 스팀 API 호출 (페이지네이션 포함)
            Console.WriteLine("[Fetch] 스팀 API 요청 시작 (전체 수집 중)...");

            using HttpClient client = new HttpClient();
            List<SteamApp> allCollectedApps = new List<SteamApp>();

            uint? lastAppId = null;
            bool haveMore = true;
            int pageCount = 0;

            try
            {
                while (haveMore)
                {
                    pageCount++;
                    // 형이 작성한 URL 구조 그대로 유지 (max_results 등)
                    string requestUrl = $"{Definition.STEAM_API_BASE_URL}?key={Definition.STEAM_API_KEY}&include_games=true&include_dlc=true&include_software=true&max_results=10000";

                    if (lastAppId.HasValue)
                        requestUrl += $"&last_appid={lastAppId}";

                    string jsonString = await client.GetStringAsync(requestUrl);

                    // 대소문자 무시 옵션 유지
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var root = JsonSerializer.Deserialize<SteamApiRoot>(jsonString, options);

                    if (root?.Response?.Apps == null || root.Response.Apps.Count == 0)
                        break;

                    allCollectedApps.AddRange(root.Response.Apps);
                    lastAppId = root.Response.Last_appid;
                    haveMore = root.Response.Have_more_results;

                    Console.Write($"\r[Fetch] {pageCount}페이지 수신 중... (누적: {allCollectedApps.Count}개)");
                }
                Console.WriteLine();
                Console.WriteLine($"[Fetch] 전체 수집 완료! 총 {allCollectedApps.Count}개");

                // 4. 필터링 및 통계 집계
                List<SteamApp> newGames = new List<SteamApp>();
                int duplicateCount = 0;
                int dummyCount = 0;

                foreach (var app in allCollectedApps)
                {
                    // Case 1: 더미 데이터
                    if (string.IsNullOrWhiteSpace(app.Name))
                    {
                        dummyCount++;
                        continue;
                    }

                    // Case 2: 히스토리 중복 확인
                    if (historyIds.Contains(app.Appid))
                    {
                        duplicateCount++;
                        continue;
                    }

                    // Case 3: 진짜 신규 게임
                    newGames.Add(app);
                }

                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine($"[Result] 분석 결과");
                Console.WriteLine($" - 신규 발견 : {newGames.Count}개 (저장 예정)");
                Console.WriteLine($" - 기존 중복 : {duplicateCount}개 (패스)");
                Console.WriteLine($" - 더미/무명 : {dummyCount}개 (제외됨)");
                Console.WriteLine("--------------------------------------------------");

                // 5. 결과 저장 (NewArrival + History 갱신)
                if (newGames.Count > 0)
                {
                    // NewArrival은 덮어쓰기(false), History는 이어쓰기(true) - 형 로직 그대로
                    using StreamWriter wNew = new StreamWriter(Definition.NEW_ARRIVAL_FILE, false, Encoding.UTF8);

                    foreach (var game in newGames)
                    {
                        string cleanName = game.Name.Replace("\n", "").Replace("\r", "");
                        string line = $"{game.Appid}_{cleanName}";

                        wNew.WriteLine(line);
                    }

                    Console.WriteLine($"[Save] {Definition.NEW_ARRIVAL_FILE} 저장 완료.");
                }
                else
                {
                    Console.WriteLine("[Save] 저장할 신규 게임이 없습니다.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Error] {ex.Message}");
            }
        }
    }
}