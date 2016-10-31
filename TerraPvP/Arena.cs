using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace TerraPvP
{
    public class Arena
    {
        public string regionName { get; set; }
        public int spawn1_x { get; set; }
        public int spawn1_y { get; set; }
        public int spawn2_x { get; set; }
        public int spawn2_y { get; set; }
        public TShockAPI.DB.Region Region { get; set; }
        public bool someoneFighting { get; set; }

        public Arena(string RegionName)
        {
            regionName = RegionName;
        }

        public Arena(string RegionName, int Spawn1_x, int Spawn1_y, int Spawn2_x, int Spawn2_y)
        {
            regionName = RegionName;
            spawn1_x = Spawn1_x;
            spawn1_y = Spawn1_y;
            spawn2_x = Spawn2_x;
            spawn2_y = Spawn2_y;
            someoneFighting = false;
        }
    }
}
