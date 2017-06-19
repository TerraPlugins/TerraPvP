using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using System.Timers;
using TShockAPI;

namespace TerraPvP
{
    public class Arena
    {
        public string RegionName { get; set; }
        public List<Spawn> SpawnPoints = new List<Spawn>();
        public List<PRank> Players = new List<PRank>();
        public Timer CountDown = new Timer();
        public bool AlreadyStarted { get; set; }
        public bool IsValid { get; set; }
        public int MaxPlayers { get; set; }
        public int ID { get; set; }

        public Arena(string RegionName)
        {
            this.RegionName = RegionName;
            AlreadyStarted = false;
            IsValid = false;
            MaxPlayers = -1;
        }

        public Arena(string RegionName, int id, params Spawn[] Spawnpoint)
        {
            this.RegionName = RegionName;
            ID = id;
            SpawnPoints.AddRange(Spawnpoint);
            MaxPlayers = SpawnPoints.Count;
            AlreadyStarted = false;
            IsValid = true;
            CountDown.Elapsed += CountDown_Elapsed;
        }

        private void CountDown_Elapsed(object sender, ElapsedEventArgs e)
        {
            Start();
        }

        public void PlayerDeath(int id)
        {
            if (!AlreadyStarted)
                return;

            PRank ply = Players.Find(x => x.UserID == id);
            ply.IsAlive = false;
            int aliveplayers = Players.Count - Players.Count(x => x.IsAlive);
            //ply.MMR = aliveplayers > Players.Count / 2 ? (ply.MMR - (aliveplayers / 2)) : (ply.MMR + (Players.Count - aliveplayers) / 2);
            ply.MMR += Players.Count - (aliveplayers == 1 ? 0 : aliveplayers);

            ply.Rank = TerraPvP.Config.RankList.OrderBy(x => x.mmr)
                .TakeWhile(x => ply.MMR > x.mmr)
                .Last()
                .name;

            DB.UpdatePlayer(ply);

            TSPlayer _player = GetPlayer(ply);
            Main.player[_player.Index].hostile = false;
            NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, Terraria.Localization.NetworkText.Empty, _player.Index);

            if (Players.Count(x => x.IsAlive == true) == 1)
            {
                // one player remaining
                PRank winner = Players.Find(x => x.IsAlive == true);
                winner.MMR += Players.Count + TerraPvP.Config.BonusWinMMR; // add this to config
                winner.Rank = TerraPvP.Config.RankList.OrderBy(x => x.mmr)
                    .TakeWhile(x => winner.MMR > x.mmr)
                    .Last()
                    .name;

                DB.UpdatePlayer(winner);

                AlreadyStarted = false;
                TSPlayer player = GetPlayer(winner);
                player.Spawn();
                TShock.Utils.Broadcast(String.Format("[TerraPvP] {0} have won in the arena '{1}'!", player.Name, RegionName), 83, 236, 226);

                Players.Clear();
            }
        }

        private TSPlayer GetPlayer(PRank ply)
        {
            return TShock.Players.FirstOrDefault(p => p?.Active == true && p.IsLoggedIn && p.User.ID == ply.UserID);
        }

        public void Broadcast(string msg)
        {
            if (Players.Count == 0)
                return;

            foreach(PRank ply in Players)
            {
                TSPlayer player = GetPlayer(ply);
                player.SendMessage(msg, 47, 182, 20);
            }
        }

        public bool AddPlayer(PRank player)
        {
            // TODO: BROADCAST PLAYER JOINED AMONG OTHER USERS
            player.IsAlive = true;

            if(Players.Count >= 3)
            {
                CountDown.Interval = 60000;
                CountDown.Start();
            }

            if (Players.Count >= SpawnPoints.Count)
                return false;
            else
            {
                Players.Add(player);
                Broadcast(String.Format("[TerraPvP] {0} has joined the arena.", player.Name));

                if (Players.Count == SpawnPoints.Count)
                {
                    CountDown.Stop();
                    Start();
                }
            }
            return true;
        }

        public void DeletePlayer(PRank player)
        {
            Players.RemoveAll(x => x.UserID == player.UserID);
        }

        private void Start()
        {
            if (AlreadyStarted)
                return;

            AlreadyStarted = true;
            int i = 0;
            foreach(PRank ply in Players)
            {
                TSPlayer player = GetPlayer(ply);
                player.SetTeam(0);
                player.Heal();

                Main.player[player.Index].hostile = true;
                NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, Terraria.Localization.NetworkText.Empty, player.Index);

                player.Teleport(SpawnPoints[i].x, SpawnPoints[i].y);

                i++;
            }

            //Send broadcast
            Color color = new Color(96, 178, 233);
            TShock.Utils.Broadcast($"[TerraPvP] Arena {RegionName} has started!", color);
        }
    }
}
