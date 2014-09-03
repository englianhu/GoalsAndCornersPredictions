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
    public class SyncOnDir : RunR
    {
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

        public override void Run(string gameId, int depth)
        {
            string path = getPath(gameId);

            if (CreateDirIfNotExist(path))
            {
                base.Run(gameId, depth);
                using (File.Create(Path.Combine(path, "rFinished.txt"))) { }
            }
            else
            {
                //wait till finished file is created
                while (!File.Exists(Path.Combine(path, "rFinished.txt")))
                {
                    System.Threading.Thread.Sleep(1000);
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

        public string execute(string gameId, int depth)
        {
            try
            {
                var gameDetails = cfg.GetGameDetails(gameId);

                SyncOnDir s = new SyncOnDir(cfg);
                s.Run(gameId, depth);

                string path = s.getPath(gameId);

                GetResults result = new GetResults(cfg.predReader, path, gameDetails);

                gameDetails.prediction.winHome = result.get("winH.csv");
                gameDetails.prediction.winAway = result.get("winA.csv");
                gameDetails.prediction.likelyProb = result.get("likelyProb.csv");
                gameDetails.prediction.likelyScore = result.get("likelyScore.csv");

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
