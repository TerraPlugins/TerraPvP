using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerraPvP
{
    public class PRank
    {
        public string Name { get; set; }
        public int UserID { get; set; }
        public int MMR { get; set; }
        public string Rank { get; set; }
        public int ArenaScore { get; set; }
        public bool IsAlive { get; set; }

        public bool Winner = false;

        public PRank(int userId, string name, int mmr, string rank)
        {
            Name = name;
            UserID = userId;
            MMR = mmr;
            Rank = rank;
            IsAlive = true;
        }

        public int Distance(PRank other)
        {
            return Math.Abs(MMR - other.MMR);
        }
    }
}
