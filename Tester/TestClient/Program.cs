using Grpc.Net.Client;
using DataBridge.Search;

namespace TestClient
{
    internal class Program
    {
        private const string ServerAddress = "http://localhost:8001";

        static async Task Main(string[] args)
        {
            // 1. gRPC 채널 생성
            using var channel = GrpcChannel.ForAddress(ServerAddress);
            var client = new SearchService.SearchServiceClient(channel);

            Console.WriteLine($"[Client] 서버({ServerAddress})에 연결 준비 완료.");
            Console.WriteLine("[사용법] !검색어(얕은검색), @검색어(깊은검색), #인덱스(상세정보)");

            // 2. 무한 루프
            while (true)
            {
                Console.WriteLine(); // 줄바꿈
                Console.Write("입력> ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input)) continue;

                char prefix = input[0]; // 첫 글자 (!, @, #)
                string query = input.Substring(1).Trim(); // 나머지 내용

                try
                {
                    switch (prefix)
                    {
                        case '!': // 얕은 탐색 (Autocomplete)
                            await TestAutocomplete(client, query);
                            break;

                        case '@': // 깊은 탐색 (Full Search)
                            await TestFullSearch(client, query);
                            break;

                        case '#': // 상세 정보 조회 (Detail)
                            if (int.TryParse(query, out int index))
                            {
                                await TestGameDetail(client, index);
                            }
                            else
                            {
                                Console.WriteLine("[오류] 상세 정보 조회는 숫자(GameIndex)만 가능합니다.");
                            }
                            break;

                        default:
                            Console.WriteLine("[알림] 명령어가 올바르지 않습니다. (!, @, # 중 하나로 시작하세요)");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[예외 발생] {ex.Message}");
                    Console.WriteLine("서버가 켜져 있는지, 포트가 맞는지 확인해주세요.");
                }
            }
        }

        // --- 기능별 테스트 함수 ---

        // 1. 얕은 탐색 요청
        private static async Task TestAutocomplete(SearchService.SearchServiceClient client, string query)
        {
            Console.WriteLine($"[요청] 얕은 탐색(Autocomplete): {query}");

            var request = new SearchQueryRequest { Query = query };
            var response = await client.GetAutocompleteAsync(request);

            Console.WriteLine($"[응답] 결과 갯수: {response.Results.Count}");
            foreach (var item in response.Results)
            {
                Console.WriteLine($" - [{item.GameIndex}] {item.Title} (가중치: {item.Weight}) / Key: {item.SearchName}");
            }
        }

        // 2. 깊은 탐색 요청
        private static async Task TestFullSearch(SearchService.SearchServiceClient client, string query)
        {
            Console.WriteLine($"[요청] 깊은 탐색(FullSearch): {query}");

            var request = new SearchQueryRequest { Query = query };
            var response = await client.GetFullSearchAsync(request);

            Console.WriteLine($"[응답] 결과 갯수: {response.Results.Count}");
            foreach (var item in response.Results)
            {
                Console.WriteLine($" - [{item.GameIndex}] {item.Title} (AppID: {item.SteamAppId})");
            }
        }

        // 3. 상세 정보 요청
        private static async Task TestGameDetail(SearchService.SearchServiceClient client, int index)
        {
            Console.WriteLine($"[요청] 상세 정보(Detail): Index {index}");

            var request = new GameIndexRequest { GameIndex = index };
            var response = await client.GetGameDetailAsync(request);

            if (response.IsFound)
            {
                Console.WriteLine("--- 게임 상세 정보 ---");
                Console.WriteLine($" 제목: {response.Title}");
                Console.WriteLine($" 개발사: {response.Developer}");
                Console.WriteLine($" 배급사: {response.Publisher}");
                Console.WriteLine($" 장르: {response.Genre}");
                Console.WriteLine($" 스팀ID: {response.SteamAppId}");
                Console.WriteLine($" 스팀검증: {(response.IsSteamVerified ? "완료" : "미완료")}");
                Console.WriteLine("----------------------");
            }
            else
            {
                Console.WriteLine("[응답] 해당 인덱스의 게임 정보를 찾을 수 없습니다.");
            }
        }
    }
}