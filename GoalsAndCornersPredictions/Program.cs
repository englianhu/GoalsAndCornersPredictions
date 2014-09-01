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
    }


    public class GlobalData
    {
        private static GlobalData instance;
        public Database dbStuff { get; set; }
        public string PredictionDir { get; set; }
        public string RexecutableFullPath { get; set; }
        public string GoalsScriptFullPath = @"D:\scriptCorners.R";
        public string CornersScriptFullPath = @"D:\scriptGoals.R";
        public string BiVariateScriptFullPath = @"D:\script_bivariate.R";

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
     
    }

    public class Service : IService
    {
        private static readonly log4net.ILog log
           = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        Predictions goalsPrediction = new Predictions(new Configuration("_g_", new CreateInputFileGoals(), new PredictionReader(), new RExecutor(PredictionType.goal)));
        Predictions cornersPrediction = new Predictions(new Configuration("_c_", new CreateInputFileCorners(), new PredictionReader(), new RNETExecutor(PredictionType.corner)));
        Predictions goalsBiVarPrediction = new Predictions(new Configuration("_g_", new CreateInputFileGoals(), new PredictionReaderWithNoNames(), new RNetBiVariateExecutor()));

        Service()
        {
        }

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
