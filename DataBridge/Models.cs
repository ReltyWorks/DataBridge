namespace DataBridge
{
    public class GameInfo()
    {
        public int GameIndex { get; set; } // 불변
        public int SteamAppID { get; set; } // 불변
        public string Title { get; set; }
        public string Developer { get; set; } 
        public string Publisher { get; set; }
        public string Genre { get; set; }
        public int ReleaseDate { get; set; } // ex. 2024년1월15일 => 20240115
        public bool IsSteamVerified { get; set; }
        public bool IsManuallyModified { get; set; }
}

    public class GameLabel()
    {
        public string SearchName { get; set; } // 불변
        public string Title { get; set; }
        public int GameIndex { get; set; } // 불변
        public int SteamAppID { get; set; } // 불변
        public int Weight { get; set; } // 검색 가중치
    }
}
