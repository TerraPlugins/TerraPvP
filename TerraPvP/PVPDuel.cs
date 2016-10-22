using System;
using System.Linq;
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
        private int player1index { get; set; }
        private int player2index { get; set; }

        public PVPDuel(PRank user1, PRank user2)
        {
            try
            {
                User1 = user1;
                User2 = user2;
                player1index = TShock.Players.FirstOrDefault(p => p?.Active == true && p.IsLoggedIn && p.User.ID == User1.UserID).Index;
                player2index = TShock.Players.FirstOrDefault(p => p?.Active == true && p.IsLoggedIn && p.User.ID == User2.UserID).Index;

                //Put them on team 0
                TShock.Players[player1index].SetTeam(0);
                TShock.Players[player2index].SetTeam(0);

                //Enable pvp on them
                Main.player[player1index].hostile = true;
                Main.player[player2index].hostile = true;
                NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player1index);
                NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player2index);

                //Send broadcast
                Color color = new Color(96, 178, 233);
                TShock.Utils.Broadcast("[TerraPvP] " + User1.Name + " and " + User2.Name + " are now fighting!", color);

                // Tele to a arena
            }
            catch(Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }


        }

        public void SetWinner(PRank winner)
        {
            winner.MMR += new Random().Next(15, 25);
            winner.checkRank();
            Winner = winner;
        }

        public void SetLoser(PRank loser)
        {
            loser.MMR -= new Random().Next(10, 18);
            loser.checkRank();
            Loser = loser;
        }

        public void SetFinished(bool isfinished)
        {
            finished = isfinished;
            if (finished)
            {
                //Disable pvp on them
                Main.player[player1index].hostile = false;
                Main.player[player2index].hostile = false;
                NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player1index);
                NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player2index);

                //Tele them to spawn
                TShock.Players[player1index].Teleport(TShock.Players[player1index].sX, TShock.Players[player1index].sY);
                TShock.Players[player2index].Teleport(TShock.Players[player2index].sX, TShock.Players[player2index].sY);

                Color color = new Color(126, 226, 126);
                TShock.Utils.Broadcast("[TerraPvP] " + Winner.Name + " won vs " + Loser.Name, color);
            }
        }
    }
}
