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
    public delegate void RunRDelegate(string gameId, int depth);

    public class SyncOnDir : RunR
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public SyncOnDir(Configuration cfg)
            : base(cfg)
        {
        }

        public override string getPath(string gameId)
        {
            string league_id = "invalid_league_id";

            cfg.dbStuff.RunSQL("SELECT league_id FROM games WHERE id = " + gameId + ";",
                (dr) =>
                {
                    league_id = dr[0].ToString();
                });

            return Path.Combine(GlobalData.Instance.PredictionDir,league_id + "_" + cfg.dayJoin + DateTime.Today.ToString("ddMMyyyy"));
        }

        private readonly object syncLock = new object();

        private bool CreateDirIfNotExist(string path)
        {
            lock (syncLock)
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public override void Run(string gameId, int depth, int totalPredictionTimeout)
        {
            string path = getPath(gameId);

            log.Info("Starting the run in path: " + path);

            //if (!Directory.Exists(path))
			//if (CreateDirIfNotExist(path))  //will bring this back soon
            //{
            //    try
            //    {
            //        base.Run(gameId, depth);
            //        using (File.Create(Path.Combine(path, "rFinished.txt"))) { }

            //    }
            //    catch (Exception e)
            //    {
            //        Directory.Delete(path, true);
            //        throw e;
            //    }
				
            if (!Directory.Exists(path) || !File.Exists(Path.Combine(path, "rFinished.txt")))
            {
                log.Info("No previous data found");
                base.Run(gameId, depth, totalPredictionTimeout);
                File.Create(Path.Combine(path, "rFinished.txt"));
            }
            else
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                while (true)
                {
                    log.Info("Waiting in SyncDir for " + sw.Elapsed.TotalSeconds);
                    
                    System.Threading.Thread.Sleep(1000);

                    if(File.Exists(Path.Combine(path, "rFinished.txt")))
                        break;
                    
                    if(sw.Elapsed.TotalSeconds > totalPredictionTimeout)
                        break;
                }
            }
        }
    };

    public class Predictions
    {
        private static readonly log4net.ILog log
         = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Configuration cfg;

        public Predictions(Configuration cfg)
        {
            this.cfg = cfg;
        }

        public string execute(string gameId, int depth, int totalPredictionTimeout)
        {
            try
            {
                log.Info("Fetching game details");
                var gameDetails = cfg.GetGameDetails(gameId);

                log.Info("Received game details");

                SyncOnDir s = new SyncOnDir(cfg);
                s.Run(gameId, depth, totalPredictionTimeout);

                string path = s.getPath(gameId);

                log.Info("Fetching prediction results");
                GetResults result = new GetResults(cfg.predReader, path, gameDetails);

                log.Info("Received rsults 1");

                gameDetails.prediction.winHome = result.get("winH.csv");
                log.Info("Received rsults 2");

                gameDetails.prediction.winAway = result.get("winA.csv");
                log.Info("Received rsults 3");

                gameDetails.prediction.likelyProb = result.get("likelyProb.csv");
                log.Info("Received rsults 4");

                gameDetails.prediction.likelyScore = result.get("likelyScore.csv");
                log.Info("Received rsults 5");


                var json_result = JsonConvert.SerializeObject(gameDetails.prediction, Formatting.Indented);

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
