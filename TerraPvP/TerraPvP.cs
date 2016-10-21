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
         * 
         * TODO:
         * Add arena regions system
         * Know if some region is free, if so teleport them and start pvp, save their info for when they finish, give mmr to winner, delete mmr from loser, change ranks if needed
         * Change rank if mmr is higher or lower than actual rank mmr limit
         * You should teleport the dueling players somewhere. 
            And not allow other players to TP to them so they don't interfere. 
            (also don't allow the dueling players to teleport away, or even use certain commands which can help them cheat)

            ryozuki [1:05 PM]  
            yeah

            [1:05]  
            i'm not there still

            patrikk [1:05 PM]  
            Also don't allow the dueling players to get in a party. Because then they could "mess up" the thing, by being in same party.

            ryozuki [1:05 PM]  
            how to force pvp?

            [1:05]  
            true

            patrikk [1:06 PM]  
            Check PvpToggle plugin.
         */

        public TerraPvP(Main game) : base(game)
        {

        }

        #region Initialize
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            TShockAPI.Hooks.PlayerHooks.PlayerPostLogin += OnPlayerLogin;
            checkQTimer.Elapsed += new ElapsedEventHandler(myEvent);
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
                TShockAPI.Hooks.PlayerHooks.PlayerPostLogin -= OnPlayerLogin;
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
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
            Commands.ChatCommands.Add(new Command("terrapvp.stats", testcommand, "testcommand")
            {
                HelpText = "Usage: /testcommand <name> or /testcommand"
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

        private void myEvent(object source, ElapsedEventArgs e)
        {
            Console.WriteLine("timer called");
            checkQTimer.Stop();
            _checkqeue();
            checkQTimer.Start();
        }

        // ????????????????
        void OnGetData(GetDataEventArgs e)
        {
            int killerid = 0;
            int deathplayerid;
            if (e.MsgID == PacketTypes.PlayerDamage)
            {
                killerid = e.Msg.reader.ReadSByte();
            }
            if (e.MsgID == PacketTypes.PlayerKillMe)
            {
                deathplayerid = e.Msg.reader.ReadSByte();

                for(int i = 0; i < pvpduel.Count; i++)
                {
                    if(pvpduel[i].User1.UserID == deathplayerid)
                    {
                        if (pvpduel[i].User2.UserID == killerid)
                        {
                            pvpduel[i].SetWinner(pvpduel[i].User2);
                            pvpduel[i].SetLoser(pvpduel[i].User1);
                            pvpduel[i].SetFinished(true);
                        }
                    }
                    else if (pvpduel[i].User2.UserID == deathplayerid)
                    {
                        if (pvpduel[i].User1.UserID == killerid)
                        {
                            pvpduel[i].SetWinner(pvpduel[i].User1);
                            pvpduel[i].SetLoser(pvpduel[i].User2);
                            pvpduel[i].SetFinished(true);
                        }
                    }
                }
            }
            if(e.MsgID == PacketTypes.TogglePvp || e.MsgID == PacketTypes.PlayerTeam || e.MsgID == PacketTypes.Teleport)
            {
                for(int i = 0; i < pvpduel.Count; i++)
                {
                    if(pvpduel[i].User1.UserID == e.Msg.whoAmI || pvpduel[i].User2.UserID == e.Msg.whoAmI)
                    {
                        e.Handled = true;
                    }
                }
            }
        }

        void testcommand(CommandArgs e)
        {
            Main.player[e.Player.Index].hostile = true;
            NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", e.Player.Index);
            TShock.Players[e.Player.Index].TPAllow = false;
            TShock.Players[e.Player.Index].TpLock = true;
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
            Console.WriteLine("checkqeue called");
            for (int i = 0; i < usersinqeue.Count; i++)
            {
                for (int ii = 0; ii < usersinqeue.Count; ii++)
                {
                    // Check if difference of mmr is not more than 100 or lower than 100
                    if (usersinqeue[i].MMR - 100 >= usersinqeue[ii].MMR && usersinqeue[i].MMR + 100 <= usersinqeue[ii].MMR)
                    {
                        try
                        {
                            //add them to a arena
                            PVPDuel duel = new PVPDuel(usersinqeue[i], usersinqeue[ii]);
                            pvpduel.Add(duel);
                            Console.WriteLine("duel added");
                            //delete them from qeue list
                            usersinqeue.RemoveAt(i);
                            int userid = usersinqeue[ii].UserID;
                            for (int o = 0; o < usersinqeue.Count; o++)
                            {
                                if (usersinqeue[o].UserID == userid)
                                {
                                    usersinqeue.RemoveAt(o);
                                }
                            }
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine(e.ToString());
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