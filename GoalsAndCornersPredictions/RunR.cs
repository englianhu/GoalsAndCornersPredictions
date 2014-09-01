using Db;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoalsAndCornersPredictions
{
    public class RunR
    {
        private static readonly log4net.ILog log
       = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Configuration cfg = null;

        public RunR(Configuration cfg)
        {
            this.cfg = cfg;
        }

        private string generateDay(string gameId)
        {
            var uuid = System.Guid.NewGuid().ToString();
            var dir = gameId + "_" + uuid.Substring(0, 8) + cfg.dayJoin + DateTime.Today.ToString("ddMMyyyy");
            return dir;
        }

        public virtual string getPath(string gameId)
        {
            String league_day = generateDay(gameId);
            string path = Path.Combine(GlobalData.Instance.PredictionDir, league_day);
            return path;
        }

        public virtual void Run(string gameId, int depth)
        {
            ArrayList games = new ArrayList();
            string leagueIds = cfg.GetLeagueIDs(gameId, depth);

            //get goals, corners from all games in a league_id
            var stop2 = new Stopwatch();
            stop2.Start();

            string sql = "SELECT t1.name, t2.name, MAX(s.hg), MAX(s.ag), MAX(s.hco), MAX(s.aco)"
            + " FROM statistics s, games g, teams t1, teams t2"
            + " WHERE g.league_id IN ( "
            + leagueIds
            + " ) AND s.game_id = g.id AND t1.id = g.team1 AND t2.id = g.team2"
            + " GROUP BY s.game_id, t1.name, t2.name;";

            cfg.dbStuff.RunSQL(sql,
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

            string path = getPath(gameId);

            Directory.CreateDirectory(path);
            cfg.createInputFile.Create(path, games);

            cfg.rExecutor.Execute(path);
        }
    }
}
