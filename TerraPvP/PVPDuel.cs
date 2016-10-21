using System;
using Terraria;
using TShockAPI;

namespace TerraPvP
{
    public class PVPDuel
    {
        public PRank User1 { get; set; }
        public PRank User2 { get; set; }
        public PRank Winner { get; set; }
        public PRank Loser { get; set; }
        public bool finished { get; set; }

        public PVPDuel(PRank user1, PRank user2)
        {
            User1 = user1;
            User2 = user2;
            Main.player[TShock.Players[User1.UserID].Index].hostile = true;
            Main.player[TShock.Players[User2.UserID].Index].hostile = true;
            NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", TShock.Players[User1.UserID].Index);
            NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", TShock.Players[User2.UserID].Index);
            Color color = new Color(100, 100, 100);
            TShock.Utils.Broadcast(User1.Name + " and " + User2.Name + " have entered in a pvp duel!", color);
            // Tele to a arena
        }

        public void SetWinner(PRank winner)
        {
            winner.MMR += new Random().Next(15, 25);
            Winner = winner;
        }

        public void SetLoser(PRank loser)
        {
            loser.MMR -= new Random().Next(10, 18);
            Loser = loser;
        }

        public void SetFinished(bool isfinished)
        {
            finished = isfinished;
            if (finished)
            {
                Color color = new Color(220, 120, 120);
                TShock.Utils.Broadcast("[TerraPvP] " + Winner.Name + " won a fight vs " + Loser, color);
            }
        }
    }
}
