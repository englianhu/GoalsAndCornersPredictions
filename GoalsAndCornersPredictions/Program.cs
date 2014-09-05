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
        public string GoalsScriptFullPath {get; set; }
        public string CornersScriptFullPath { get; set; }
        public string GoalsBiVariateScriptFullPath { get; set; }
        public string CornersBiVariateScriptFullPath { get; set; }

        private GlobalData() { }

        public static GlobalData Instance
        {
            get { return instance ?? (instance = new GlobalData()); }
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


    public enum PredictionType
    {
        goal,
        corner
    };
   
    public class Service : IService
    {
        private static readonly log4net.ILog log
           = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        Predictions goalsPrediction = new Predictions(new Configuration("_g_", new CreateInputFileGoals(), new PredictionReader(), new RExecutor(PredictionType.goal)));
        Predictions cornersPrediction = new Predictions(new Configuration("_c_", new CreateInputFileCorners(), new PredictionReader(), new RNETExecutor(PredictionType.corner)));
        Predictions goalsBiVarPrediction = new Predictions(new Configuration("_g_", new CreateInputFileGoals(), new PredictionReaderWithNoNames(), new RNetBiVariateExecutor(PredictionType.goal)));
        Predictions cornersBiVarPrediction = new Predictions(new Configuration("_c_", new CreateInputFileCorners(), new PredictionReaderWithNoNames(), new RNetBiVariateExecutor(PredictionType.corner)));

        int serviceTimeout = 240; 
        public string GetCornersPrediction(string gameId)
        {
            log.Info("GetCornersPrediction is invoked for " + gameId);
            return cornersPrediction.execute(gameId, 0, serviceTimeout);
        }

        public string GetCornersPredictionWithDepth(string gameId, int depth)
        {
            log.Info("GetCornersPredictionWithDepth is invoked for " + gameId);
            return cornersPrediction.execute(gameId, depth, serviceTimeout);
        }

        public string GetGoalsPrediction(string gameId)
        {
            log.Info("GetGoalsPrediction is invoked for " + gameId);
            return goalsPrediction.execute(gameId, 0, serviceTimeout);
        }

        public string GetGoalsPredictionWithDepth(string gameId, int depth)
        {
            log.Info("GetGoalsPredictionWithDepth is invoked for " + gameId);
            return goalsPrediction.execute(gameId, depth, serviceTimeout);
        }

        public string GetGoalsBiVarPrediction(string gameId)
        {
            log.Info("GetGoalsBiVarPrediction is invoked for " + gameId);
            return goalsBiVarPrediction.execute(gameId, 0, serviceTimeout);
        }

        public string GetCornersBiVarPrediction(string gameId)
        {
            log.Info("GetCornersBiVarPrediction is invoked for " + gameId);
            return cornersBiVarPrediction.execute(gameId, 0, serviceTimeout);
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
            gd.GoalsScriptFullPath = ConfigurationManager.AppSettings["GoalsScriptFullPath"];
            gd.CornersScriptFullPath = ConfigurationManager.AppSettings["CornersScriptFullPath"];
            gd.GoalsBiVariateScriptFullPath = ConfigurationManager.AppSettings["GoalsBiVariateScriptFullPath"];
            gd.CornersBiVariateScriptFullPath = ConfigurationManager.AppSettings["CornersBiVariateScriptFullPath"];

            //create working directory
            if (!Directory.Exists(gd.PredictionDir))
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
