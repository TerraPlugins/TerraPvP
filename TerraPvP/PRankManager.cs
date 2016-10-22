using System;
using System.Collections.Generic;
using System.Data;
using TShockAPI.DB;
using MySql.Data.MySqlClient;

namespace TerraPvP
{
    public class PRankManager
    {
        private IDbConnection db;
        public List<PRank> pranks = new List<PRank>();
        private bool exists = false;

        public PRankManager(IDbConnection db)
        {
            this.db = db;

            var sqlCreator = new SqlTableCreator(db, (IQueryBuilder)new SqliteQueryCreator());
            sqlCreator.EnsureTableStructure(new SqlTable("UserRanks",
                new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
                new SqlColumn("UserID", MySqlDbType.Int32),
                new SqlColumn("Name", MySqlDbType.Text),
                new SqlColumn("MMR", MySqlDbType.Int32),
                new SqlColumn("Rank", MySqlDbType.Text)));

            using (QueryResult result = db.QueryReader("SELECT * FROM UserRanks"))
            {
                while (result.Read())
                {
                    pranks.Add(new PRank(
                        result.Get<int>("UserID"),
                        result.Get<string>("Name"),
                        result.Get<int>("MMR"),
                        result.Get<string>("Rank")));
                }
            }
        }

        public void addPlayer(PRank player)
        {
            for (int i = 0; i < pranks.Count; i++)
            {
                if (pranks[i].UserID == player.UserID)
                {
                    exists = true;
                }
            }
            
            if (exists == false)
            {
                db.Query("INSERT INTO UserRanks (UserID, Name, MMR, Rank) VALUES (@0, @1, @2, @3)",
                player.UserID,
                player.Name,
                player.MMR,
                player.Rank
                );

                pranks.Add(new PRank(
                player.UserID,
                player.Name,
                player.MMR,
                player.Rank));
            }
            exists = false;
        }

        public void updatePlayer(PRank player)
        {
            try
            {
                db.Query("UPDATE UserRanks SET MMR=@0, Rank=@1 WHERE UserID=@2",
                player.MMR,
                player.Rank,
                player.UserID
                );
                for (int i = 0; i < pranks.Count; i++)
                {
                    if (pranks[i].UserID == player.UserID)
                    {
                        pranks[i].updateMMR(player.MMR);
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
