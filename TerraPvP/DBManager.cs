using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using TShockAPI.DB;
using MySql.Data.MySqlClient;

namespace TerraPvP
{
    public class DBManager
    {
        private IDbConnection db;

        public DBManager(IDbConnection db)
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
                new SqlColumn("spawn1_x", MySqlDbType.Int32),
                new SqlColumn("spawn1_y", MySqlDbType.Int32),
                new SqlColumn("spawn2_x", MySqlDbType.Int32),
                new SqlColumn("spawn2_y", MySqlDbType.Int32)
                ));
        }

        public PRank LoadPlayer(int userid)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM UserRanks WHERE UserID=@0", userid))
            {
                while (result.Read())
                {
                    return new PRank(
                        result.Get<int>("UserID"),
                        result.Get<string>("Name"),
                        result.Get<int>("MMR"),
                        result.Get<string>("Rank"));
                }
            }

            return null;
        }

        public PRank LoadPlayer(string name)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM UserRanks WHERE Name=@0", name))
            {
                while (result.Read())
                {
                    return new PRank(
                        result.Get<int>("UserID"),
                        result.Get<string>("Name"),
                        result.Get<int>("MMR"),
                        result.Get<string>("Rank"));
                }
            }

            return null;
        }

        public bool PlayerExist(int userid)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM UserRanks WHERE UserID=@0", userid))
            {
                if (result.Read())
                {
                    return true; // Test this
                }
            }
            return false;
        }

        public bool PlayerExist(string name)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM UserRanks WHERE Name=@0", name))
            {
                if (result.Read())
                {
                    return true; // Test this
                }
            }
            return false;
        }

        public void addArena(Arena arena)
        {
            db.Query("INSERT INTO Arenas (Region, spawn1_x, spawn1_y, spawn2_x, spawn2_y) VALUES (@0, @1, @2, @3, @4)",
                arena.regionName,
                arena.spawn1_x,
                arena.spawn1_y,
                arena.spawn2_x,
                arena.spawn2_y
                );
        }

        public void delArena(Arena arena)
        {
            db.Query("DELETE FROM Arenas WHERE Region = @0", arena.regionName);
        }

        public List<PRank> topTen()
        {
            List<PRank> top = new List<PRank>();
            using (QueryResult result = db.QueryReader("SELECT * FROM UserRanks ORDER BY MMR DESC LIMIT 10"))
            {
                while (result.Read())
                {
                    top.Add(new PRank(
                        result.Get<int>("UserID"),
                        result.Get<string>("Name"),
                        result.Get<int>("MMR"),
                        result.Get<string>("Rank")));
                }
            }

            return top;
        }

        public void addPlayer(PRank player)
        {
            db.Query("INSERT INTO UserRanks (UserID, Name, MMR, Rank) VALUES (@0, @1, @2, @3)",
                player.UserID,
                player.Name,
                player.MMR,
                player.Rank
                );
        }

        public void updatePlayer(PRank player)
        {
            db.Query("UPDATE UserRanks SET MMR=@0, Rank=@1 WHERE UserID=@2",
                player.MMR,
                player.Rank,
                player.UserID
                );
        }

        public List<Arena> LoadArenas()
        {
            List<Arena> arenas = new List<Arena>();
            using (QueryResult result = db.QueryReader("SELECT * FROM Arenas"))
            {
                while (result.Read())
                {
                    arenas.Add(new Arena(
                        result.Get<string>("Region"),
                        result.Get<int>("spawn1_x"),
                        result.Get<int>("spawn1_y"),
                        result.Get<int>("spawn2_x"),
                        result.Get<int>("spawn2_y")));
                }
            }
            return arenas;
        }
    }
}
