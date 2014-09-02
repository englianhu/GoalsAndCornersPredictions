using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoalsAndCornersPredictions
{
    public class RNetBiVariateExecutor : RExecutor
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public RNetBiVariateExecutor(PredictionType ptype)
            : base(ptype)
        {
        }

        protected override void CopyScripts(string workingDirectory)
        {
            log.Debug("Copying script files");
            
            string path = Path.GetDirectoryName(GlobalData.Instance.GoalsBiVariateScriptFullPath);

            if (m_predType == PredictionType.goal)
            {
                string filename = Path.GetFileName(GlobalData.Instance.GoalsBiVariateScriptFullPath);
                System.IO.File.Copy(GlobalData.Instance.GoalsBiVariateScriptFullPath, Path.Combine(workingDirectory, filename), true);
            }
            else
            {
                string filename = Path.GetFileName(GlobalData.Instance.CornersBiVariateScriptFullPath);
                System.IO.File.Copy(GlobalData.Instance.CornersBiVariateScriptFullPath, Path.Combine(workingDirectory, filename), true);
            }

            System.IO.File.Copy(Path.Combine(path, "pbivpois.R"), Path.Combine(workingDirectory, "pbivpois.R"), true);
            System.IO.File.Copy(Path.Combine(path, "simplebp.R"), Path.Combine(workingDirectory, "simplebp.R"), true);
            System.IO.File.Copy(Path.Combine(path, "lmbp.R"), Path.Combine(workingDirectory, "lmbp.R"), true);
            System.IO.File.Copy(Path.Combine(path, "newnamesbeta.R"), Path.Combine(workingDirectory, "newnamesbeta.R"), true);
            System.IO.File.Copy(Path.Combine(path, "splitbeta.R"), Path.Combine(workingDirectory, "splitbeta.R"), true);
        }

        protected override ProcessStartInfo SetupProcess(string workingDirectory)
        {
            ProcessStartInfo si = new ProcessStartInfo();
            si.FileName = GlobalData.Instance.RexecutableFullPath;

            if (m_predType == PredictionType.goal)
            {
                si.Arguments = @"CMD BATCH " + Path.GetFileName(GlobalData.Instance.GoalsBiVariateScriptFullPath);
            }
            else
            {
                si.Arguments = @"CMD BATCH " + Path.GetFileName(GlobalData.Instance.CornersBiVariateScriptFullPath);
            }

            si.WorkingDirectory = workingDirectory;
            si.UseShellExecute = true;
            si.CreateNoWindow = true;
            return si;
        }
    };
}
