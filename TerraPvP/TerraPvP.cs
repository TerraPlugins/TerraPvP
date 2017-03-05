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
using System.Linq;

namespace TerraPvP
{
    [ApiVersion(2, 0)]
    public class TerraPvP : TerrariaPlugin
    {
        public static IDbConnection Db { get; private set; }
        public static DBManager DbManager { get; private set; }

        public static List<ConfigFile.Rank> RankList = new List<ConfigFile.Rank>();
        public List<PRank> UsersInQeue = new List<PRank>();
        public List<PVPFight> PvPFights = new List<PVPFight>();
        public static List<Arena> Arenas = new List<Arena>();
        public static List<PRank> PlayerRanks = new List<PRank>();

        public ConfigFile Config = new ConfigFile();

        Timer CheckQTimer = new Timer();

        #region Info
        public override string Name { get { return "TerraPvP"; } }
        public override string Author { get { return "Ryozuki"; } }
        public override string Description { get { return "A PvP plugin with ladder and ranks system"; } }
        public override Version Version { get { return new Version(1, 1, 0); } }
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
            Commands.ChatCommands.Add(new Command("terrapvp.queue", JoinQueue, "tqueue")
            {
                HelpText = "Usage: /tqueue or /tqueue <user> [Joins the queue]"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.queue", LeaveQueue, "tleave")
            {
                HelpText = "Usage: /tleave [Leaves the queue]"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.queue", ListQueue, "tinqueue")
            {
                HelpText = "Usage: /tinqueue [Returns a list with the users in the queue]"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.stats", GetStats, "tstats")
            {
                HelpText = "Usage: /tstats <name> or /tstats"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", CreateArena, "tcreate")
            {
                HelpText = "Usage: /tcreate <arena name>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", SetArenaSpawn, "tsetspawn")
            {
                HelpText = "Usage: /tsetspawn <arena name> <1 / 2>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", saveArena, "tsave")
            {
                HelpText = "Usage: /tsave <arena name>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.list", listArenas, "tlist")
            {
                HelpText = "Usage: /tlist"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", delArena, "tdel")
            {
                HelpText = "Usage: /tdel <arena name>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.stats", topTen, "ttopten")
            {
                HelpText = "Usage: /ttopten"
            });

            Db = new SqliteConnection("uri=file://" + Path.Combine(TShock.SavePath, "TerraPvP.sqlite") + ",Version=3");

            RankList = Config.RankList;
        }

        private void OnPostInitialize(EventArgs args)
        {
            DbManager = new DBManager(Db);
            Arenas = DbManager.LoadArenas();
        }

        void OnPlayerLogin(TShockAPI.Hooks.PlayerPostLoginEventArgs args)
        {
            if (args.Player == null)
                return;


            if (DbManager.PlayerExist(args.Player.User.ID))
            {
                PlayerRanks.Add(DbManager.LoadPlayer(args.Player.User.ID));
            }
            else
            {
                PRank prank = new PRank(args.Player.User.ID, args.Player.Name, 1500, Config.RankList[0].name);
                DbManager.addPlayer(prank);
                PlayerRanks.Add(prank);
            }
        }

        private void OnTeamChange(object sender, GetDataHandlers.PlayerTeamEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];

            if(ply == null)
                return;

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
            if (args.Player == null)
                return;

            if (PvPFights.Count == 0)
                return;

            if (!args.Player.IsLoggedIn)
                return;

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
            if (e.Player == null)
                return;
            
            int i = 1;
            e.Player.SendSuccessMessage("[TerraPvP] Top 10 ladder:");
            foreach (PRank player in DbManager.topTen())
            {
                e.Player.SendSuccessMessage($"{i}. Name: {player.Name} | Rank: {player.Rank} | MMR: {player.MMR}");
                i++;
            }
        }

        void listArenas(CommandArgs e)
        {
            if (e.Player == null)
                return;

            StringBuilder arena_list = new StringBuilder();

            Arenas.ForEach(x => arena_list.Append($" {x.regionName}"));

            if (!string.IsNullOrWhiteSpace(arena_list.ToString()))
                e.Player.SendSuccessMessage("[TerraPvP] Arenas:" + arena_list.ToString());
            else
                e.Player.SendErrorMessage("[TerraPvP] There are no arenas");
        }

        private void ListQueue(CommandArgs e)
        {
            if (e.Player == null)
                return;

            StringBuilder queueList = new StringBuilder();

            UsersInQeue.ForEach(x => queueList.Append($" {x.Name}"));

            if (!string.IsNullOrWhiteSpace(queueList.ToString()))
                e.Player.SendSuccessMessage("[TerraPvP] Users in Queue:" + queueList.ToString());
            else
                e.Player.SendErrorMessage("[TerraPvP] Queue is empty");
        }

        void delArena(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if(e.Parameters.Count > 0)
            {
                string[] args = e.Parameters.ToArray();

                if (Arenas.Any(x => x.regionName == args[0]))
                {
                    var arena = Arenas.Find(x => x.regionName == args[0]);
                    DbManager.delArena(arena);
                    Arenas.RemoveAll(x => x.regionName == args[0]);
                    e.Player.SendSuccessMessage("Arena deleted.");
                }
                else
                    e.Player.SendErrorMessage("[TerraPvP]  A arena with that name doesn't exist.");
            }
            else
            {
                e.Player.SendInfoMessage("Usage: / tdel <arena name>");
            }
        }

        private void LeaveQueue(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count == 0)
            {
                if(UsersInQeue.Any(x => x.UserID == e.Player.User.ID))
                {
                    UsersInQeue.RemoveAll(x => x.UserID == e.Player.User.ID);
                    e.Player.SendSuccessMessage("[TerraPvP] You left the queue");
                }
                else
                {
                    e.Player.SendInfoMessage("[TerraPvP] You are not in the queue");
                }
                return;
            }
            else
            {
                if (e.Player.HasPermission("terrapvp.forceleave"))
                {
                    if (UsersInQeue.Any(x => x.Name == e.Parameters[0]))
                    {
                        UsersInQeue.RemoveAll(x => x.Name == e.Parameters[0]);
                        e.Player.SendSuccessMessage($"[TerraPvP] Deleted {e.Parameters[0]} from the queue");
                    }
                    else
                    {
                        e.Player.SendInfoMessage($"[TerraPvP] {e.Parameters[0]} is no in the queue");
                    }
                }
            }
        }

        void CreateArena(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}tcreate <arena name>", Commands.Specifier);
                return;
            }

            string[] args = e.Parameters.ToArray();

            Arena arena = new Arena(args[0]);
            Arenas.Add(arena);
            e.Player.SendSuccessMessage("[TerraPvP]  Succesfully created arena '" + args[0] + "'");
        }

