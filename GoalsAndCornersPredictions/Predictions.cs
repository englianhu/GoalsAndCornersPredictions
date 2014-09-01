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
    public class Predictions : ServiceCommon
    {
        private static readonly log4net.ILog log
         = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Configuration cfg;

        public Predictions(Configuration cfg)
        {
            this.cfg = cfg;
        }

        public string execute(string gameId, int depth)
        {
            try
            {
                string path = "";

                var gameDetails = GetGameDetails(gameId);
                
                ArrayList games = new ArrayList();

                string leagueIds = GetLeagueIDs(gameId, depth);

                //get goals, corners from all games in a league_id
                var stop2 = new Stopwatch();
                stop2.Start();

                string sql = "SELECT t1.name, t2.name, MAX(s.hg), MAX(s.ag), MAX(s.hco), MAX(s.aco)"
                + " FROM statistics s, games g, teams t1, teams t2"
                + " WHERE g.league_id IN ( "
                + leagueIds
                + " ) AND s.game_id = g.id AND t1.id = g.team1 AND t2.id = g.team2"
                + " GROUP BY s.game_id, t1.name, t2.name;";

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
                stop2.Stop();
                log.Info("getting gamess from DB: " + stop2.Elapsed.TotalSeconds + " seconds");

                String league_day = cfg.generateDay(gameId);

                path = Path.Combine(GlobalData.Instance.PredictionDir, league_day);

                if (Directory.Exists(path) == false)
                {
                    Directory.CreateDirectory(path);
                    cfg.createInputFile.Create(path, games);
                    
                    cfg.rExecutor.Execute(path);
                }

                PredRow row = new PredRow();

                row.gameId = gameId;
             
                GetResults result = new GetResults(cfg.predReader, path, gameDetails);

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
