namespace TerraPvP
{
    public class PVPDuel
    {
        public PRank User1 { get; set; }
        public PRank User2 { get; set; }

        public PVPDuel(PRank user1, PRank user2)
        {
            User1 = user1;
            User2 = user2;
        }
    }
}
