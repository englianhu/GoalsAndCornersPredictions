using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using Newtonsoft.Json;
using System.Collections;
using Db;
using System.IO;
using System.Diagnostics;

namespace GoalsAndCornersPredictions
{

    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        [WebGet]
        string GetGoalsAndCornersPred(string game_id);
    }


    public class GlobalData
    {
        private static GlobalData instance;
        public Database dbStuff { get; set; }
        public string predictDir { get; set; }

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

    public class CreateInputFile
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public CreateInputFile(String workingDirectory, ArrayList games)
        {
            String file_name = Path.Combine(workingDirectory, "input.txt");
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(file_name, false))
            {
                // write header
                file.WriteLine("HomeTeam,AwayTeam,HomeGoals,AwayGoals,HomeCorners,AwayCorners");
                foreach (GameResult game in games)
                {
                    String line = game.homeTeam + "," + game.awayTeam + "," + game.homeGoals + "," + game.awayGoals + "," + game.homeCorners + "," + game.awayCorners;
                    file.WriteLine(line);
                }
            }
        }
    };

    public class ExecuteR
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ExecuteR(String workingDirectory)
        {
            log.Debug("Running process in directory: " + workingDirectory);
            //TODO: either use PATH env. or configurable full path
            ProcessStartInfo si = new ProcessStartInfo();
            si.FileName = "R";
            si.Arguments = "CMD BATCH ..\\script.R";
            si.WorkingDirectory = workingDirectory;
            si.UseShellExecute = true;
            si.CreateNoWindow = true;

            try
            {
                using (Process p = Process.Start(si))
                {
                    log.Debug("Waiting for process to finish");
                    p.WaitForExit();
                    System.Threading.Thread.Sleep(10000);
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
    };

    public class Predictions : Dictionary<int, Dictionary<int, String>>
    {

    };

    public class ReadPrediction
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Predictions data = new Predictions();

        public ReadPrediction(Database dbStuff, String path, String file_name)
        {
            String full_name = Path.Combine(path, file_name);
            var reader = new StreamReader(File.OpenRead(full_name));

            //read header which is:
            //Teams, TeamName1, TeamName2
            var header = reader.ReadLine();
            String[] team_names = header.Split(';');


            log.Debug(team_names);

            //store team with their team id not team name
            List<int> team_ids = new List<int>();
            team_ids.Add(0);
            bool skip_first = true;
            foreach (String team_name in team_names)
            {
                if (skip_first)
                {
                    dbStuff.RunSQL("select id from teams where name = " + team_name + ";",
                        (dr) =>
                        {
                            team_ids.Add(int.Parse(dr[0].ToString()));
                        }
                         );
                }
                else
                {
                    skip_first = false;
                }
            }
            int j = 1;

            while (!reader.EndOfStream)
            {
                Dictionary<int, String> row = new Dictionary<int, String>();
                var line = reader.ReadLine();
                String[] values = line.Split(';');

                for (int i = 1; i < values.Length; i++)
                {
                    row.Add(team_ids[i], values[i]);
                }

                data.Add(team_ids[j], row);
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

        public string GetGoalsAndCornersPred(string game_id)
        {
            log.Info("GetGoalsAndCornersPred is being invoked");
            string league_id = null;

            //get league id
            dbStuff.RunSQL("SELECT league_id FROM games WHERE id = " + game_id + ";",
                (dr) =>
                {
                    league_id = dr[0].ToString();
                }
            );

            ArrayList games = new ArrayList();

            //get goals, corners from all games in a league_id
            dbStuff.RunSQL("SELECT t1.name, t2.name, MAX(s.hg), MAX(s.ag), MAX(s.hco), MAX(s.aco)"
            + " FROM statistics s, games g, teams t1, teams t2"
            + " WHERE g.league_id = "
            + league_id
            + " AND s.game_id = g.id AND t1.id = g.team1 AND t2.id = g.team2 GROUP BY s.game_id;",
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

            String league_day = league_id + "_" + DateTime.Today.ToString("ddMMyyyy");
            String path = Path.Combine(GlobalData.Instance.predictDir, league_day);

            //create working directory
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }

            var file = new CreateInputFile(path, games);
            ExecuteR r = new ExecuteR(path);

            //read data back
            ReadPrediction winH = new ReadPrediction(dbStuff, path, "winH.csv");
            ReadPrediction winA = new ReadPrediction(dbStuff, path, "winA.csv");
            ReadPrediction likelyScore = new ReadPrediction(dbStuff, path, "likelyScore.csv");
            ReadPrediction likelyProb = new ReadPrediction(dbStuff, path, "likelyProb.csv");

            int team1 = -1;
            int team2 = -1;
            dbStuff.RunSQL("SELECT team1, team2 FROM games WHERE id = " + game_id + ";",
                (dr) =>
                {
                    team1 = int.Parse(dr[0].ToString());
                    team2 = int.Parse(dr[1].ToString());
                }
            );

            log.Info("Game: " + game_id + " team1: " + team1 + " team2: " + team2);

            PredRow row = new PredRow();
            try
            {
                row.gameId = game_id;
                row.winHome = winH.data[team1][team2];
                row.winAway = winA.data[team1][team2];
                row.likelyProb = likelyProb.data[team1][team2];
                row.likelyScore = likelyScore.data[team1][team2];
            }
            catch (Exception e)
            {
                log.Warn("Exception caught while getting match predictions for game: " + game_id + " exception: " + e);
            }

            return JsonConvert.SerializeObject(row, Formatting.Indented);
        }
    }

    class Program
    {
        private static readonly log4net.ILog log
           = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static string dbConnectionString = ConfigurationManager.AppSettings["dbConnectionString"];
        static string dbtype = ConfigurationManager.AppSettings["dbtype"];
        static string uriHostPort = ConfigurationManager.AppSettings["uriHostPort"];
        static string predictDir = ConfigurationManager.AppSettings["predictDir"];
        static bool alive = true;

        static void Main(string[] args)
        {
            string uriString;
            string dbtype = "sqlite";
            string dbConnectionString = "";
            DbCreator dbCreator = null;

            switch (dbtype)
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
            db.Connect(dbConnectionString);

            //create working directory
            if (Directory.Exists(predictDir) == false)
            {
                Directory.CreateDirectory(predictDir);
            }

            GlobalData gd = GlobalData.Instance;
            gd.dbStuff = db;
            gd.predictDir = predictDir;

            uriString = "http://" + uriHostPort;

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
