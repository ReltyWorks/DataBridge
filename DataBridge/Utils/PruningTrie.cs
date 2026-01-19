namespace DataBridge.Utils
{
    public class PruningTrie
    {
        private class TrieNode
        {
            // 자식 노드들 (다음 글자)
            public Dictionary<char, TrieNode> Children = new();

            // 핵심: 이 노드까지 쳤을 때 보여줄 '추천 리스트' (최대 10개)
            // 여기에 11등부터는 아예 저장도 안 함.
            public List<GameLabel> TopRecommendations = new();
        }

        private TrieNode _root = new TrieNode();
        private const int MAX_DEPTH = 8; // 8글자 제한
        private const int MAX_ITEMS = 10; // 리스트 크기 제한

        // 1. 구축 (Insert)
        public void Insert(GameLabel game, int weight)
        {
            var node = _root;
            string key = game.SearchName; // "pubg..."

            // 8글자까지만 노드 생성
            int limit = Math.Min(key.Length, MAX_DEPTH);

            for (int i = 0; i < limit; i++)
            {
                char c = key[i];
                if (!node.Children.ContainsKey(c))
                    node.Children[c] = new TrieNode();

                node = node.Children[c];

                // Pruning 로직:
                // 이 노드의 추천 리스트에 게임을 넣되,
                // 인기도 순으로 정렬해서 10개가 넘어가면 꼴찌를 삭제
                AddToTopList(node.TopRecommendations, game, weight);
            }
        }

        // 2. 검색
        public List<GameLabel> Autocomplete(string query)
        {
            // 8글자 넘어가면 8글자로 잘라버림
            if (query.Length > MAX_DEPTH)
                query = query.Substring(0, MAX_DEPTH);

            var node = _root;
            foreach (char c in query)
            {
                if (!node.Children.TryGetValue(c, out var nextNode))
                    return new List<GameLabel>(); // 매칭되는 거 없음
                node = nextNode;
            }

            // 해당 노드가 기억하고 있던 Top 10 리턴
            return node.TopRecommendations;
        }

        // 리스트 관리 헬퍼
        private void AddToTopList(List<GameLabel> list, GameLabel game, int weight)
        {
            // 일단 넣고
            list.Add(game);

            // TODO : 가중치 등으로 정렬 후 10개 넘으면 자르기
            // ...
        }
    }
}
