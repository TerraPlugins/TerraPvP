using System;
using System.Collections.Generic;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Mono.Data.Sqlite;
using System.IO;
using System.Data;
using System.Timers;
using System.Text;

namespace TerraPvP
{
    [ApiVersion(1, 25)]
    public class TerraPvP : TerrariaPlugin
    {
        public static IDbConnection Db { get; private set; }
        public static DBManager DbManager { get; private set; }
        public static List<ConfigFile.Rank> ranklist = new List<ConfigFile.Rank>();

        public List<PRank> UsersInQeue = new List<PRank>();
        public List<PVPFight> PvPFights = new List<PVPFight>();
        public List<Arena> Arenas = new List<Arena>();
        public ConfigFile Config = new ConfigFile();
        Timer CheckQTimer = new Timer();

        #region Info
        public override string Name { get { return "TerraPvP"; } }
        public override string Author { get { return "Ryozuki"; } }
        public override string Description { get { return "A PvP plugin with ladder and ranks system"; } }
        public override Version Version { get { return new Version(1, 0, 2); } }
        #endregion

        public TerraPvP(Main game) : base(game)
        {

        }

        #region Initialize
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            TShockAPI.Hooks.PlayerHooks.PlayerPostLogin += OnPlayerLogin;
            TShockAPI.Hooks.PlayerHooks.PlayerCommand += OnCommand;
            GetDataHandlers.PlayerTeam += OnTeamChange;
            GetDataHandlers.TogglePvp += onPvPToggle;
            GetDataHandlers.KillMe += onPlayerDeath;

            LoadConfig();

            CheckQTimer.Elapsed += new ElapsedEventHandler(timer_elapsed);
            CheckQTimer.Interval = 10000;
            CheckQTimer.Enabled = true;
        }
        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                TShockAPI.Hooks.PlayerHooks.PlayerCommand -= OnCommand;
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
            Commands.ChatCommands.Add(new Command("terrapvp.arena", createArena, "createarena")
            {
                HelpText = "Usage: /createarena <arena name>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", setArenaSpawn, "setarenaspawn")
            {
                HelpText = "Usage: /setarenaspawn <arena name> <1 / 2>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", saveArena, "savearena")
            {
                HelpText = "Usage: /savearena <arena name>"
            });

            Commands.ChatCommands.Add(new Command("terrapvp.list", listArenas, "listarenas")
            {
                HelpText = "Usage: /listarenas"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", delArena, "delarena")
            {
                HelpText = "Usage: /delarena <arena name>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.stats", topTen, "topten")
            {
                HelpText = "Usage: /topten"
            });

            Db = new SqliteConnection("uri=file://" + Path.Combine(TShock.SavePath, "TerraPvP.sqlite") + ",Version=3");

            ranklist = Config.RankList;
        }

        private void OnPostInitialize(EventArgs args)
        {
            DbManager = new DBManager(Db);
        }

        void OnPlayerLogin(TShockAPI.Hooks.PlayerPostLoginEventArgs args)
        {
            PRank playerrank = new PRank(args.Player.User.ID, args.Player.Name, 1500, Config.RankList[0].name);
            DbManager.addPlayer(playerrank);
        }

        private void OnTeamChange(object sender, GetDataHandlers.PlayerTeamEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];

            if(ply == null)
            {
                return;
            }

