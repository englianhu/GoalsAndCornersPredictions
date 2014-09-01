using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GoalsAndCornersPredictions
{

    public class GoalsPredictions : ServiceCommon
    {
        private static readonly log4net.ILog log
         = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Configuration cfg;

        public GoalsPredictions(Configuration cfg)
        {
            this.cfg = cfg;
        }

        public string execute(string gameId, int depth)
        {
            try
            {
                string path = "";

                var gameDetails = GetGameDetails(gameId);
                var team1Name = gameDetails.Split('|').ElementAt(0);
                var team2Name = gameDetails.Split('|').ElementAt(1);

                ArrayList games = new ArrayList();

                string leagueIds = GetLeagueIDs(gameId, depth);

                //get goals, corners from all games in a league_id

                string sql = "SELECT t1.name, t2.name, MAX(s.hg), MAX(s.ag), MAX(s.hco), MAX(s.aco)"
                + " FROM statistics s, games g, teams t1, teams t2"
                + " WHERE g.league_id in ( "
                + leagueIds
                + " ) AND s.game_id = g.id AND t1.id = g.team1 AND t2.id = g.team2 GROUP BY s.game_id, t1.name, t2.name;";

                dbStuff.RunSQL(sql,
                    (dr) =>
                    {
                        GameResult res = new GameResult();
                        res.homeTeam = dr[0].ToString();
                        res.awayTeam = dr[1].ToString();
                        res.homeGoals = dr[2].ToString();
                        res.awayGoals = dr[3].ToString();
                        res.homeCorners = dr[4].ToString();
                        res.awayCorners = dr[4].ToString();
                        games.Add(res);
                    }
                );

                log.Debug("Number of games : " + games.Count);

                String league_day = cfg.generateDay(gameId);

                path = Path.Combine(GlobalData.Instance.PredictionDir, league_day);

                if (Directory.Exists(path) == false)
                {
                    Directory.CreateDirectory(path);
                    cfg.createInputFile.Create(path, games);
                    
                    cfg.rExecutor.Execute(path);
                }

                var stop1 = new Stopwatch();
                stop1.Start();
 
                string team1name = "";
                string team2name = "";

                string sql2 = "SELECT t1.name, t2.name"
                + " FROM games g, teams t1, teams t2"
                + " WHERE t1.id = g.team1 AND t2.id = g.team2;";

                dbStuff.RunSQL(sql2,
                    (dr) =>
                    {
                        team1name = dr[0].ToString();
                        team2name = dr[1].ToString();
                    });

                log.Info("Game: " + gameId + " team1name: '" + team1name + "' team2name: '" + team2name + "'");

                stop1.Stop();
                log.Info("getting team names from DB: " + stop1.Elapsed.TotalSeconds + " seconds");

                PredRow row = new PredRow();

                row.gameId = gameId;
             
                GetResults result = new GetResults(cfg.predReader, path, team1name, team2name);

                row.winHome = result.get("winH.csv");
                row.winAway = result.get("likelyProb.csv");
                row.likelyProb = result.get("likelyProb.csv");
                row.likelyScore = result.get("likelyScore.csv");

                var json_result = JsonConvert.SerializeObject(row, Formatting.Indented);
                log.Info("Result:");
                log.Info(json_result);

                return json_result;
            }
            catch (Exception ce)
            {
                string msg = "Exception caught: " + ce;
                log.Error(msg);
                return msg;
            }
        }
    }

}
