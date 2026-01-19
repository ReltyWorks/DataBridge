namespace DataBridge.Search
{
    /// <summary> 가중치 기반 가지치기 트라이 </summary>
    public class PruningTrie
    {
        // 트라이 노드 정의
        private class TrieNode
        {
            // 자식 노드들 (Key: 'a', 'b', '가'...)
            public Dictionary<char, TrieNode> Children = new();

            // 이 노드까지 쳤을 때 보여줄 '추천 리스트' (최대 5개)
            // 6등부터는 메모리에 저장 안 함 (Pruning)
            public List<GameLabel> TopRecommendations = new();
        }

        private readonly TrieNode _root = new TrieNode();


        // 1. 데이터 구축 (서버 켜질 때 호출)
        public void Insert(GameLabel game)
        {
            if (string.IsNullOrWhiteSpace(game.SearchName)) return;

            var node = _root;
            string key = game.SearchName; // 이미 소문자라고 가정

            // 8글자까지만 노드 생성 (그 뒤는 짤림)
            int limit = Math.Min(key.Length, Definition.TRIE_MAX_DEPTH);

            for (int i = 0; i < limit; i++)
            {
                char c = key[i];

                // 자식 노드가 없으면 생성
                if (!node.Children.ContainsKey(c))
                    node.Children[c] = new TrieNode();

                node = node.Children[c];

                // Pruning 로직, 노드를 지나갈 때마다 등록하는데, Top 5 안에 못 들면 버림
                AddToTopList(node.TopRecommendations, game);
            }
        }

        // 2. 검색 (Autocomplete)
        public List<GameLabel> Autocomplete(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<GameLabel>();

            string key = query.ToLower().Trim();

            // 8글자 넘어가면 8글자로 자름
            if (key.Length > Definition.TRIE_MAX_DEPTH)
            {
                key = key.Substring(0, Definition.TRIE_MAX_DEPTH);
            }

            var node = _root;

            // 한 글자씩 따라 내려감
            foreach (char c in key)
            {
                if (!node.Children.TryGetValue(c, out var nextNode))
                {
                    // 끊긴 길이면 추천 결과 없음
                    return new List<GameLabel>();
                }
                node = nextNode;
            }

            // 해당 노드가 기억하고 있던 Top 5 리턴 (이미 정렬되어 있음)
            return node.TopRecommendations.ToList(); // 방어적 복사
        }

        // 리스트 관리 헬퍼 (항상 Top 5 유지 & 가중치 정렬)
        private void AddToTopList(List<GameLabel> list, GameLabel game)
        {
            // 1. 리스트가 아직 꽉 안 찼으면 무조건 추가
            if (list.Count < Definition.TRIE_MAX_ITEMS)
            {
                list.Add(game);
                // 가중치 높은 순으로 정렬 (내림차순)
                list.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            }
            // 2. 리스트가 꽉 찼으면? 꼴찌랑 비교해서 이길 때만 교체
            else
            {
                // 현재 꼴찌의 점수
                int minWeight = list[list.Count - 1].Weight;

                // 새로 들어온 녀석이 꼴찌보다 점수가 높으면?
                if (game.Weight > minWeight)
                {
                    list.RemoveAt(list.Count - 1); // 꼴찌 삭제
                    list.Add(game); // 새 녀석 추가
                    list.Sort((a, b) => b.Weight.CompareTo(a.Weight)); // 다시 정렬
                }
                // 아니면? 그냥 무시 (Pruning)
            }
        }
    }
}