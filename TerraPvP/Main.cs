using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using System.IO;
using System.Reflection;

// TODO: Custom inventory (kits).

namespace TerraPvP
{
    [ApiVersion(2, 1)]
    public class TerraPvP : TerrariaPlugin
    {
        #region Plugin Info
        public override string Name => "TerraPvP";
        public override string Author => "Ryozuki";
        public override string Description => "A PvP plugin with arenas, ladder and ranks system";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        #endregion

        public static ConfigFile Config = new ConfigFile();
        public static List<Arena> Arenas = new List<Arena>();

        public TerraPvP(Main game) : base(game)
        {
        }

        private void LoadConfig()
        {
            string path = Path.Combine(TShock.SavePath, "TerraPvP.json");
            Config = ConfigFile.Read(path);
        }

        public override void Initialize()
        {
            LoadConfig();
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            TShockAPI.Hooks.PlayerHooks.PlayerPostLogin += OnPlayerLogin;
            TShockAPI.Hooks.PlayerHooks.PlayerCommand += OnCommand;
            TShockAPI.Hooks.PlayerHooks.PlayerLogout += OnPlayerLogout;
            GetDataHandlers.PlayerTeam += OnTeamChange;
            GetDataHandlers.TogglePvp += OnPvPToggle;
            GetDataHandlers.KillMe += OnPlayerDeath;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                TShockAPI.Hooks.PlayerHooks.PlayerPostLogin -= OnPlayerLogin;
                TShockAPI.Hooks.PlayerHooks.PlayerLogout -= OnPlayerLogout;
                TShockAPI.Hooks.PlayerHooks.PlayerCommand -= OnCommand;
                GetDataHandlers.PlayerTeam -= OnTeamChange;
                GetDataHandlers.TogglePvp -= OnPvPToggle;
                GetDataHandlers.KillMe -= OnPlayerDeath;
            }
            base.Dispose(disposing);
        }

        #region Hooks
        private void OnTeamChange(object sender, GetDataHandlers.PlayerTeamEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];

            if (ply == null)
                return;

