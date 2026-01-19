using Grpc.Core;
using DataBridge.Search;
using DataBridge.Manager;

namespace DataBridge.Services
{
    public class SearchServiceImpl : SearchService.SearchServiceBase
    {
        private readonly SearchManager _searchManager;

        public SearchServiceImpl(SearchManager searchManager)
        {
            _searchManager = searchManager;
        }

        // 1. 얕은 탐색 (자동완성) - Trie 사용
        public override Task<GameListResponse> GetAutocomplete(SearchQueryRequest request, ServerCallContext context)
        {
            var response = new GameListResponse();

            // 매니저의 트라이 엔진 (이미 Top 5 & 8글자 제한 적용됨)
            var results = _searchManager.Autocomplete(request.Query);

            // 결과 리스트를 Proto 메시지로 변환해서 담기
            foreach (var item in results)
            {
                response.Results.Add(new Label
                {
                    SearchName = item.SearchName,
                    Title = item.Title,
                    GameIndex = item.GameIndex,
                    SteamAppId = item.SteamAppID,
                    Weight = item.Weight
                });
            }

            return Task.FromResult(response);
        }

        // 2. 깊은 탐색 (전체 검색) - List 전수조사
        public override Task<GameListResponse> GetFullSearch(SearchQueryRequest request, ServerCallContext context)
        {
            var response = new GameListResponse();

            // 매니저의 리스트 전수조사
            var results = _searchManager.FullSearch(request.Query);

            // 결과 담기
            foreach (var item in results)
            {
                response.Results.Add(new Label
                {
                    SearchName = item.SearchName,
                    Title = item.Title,
                    GameIndex = item.GameIndex,
                    SteamAppId = item.SteamAppID,
                    Weight = item.Weight
                });
            }

            return Task.FromResult(response);
        }

        // 3. 상세 정보 조회 (클릭 시)
        public override async Task<GameInfoResponse> GetGameDetail(GameIndexRequest request, ServerCallContext context)
        {
            var response = new GameInfoResponse { IsFound = false };

            // 매니저에게 상세 정보 요청 (캐시 -> DB -> 스팀)
            var info = await _searchManager.GetGameInfoAsync(request.GameIndex);

            if (info != null)
            {
                response.IsFound = true;
                response.GameIndex = info.GameIndex;
                response.SteamAppId = info.SteamAppID;
                response.Title = info.Title;
                response.Developer = info.Developer;
                response.Publisher = info.Publisher;
                response.Genre = info.Genre;
                response.ReleaseDate = info.ReleaseDate;
                response.IsSteamVerified = info.IsSteamVerified;
            }

            return response;
        }
    }
}