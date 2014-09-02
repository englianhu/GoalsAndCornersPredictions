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
using System.Threading;
using System.Threading.Tasks;

namespace GoalsAndCornersPredictions
{

    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        [WebGet]
        string GetGoalsPrediction(string gameId);

        [WebGet]
        string GetGoalsPredictionWithDepth(string gameId, int depth);

        [WebGet]
        string GetCornersPrediction(string gameId);

        [WebGet]
        string GetCornersPredictionWithDepth(string gameId, int depth);

        [WebGet]
        string GetGoalsBiVarPrediction(string gameId);
        
        [WebGet]
        string GetCornersBiVarPrediction(string gameId);
    }


    public class GlobalData
    {
        private static GlobalData instance;
        public Database dbStuff { get; set; }
        public string PredictionDir { get; set; }
        public string RexecutableFullPath { get; set; }
        public string GoalsScriptFullPath = @"C:\Users\daddy\Documents\GitHub\GoalsAndCornersPredictions\GoalsAndCornersPredictions\scriptCorners.R";
        public string CornersScriptFullPath = @"C:\Users\daddy\Documents\GitHub\GoalsAndCornersPredictions\GoalsAndCornersPredictions\scriptGoals.R";
        public string GoalsBiVariateScriptFullPath = @"C:\Users\daddy\Documents\GitHub\GoalsAndCornersPredictions\GoalsAndCornersPredictions\script_bivariate_goals.R";
        public string CornersBiVariateScriptFullPath = @"C:\Users\daddy\Documents\GitHub\GoalsAndCornersPredictions\GoalsAndCornersPredictions\script_bivariate_goals.R";

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

    public abstract class CreateInputFile
    {
        public abstract void Create(String workingDirectory, ArrayList games);
    }

    public class CreateInputFileGoals : CreateInputFile
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override void Create(String workingDirectory, ArrayList games)
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
                file.Close();
            }
        }
    };

    public class CreateInputFileCorners : CreateInputFile
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override void Create(String workingDirectory, ArrayList games)
        {
            String file_name = Path.Combine(workingDirectory, "input.txt");

            log.Info("CreateInputFileCorners --> start");

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(file_name, false))
            {
                // write header
                file.WriteLine("HomeTeam,AwayTeam,HomeGoals,AwayGoals,HomeCorners,AwayCorners");
                foreach (GameResult game in games)
                {
                    String line = game.homeTeam + "," + game.awayTeam + "," + game.homeCorners + "," + game.awayCorners + "," + 0 + "," + 0;
                    file.WriteLine(line);
                }

                file.Close();
            }

            log.Info("CreateInputFileCorners --> finish");
        }
    };

    public enum PredictionType
    {
        goal,
        corner
    };

    public class GameDetails
    {
        public int gameId;
        public string team1Name;
        public string team2Name;
        public string leagueName;
        public string koDate;

        public PredRow prediction = new PredRow();

        public override string ToString()
        {
            return "game: " + gameId + " : " + team1Name + "|" + team2Name + "|" + leagueName + "|" + koDate;
        }
    };

    public class ServiceCommon
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected Database dbStuff;
		/*
        protected List<string> naughtyLeagues = null;

        public ServiceCommon()
        {
            GlobalData gd = GlobalData.Instance;
            dbStuff = gd.dbStuff;

            naughtyLeagues = dbStuff.OneColumnQuery("select id from leagues where name like '%Friendl%';");
            naughtyLeagues.Add("-1");

            naughtyLeagues.ForEach(x =>
            Console.WriteLine("naughty leagues --> " + x));
        }

        public GameDetails GetGameDetails(string id)
        {
            var sql = "SELECT g1.id, t1.name, t2.name, l1.name, g1.kodate"
            + " FROM games g1"
            + " JOIN teams t1 ON g1.team1 = t1.id"
            + " JOIN teams t2 ON g1.team2 = t2.id"
            + " JOIN leagues l1 ON l1.id = g1.league_id"
            + " WHERE g1.id =" + id;

            GameDetails gameDetails = new GameDetails();

            dbStuff.RunSQL(sql,
              (dr) =>
              {
                  gameDetails.gameId = int.Parse(dr[0].ToString());
                  gameDetails.team1Name = dr[1].ToString();
                  gameDetails.team2Name = dr[2].ToString();
                  gameDetails.leagueName = dr[3].ToString();
                  gameDetails.koDate = dr[4].ToString();
              });

            log.Info("GetGoalsPrediction is being invoked for " + gameDetails.ToString());
            log.Info("This pointer = " + this);

            return gameDetails;
        }

        public string GetLeagueIDs(string gameId, int depth = 0)
        {
            string leagueIds = null;

            //get league id
            dbStuff.RunSQL("SELECT league_id FROM games WHERE id = " + gameId + ";",
                (dr) =>
                {
                    leagueIds = dr[0].ToString();
                }
            );

            if (depth != 0 || naughtyLeagues.Any(x => x == leagueIds))
            {
                leagueIds = null;

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

            if (leagueIds == null) throw new Exception("Could not determine league for given game");

            log.Info("League Search: " + leagueIds);

            return leagueIds;
        }*/
    }

    public class Service : IService
    {
        private static readonly log4net.ILog log
           = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        Predictions goalsPrediction = new Predictions(new Configuration("_g_", new CreateInputFileGoals(), new PredictionReader(), new RExecutor(PredictionType.goal)));
        Predictions cornersPrediction = new Predictions(new Configuration("_c_", new CreateInputFileCorners(), new PredictionReader(), new RNETExecutor(PredictionType.corner)));
        Predictions goalsBiVarPrediction = new Predictions(new Configuration("_g_", new CreateInputFileGoals(), new PredictionReaderWithNoNames(), new RNetBiVariateExecutor(PredictionType.goal)));
        Predictions cornersBiVarPrediction = new Predictions(new Configuration("_c_", new CreateInputFileCorners(), new PredictionReaderWithNoNames(), new RNetBiVariateExecutor(PredictionType.corner)));

        public string GetCornersPrediction(string gameId)
        {
            return cornersPrediction.execute(gameId, 0);
        }

        public string GetCornersPredictionWithDepth(string gameId, int depth)
        {
            return cornersPrediction.execute(gameId, depth);
        }

        public string GetGoalsPrediction(string gameId)
        {
            return goalsPrediction.execute(gameId, 0);
        }

        public string GetGoalsPredictionWithDepth(string gameId, int depth)
        {
            return goalsPrediction.execute(gameId, depth);
        }

        public string GetGoalsBiVarPrediction(string gameId)
        {
            return goalsBiVarPrediction.execute(gameId, 0);
        }

        public string GetCornersBiVarPrediction(string gameId)
        {
            return cornersBiVarPrediction.execute(gameId, 0);
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
