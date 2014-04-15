using System;
using System.Collections.Generic;
using System.Linq;
using System.Configuration;

using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using Newtonsoft.Json;
using System.Collections;
using Db;
using System.IO;
using System.Diagnostics;
using System.Data.Common;

namespace GoalsAndCornersPredictions
{

    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        [WebGet]
        string GetGoalsPrediction(string gameId);

        [WebGet]
        string GetCornersPrediction(string gameId);
    }


    public class GlobalData
    {
        private static GlobalData instance;
        public Database dbStuff { get; set; }
        public string PredictionDir { get; set; }
        public string RexecutableFullPath { get; set; }
        public string ScriptFullPath { get; set; }

        private GlobalData() { }

        public static GlobalData Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GlobalData();
                }
                return instance;
            }
        }
    }

    public class PredRow
    {
        public string gameId { get; set; }
        public string winHome { get; set; }
        public string winAway { get; set; }
        public string likelyScore { get; set; }
        public string likelyProb { get; set; }
    };

    public class GameResult
    {
        public string homeTeam;
        public string awayTeam;
        public string homeGoals;
        public string awayGoals;
        public string homeCorners;
        public string awayCorners;
    };

    public class CreateInputFileGoals
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public CreateInputFileGoals(String workingDirectory, ArrayList games, string team1name, string team2Name)
        {
            String file_name = Path.Combine(workingDirectory, "input.txt");
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(file_name, false))
            {
                // write header
                file.WriteLine("HomeTeam,AwayTeam,HomeGoals,AwayGoals,HomeCorners,AwayCorners");
                foreach (GameResult game in games)
                {
                    
                        String line = game.homeTeam + "," + game.awayTeam + "," + game.homeGoals + "," + game.awayGoals + "," + 0 + "," + 0;
                        file.WriteLine(line);
                }
            }
        }
    };

    public class CreateInputFileCorners
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public CreateInputFileCorners(String workingDirectory, ArrayList games, string team1name, string team2Name)
        {
            String file_name = Path.Combine(workingDirectory, "input.txt");
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(file_name, false))
            {
                // write header
                file.WriteLine("HomeTeam,AwayTeam,HomeGoals,AwayGoals,HomeCorners,AwayCorners");
                foreach (GameResult game in games)
                {
                      String line = game.homeTeam + "," + game.awayTeam + "," + game.homeCorners + "," + game.awayCorners + "," + 0 + "," + 0;
                      file.WriteLine(line);
                }
            }
        }
    };

    public class ExecuteR
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        bool rHasExited = false;
        public ExecuteR(String workingDirectory)
        {
            log.Debug("Running process in directory: " + workingDirectory);
            //TODO: either use PATH env. or configurable full path
            ProcessStartInfo si = new ProcessStartInfo();
            si.FileName = GlobalData.Instance.RexecutableFullPath;
            //si.Arguments = "CMD BATCH " + GlobalData.Instance.ScriptFullPath;
            si.Arguments = @"CMD BATCH C:\Users\daddy\Documents\GitHub\GoalsAndCornersPredictions\GoalsAndCornersPredictions\script.R";
            si.WorkingDirectory = workingDirectory;
            si.UseShellExecute = true;
            si.CreateNoWindow = true;

            try
            {
                int maxRWaitTime = 300;
                rHasExited = false;
                using (Process p = new Process())
                {
                    p.StartInfo = si;
                    p.Exited += processExited;
                    p.EnableRaisingEvents = true;
                    p.Start();

                    int waitedTime = 0;
                    while (rHasExited == false || waitedTime < maxRWaitTime)
                    {
                        log.Debug("Waiting " + waitedTime + " seconds for R process to finish");
                        System.Threading.Thread.Sleep(5000);
                        waitedTime += 5;
                    }
                }
            }
            catch (InvalidOperationException e)
            {
                log.Error("Error executing process exception: " + e);
            }
            catch (Exception e)
            {
                log.Error("Error executing process exception: " + e);
            }
        }

        void processExited(object sender, EventArgs e)
        {
            rHasExited = true;
        }
    };

    public class ProbabilityHolder
    {
        public int team1Id;
        public int team2Id;
        public string probability;
    }


    public class ReadPrediction
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public List<ProbabilityHolder> data = new List<ProbabilityHolder>();

        public ReadPrediction(Database dbStuff, String path, String file_name)
        {
            String full_name = Path.Combine(path, file_name);
            var reader = new StreamReader(File.OpenRead(full_name));

            //read header which is:
            //Teams, TeamName1, TeamName2
            var header = reader.ReadLine();
            var team_names = header.Split(';').ToList();
            team_names.RemoveAt(0);

            log.Debug(team_names);

            //store team with their team id not team name
            List<int> team_ids = new List<int>();
            team_ids.Add(0);

            foreach (String team_name in team_names)
            {
                dbStuff.RunSQL("select id from teams where name = '" + team_name + "';",
                    (dr) =>
                    {
                        team_ids.Add(int.Parse(dr[0].ToString()));
                    }
                );
            }

            int j = 1;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                String[] values = line.Split(';');

                for (int i = 1; i < values.Length; i++)
                {
                    data.Add(new ProbabilityHolder() { team1Id = team_ids[j], team2Id = team_ids[i], probability = values[i] });
                }
                j++;
            }
        }
    };

    public class Service : IService
    {
        private static readonly log4net.ILog log
           = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        Database dbStuff;
        Service()
        {
            GlobalData gd = GlobalData.Instance;
            dbStuff = gd.dbStuff;
        }

        public string GetCornersPrediction(string gameId)
        {
            var gameDetails = GetGameDetails(gameId);
            var team1Name = gameDetails.Split('|').ElementAt(0);
            var team2Name = gameDetails.Split('|').ElementAt(1);

            log.Info("GetCornersPrediction is being invoked for: " + gameDetails);

            ArrayList games = new ArrayList();
            string leagueIds = GetLeagueString(gameId);

            log.Info("League Search: " + leagueIds);

            //get goals, corners from all games in a league_id
            dbStuff.RunSQL("SELECT t1.name, t2.name, MAX(s.hg), MAX(s.ag), MAX(s.hco), MAX(s.aco)"
            + " FROM statistics s, games g, teams t1, teams t2"
            + " WHERE g.league_id in ( "
            + leagueIds
            + " ) AND s.game_id = g.id AND t1.id = g.team1 AND t2.id = g.team2 GROUP BY s.game_id, t1.name, t2.name;",
                (dr) =>
                {
                    GameResult res = new GameResult();
                    res.homeTeam = dr[0].ToString();
                    res.awayTeam = dr[1].ToString();
                    res.homeGoals = dr[2].ToString();
                    res.awayGoals = dr[3].ToString();
                    res.homeCorners = dr[4].ToString();
                    res.awayCorners = dr[5].ToString();
                    games.Add(res);
                }
            );

            log.Debug("Number of games : " + games.Count);

            String league_day = leagueIds.Replace(",", "_") + "_c_" + DateTime.Today.ToString("ddMMyyyy");
            String path = Path.Combine(GlobalData.Instance.PredictionDir, league_day);

            //create working directory
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.Delete(path, true);
                Directory.CreateDirectory(path);
            }

            var file = new CreateInputFileCorners(path, games, team1Name, team2Name);
            ExecuteR r = new ExecuteR(path);

            //read data back
            ReadPrediction winH = new ReadPrediction(dbStuff, path, "winH.csv");
            ReadPrediction winA = new ReadPrediction(dbStuff, path, "winA.csv");
            ReadPrediction likelyScore = new ReadPrediction(dbStuff, path, "likelyScore.csv");
            ReadPrediction likelyProb = new ReadPrediction(dbStuff, path, "likelyProb.csv");

            int team1 = -1;
            int team2 = -1;

            dbStuff.RunSQL("SELECT team1, team2 FROM games WHERE id = " + gameId + ";",
                (dr) =>
                {
                    team1 = int.Parse(dr[0].ToString());
                    team2 = int.Parse(dr[1].ToString());
                }
            );

            log.Info("Game: " + gameId + " team1: " + team1 + " team2: " + team2);

            PredRow row = new PredRow();
            try
            {
                row.gameId = gameId;
                row.winHome = winH.data.Where(x => x.team1Id == team1 && x.team2Id == team2).First().probability;
                row.winAway = winA.data.Where(x => x.team1Id == team1 && x.team2Id == team2).First().probability;
                row.likelyProb = likelyProb.data.Where(x => x.team1Id == team1 && x.team2Id == team2).First().probability;
                row.likelyScore = likelyScore.data.Where(x => x.team1Id == team1 && x.team2Id == team2).First().probability;
            }
            catch (Exception e)
            {
                log.Warn("Exception caught while getting match predictions for game: " + gameId + " exception: " + e);
            }

            return JsonConvert.SerializeObject(row, Formatting.Indented);
        }

        public string GetGameDetails(string id)
        {
            var sql = "select g1.id, t1.name, t2.name, l1.name, g1.kodate from games g1 join teams t1 on g1.team1 = t1.id join teams t2 on g1.team2 = t2.id join leagues l1 on l1.id = g1.league_id where g1.id =" + id;

            string retVal = "";

            dbStuff.RunSQL(sql,
              (dr) =>
              {
                  retVal = dr[1].ToString() + "|" + dr[2].ToString() + "|" + dr[3].ToString() + "|" + dr[4].ToString();
              });

            return retVal;
        }

        private string GetLeagueString(string gameId)
        {
            string leagueIds = null;
            string team1Id = "";
            string team2Id = "";

            dbStuff.RunSQL("select team1, team2 from games where id = " + gameId,
               (dr) =>
               {
                   team1Id = dr[0].ToString();
                   team2Id = dr[1].ToString();
               });

            string team1LeaguesSQL = "select distinct league_id from games where team1 = " + team1Id + " or team2 = " + team1Id + " and league_id != -1";
            string team2LeaguesSQL = "select distinct league_id from games where team1 = " + team2Id + " or team2 = " + team2Id + " and league_id != -1";

            var team1Leagues = dbStuff.OneColumnQuery(team1LeaguesSQL);
            var team2Leagues = dbStuff.OneColumnQuery(team2LeaguesSQL);

            var common = team1Leagues.Intersect(team2Leagues);

            if (common.Count() != 0)
            {
                leagueIds = String.Join(",", common);
            }
            else
            {
                //get league id
                dbStuff.RunSQL("SELECT league_id FROM games WHERE id = " + gameId + ";",
                    (dr) =>
                    {
                        leagueIds = dr[0].ToString();
                    }
                );
            }
            return leagueIds;
        }

        public string GetGoalsPrediction(string gameId)
        {
            var gameDetails = GetGameDetails(gameId);
            var team1Name = gameDetails.Split('|').ElementAt(0);
            var team2Name = gameDetails.Split('|').ElementAt(1);

            log.Info("GetGoalsPrediction is being invoked for: " + gameDetails);
            ArrayList games = new ArrayList();

            string leagueIds = GetLeagueString(gameId);
            log.Info("League Search: " + leagueIds);

            string sql = "SELECT t1.name, t2.name, MAX(s.hg), MAX(s.ag), MAX(s.hco), MAX(s.aco)"
            + " FROM statistics s, games g, teams t1, teams t2"
            + " WHERE g.league_id in ( "
            + leagueIds
            + " ) AND s.game_id = g.id AND t1.id = g.team1 AND t2.id = g.team2 GROUP BY s.game_id, t1.name, t2.name;";

            //get goals, corners from all games in a league_id
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

            String league_day = leagueIds.Replace(",", "_") + "_g_" + DateTime.Today.ToString("ddMMyyyy");
            String path = Path.Combine(GlobalData.Instance.PredictionDir, league_day);

            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.Delete(path, true);
                Directory.CreateDirectory(path);
            }

            var file = new CreateInputFileGoals(path, games, team1Name, team2Name);
            ExecuteR r = new ExecuteR(path);

            //read data back
            ReadPrediction winH = new ReadPrediction(dbStuff, path, "winH.csv");
            ReadPrediction winA = new ReadPrediction(dbStuff, path, "winA.csv");
            ReadPrediction likelyScore = new ReadPrediction(dbStuff, path, "likelyScore.csv");
            ReadPrediction likelyProb = new ReadPrediction(dbStuff, path, "likelyProb.csv");

            int team1 = -1;
            int team2 = -1;
            dbStuff.RunSQL("SELECT team1, team2 FROM games WHERE id = " + gameId + ";",
                (dr) =>
                {
                    team1 = int.Parse(dr[0].ToString());
                    team2 = int.Parse(dr[1].ToString());
                }
            );

            log.Info("Game: " + gameId + " team1: " + team1 + " team2: " + team2);

            PredRow row = new PredRow();
            try
            {
                row.gameId = gameId;
                var winHomeResults = winH.data.Where(x => x.team1Id == team1 && x.team2Id == team2);
                row.winHome = winHomeResults.Count() != 0 ? winHomeResults.First().probability : "-1";

                var winAwayResults = winA.data.Where(x => x.team1Id == team1 && x.team2Id == team2);
                row.winAway = winAwayResults.Count() != 0 ? winAwayResults.First().probability : "-1";

                var likelyProbResults = likelyProb.data.Where(x => x.team1Id == team1 && x.team2Id == team2);
                row.likelyProb = likelyProbResults.Count() != 0 ? likelyProbResults.First().probability : "-1";

                var likelyScoreResults = likelyScore.data.Where(x => x.team1Id == team1 && x.team2Id == team2);
                row.likelyScore = likelyScoreResults.Count() != 0 ? likelyScoreResults.First().probability : "-1";

                if (row.winHome == "-1") { log.Warn("WARNING! Failed to calulate win home probabilty for " + gameId); }
                if (row.winAway == "-1") { log.Warn("WARNING! Failed to calulate win away probabilty for " + gameId); }
                if (row.likelyProb == "-1") { log.Warn("WARNING! Failed to calulate likely probabilty for " + gameId); }
                if (row.likelyScore == "-1") { log.Warn("WARNING! Failed to calulate likely score for " + gameId); }
            }
            catch (Exception e)
            {
                log.Warn("Exception caught while getting match predictions for game: " + gameId + " exception: " + e);
            }

            return JsonConvert.SerializeObject(row, Formatting.Indented);
        }
    }

    class Program
    {
        private static readonly log4net.ILog log
           = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static bool alive = true;

        static void Main(string[] args)
        {
            string uriString;
            DbCreator dbCreator = null;

            switch (ConfigurationManager.AppSettings["dbtype"])
            {
                case "pg":
                    dbCreator = new NpgsqlCreator();
                    break;
                case "sqlite":
                    dbCreator = new SQLiteCreator();
                    break;
                default:
                    dbCreator = new SQLiteCreator();
                    break;
            }

            Database db = new Database(dbCreator);
            db.Connect(ConfigurationManager.AppSettings["dbConnectionString"]);

            GlobalData gd = GlobalData.Instance;
            gd.dbStuff = db;
            gd.PredictionDir = ConfigurationManager.AppSettings["PredictionDir"];
            gd.RexecutableFullPath = ConfigurationManager.AppSettings["RexecutableFullPath"];
            gd.ScriptFullPath = ConfigurationManager.AppSettings["ScriptFullPath"];

            //create working directory
            if (Directory.Exists(gd.PredictionDir) == false)
            {
                Directory.CreateDirectory(gd.PredictionDir);
            }

            uriString = "http://" + ConfigurationManager.AppSettings["uriHostPort"];

            log.Info("Creating Webservice on: " + uriString);

            WebServiceHost host = new WebServiceHost(typeof(Service), new Uri(uriString));

            try
            {
                ServiceEndpoint ep = host.AddServiceEndpoint(typeof(IService), new WebHttpBinding(), "");
                host.Open();
                using (ChannelFactory<IService> cf = new ChannelFactory<IService>(new WebHttpBinding(), uriString))
                {
                    cf.Endpoint.Behaviors.Add(new WebHttpBehavior());

                    IService channel = cf.CreateChannel();
                }
            }
            catch (CommunicationException cex)
            {
                log.Error("An exception occurred: " + cex.Message);
                host.Abort();
            }

            log.Info("Service is up and running");

            while (alive)
            {
                System.Threading.Thread.Sleep(2000);
            }

            host.Close();
        }
    }
}
