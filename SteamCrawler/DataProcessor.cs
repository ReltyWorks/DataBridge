using System.Text.RegularExpressions;

namespace SteamCrawler
{
    public static class DataProcessor
    {
        public static List<TempGameData> LoadAndProcessData()
        {
            List<TempGameData> resultList = new List<TempGameData>();

            // 1. 파일이 있는지 확인
            if (!File.Exists(Definition.NEW_ARRIVAL_FILE))
            {
                Console.WriteLine($"[Error] 파일이 없습니다: {Definition.NEW_ARRIVAL_FILE}");
                return resultList;
            }

            // 2. DB에서 기초 정보 로딩 (Start Index, 기존 숏네임들)
            int currentIndex = DbManager.GetLastGameIndex() + 1; // 마지막 번호 + 1부터 시작
            HashSet<string> existingShortNames = DbManager.GetAllShortNames();

            Console.WriteLine($"[Info] 시작 인덱스: {currentIndex}");
            Console.WriteLine($"[Info] 기존 숏네임 개수: {existingShortNames.Count}개 로드됨.");

            // 3. 파일 읽기
            string[] lines = File.ReadAllLines(Definition.NEW_ARRIVAL_FILE);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // 포맷: "10_Counter-Strike" -> '_' 기준으로 2개로만 나눔
                string[] parts = line.Split('_', 2);
                if (parts.Length < 2) continue;

                if (int.TryParse(parts[0], out int steamId))
                {
                    string rawTitle = parts[1];

                    // 한글, 영어, 숫자만 남기고 싹 날려버리기
                    string cleanTitle = Regex.Replace(rawTitle, @"[^a-zA-Z0-9가-힣]", "");

                    // 1. 다 지웠더니 남는 게 없다? (순수 중국어, 일본어, 특수문자 등) -> 갖다버려 (Skip)
                    if (string.IsNullOrWhiteSpace(cleanTitle))
                    {
                        continue;
                    }

                    // 2. 혹시 정제된 것도 255자가 넘으면 자르기 (안전장치)
                    if (cleanTitle.Length > 250)
                    {
                        cleanTitle = cleanTitle.Substring(0, 250); // 미친새끼들
                    }

                    // 3. 숏네임 생성 (정제된 cleanTitle 기반으로 생성)
                    string shortName = GenerateUniqueShortName(cleanTitle, existingShortNames);

                    // 4. 리스트 추가 (제목도 깔끔한 cleanTitle로 저장)
                    resultList.Add(new TempGameData
                    {
                        GameIndex = currentIndex,
                        SteamID = steamId,
                        Title = cleanTitle,
                        ShortName = shortName
                    });

                    currentIndex++;
                }
            }

            Console.WriteLine($"[Info] 처리 완료: {resultList.Count}개의 데이터가 준비되었습니다.");
            return resultList;
        }

        // 숏네임 생성 로직 (핵심)
        private static string GenerateUniqueShortName(string title, HashSet<string> existingNames)
        {
            // 1. 앞 8글자 자르기
            string baseName = title.Length > 8 ? title.Substring(0, 8) : title;

            // 2. 중복 검사 및 번호 붙이기
            string finalName = baseName;
            int count = 1;

            // 3. HashSet에 있는지 확인 (O(1) 속도)
            while (existingNames.Contains(finalName))
            {
                // 겹치면 뒤에 숫자 붙임 (Mooni -> Mooni1 -> Mooni2)
                finalName = $"{baseName}{count}";
                count++;
            }

            // 4. 확정된 이름을 HashSet에 등록 (다음 루프에서 중복 방지)
            existingNames.Add(finalName);

            return finalName;
        }
    }
}