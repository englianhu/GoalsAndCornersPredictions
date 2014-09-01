﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoalsAndCornersPredictions
{
    class GetResults
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        string path = null;
        string team1name;
        string team2name;
        PredictionReader predictionReader = null;

        public GetResults(PredictionReader reader, string path, string team1, string team2)
        {
            this.path = path;
            this.team1name = team1;
            this.team2name = team2;
            this.predictionReader = reader;
        }

        public string get(string fileName)
        {
            if (!File.Exists(Path.Combine(path, "winH.csv")))
            {
                throw new Exception("File " + Path.Combine(path, "winH.csv") + " does not exist");
            }

            string ret_val = null;
            var stopWatchA1 = new Stopwatch();
            stopWatchA1.Start();

            Statistics stats = predictionReader.Read(Path.Combine(path, fileName));

            if (stats == null)
            {
                log.Error("PredictionReader failed " + fileName + " null");
            }
            else
            {
                int team1statId = -1;
                int team2statId = -1;
                stats.statsId2teamName.TryGetValue(team1name, out team1statId);
                stats.statsId2teamName.TryGetValue(team2name, out team2statId);

                if (team1statId == -1 || team2statId == -1)
                {
                    var msg = "WARNING! Failed to calulate probabilty for team1: '" + team1name + "' team2: '" + team2name + "'";
                    log.Warn(msg);
                    throw new Exception(msg);
                }

                ret_val = stats.stats[team1statId, team2statId];                           
            }
            stopWatchA1.Stop();
            log.Info("getting results completed in: " + stopWatchA1.Elapsed.TotalSeconds + " seconds");
            return ret_val;
        }
    }
}
