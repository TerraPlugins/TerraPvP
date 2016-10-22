using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace TerraPvP
{
    public class ConfigFile
    {
        public class Rank
        {
            public string name { get; set; }
            public int mmr { get; set; }
        }

        public int MaxMMRDifference = 100;

        public List<Rank> ranklist = new List<Rank>();

        public List<string> banned_commands = new List<string>();

        public string PvPFinishMessage = "[TerraPvP] Type /spawn to return";

        public static ConfigFile Read(string path)
        {
            if (!File.Exists(path))
            {
                ConfigFile config = new ConfigFile();

                config.ranklist.Add(new Rank
                {
                    name = "copper",
                    mmr = 0
                });
                config.ranklist.Add(new Rank
                {
                    name = "silver",
                    mmr = 1700
                });
                config.ranklist.Add(new Rank
                {
                    name = "gold",
                    mmr = 2000
                });
                config.ranklist.Add(new Rank
                {
                    name = "adamantite",
                    mmr = 2400
                });
                config.ranklist.Add(new Rank
                {
                    name = "luminite",
                    mmr = 2800
                });

                config.banned_commands.Add("tp");
                config.banned_commands.Add("warp");
                config.banned_commands.Add("spawn");

                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
                return config;
            }

            return JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(path));
        }
    }
}
