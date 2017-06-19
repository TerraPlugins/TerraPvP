using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI.DB;
using MySql.Data.MySqlClient;
using System.Data;
using TShockAPI;
using Mono.Data.Sqlite;
using System.IO;
using System.Text.RegularExpressions;

namespace TerraPvP
{
    public class DB
    {
        private static IDbConnection db;

        public static void Connect()
        {
            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] dbHost = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            dbHost[0],
                            dbHost.Length == 1 ? "3306" : dbHost[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)

                    };
                    break;

                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "TerraPvP.sqlite");
                    db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;

            }

            SqlTableCreator sqlcreator = new SqlTableCreator(db,
                db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            sqlcreator.EnsureTableStructure(new SqlTable("TerraPvP_Users",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, Unique = true, Length = 7, AutoIncrement = true },
                new SqlColumn("UserID", MySqlDbType.Int32) { Length = 6 },
                new SqlColumn("Name", MySqlDbType.String),
                new SqlColumn("MMR", MySqlDbType.Int32) { DefaultValue = "1500" },
                new SqlColumn("Rank", MySqlDbType.String)
                ));

            sqlcreator.EnsureTableStructure(new SqlTable("TerraPvP_Arenas",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("Region", MySqlDbType.Text) { Unique = true },
                new SqlColumn("Spawns", MySqlDbType.String)
                ));
        }

        public static PRank GetPlayer(int id)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM TerraPvP_Users WHERE UserID=@0", id))
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

        public static void AddArena(Arena arena)
        {

            StringBuilder f = new StringBuilder();

            for (int i = 0; i < arena.SpawnPoints.Count; i++)
            {
                f.Append(String.Format("({0}, {1})", arena.SpawnPoints[i].x, arena.SpawnPoints[i].x));
            }

            db.Query("INSERT INTO TerraPvP_Arenas (Region, Spawns) VALUES (@0, @1)",
                arena.RegionName,
                f.ToString()
                );
        }

        public static void DeleteArena(Arena arena)
        {

            db.Query("DELETE FROM TerraPvP_Arenas WHERE Region = @0", arena.RegionName);
        }

        public static void LoadArenas()
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM TerraPvP_Arenas"))
            {
                while (result.Read())
                {
                    int id = result.Get<int>("ID");
                    string region = result.Get<string>("Region");
                    string raw_spawns = result.Get<string>("Spawns");

                    const string pattern = @"\(([0-9]+), ([0-9]+)\)+";

                    MatchCollection matches = Regex.Matches(raw_spawns, pattern);
                    List<Spawn> spawns = new List<Spawn>();

                    foreach(Match match in matches)
                    {
                        spawns.Add(new Spawn(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)));
                    }

                    TerraPvP.Arenas.Add(new Arena(region, id, spawns.ToArray()));
                }
            }
        }

        public static int GetArenaID(string Region)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM TerraPvP_Arenas WHERE Region=@0", Region))
            {
                while (result.Read())
                {
                    return result.Get<int>("ID");
                }
            }
            return -1;
        }

        public static List<PRank> TopTen()
        {
            List<PRank> top = new List<PRank>();
            using (QueryResult result = db.QueryReader("SELECT * FROM TerraPvP_Users ORDER BY MMR DESC LIMIT 10"))
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

        public static PRank LoadPlayer(string name)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM TerraPvP_Users WHERE Name=@0", name))
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

        public static PRank LoadPlayer(int id)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM TerraPvP_Users WHERE UserID=@0", id))
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

        public static bool PlayerExist(int userid)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM TerraPvP_Users WHERE UserID=@0", userid))
            {
                if (result.Read())
                {
                    return true;
                }
            }
            return false;
        }

        public static bool PlayerExist(string name)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM TerraPvP_Users WHERE Name=@0", name))
            {
                if (result.Read())
                {
                    return true;
                }
            }
            return false;
        }

        public static void UpdatePlayer(PRank player)
        {
            db.Query("UPDATE TerraPvP_Users SET MMR=@0, Rank=@1 WHERE UserID=@2",
                player.MMR,
                player.Rank,
                player.UserID
                );
        }

        public static void AddPlayer(PRank player)
        {
            db.Query("INSERT INTO TerraPvP_Users (UserID, Name, MMR, Rank) VALUES (@0, @1, @2, @3)",
                player.UserID,
                player.Name,
                player.MMR,
                player.Rank
                );
        }

    }
}
