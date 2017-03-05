using System;
using System.Collections.Generic;

namespace TerraPvP
{
    public class PRank
    {
        public string Name { get; set; }
        public int UserID { get; set; }
        public int MMR { get; set; }
        public string Rank { get; set; }

        public PRank(int userId, string name, int mmr, string rank)
        {
            Name = name;
            UserID = userId;
            MMR = mmr;
            Rank = rank;
        }

        public void updateMMR(int mmr)
        {
            MMR = mmr;
        }

        public void checkRank()
        {
            try
            {
                foreach (ConfigFile.Rank rank in TerraPvP.RankList)
                {
                    if (MMR >= rank.mmr)
                    {
                        Rank = rank.name;
                    }
                    else
                    {
                        break;
                    }
                }

                TerraPvP.DbManager.updatePlayer(this);
                TerraPvP.PlayerRanks.Find(x => x.UserID == UserID).Rank = Rank;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            
        }
    }
}