            if(Arenas.Any(x => x.Players.Any(j => j.UserID == ply.User.ID)))
            {
                ply.SetTeam(0);
                ply.SendWarningMessage("[TerraPvP] You can't change team right now!");
                args.Handled = true;
            }
        }

        void OnPlayerLogin(TShockAPI.Hooks.PlayerPostLoginEventArgs e)
        {
            if (e.Player == null)
                return;


            if (!DB.PlayerExist(e.Player.User.ID))
            {
                PRank prank = new PRank(e.Player.User.ID, e.Player.Name, Config.DefaultMMR, Config.RankList[0].name);
                DB.AddPlayer(prank);
            }
        }

        private void OnPlayerLogout(TShockAPI.Hooks.PlayerLogoutEventArgs e)
        {
            Arenas.Find(x => x.Players.Any(j => j.UserID == e.Player.User.ID))
                .Players.RemoveAll(x => x.UserID == e.Player.User.ID);
        }

        private void OnCommand(TShockAPI.Hooks.PlayerCommandEventArgs e)
        {
            if (e.Player == null)
                return;

            if (Arenas.Count == 0)
                return;

            if (!e.Player.IsLoggedIn)
                return;

            if (Arenas.Any(x => x.Players.Any(j => j.UserID == e.Player.User.ID)))
            {
                foreach (string command in Config.BannedCommands)
                {
                    if (e.CommandName == command)
                    {
                        e.Player.SendErrorMessage("[TerraPvP] You can't use that command right now!");
                        e.Handled = true;
                    }
                }
            }
        }

        private void OnPvPToggle(object sender, GetDataHandlers.TogglePvpEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];

            if (ply == null)
            {
                return;
            }

            if (Arenas.Any(x => x.Players.Any(j => j.UserID == ply.User.ID)))
            {
                Main.player[ply.Index].hostile = true;
                NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, Terraria.Localization.NetworkText.Empty, ply.Index);
                ply.SendWarningMessage("[TerraPvP] Your PvP has been forced on, don't try to turn it off!");
                args.Handled = true;
            }
        }

        private void OnPlayerDeath(object sender, GetDataHandlers.KillMeEventArgs args)
        {
            var ply = TShock.Players[args.PlayerId];

            if (ply == null)
            {
                return;
            }

            if (Arenas.Count == 0)
                return;

            if (Arenas.Any(x => x.Players.Any(j => j.UserID == ply.User.ID)))
            {
                Arenas.Find(x => x.Players.Any(j => j.UserID == ply.User.ID)).PlayerDeath(ply.User.ID);
            }
        }

        private void OnInitialize(EventArgs args)
        {
            DB.Connect();
            Commands.ChatCommands.Add(new Command("terrapvp.join", JoinArena, "tjoin")
            {
                HelpText = "Usage: /tjoin <arena id> [Use /tlist to see the arenas]"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.join", LeaveArena, "tleave")
            {
                HelpText = "Usage: /tleave [Leaves the queue]"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.join", ListPlayers, "tlistp")
            {
                HelpText = "Usage: /tlistp [Returns a list with the users in the arena]"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.stats", GetStats, "tstats")
            {
                HelpText = "Usage: /tstats <name> or /tstats"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", CreateArena, "tcreate")
            {
                HelpText = "Usage: /tcreate <arena name>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", AddArenaSppawn, "taddspawn")
            {
                HelpText = "Usage: /taddspawn <arena name>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", SaveArena, "tsave")
            {
                HelpText = "Usage: /tsave <arena name>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.join", ListArenas, "tlist")
            {
                HelpText = "Usage: /tlist"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.arena", DeleteArena, "tdel")
            {
                HelpText = "Usage: /tdel <arena name>"
            });
            Commands.ChatCommands.Add(new Command("terrapvp.stats", TopTen, "ttopten")
            {
                HelpText = "Usage: /ttopten"
            });
        }

        private void OnPostInitialize(EventArgs e)
        {
            DB.LoadArenas();
        }
        #endregion

        #region Commands
        private void CreateArena(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}tcreate <arena name>", Commands.Specifier);
                return;
            }

            string[] args = e.Parameters.ToArray();
            Arenas.Add(new Arena(args[0]));
            e.Player.SendSuccessMessage(String.Format("[TerraPvP] Succesfully created arena '{0}'", args[0]));
        }

        private void AddArenaSppawn(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}taddspawn <region name>", Commands.Specifier);
                return;
            }

            string[] args = e.Parameters.ToArray();

            if (Arenas.Any(x => x.RegionName == args[0]))
            {
                Arena arena = Arenas.Find(x => x.RegionName == args[0]);
                
                var positionX = e.Player.TileX;
                var positionY = e.Player.TileY;
                arena.SpawnPoints.Add(new Spawn(positionX, positionY));
                e.Player.SendSuccessMessage($"[TerraPvP] Added spawnpoint for arena {arena.RegionName} at {positionX}, {positionY}");
                if (arena.SpawnPoints.Count == 2)
                    e.Player.SendInfoMessage("[TerraPvP] You added 2 or more spawns, you may now save (/tsave) or add more.");
            }
            else
                e.Player.SendErrorMessage("[TerraPvP] A arena with that name doesn't exist");
        }

        private void SaveArena(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}tsave <arena name>", Commands.Specifier);
                return;
            }

            string[] args = e.Parameters.ToArray();

            if (Arenas.Any(x => x.RegionName == args[0]))
            {
                Arena arena = Arenas.Find(x => x.RegionName == args[0]);

                if (arena.SpawnPoints.Count < 2)
                {
                    e.Player.SendErrorMessage("[TerraPvP] You need atleast 2 spawnpoints to create a arena!");
                    return;
                }
                else
                {
                    arena.IsValid = true;
                    arena.MaxPlayers = arena.SpawnPoints.Count;
                    DB.AddArena(arena);
                    arena.ID = DB.GetArenaID(arena.RegionName);
                    e.Player.SendSuccessMessage(String.Format("[TerraPvP] Succesfully saved arena '{0}'", args[0]));
                }
            }
            else
                e.Player.SendInfoMessage("[TerraPvP] A arena with that name doesn't exist.");
        }

        private void DeleteArena(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count > 0)
            {
                string[] args = e.Parameters.ToArray();

                if (Arenas.Any(x => x.RegionName == args[0]))
                {
                    var arena = Arenas.Find(x => x.RegionName == args[0]);
                    DB.DeleteArena(arena);
                    Arenas.RemoveAll(x => x.RegionName == args[0]);
                    e.Player.SendSuccessMessage("[TerraPvP] Arena deleted.");
                }
                else
                    e.Player.SendErrorMessage("[TerraPvP] A arena with that name doesn't exist.");
            }
            else
            {
                e.Player.SendInfoMessage("Usage: /tdel <arena name>");
            }
        }

        private void ListArenas(CommandArgs e)
        {
            if (e.Player == null)
                return;

            StringBuilder arena_list = new StringBuilder();

            Arenas.FindAll(x => x.IsValid).ForEach(x => arena_list.Append($"{x.ID} - ({x.Players.Count}/{x.SpawnPoints.Count}) - {x.RegionName}\n"));

            if (!string.IsNullOrWhiteSpace(arena_list.ToString()))
                e.Player.SendSuccessMessage("[TerraPvP] Arenas:\nID - Players - Name\n" + arena_list.ToString());
            else
                e.Player.SendErrorMessage("[TerraPvP] There are no arenas");
        }

        private void TopTen(CommandArgs e)
        {
            if (e.Player == null)
                return;

            int i = 1;
            e.Player.SendSuccessMessage("[TerraPvP] Top 10 ladder:");
            foreach (PRank player in DB.TopTen())
            {
                e.Player.SendSuccessMessage($"{i}. Name: {player.Name} | Rank: {player.Rank} | MMR: {player.MMR}");
                i++;
            }
        }

        private void GetStats(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if (e.Parameters.Count == 0)
            {
                if (!e.Player.IsLoggedIn)
                    return;

                PRank player = DB.LoadPlayer(e.Player.Name);

                if(player == null)
                {
                    e.Player.SendErrorMessage($"[TerraPvP] There was an error.");
                    return;
                }

                e.Player.SendSuccessMessage($"[TerraPvP] {e.Player.Name}'s stats:");
                e.Player.SendSuccessMessage("Rank: " + player.Rank);
                e.Player.SendSuccessMessage("MMR: " + player.MMR);
            }
            else
            {
                var name = e.Parameters[0];

                if (DB.PlayerExist(name))
                {
                    PRank player = DB.LoadPlayer(name);
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

        void JoinArena(CommandArgs e)
        {
            if (Arenas.Count == 0)
            {
                e.Player.SendErrorMessage("[TerraPvP] There are no arenas to join.");
                return;
            }

            if (e.Player == null)
                return;

            if (!e.Player.IsLoggedIn)
                return;

            if (e.Parameters.Count != 0)
            {
                string[] args = e.Parameters.ToArray();

                int ParseTest;
                if (!int.TryParse(args[0], out ParseTest))
                {
                    e.Player.SendErrorMessage("[TerraPvP] Usage: /tjoin <arena id> [Remember the id is a number]");
                    return;
                }
                    

                PRank player = DB.LoadPlayer(e.Player.User.ID);

                if(player == null)
                {
                    e.Player.SendErrorMessage("[TerraPvP] There was an error loading your player from the database.");
                    return;
                }

                if (Arenas.Any(x => x.Players.Any(j => j.UserID == player.UserID)))
                    e.Player.SendErrorMessage("[TerraPvP] You are already in a arena!");
                else
                {
                    Arena arena = Arenas.Find(x => x.AlreadyStarted == false && x.Players.Count < x.MaxPlayers && x.ID == int.Parse(args[0]));

                    if(arena == null)
                    {
                        e.Player.SendErrorMessage("[TerraPvP] Arena not found");
                        return;
                    }

                    if (!arena.AddPlayer(player))
                        e.Player.SendErrorMessage("[TerraPvP] There was a error or the arena is full.");
                }
            }
            else
                e.Player.SendErrorMessage("[TerraPvp] Usage: /tjoin <arena id>");
        }

        private void LeaveArena(CommandArgs e)
        {
            if (e.Player == null)
                return;

            if(Arenas.Count == 0)
            {
                e.Player.SendErrorMessage("[TerraPvP] You are not in a arena");
            }

            if (Arenas.Any(x => x.Players.Any(j => j.UserID == e.Player.User.ID)))
            {
                Arena arena = Arenas.Find(x => x.Players.Any(j => j.UserID == e.Player.User.ID));
                arena.DeletePlayer(DB.GetPlayer(e.Player.User.ID));
                e.Player.SendInfoMessage("[TerraPvP] You left the arena");
                arena.Broadcast(String.Format("[TerraPvP] {0} left the arena.", e.Player.Name));
            }
            else
            {
                e.Player.SendErrorMessage("[TerraPvP] You are not in a arena");
            }
        }

        private void ListPlayers(CommandArgs e)
        {
            if (e.Player == null)
                return;

            StringBuilder playerlist = new StringBuilder();

            if(Arenas.Any(x => x.Players.Any(j => j.UserID == e.Player.User.ID)))
            {
                foreach(PRank ply in Arenas.Find(x => x.Players.Any(j => j.UserID == e.Player.User.ID)).Players)
                {
                    playerlist.Append(" " + ply.Name);
                }
                e.Player.SendSuccessMessage("[TerraPvP] Users in this Arena:" + playerlist.ToString());
            }
            else
            {
                e.Player.SendErrorMessage("[TerraPvP] You are not in a arena");
            }
        }
        #endregion
    }
}
