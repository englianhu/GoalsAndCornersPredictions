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

        string path = null;
        GameDetails gameDetails = null;
        PredictionReader predictionReader = null;

        public GetResults(PredictionReader reader, string path, GameDetails gameDetails)
        {
            this.path = path;
            this.gameDetails = gameDetails;
            this.predictionReader = reader;
        }

        public string get(string fileName)
        {
            string ret_val = null;
        
            Statistics stats = predictionReader.Read(Path.Combine(path, fileName));

            if (stats == null)
            {
                log.Error("PredictionReader failed " + fileName + " null");
            }
            else
            {
                int team1statId = -1;
                int team2statId = -1;
                stats.statsId2teamName.TryGetValue(gameDetails.team1Name, out team1statId);
                stats.statsId2teamName.TryGetValue(gameDetails.team2Name, out team2statId);

                if (team1statId == -1 || team2statId == -1)
                {
                    var msg = "WARNING! Failed to calulate probabilty for " + gameDetails.ToString() ;
                    log.Warn(msg);
                    throw new Exception(msg);
                }

                ret_val = stats.stats[team1statId, team2statId];                           
            }

            return ret_val;
        }
    }
}
