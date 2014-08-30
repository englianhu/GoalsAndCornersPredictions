using System;
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
        
        TeamNameToId team2id = null;
        string path = null;
        int team1 = -1;
        int team2 = -1;
        PredictionReader predictionReader = null;

        public GetResults(TeamNameToId team2id, PredictionReader reader, string path, int team1, int team2)
        {
            this.team2id = team2id;
            this.path = path;
            this.team1 = team1;
            this.team2 = team2;
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

            var predReader = predictionReader.Read(team2id, Path.Combine(path, fileName));

            if (predReader == null)
            {
                log.Error("PredictionReader failed " + fileName + " null");
            }
            else
            {
                var results = predReader.Where(x => x != null && x.team1Id == team1 && x.team2Id == team2);
                log.Info(fileName + " contains: " + predReader.Count() + " results: " + results);
                ret_val = results.Count() != 0 ? results.First().probability : "-1";
                if (ret_val == "-1") {
                    var msg = "WARNING! Failed to calulate probabilty for team1: " + team1 + " team2: " + team2;
                    log.Warn(msg);
                    throw new Exception( msg );
                }
                  
            }
            stopWatchA1.Stop();
            log.Info("getting results completed in: " + stopWatchA1.Elapsed.TotalSeconds + " seconds");
            return ret_val;
        }
    }
}
