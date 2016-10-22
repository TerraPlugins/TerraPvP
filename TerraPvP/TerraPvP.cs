using System;
using System.Collections.Generic;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Mono.Data.Sqlite;
using System.IO;
using System.Data;
using System.Timers;
using System.Linq;

namespace TerraPvP
{
    [ApiVersion(1, 25)]
    public class TerraPvP : TerrariaPlugin
    {
        public static IDbConnection Db { get; private set; }
        public static PRankManager RankManager { get; private set; }
        public List<PRank> usersinqeue = new List<PRank>();
        public List<PVPDuel> pvpduel = new List<PVPDuel>();
        Timer checkQTimer = new Timer();

        #region Info
        public override string Name { get { return "TerraPvP"; } }
        public override string Author { get { return "Ryozuki"; } }
        public override string Description { get { return "A PvP plugin with MMR, ranks and stats"; } }
        public override Version Version { get { return new Version(1, 0, 0); } }
        #endregion

        /* already done:
         * Database
         * Player list
         * Duel list
         * Duel system
         * 
         * TODO:
         * Add arena regions system
         * Change rank if mmr is higher or lower than actual rank mmr limit
         */

        public TerraPvP(Main game) : base(game)
        {

        }

        #region Initialize
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            TShockAPI.Hooks.PlayerHooks.PlayerPostLogin += OnPlayerLogin;
            GetDataHandlers.PlayerTeam += OnTeamChange;
            GetDataHandlers.TogglePvp += onPvPToggle;
            GetDataHandlers.KillMe += onPlayerDeath;

