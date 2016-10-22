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

        public void updateMMR(int mmr)
        {
            MMR = mmr;
        }

        public void checkRank()
        {
            //if mmr is bigger than x, update rank.. make a rank list config
        }
    }
}
