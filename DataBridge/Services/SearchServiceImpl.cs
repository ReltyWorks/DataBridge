using Grpc.Core;
using DataBridge.Search;
using DataBridge.Manager;

namespace DataBridge.Services
{
    public class SearchServiceImpl : SearchService.SearchServiceBase
    {
        private readonly SearchManager _searchManager;

        // 생성자 주입 (Program.cs에서 등록한 싱글톤이 들어옴)
        public SearchServiceImpl(SearchManager searchManager)
        {
            _searchManager = searchManager;
        }

        // 1. 얕은 탐색 (SearchName -> Label)
        // 정밀 검색 로직(AND/OR)을 거친 후, 가장 첫 번째 결과를 반환
        public override Task<GameLabelResponse> GetGameBySearchName(SearchNameRequest request, ServerCallContext context)
        {
            var response = new GameLabelResponse { IsFound = false };

            // 매니저에게 검색 위임 (메모리 조회라 엄청 빠름)
            List<GameLabel> results = _searchManager.Search(request.SearchName);

            // 결과가 하나라도 있으면 첫 번째꺼 리턴
            var firstMatch = results.FirstOrDefault();

            if (firstMatch != null)
            {
                response.IsFound = true;
                // Proto에는 SearchName 필드가 있으므로, 
                // 원본 데이터를 줄지, 아니면 요청한 키워드를 줄지 결정해야 하는데
                // 보통은 '찾아낸 데이터의 제목' 등을 주는 게 맞음.
                // 여기서는 GameLabel 모델엔 SearchName(Key)이 없으므로, Title이나 ID 위주로 채움

                // ※ 중요: Proto의 'search_name' 필드에 뭘 채울까?
                // 요청했던 검색어? 아니면 매칭된 내부 키?
                // 일단은 빈값으로 두거나, 필요하면 GameLabel 구조를 다시 조정해야 함.
                // 여기선 Title과 ID가 핵심이니 그것만 확실히 채워줌.
                // TODO : 고민중

                response.SearchName = request.SearchName; // 요청했던 키워드 다시 반환 (에코)
                response.Title = firstMatch.Title;
                response.GameIndex = firstMatch.GameIndex;
                response.SteamAppId = firstMatch.SteamAppID;
            }

            return Task.FromResult(response);
        }

        // 2. 깊은 탐색 (GameIndex -> Label)
        public override Task<GameLabelResponse> GetGameByIndex(GameIndexRequest request, ServerCallContext context)
        {
            var response = new GameLabelResponse { IsFound = false };

            // 매니저에게 ID로 직접 조회 위임
            var match = _searchManager.GetLabelByIndex(request.GameIndex);

            if (match != null)
            {
                response.IsFound = true;
                response.SearchName = ""; // Index로 찾았으니 이름 키는 모름 (필요시 역추적 로직 필요하지만 일단 패스)
                response.Title = match.Title;
                response.GameIndex = match.GameIndex;
                response.SteamAppId = match.SteamAppID;
            }

            return Task.FromResult(response);
        }
    }
}