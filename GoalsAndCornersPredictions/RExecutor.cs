using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoalsAndCornersPredictions
{
    public abstract class R
    {
        protected abstract void CopyScripts(string workingDirectory);
        protected abstract bool Run(ProcessStartInfo si);
        protected abstract ProcessStartInfo SetupProcess(string workingDirectory);
        public abstract bool Execute(String workingDirectory);
    };

    public class RExecutor : R
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected PredictionType m_predType;

 //       protected string rBinPath = @"C:\Program Files\R\R-3.0.2\bin\x64";

        public RExecutor(PredictionType predType)
        {
            this.m_predType = predType;
        }

        protected override void CopyScripts(string workingDirectory)
        {
            log.Debug("Copying script files");
            if (m_predType == PredictionType.corner)
            {
                string filename = Path.GetFileName(GlobalData.Instance.CornersScriptFullPath); 
                System.IO.File.Copy(GlobalData.Instance.CornersScriptFullPath, Path.Combine(workingDirectory, filename), true);
            }
            else {
                string filename = Path.GetFileName(GlobalData.Instance.GoalsScriptFullPath); 
                System.IO.File.Copy(GlobalData.Instance.GoalsScriptFullPath, Path.Combine(workingDirectory, filename), true);
            }
        }

        protected override bool Run(ProcessStartInfo si)
        {
                using (Process p = new Process())
                {
                    p.StartInfo = si;
                //    p.Exited += processExited;
                    p.EnableRaisingEvents = true;

                    if (p.Start())
                    {
                        p.PriorityClass = ProcessPriorityClass.AboveNormal;
                    }

                    int maxRWaitTime = 10;
                    int waitedTime = 0;

                    while (p.HasExited == false || waitedTime > maxRWaitTime)
                    {
                        log.Debug("Waiting " + waitedTime + " seconds for R process to finish");

                        System.Threading.Thread.Sleep(2000);

                        waitedTime++;
                        /*
                        var rProcesses = Process.GetProcesses().ToArray().ToList().Select(x => x.MainWindowTitle);

                        if (rProcesses.Any(y => y.Equals(GlobalData.Instance.RexecutableFullPath)) == false)
                        {
                            log.Warn("Looks like R has crashed, exitting...");
                            break;
                        }
                         */
                    }
                    if (waitedTime < maxRWaitTime) return false;
                    return true;
                }
        }

        protected override ProcessStartInfo SetupProcess(string workingDirectory)
        {
            ProcessStartInfo si = new ProcessStartInfo();
            si.FileName = GlobalData.Instance.RexecutableFullPath;

            if (m_predType == PredictionType.corner)
            {
                si.Arguments = @"CMD BATCH " + Path.GetFileName(GlobalData.Instance.CornersScriptFullPath);
            }
            else
            {
                si.Arguments = @"CMD BATCH " + Path.GetFileName(GlobalData.Instance.GoalsScriptFullPath);
            }

            si.WorkingDirectory = workingDirectory;
            si.UseShellExecute = true;
            si.CreateNoWindow = true;
            return si;
        }

        public override bool Execute(String workingDirectory)
        {
            log.Debug("Running process in directory: " + workingDirectory);

            CopyScripts(workingDirectory);

            ProcessStartInfo si = SetupProcess(workingDirectory);
         
            return Run(si);
        }

    };
}
