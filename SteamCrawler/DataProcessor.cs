using System.Text;
using System.Text.RegularExpressions;

namespace SteamCrawler
{
    public static class DataProcessor
    {
        public static List<TempGameData> LoadAndProcessData()
        {
            List<TempGameData> resultList = new List<TempGameData>();

            // 1. 파일 확인
            if (!File.Exists(Definition.NEW_ARRIVAL_FILE))
            {
                Console.WriteLine($"[Error] 파일이 없습니다: {Definition.NEW_ARRIVAL_FILE}");
                return resultList;
            }

            // 2. DB에서 기초 정보 로딩
            int currentIndex = DbManager.GetLastGameIndex() + 1;

            // 기존 SearchName들을 로드
            HashSet<string> existingSearchNames = DbManager.GetAllSearchNames();

            Console.WriteLine($"[Info] 시작 인덱스: {currentIndex}");
            Console.WriteLine($"[Info] 기존 서치네임 개수: {existingSearchNames.Count}개 로드됨.");

            // 3. 파일 읽기
            string[] lines = File.ReadAllLines(Definition.NEW_ARRIVAL_FILE);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split('_', 2);

                if (parts.Length < 2)
                    continue;

                if (int.TryParse(parts[0], out int steamId))
                {
                    string rawTitle = parts[1]; // 원본 제목

                    // ---------------------------------------------------------
                    // [Step A] 검색용 이름(SearchName) 만들기
                    // ---------------------------------------------------------

                    // 1. 정규화, 전각 문자(３, Ａ) 등을 반각 문자(3, A)로 강제 변환
                    string normalizedTitle = rawTitle.Normalize(NormalizationForm.FormKC);

                    // 2. 특수문자 제거 후 바로 '소문자 변환' (.ToLower())
                    // 이제 'PUBG' -> 'pubg', 'NieR' -> 'nier'가 됨
                    string cleanNameBase = Regex.Replace(normalizedTitle, @"[^\p{L}\p{N}]", "").ToLower();

                    // 3. 빈 값 체크
                    if (string.IsNullOrWhiteSpace(cleanNameBase))
                        cleanNameBase = $"unknowngame{steamId}";

                    string safeBaseCheck = cleanNameBase.Length > 30 ? cleanNameBase.Substring(0, 30)
                                                                     : cleanNameBase;

                    // 4. 필터링, 하나만 저장할 게임인지 확인
                    if (GameFilter.ShouldSkip(safeBaseCheck))
                        continue;

                    // 5. SearchName 생성
                    string searchName = GenerateUniqueSearchName(cleanNameBase, existingSearchNames);

                    // ---------------------------------------------------------
                    // [Step B] 상세 정보용 제목(Title) 다듬기
                    // ---------------------------------------------------------

                    // 원본을 쓰되, DB 컬럼(255) 터짐 방지를 위해 250자에서 안전하게 자름
                    string finalTitle = rawTitle;
                    if (finalTitle.Length > 250)
                        finalTitle = finalTitle.Substring(0, 250);

                    // ---------------------------------------------------------
                    // [Step C] 리스트 추가
                    // ---------------------------------------------------------
                    resultList.Add(new TempGameData
                    {
                        GameIndex = currentIndex,
                        SteamID = steamId,
                        Title = finalTitle,      // 원본 (잘림)
                        SearchName = searchName  // 규격화된 키
                    });

                    currentIndex++;
                }
            }

            Console.WriteLine($"[Info] 처리 완료: {resultList.Count}개의 데이터가 준비되었습니다.");
            return resultList;
        }

        // SearchName 생성 로직 (32자 고정 규칙)
        private static string GenerateUniqueSearchName(string baseName, HashSet<string> existingNames)
        {
            // 1. 일단 30자까지만 사용 (뒤에 숫자 2자리 붙여야 하니까)
            string safeBase = baseName.Length > 30 ? baseName.Substring(0, 30) : baseName;

            // 2. 숫자 붙여가며 중복 검사, 무조건 00부터 시작
            int counter = 0;
            string finalName = $"{safeBase}{counter:D2}";

            // 3. HashSet에 있는지 확인
            while (existingNames.Contains(finalName))
            {
                counter++;

                if (counter > 99)
                    throw new InvalidOperationException($"[Error] '{safeBase}' 이름으로 100개 이상의 중복이 발생했습니다.");

                // 숫자가 늘어나면 다시 조합 (PUBG00 -> PUBG01)
                // 혹시라도 99 넘어가면? 3자리 늘어나겠지만(100), 
                // VARCHAR(32)니까 30+3=33 되어서 터질 수 있음.
                // 하지만 현실적으로 이름 겹치는 게 100개 넘을 일은 극히 드묾.
                finalName = $"{safeBase}{counter:D2}";
            }

            // 4. 확정된 이름을 등록
            existingNames.Add(finalName);

            return finalName;
        }
    }
}