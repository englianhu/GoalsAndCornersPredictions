using Db;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoalsAndCornersPredictions
{

    public class LeagueId
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Database dbStuff = null;
        Dictionary<string, string> data = new Dictionary<string, string>();

        public LeagueId(Database db)
        {
            dbStuff = db;

            Task t = Task.Run(() =>
            {
                log.Info("Preloading of league ids");
                dbStuff.RunSQL("SELECT id, league_id FROM games;",
                    (dr) =>
                    {
                        data.Add(dr[0].ToString(), dr[1].ToString());
                    });
                log.Info("Preloading of league ids finished");
            });
        }

        public string get(string gameId)
        {
            if (!data.ContainsKey(gameId))
            {
                string league_id = null;
                dbStuff.RunSQL("SELECT league_id FROM games WHERE id = " + gameId + ";",
                (dr) =>
                {
                    league_id = dr[0].ToString();
                });
                data[gameId] = league_id;
                return league_id;
            }
            else
            {
                return data[gameId];
            }
        }
    }

    public class CachedDb
    {
        protected Database dbStuff = null;
        LeagueId leagueIds;

        public CachedDb(Database db)
        {
            dbStuff = db;
            leagueIds = new LeagueId(db);
        }

        public List<string> OneColumnQuery(string sql)
        {
            return dbStuff.OneColumnQuery(sql);
        }

        public void RunSQL(string sql, Action<DbDataReader> a)
        {
            dbStuff.RunSQL(sql, a);
        }

        public string getLeagueId(string gameId)
        {
            lock (leagueIds)
            {
                return leagueIds.get(gameId);
            }
        }
    }


    public class Configuration
    {
        private static readonly log4net.ILog log
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private string prefix = "NoPrefix";

        public R rExecutor = null;
        public CreateInputFile createInputFile = null;
        public PredictionReader predReader = null;
        public CachedDb dbStuff = null;

        protected List<string> naughtyLeagues = null;

        public Configuration(string prefix, CreateInputFile createInputFile, PredictionReader reader, RExecutor r)
        {
            this.prefix = prefix;
            this.rExecutor = r;
            this.createInputFile = createInputFile;
            this.predReader = reader;

            GlobalData gd = GlobalData.Instance;
            dbStuff = gd.dbStuff;

            naughtyLeagues = dbStuff.OneColumnQuery("select id from leagues where name like '%Friendl%';");
            naughtyLeagues.Add("-1");

            naughtyLeagues.ForEach(x =>
            Console.WriteLine("naughty leagues --> " + x));
        }

        public GameDetails GetGameDetails(string id)
        {
            var sql = "SELECT t1.name, t2.name, l1.name, g1.kodate"
            + " FROM games g1"
            + " JOIN teams t1 ON g1.team1 = t1.id"
            + " JOIN teams t2 ON g1.team2 = t2.id"
            + " JOIN leagues l1 ON l1.id = g1.league_id"
            + " WHERE g1.id =" + id + ";";

            GameDetails gameDetails = new GameDetails();
            gameDetails.gameId = id;

            dbStuff.RunSQL(sql,
              (dr) =>
              {
                  gameDetails.team1Name = dr[0].ToString();
                  gameDetails.team2Name = dr[1].ToString();
                  gameDetails.leagueName = dr[2].ToString();
                  gameDetails.koDate = dr[3].ToString();
              });

            return gameDetails;
        }

        public string getPrefix()
        {
            return prefix;
        }

        public string GetLeagueIDs(string gameId, int depth = 0)
        {
            string leagueIds = "";

            log.Info("Fetching leagues...");

            leagueIds = dbStuff.getLeagueId(gameId);

            if (naughtyLeagues.Any(x => x == leagueIds))
            {
                log.Warn("Cannot run prediction on this league: " + leagueIds + " for game: " + gameId);
                leagueIds = "";
            }

            if (depth != 0)
            {
                string team1Id = "";
                string team2Id = "";

                dbStuff.RunSQL("select team1, team2 from games where id = " + gameId,
                   (dr) =>
                   {
                       team1Id = dr[0].ToString();
                       team2Id = dr[1].ToString();
                   });

                string team1LeaguesSQL = "select distinct league_id from games where team1 = " + team1Id + " or team2 = " + team1Id + " and league_id not in ( -1, 834 )";
                string team2LeaguesSQL = "select distinct league_id from games where team1 = " + team2Id + " or team2 = " + team2Id + " and league_id not in ( -1, 834 )";

                var team1Leagues = dbStuff.OneColumnQuery(team1LeaguesSQL);
                var team2Leagues = dbStuff.OneColumnQuery(team2LeaguesSQL);

                var common = team1Leagues.Intersect(team2Leagues).ToList();

                common.RemoveAll(x => x == leagueIds);
                common.RemoveAll(x => x == "-1");

                var newList = new List<string>() { leagueIds };
                newList.AddRange(common);

                newList.RemoveAll(x => naughtyLeagues.Contains(x));

                if (newList.Count() != 0 && depth != 0)
                {
                    var gamesCount = 0;
                    for (var i = 0; i < newList.Count(); ++i)
                    {
                        dbStuff.RunSQL("select count(*) from games where league_id = " + newList[i],
                        (dr) =>
                        {
                            gamesCount += int.Parse(dr[0].ToString());
                        });

                        if (gamesCount > depth)
                        {
                            log.Warn("Game Count too deep: " + gamesCount);
                            log.Warn("Removing league id: " + newList[i]);
                            if (i != 0)
                            {
                                newList.RemoveAt(i);
                            }
                        }
                    }

                    leagueIds = String.Join(",", newList);
                }
            }

            if (leagueIds == "")
            {
                throw new Exception("Could not determine league for given game");
            }
            else
            {
                log.Info("League Search: " + leagueIds);
            }

            return leagueIds;
        }
    }
}
