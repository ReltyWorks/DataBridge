namespace SteamCrawler
{
    public static class GameFilter
    {
        // 하나만 남기고 나머지는 다 버릴 '키워드' 목록
        // ex. 'Rocksmith 2014 Edition - Remastered' 게임은 약 1,500개 이상의 DLC를 보유하고 있으며,
        // 모든 DLC의 타이틀이 동일한 긴 문자열로 시작, 이걸 다 저장하는건 비효율적임

        // Key: 검사할 이름 (소문자, 특수문자 제외, 최대 30자)
        // Value: false(아직 안 나옴), true(이미 저장됨 -> 이제부터 스킵)
        private static Dictionary<string, bool> _oneEntryOnlyMap = new()
        {
            { "rocksmith2014editionremastered", false },
            // 원본 - Rocksmith® 2014 Edition - Remastered / 예상 물량: 약 1,570개 이상
            { "jigsawpuzzlepackpixelpuzzlesul", false },
            // 원본 - Pixel Puzzles Ultimate / 예상 물량: 약 400~500개
            { "thelegendofheroestrailsofcolds", false },
            // 원본 - The Legend of Heroes: Trails of Cold Steel (영웅전설: 섬의 궤적) 시리즈
            // 캐릭터 코스튬, 회복 아이템, 배경음악 등을 낱개로 쪼개서 팜예상 물량: 약 수백개 이상
            { "fantasygroundspathfinderrpgpat", false },
            // 원본 - Fantasy Grounds / 예상 물량: 2,000개 이상
        };

        /// <summary> 이 게임을 스킵해야 하나요? (True: 스킵 / False: 저장) </summary>
        public static bool ShouldSkip(string safeBaseName)
        {
            // 1. 관리 대상 목록에 없는 평범한 게임인가? -> 스킵하지 마(False)
            if (!_oneEntryOnlyMap.ContainsKey(safeBaseName))
                return false;

            // 2. 관리 대상이라면, 이미 저장된 적이 있나?
            bool isAlreadySaved = _oneEntryOnlyMap[safeBaseName];

            if (isAlreadySaved)
            {
                // 이미 저장했음 -> 이번 건 스킵해(True)
                return true;
            }
            else
            {
                // 아직 저장 안 함 -> 이번 건 저장하고, 체크 표시(True) 남기기
                _oneEntryOnlyMap[safeBaseName] = true;
                return false; // 스킵하지 마(저장해)
            }
        }
    }
}