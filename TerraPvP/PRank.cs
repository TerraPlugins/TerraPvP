namespace TerraPvP
{
    public class PRank
    {
        public string Name { get; set; }
        public int UserID { get; set; }
        public int MMR { get; set; }
        public string Rank { get; set; }

        public PRank(int userId, string name, int mmr, string rank)
        {
            Name = name;
            UserID = userId;
            MMR = mmr;
            Rank = rank;
        }

        public void updateRank(int mmr)
        {
            MMR = mmr;
        }

        public void updateRank(int mmr, string rank)
        {
            MMR = mmr;
            Rank = rank;
        }
    }
}
