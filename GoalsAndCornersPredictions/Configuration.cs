using Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GoalsAndCornersPredictions
{
    public class Configuration
    {
        private static readonly log4net.ILog log 
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string dayJoin = "__";

        public R rExecutor = null;
        public CreateInputFile createInputFile = null;
        public PredictionReader predReader = null;
        public Database dbStuff = null;

        protected List<string> naughtyLeagues = null;

        public Configuration(string dayJoin, CreateInputFile createInputFile, PredictionReader reader, RExecutor r)
        {
            this.dayJoin = dayJoin;
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

        public string GetLeagueIDs(string gameId, int depth = 0)
        {
            string leagueIds = "";

            log.Info("Fetching leagues...");

            //get league id
            dbStuff.RunSQL("SELECT league_id FROM games WHERE id = " + gameId + ";",
                (dr) =>
                {
                    leagueIds = dr[0].ToString();
                }
            );

            if(naughtyLeagues.Any(x => x == leagueIds))
            {
                log.Warn("Cannot run prodiction on this league: " + leagueIds + " for game: " + gameId);
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
                log.Warn("Could not determine league for given game");
            }
            else
            {
                log.Info("League Search: " + leagueIds);
            }

            return leagueIds;
        }
    }
}