            foreach (PVPFight duel in PvPFights)
            {
                if (duel.User1.UserID == ply.User.ID || duel.User2.UserID == ply.User.ID)
                {
                    ply.SetTeam(0);
                    ply.SendWarningMessage("[TerraPvP] You can't change team right now!");
                    args.Handled = true;
                }
            }
        }

        private void onPvPToggle(object sender, GetDataHandlers.TogglePvpEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];

            if (ply == null)
            {
                return;
            }

            foreach (PVPFight duel in PvPFights)
            {
                if (duel.User1.UserID == ply.User.ID || duel.User2.UserID == ply.User.ID)
                {
                    Main.player[ply.Index].hostile = true;
                    NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", ply.Index);
                    ply.SendWarningMessage("[TerraPvP] Your PvP has been forced on, don't try and turn it off!");
                    args.Handled = true;
                }
            }
        }

        private void OnCommand(TShockAPI.Hooks.PlayerCommandEventArgs args)
        {
            foreach (PVPFight duel in PvPFights)
            {
                if(duel.User1.UserID == args.Player.User.ID || duel.User2.UserID == args.Player.User.ID)
                {
                    foreach (string command in Config.BannedCommands)
                    {
                        if(args.CommandName == command)
                        {
                            args.Player.SendErrorMessage("[TerraPvP] You can't use that command right now!");
                            args.Handled = true;
                        }
                    }
                }
            }
        }

        private void onPlayerDeath(object sender, GetDataHandlers.KillMeEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];

            if (ply == null)
            {
                return;
            }

            for (var i = 0; i < PvPFights.Count; i++)
            {
                if (PvPFights[i].User1.UserID == ply.User.ID)
                {
                    Random rnd = new Random();

                    PvPFights[i].SetWinner(PvPFights[i].User2);
                    PvPFights[i].SetLoser(PvPFights[i].User1);
                    PvPFights[i].SetFinished(true);

                    DbManager.updatePlayer(PvPFights[i].User1);
                    DbManager.updatePlayer(PvPFights[i].User2);

                    PvPFights.RemoveAt(i);
                }
                else if (PvPFights[i].User2.UserID == ply.User.ID)
                {
                    Random rnd = new Random();

                    PvPFights[i].SetWinner(PvPFights[i].User1);
                    PvPFights[i].SetLoser(PvPFights[i].User2);
                    PvPFights[i].SetFinished(true);

                    DbManager.updatePlayer(PvPFights[i].User1);
                    DbManager.updatePlayer(PvPFights[i].User2);

                    PvPFights.RemoveAt(i);
                }
            }
        }

        private void timer_elapsed(object source, ElapsedEventArgs e)
        {
            CheckQTimer.Stop();
            _checkqeue();
            CheckQTimer.Start();
        }

        void topTen(CommandArgs e)
        {
            DbManager.topTen();
            int i = 1;
            e.Player.SendSuccessMessage("[TerraPvP] Top 10 ladder:");
            foreach (PRank player in DbManager.topten)
            {
                e.Player.SendSuccessMessage(i + ". Name: " + player.Name + " Rank: " + player.Rank + " MMR: " + player.MMR);
                i++;
            }
        }

        void listArenas(CommandArgs e)
        {
            StringBuilder arena_list = new StringBuilder();
            foreach (Arena arena in DbManager.Arenas)
            {
                arena_list.Append(" " + arena.regionName);
            }
            if (!string.IsNullOrWhiteSpace(arena_list.ToString()))
            {
                e.Player.SendSuccessMessage("[TerraPvP]  Arenas:" + arena_list.ToString());
            }
            else
            {
                e.Player.SendErrorMessage("[TerraPvP] There are no arenas");
            }
        }

        void delArena(CommandArgs e)
        {
            string[] args = e.Parameters.ToArray();
            bool exist = false;
            foreach(Arena arena in DbManager.Arenas)
            {
                if(args[0] == arena.regionName)
                {
                    exist = true;
                    DbManager.delArena(arena);
                    break;
                }
            }
            if (!exist)
            {
                e.Player.SendErrorMessage("[TerraPvP]  A arena with that name doesn't exist.");
            }
        }

        void createArena(CommandArgs e)
        {
            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}createarena <arena name>", Commands.Specifier);
                return;
            }

            string[] args = e.Parameters.ToArray();

            Arena arena = new Arena(args[0]);
            Arenas.Add(arena);
            e.Player.SendSuccessMessage("[TerraPvP]  Succesfully created arena '" + args[0] + "'");
        }

        void saveArena(CommandArgs e)
        {
            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}savearena <arena name>", Commands.Specifier);
                return;
            }

            string[] args = e.Parameters.ToArray();

            foreach (Arena arena in Arenas)
            {
                if(arena.regionName == args[0])
                {
                    if(float.IsNaN(arena.spawn1_x) || float.IsNaN(arena.spawn1_y) || float.IsNaN(arena.spawn2_x) || float.IsNaN(arena.spawn2_y))
                    {
                        e.Player.SendErrorMessage("[TerraPvP]  Error: One or more spawn points missing!");
                    }
                    else
                    {
                        DbManager.addArena(arena);
                        e.Player.SendSuccessMessage("[TerraPvP] Succesfully saved arena '" + args[0] + "'");
                    }
                }
            }
        }

        void setArenaSpawn(CommandArgs e)
        {
            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}setarenaspawn <region name> <1 / 2> (First use set 1)", Commands.Specifier);
                return;
            }

            string[] args = e.Parameters.ToArray();

            bool exist = false;
            foreach (Arena arena in Arenas)
            {
                if(arena.regionName == args[0].ToString() && args[1] == "1")
                {
                    arena.spawn1_x = e.Player.X;
                    arena.spawn1_y = e.Player.Y;
                    if(float.IsNaN(arena.spawn1_y) || float.IsNaN(arena.spawn1_y))
                    {
                        e.Player.SendErrorMessage("[TerraPvP]  There was an error, please try again.");
                    }
                    else
                    {
                        e.Player.SendSuccessMessage("[TerraPvP]  Spawn point added succesfully, add now the second.");
                    }
                    exist = true;
                }

                if (arena.regionName == args[0].ToString() && args[1] == "2")
                {
                    arena.spawn2_x = e.Player.X;
                    arena.spawn2_y = e.Player.Y;
                    exist = true;
                    if (float.IsNaN(arena.spawn2_y) || float.IsNaN(arena.spawn2_x))
                    {
                        e.Player.SendErrorMessage("[TerraPvP]  There was an error, please try again.");
                    }
                    else
                    {
                        e.Player.SendSuccessMessage("[TerraPvP]  Spawn point added succesfully, now you can save it with /savearena <arena name>");
                    }
                }
            }

            if (!exist)
            {
                e.Player.SendErrorMessage("[TerraPvP]  A arena with that name doesn't exist");
            }
            exist = false;
        }

        void pvpqeue(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (!e.Player.IsLoggedIn)
                return;

            int mmr = 0;
            string rank = "";

            for (int i = 0; i < DbManager.pranks.Count; i++)
            {
                if (DbManager.pranks[i].UserID == e.Player.User.ID)
                {
                    mmr = DbManager.pranks[i].MMR;
                    rank = DbManager.pranks[i].Rank;
                }

            }
            PRank player = new PRank(e.Player.User.ID, e.Player.Name, mmr, rank);
            bool already_in_qeue = false;
            foreach(PRank playerr in UsersInQeue)
            {
                if(playerr.UserID == e.Player.User.ID)
                {
                    already_in_qeue = true;
                }
            }

            if (already_in_qeue)
            {
                e.Player.SendErrorMessage("[TerraPvP]  You are already in qeue!");
            }
            else
            {
                UsersInQeue.Add(player);
                e.Player.SendSuccessMessage("[TerraPvP]  You entered the qeue succesfully");
            }
        }

        void _checkqeue()
        {
            for (int i = 0; i < UsersInQeue.Count; i++)
            {
                for (int ii = 0; ii < UsersInQeue.Count; ii++)
                {
                    if(UsersInQeue[i].UserID != UsersInQeue[ii].UserID)
                    {
                        if (UsersInQeue[ii].MMR >= UsersInQeue[i].MMR - Config.MaxMMRDifference && UsersInQeue[ii].MMR <= UsersInQeue[i].MMR + Config.MaxMMRDifference)
                        {
                            try
                            {
                                //add them to a arena
                                PVPFight duel = new PVPFight(UsersInQeue[i], UsersInQeue[ii]);
                                if (duel.creationSucces)
                                {
                                    PvPFights.Add(duel);
                                }
                                else
                                {
                                    continue;
                                }
                                
                                //delete them from qeue list
                                int userid = UsersInQeue[ii].UserID;
                                UsersInQeue.RemoveAt(i);
                                for (int o = 0; o < UsersInQeue.Count; o++)
                                {
                                    if (UsersInQeue[o].UserID == userid)
                                    {
                                        UsersInQeue.RemoveAt(o);
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
                if (e.Player == null)
                    return;

                for (int i = 0; i < DbManager.pranks.Count; i++)
                {
                    if (DbManager.pranks[i].UserID == e.Player.User.ID)
                    {
                        mmr = DbManager.pranks[i].MMR;
                        rank = DbManager.pranks[i].Rank;
                    }

                }
                e.Player.SendSuccessMessage("Stats for " + e.Player.Name);
                e.Player.SendSuccessMessage("Rank: " + rank);
                e.Player.SendSuccessMessage("MMR: " + mmr);
            }
            else
            {
                string plrName = String.Join(" ", e.Parameters.ToArray());

                var players = TShock.Utils.FindPlayer(plrName);

                if (players.Count == 0)
                {
                    e.Player.SendErrorMessage("Invalid player!");
                    return;
                } 
                else if (players.Count > 1)
                {
                    e.Player.SendErrorMessage("More than one player matched!");
                    return;
                }
                    

                int playerid;
                try
                {
                    playerid = TShock.Users.GetUserByName(plrName).ID;
                }
                catch
                {
                    e.Player.SendErrorMessage("Player not found");
                    return;
                }

                for (int i = 0; i < DbManager.pranks.Count; i++)
                {
                    if (DbManager.pranks[i].UserID == playerid)
                    {
                        mmr = DbManager.pranks[i].MMR;
                        rank = DbManager.pranks[i].Rank;
                        e.Player.SendSuccessMessage("[TerraPvP] Stats:");
                        e.Player.SendSuccessMessage("Name: " + DbManager.pranks[i].Name);
                        e.Player.SendSuccessMessage("Rank: " + rank);
                        e.Player.SendSuccessMessage("MMR: " + mmr);
                    }
                }
            }
        }

        #region Load Config
        private void LoadConfig()
        {
            string path = Path.Combine(TShock.SavePath, "TerraPvP.json");
            Config = ConfigFile.Read(path);
        }
        #endregion
    }
}
