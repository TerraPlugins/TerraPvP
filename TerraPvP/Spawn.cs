using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerraPvP
{
    public class Spawn
    {
        public int x { get; set; }
        public int y { get; set; }

        public Spawn(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
}
