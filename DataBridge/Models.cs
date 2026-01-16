namespace DataBridge
{
    public class GameInfo()
    {
        public int GameIndex { get; set; } // 불변
        public int SteamAppID { get; set; } // 불변
        public string Title { get; set; } // 불변
        public string Developer { get; set; } 
        public string Publisher { get; set; }
        public string Genre { get; set; }
        public int ReleaseDate { get; set; } // ex. 2024년1월15일 => 20240115
        public bool IsSteamVerified { get; set; }
        public bool IsManuallyModified { get; set; }
}

    public class GameLabel()
    {
        // 1. 짧은 이름은 고유해야 함
        // 2. 짧은 이름은 최소 8자부터 생성
        // 3. 짤은 이름은 공백없이 생성
        // 4. 정식 이름을 가지고 앞의 8자리로 생성
        // 5. 생성시 겹친다면, 뒤에 한자리씩 추가해서 겹치지 않을때까지 추가해서 생성
        public string ShortName { get; set; } // 불변
        public string Title { get; set; } // 불변
        public int GameIndex { get; set; } // 불변
        public int SteamAppID { get; set; } // 불변
    }
}
