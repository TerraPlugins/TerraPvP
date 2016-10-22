using System;
using System.Collections.Generic;
using System.Data;
using TShockAPI.DB;
using MySql.Data.MySqlClient;
using TShockAPI;

namespace TerraPvP
{
    public class PRankManager
    {
        private IDbConnection db;
        public List<PRank> pranks = new List<PRank>();
        public List<Arena> Arenas = new List<Arena>();
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

            sqlCreator.EnsureTableStructure(new SqlTable("Arenas",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("Region", MySqlDbType.Text) { Unique = true },
                new SqlColumn("spawn1_x", MySqlDbType.Float),
                new SqlColumn("spawn1_y", MySqlDbType.Float),
                new SqlColumn("spawn2_x", MySqlDbType.Float),
                new SqlColumn("spawn2_y", MySqlDbType.Float)
                ));

            using (QueryResult result = db.QueryReader("SELECT * FROM Arenas"))
            {
                while (result.Read())
                {
                    Arenas.Add(new Arena(
                        result.Get<string>("Region"),
                        result.Get<float>("spawn1_x"),
                        result.Get<float>("spawn1_y"),
                        result.Get<float>("spawn2_x"),
                        result.Get<float>("spawn2_x")));
                }
            }

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

        public void addArena(Arena arena)
        {
            db.Query("INSERT INTO Arenas (Region, spawn1_x, spawn1_y, spawn2_x, spawn2_x) VALUES (@0, @1, @2, @3, @4)",
                arena.regionName,
                arena.spawn1_x,
                arena.spawn1_y,
                arena.spawn2_x,
                arena.spawn2_y
                );

            Arenas.Add(arena);
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