            checkQTimer.Elapsed += new ElapsedEventHandler(timer_elapsed);
            checkQTimer.Interval = 30000;
            checkQTimer.Enabled = true;
        }
        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                GetDataHandlers.PlayerTeam -= OnTeamChange;
                GetDataHandlers.TogglePvp -= onPvPToggle;
                GetDataHandlers.KillMe -= onPlayerDeath;
                TShockAPI.Hooks.PlayerHooks.PlayerPostLogin -= OnPlayerLogin;
            }
        }

        

        void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("terrapvp.qeue", pvpqeue, "pvpqeue")
            {
                HelpText = "Usage: /pvpqeue"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.stats", getstats, "pvpstats")
            {
                HelpText = "Usage: /pvpstats <name> or /pvpstats"
            });

            Db = new SqliteConnection("uri=file://" + Path.Combine(TShock.SavePath, "terrapvp.sqlite") + ",Version=3");
        }

        private void OnPostInitialize(EventArgs args)
        {
            RankManager = new PRankManager(Db);
        }

        void OnPlayerLogin(TShockAPI.Hooks.PlayerPostLoginEventArgs args)
        {
            PRank playerrank = new PRank(args.Player.User.ID, args.Player.Name, 1500, "wood");
            RankManager.addPlayer(playerrank);
        }

        private void OnTeamChange(object sender, GetDataHandlers.PlayerTeamEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];
            foreach (PVPDuel duel in pvpduel)
            {
                if (duel.User1.UserID == ply.User.ID || duel.User2.UserID == ply.User.ID)
                {
                    ply.SetTeam(0);
                    ply.SendWarningMessage("You can't change team right now!");
                    args.Handled = true;
                }
            }
        }

        private void onPvPToggle(object sender, GetDataHandlers.TogglePvpEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];

            foreach (PVPDuel duel in pvpduel)
            {
                if (duel.User1.UserID == ply.User.ID || duel.User2.UserID == ply.User.ID)
                {
                    Main.player[ply.Index].hostile = true;
                    NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", ply.Index);
                    ply.SendWarningMessage("Your PvP has been forced on, don't try and turn it off!");
                    args.Handled = true;
                }
            }
        }

        private void onPlayerDeath(object sender, GetDataHandlers.KillMeEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];
            for (var i = 0; i < pvpduel.Count; i++)
            {
                if (pvpduel[i].User1.UserID == ply.User.ID)
                {
                    Random rnd = new Random();

                    pvpduel[i].SetWinner(pvpduel[i].User2);
                    pvpduel[i].SetLoser(pvpduel[i].User1);
                    pvpduel[i].SetFinished(true);

                    RankManager.updatePlayer(pvpduel[i].User1);
                    RankManager.updatePlayer(pvpduel[i].User2);

                    pvpduel.RemoveAt(i);
                }
                else if (pvpduel[i].User2.UserID == ply.User.ID)
                {
                    Random rnd = new Random();

                    pvpduel[i].SetWinner(pvpduel[i].User1);
                    pvpduel[i].SetLoser(pvpduel[i].User2);
                    pvpduel[i].SetFinished(true);

                    RankManager.updatePlayer(pvpduel[i].User1);
                    RankManager.updatePlayer(pvpduel[i].User2);

                    pvpduel.RemoveAt(i);
                }
            }
        }

        private void timer_elapsed(object source, ElapsedEventArgs e)
        {
            checkQTimer.Stop();
            _checkqeue();
            checkQTimer.Start();
        }

        void pvpqeue(CommandArgs e)
        {
            int mmr = 0;
            string rank = "";

            for (int i = 0; i < RankManager.pranks.Count; i++)
            {
                if (RankManager.pranks[i].UserID == e.Player.User.ID)
                {
                    mmr = RankManager.pranks[i].MMR;
                    rank = RankManager.pranks[i].Rank;
                }

            }
            PRank player = new PRank(e.Player.User.ID, e.Player.Name, mmr, rank);
            usersinqeue.Add(player);
            e.Player.SendSuccessMessage("You entered the qeue succesfully");
        }

        void _checkqeue()
        {
            for (int i = 0; i < usersinqeue.Count; i++)
            {
                for (int ii = 0; ii < usersinqeue.Count; ii++)
                {
                    // Check if difference of mmr is not more than 100 or lower than 100
                    if(usersinqeue[i].UserID != usersinqeue[ii].UserID)
                    {
                        if (usersinqeue[ii].MMR >= usersinqeue[i].MMR - 100 && usersinqeue[ii].MMR <= usersinqeue[i].MMR + 100)
                        {
                            try
                            {
                                //add them to a arena
                                PVPDuel duel = new PVPDuel(usersinqeue[i], usersinqeue[ii]);
                                pvpduel.Add(duel);
                                //delete them from qeue list
                                int userid = usersinqeue[ii].UserID;
                                usersinqeue.RemoveAt(i);
                                for (int o = 0; o < usersinqeue.Count; o++)
                                {
                                    if (usersinqeue[o].UserID == userid)
                                    {
                                        usersinqeue.RemoveAt(o);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                            }

                        }
                    }
                }
            }
        }

        void getstats(CommandArgs e)
        {
            int mmr = 0;
            string rank = "";

            if (e.Parameters.Count == 0)
            {
                for (int i = 0; i < RankManager.pranks.Count; i++)
                {
                    if (RankManager.pranks[i].UserID == e.Player.User.ID)
                    {
                        mmr = RankManager.pranks[i].MMR;
                        rank = RankManager.pranks[i].Rank;
                    }

                }
                e.Player.SendSuccessMessage("Stats for " + e.Player.Name);
                e.Player.SendSuccessMessage("Rank: " + rank);
                e.Player.SendSuccessMessage("MMR: " + mmr);
            }
            else
            {
                string args = String.Join(" ", e.Parameters.ToArray());
                int playerid;
                try
                {
                    playerid = TShock.Users.GetUserByName(args).ID;
                }
                catch
                {
                    e.Player.SendErrorMessage("Player not found");
                    return;
                }

                for (int i = 0; i < RankManager.pranks.Count; i++)
                {
                    if (RankManager.pranks[i].UserID == playerid)
                    {
                        mmr = RankManager.pranks[i].MMR;
                        rank = RankManager.pranks[i].Rank;
                        e.Player.SendSuccessMessage("Stats for " + RankManager.pranks[i].Name);
                        e.Player.SendSuccessMessage("Rank: " + rank);
                        e.Player.SendSuccessMessage("MMR: " + mmr);
                    }
                }
            }
        }
    }
}