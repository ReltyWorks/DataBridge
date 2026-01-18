namespace DataBridge
{
    public class GameInfo()
    {
        // Key: GameIndex (int) -> Value: GameInfo
        // public int GameIndex { get; set; }
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
        // Key: SearchName (string) -> Value: GameLabel
        // public string SearchName { get; set; }
        public string Title { get; set; }
        public int GameIndex { get; set; }
        public int SteamAppID { get; set; }
    }
}
