using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerraPvP
{
    public class ConfigFile
    {
        public class Rank
        {
            public string name { get; set; }
            public int mmr { get; set; }
        }

        public int BonusWinMMR = 25;
        public int DefaultMMR = 1500;
        public int SecondsToStart = 60;
        public int MinPlayersToCountDown = 3;

        public List<Rank> RankList = new List<Rank>();

        public List<string> BannedCommands = new List<string>();

        public static ConfigFile Read(string path)
        {
            if (!File.Exists(path))
            {
                ConfigFile config = new ConfigFile();

                config.RankList.Add(new Rank
                {
                    name = "copper",
                    mmr = 0
                });
                config.RankList.Add(new Rank
                {
                    name = "silver",
                    mmr = 1700
                });
                config.RankList.Add(new Rank
                {
                    name = "gold",
                    mmr = 2000
                });
                config.RankList.Add(new Rank
                {
                    name = "adamantite",
                    mmr = 2400
                });
                config.RankList.Add(new Rank
                {
                    name = "luminite",
                    mmr = 2800
                });

                config.BannedCommands.Add("tp");
                config.BannedCommands.Add("warp");
                config.BannedCommands.Add("spawn");

                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
                return config;
            }

            return JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(path));
        }
    }
}

