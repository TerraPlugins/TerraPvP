using Microsoft.Xna.Framework;
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
        private TSPlayer player1 { get; set; }
        private TSPlayer player2 { get; set; }

        public ConfigFile Config = new ConfigFile();

        public PVPFight(PRank user1, PRank user2)
        {
            creationSucces = false;
            try
            {
                foreach(Arena arena in TerraPvP.Arenas)
                {
                    if (!arena.someoneFighting && arena.IsValid)
                    {
                        creationSucces = true;
                        User1 = user1;
                        User2 = user2;
                        player1 = TShock.Players.FirstOrDefault(p => p?.Active == true && p.IsLoggedIn && p.User.ID == User1.UserID);
                        player2 = TShock.Players.FirstOrDefault(p => p?.Active == true && p.IsLoggedIn && p.User.ID == User2.UserID);

                        //Put them on team 0
                        player1.SetTeam(0);
                        player2.SetTeam(0);

                        //Heal them
                        player1.Heal();
                        player2.Heal();

                        //Enable pvp on them
                        Main.player[player1.Index].hostile = true;
                        Main.player[player2.Index].hostile = true;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player1.Index);
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player2.Index);

                        //Send broadcast
                        Color color = new Color(96, 178, 233);
                        TShock.Utils.Broadcast("[TerraPvP] " + User1.Name + " and " + User2.Name + " are now fighting!", color);

                        arena.someoneFighting = true;
                        // Tele to a arena
                        player1.Teleport(arena.spawn1_x, arena.spawn1_y);
                        player2.Teleport(arena.spawn2_x, arena.spawn2_y);
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
                Main.player[player1.Index].hostile = false;
                Main.player[player2.Index].hostile = false;
                NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player1.Index);
                NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player2.Index);

                Color color = new Color(126, 226, 126);
                TShock.Utils.Broadcast("[TerraPvP] " + Winner.Name + " ("+ Winner.Rank+ ", " + Winner.MMR + ")" + " won vs " + Loser.Name + " (" + Loser.Rank + ", " + Loser.MMR + ")", color);
                player1.SendInfoMessage(Config.onPvPFinishMessage);
                player2.SendInfoMessage(Config.onPvPFinishMessage);
                TerraPvP.Arenas.Find(x => x.regionName == arenaName).someoneFighting = false;
            }
        }
    }
}
