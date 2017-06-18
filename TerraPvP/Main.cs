using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using System.Data;
using System.IO;
using Mono.Data.Sqlite;
using System.Reflection;

namespace TerraPvP
{
    [ApiVersion(2, 1)]
    public class TerraPvP : TerrariaPlugin
    {
        #region Plugin Info
        // What does Name => string ? -> https://goo.gl/o7WAvJ
        public override string Name => "TerraPvP";
        public override string Author => "Ryozuki";
        public override string Description => "Replace me!!!";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        #endregion

        public ConfigFile Config = new ConfigFile();

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
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
            }
            base.Dispose(disposing);
        }

        #region Hooks
        private void OnInitialize(EventArgs args)
        {
            DB.Connect();
            Commands.ChatCommands.Add(new Command("TerraPvP.help".ToLower(), CHelp, "chelp")
            {
                HelpText = "Usage: /chelp"
            });
        }

        private void OnPostInitialize(EventArgs args)
        {

        }
        #endregion

        #region Commands
        private void CHelp(CommandArgs args)
        {
            args.Player.SendInfoMessage("Author, please change me.");
        }
        #endregion
    }
}
