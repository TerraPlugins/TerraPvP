using System;
using System.Linq;
using TerraPvP;
using Terraria;
using TShockAPI;

namespace TerraPvP
{
    public class PVPFight
    {
        public PRank User1 { get; set; }
        public PRank User2 { get; set; }
        public PRank Winner { get; set; }
        public PRank Loser { get; set; }
        public bool finished { get; set; }
        public bool creationSucces { get; set; }
        public string arenaName { get; set; }
        private int player1index { get; set; }
        private int player2index { get; set; }

        public ConfigFile Config = new ConfigFile();

        public PVPFight(PRank user1, PRank user2)
        {
            creationSucces = false;
            try
            {
                foreach(Arena arena in TerraPvP.DbManager.Arenas)
                {
                    if (!arena.someoneFighting)
                    {
                        creationSucces = true;
                        User1 = user1;
                        User2 = user2;
                        player1index = TShock.Players.FirstOrDefault(p => p?.Active == true && p.IsLoggedIn && p.User.ID == User1.UserID).Index;
                        player2index = TShock.Players.FirstOrDefault(p => p?.Active == true && p.IsLoggedIn && p.User.ID == User2.UserID).Index;

                        //Put them on team 0
                        TShock.Players[player1index].SetTeam(0);
                        TShock.Players[player2index].SetTeam(0);

                        //Heal them
                        TShock.Players[player1index].Heal();
                        TShock.Players[player2index].Heal();

                        //Enable pvp on them
                        Main.player[player1index].hostile = true;
                        Main.player[player2index].hostile = true;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player1index);
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player2index);

                        //Send broadcast
                        Color color = new Color(96, 178, 233);
                        TShock.Utils.Broadcast("[TerraPvP] " + User1.Name + " and " + User2.Name + " are now fighting!", color);

                        arena.someoneFighting = true;
                        // Tele to a arena
                        TShock.Players[player1index].Teleport(arena.spawn1_x, arena.spawn1_y);
                        TShock.Players[player2index].Teleport(arena.spawn2_x, arena.spawn2_y);
                        arenaName = arena.regionName;
                        break;
                    }
                }
                
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

                Color color = new Color(126, 226, 126);
                TShock.Utils.Broadcast("[TerraPvP] " + Winner.Name + " ("+ Winner.Rank+ ", " + Winner.MMR + ")" + " won vs " + Loser.Name + " (" + Loser.Rank + ", " + Loser.MMR + ")", color);
                TShock.Players[player1index].SendInfoMessage(Config.onPvPFinishMessage);
                TShock.Players[player2index].SendInfoMessage(Config.onPvPFinishMessage);

                foreach(Arena arena in TerraPvP.DbManager.Arenas)
                {
                    if(arena.regionName == arenaName)
                    {
                        arena.someoneFighting = false;
                    }
                }
            }
        }
    }
}
