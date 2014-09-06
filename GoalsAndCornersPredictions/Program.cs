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
        private static GlobalData instance = null;

        public Database dbStuff { get; set; }
        public string PredictionDir { get; set; }
        public string RexecutableFullPath { get; set; }
        public string GoalsScriptFullPath { get; set; }
        public string CornersScriptFullPath { get; set; }
        public string GoalsBiVariateScriptFullPath { get; set; }
        public string CornersBiVariateScriptFullPath { get; set; }


        private GlobalData()
        {
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
            dbStuff = db;
            PredictionDir = ConfigurationManager.AppSettings["PredictionDir"];
            RexecutableFullPath = ConfigurationManager.AppSettings["RexecutableFullPath"];
            GoalsScriptFullPath = ConfigurationManager.AppSettings["GoalsScriptFullPath"];
            CornersScriptFullPath = ConfigurationManager.AppSettings["CornersScriptFullPath"];
            GoalsBiVariateScriptFullPath = ConfigurationManager.AppSettings["GoalsBiVariateScriptFullPath"];
            CornersBiVariateScriptFullPath = ConfigurationManager.AppSettings["CornersBiVariateScriptFullPath"];

        }

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

    public class DirReader
    {
        public static void Populate(PredictionReaderWithCache reader)
        {
            Task t = Task.Run(() =>
            {
                string path = GlobalData.Instance.PredictionDir;
                foreach (var dir in Directory.GetDirectories(path))
                {
                    reader.Read(path);
                }
            });
        }
    }

    public class GlobalService
    {
        private static GlobalService instance = null;

        public Predictions goalsPrediction = null;
        public Predictions cornersPrediction = null;
        public Predictions goalsBiVarPrediction = null;
        public Predictions cornersBiVarPrediction = null;

        private GlobalService()
        {
            var goalsReader = new PredictionReaderWithCache(new PredictionReader());
            var cornersReader = new PredictionReaderWithCache(new PredictionReader());
            var goalsBiVarReader = new PredictionReaderWithCache(new PredictionReaderWithNoNames());
            var cornersBiVarReader = new PredictionReaderWithCache(new PredictionReaderWithNoNames());

            DirReader.Populate(goalsReader);
            DirReader.Populate(cornersReader);
            DirReader.Populate(goalsBiVarReader);
            DirReader.Populate(cornersBiVarReader);

            goalsPrediction = new Predictions(new Configuration("GoalsPrediction", new CreateInputFileGoals(), goalsReader, new RExecutor(PredictionType.goal)));
            cornersPrediction = new Predictions(new Configuration("CornersPrediction", new CreateInputFileCorners(), cornersReader, new RNETExecutor(PredictionType.corner)));
            goalsBiVarPrediction = new Predictions(new Configuration("GoalsBivariate", new CreateInputFileGoals(), goalsBiVarReader, new RNetBiVariateExecutor(PredictionType.goal)));
            cornersBiVarPrediction = new Predictions(new Configuration("CornersBivariate", new CreateInputFileCorners(), cornersBiVarReader, new RNetBiVariateExecutor(PredictionType.corner)));
        }

        public static GlobalService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GlobalService();
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


    public enum PredictionType
    {
        goal,
        corner
    };

    public class Service : IService
    {
        private static readonly log4net.ILog log
           = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        int serviceTimeout = 240;
        public string GetCornersPrediction(string gameId)
        {
            var json_result = "null";
            try
            {
                log.Info("GetCornersPrediction is invoked for " + gameId);
                var data = GlobalService.Instance.cornersPrediction.execute(gameId, 0, serviceTimeout);
                json_result = JsonConvert.SerializeObject(data, Formatting.Indented);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return json_result;
        }

        public string GetCornersPredictionWithDepth(string gameId, int depth)
        {
            var json_result = "null";
            try
            {
                log.Info("GetCornersPredictionWithDepth is invoked for " + gameId);
                var data = GlobalService.Instance.cornersPrediction.execute(gameId, depth, serviceTimeout);
                json_result = JsonConvert.SerializeObject(data, Formatting.Indented);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return json_result;
        }

        public string GetGoalsPrediction(string gameId)
        {
            var json_result = "null";
            try
            {
                log.Info("GetGoalsPrediction is invoked for " + gameId);
                var data = GlobalService.Instance.goalsPrediction.execute(gameId, 0, serviceTimeout);
                json_result = JsonConvert.SerializeObject(data, Formatting.Indented);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return json_result;
        }

        public string GetGoalsPredictionWithDepth(string gameId, int depth)
        {
            var json_result = "null";
            try
            {
                log.Info("GetGoalsPredictionWithDepth is invoked for " + gameId);
                var data = GlobalService.Instance.goalsPrediction.execute(gameId, depth, serviceTimeout);
                json_result = JsonConvert.SerializeObject(data, Formatting.Indented);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return json_result;
        }

        public string GetGoalsBiVarPrediction(string gameId)
        {
            var json_result = "null";
            try
            {
                log.Info("GetGoalsBiVarPrediction is invoked for " + gameId);
                var data = GlobalService.Instance.goalsBiVarPrediction.execute(gameId, 0, serviceTimeout);
                json_result = JsonConvert.SerializeObject(data, Formatting.Indented);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return json_result;
        }

        public string GetCornersBiVarPrediction(string gameId)
        {
            var json_result = "null";
            try
            {
                log.Info("GetCornersBiVarPrediction is invoked for " + gameId);
                var data = GlobalService.Instance.cornersBiVarPrediction.execute(gameId, 0, serviceTimeout);
                json_result = JsonConvert.SerializeObject(data, Formatting.Indented);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return json_result;
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

            GlobalData gd = GlobalData.Instance;
            GlobalService gp = GlobalService.Instance;

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
