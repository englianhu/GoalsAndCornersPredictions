using System;
using System.Collections.Generic;
using System.Linq;
using System.Configuration;

using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Collections;
using Db;
using System.IO;
using System.Diagnostics;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace GoalsAndCornersPredictions
{
    public class GlobalData
    {
        private static GlobalData instance = null;

        public CachedDb dbStuff { get; set; }
        public string PredictionDir { get; set; }
        public string RexecutableFullPath { get; set; }
        public string GoalsScriptFullPath { get; set; }
        public string CornersScriptFullPath { get; set; }
        public string GoalsBiVariateScriptFullPath { get; set; }
        public string CornersBiVariateScriptFullPath { get; set; }

        public void Init(Database db)
        {
           
            dbStuff = new CachedDb(db);
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

  

    public class GameResult
    {
        public string homeTeam;
        public string awayTeam;
        public string homeGoals;
        public string awayGoals;
        public string homeCorners;
        public string awayCorners;
    };

}