        void saveArena(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}tsave <arena name>", Commands.Specifier);
                return;
            }

            string[] args = e.Parameters.ToArray();

            if (Arenas.Any(x => x.regionName == args[0]))
            {
                Arena arena = Arenas.Find(x => x.regionName == args[0]);

                if (float.IsNaN(arena.spawn1_x) || float.IsNaN(arena.spawn1_y) || float.IsNaN(arena.spawn2_x) || float.IsNaN(arena.spawn2_y))
                {
                    e.Player.SendErrorMessage("[TerraPvP]  Error: One or more spawn points missing!");
                }
                else
                {
                    DbManager.addArena(arena);
                    arena.IsValid = true;
                    e.Player.SendSuccessMessage($"[TerraPvP] Succesfully saved arena '{args[0]}'");
                }
            }
            else
                e.Player.SendInfoMessage("[TerraPvP] A arena with that name doesn't exist.");
        }

        void SetArenaSpawn(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}tsetspawn <region name> <1 / 2> (First use set 1)", Commands.Specifier);
                return;
            }

            string[] args = e.Parameters.ToArray();

            if(Arenas.Any(x => x.regionName == args[0]))
            {
                Arena arena = Arenas.Find(x => x.regionName == args[0]);

                if(args[1] == "1")
                {
                    arena.spawn1_x = e.Player.TileX * 16;
                    arena.spawn1_y = e.Player.TileY * 16;
                    if (float.IsNaN(arena.spawn1_y) || float.IsNaN(arena.spawn1_y))
                        e.Player.SendErrorMessage("[TerraPvP]  There was an error, please try again.");
                    else
                        e.Player.SendSuccessMessage("[TerraPvP]  Spawn point added succesfully, add now the second.");
                }
                else if(args[1] == "2")
                {
                    arena.spawn2_x = e.Player.TileX * 16;
                    arena.spawn2_y = e.Player.TileY * 16;
                    if (float.IsNaN(arena.spawn2_y) || float.IsNaN(arena.spawn2_x))
                        e.Player.SendErrorMessage("[TerraPvP]  There was an error, please try again.");
                    else
                        e.Player.SendSuccessMessage("[TerraPvP]  Spawn point added succesfully, now you can save it with /tsave <arena name>");
                }
                else
                {
                    e.Player.SendInfoMessage($"{Commands.Specifier}tsetspawn <region name> <1 / 2> (First use set 1)");
                }
            }
            else
                e.Player.SendErrorMessage("[TerraPvP]  A arena with that name doesn't exist");
        }

        void JoinQueue(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (!e.Player.IsLoggedIn)
                return;

            PRank player = PlayerRanks.Find(x => x.UserID == e.Player.User.ID);

            if (UsersInQeue.Any(x => x.UserID == player.UserID))
                e.Player.SendErrorMessage("[TerraPvP]  You are already in queue!");
            else
            {
                UsersInQeue.Add(player);
                e.Player.SendSuccessMessage("[TerraPvP] You entered the queue succesfully");
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
                        if (UsersInQeue[ii].MMR >= UsersInQeue[i].MMR - Config.MaxMMRDifference 
                            && UsersInQeue[ii].MMR <= UsersInQeue[i].MMR + Config.MaxMMRDifference)
                        {
                            //add them to a arena
                            PVPFight duel = new PVPFight(UsersInQeue[i], UsersInQeue[ii]);
                            if (duel.creationSucces)
                                PvPFights.Add(duel);
                            else
                                continue;

                            //delete them from queue list
                            UsersInQeue.RemoveAll(x => x.UserID == UsersInQeue[i].UserID);
                            UsersInQeue.RemoveAll(x => x.UserID == UsersInQeue[ii].UserID);
                        }
                    }
                }
            }
        }

        void GetStats(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count == 0)
            {
                if (e.Player == null)
                    return;

                PRank player = PlayerRanks.Find(x => x.UserID == e.Player.User.ID);
                e.Player.SendSuccessMessage($"[TerraPvP] {e.Player.Name}'s stats:");
                e.Player.SendSuccessMessage("Rank: " + player.Rank);
                e.Player.SendSuccessMessage("MMR: " + player.MMR);
            }
            else
            {
                var name = e.Parameters[0];

                if (DbManager.PlayerExist(name))
                {
                    PRank player = DbManager.LoadPlayer(name);
                    e.Player.SendSuccessMessage($"[TerraPvP] {e.Player.Name}'s stats:");
                    e.Player.SendSuccessMessage("Rank: " + player.Rank);
                    e.Player.SendSuccessMessage("MMR: " + player.MMR);
                }
                else
                {
                    e.Player.SendInfoMessage("[TerraPvP] Player not found.");
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
