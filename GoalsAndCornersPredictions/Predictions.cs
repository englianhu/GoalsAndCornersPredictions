using Common;
using Db;
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

            league_id = cfg.dbStuff.getLeagueId(gameId);

            return Path.Combine(GlobalData.Instance.PredictionDir, cfg.getPrefix() + "_" + league_id + "_" + DateTime.Today.ToString("ddMMyyyy"));
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

            if (CreateDirIfNotExist(path))  //will bring this back soon
            {
                try
                {
                    base.Run(gameId, depth, totalPredictionTimeout);
                    using (File.Create(Path.Combine(path, "rFinished.txt"))) { }

                }
                catch (Exception e)
                {
                    using (File.Create(Path.Combine(path, "rError.txt"))) { }
                    throw e;
                }
            }
            else
            {
                while (!File.Exists(Path.Combine(path, "rFinished.txt")))
                {
                    if (File.Exists(Path.Combine(path, "rError.txt"))) throw new Exception("error file exists for directory: " + path);
                    // the following is to break the loop in case it got stuck
                    if (!Directory.Exists(path)) throw new Exception("Directory " + path + " has disappeared");

                    System.Threading.Thread.Sleep(1000);
                }
            }
        }
    };

    public class GoalsAndCorners : GoalsAndCornersPrediction
    {
        private static readonly log4net.ILog log
         = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Configuration cfg;

        public GoalsAndCorners(Configuration cfg)
        {
            this.cfg = cfg;
        }

        public override PredRow execute(string gameId, int depth, int totalPredictionTimeout)
        {
            log.Info("Fetching game details");
            GameDetails gameDetails = cfg.GetGameDetails(gameId);

            log.Info("Received game details");

            SyncOnDir s = new SyncOnDir(cfg);
            s.Run(gameId, depth, totalPredictionTimeout);

            string path = s.getPath(gameId);

            GetResults result = new GetResults(cfg.predReader, path, gameDetails);

            PredRow prediction = new PredRow();

            prediction.gameId = gameId;
            prediction.winHome = result.get("winH.csv");
            prediction.winAway = result.get("winA.csv");
            prediction.likelyProb = result.get("likelyProb.csv");
            prediction.likelyScore = result.get("likelyScore.csv");

            //might be used later
            gameDetails.prediction = prediction;

            return prediction;
        }
    }
}
