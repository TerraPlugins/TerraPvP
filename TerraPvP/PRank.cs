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
                foreach (ConfigFile.Rank rank in TerraPvP.ranklist)
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

                TerraPvP.RankManager.updatePlayer(this);
                foreach(PRank prank in TerraPvP.RankManager.pranks)
                {
                    if(prank.UserID == UserID)
                    {
                        prank.Rank = Rank;
                    }
                }
                
            }
            catch(Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            
        }
    }
}
