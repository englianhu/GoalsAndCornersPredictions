using System;
using System.Collections;
using System.IO;

namespace GoalsAndCornersPredictions
{
    public abstract class CreateInputFile
    {
        public abstract void Create(String workingDirectory, ArrayList games);
    }

    public class CreateInputFileGoals : CreateInputFile
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override void Create(String workingDirectory, ArrayList games)
        {
            String file_name = Path.Combine(workingDirectory, "input.txt");
            using (var file = new System.IO.StreamWriter(file_name, false))
            {
                // write header
                file.WriteLine("HomeTeam,AwayTeam,HomeGoals,AwayGoals,HomeCorners,AwayCorners");
                foreach (GameResult game in games)
                {
                    String line = game.homeTeam + "," + game.awayTeam + "," + game.homeGoals + "," + game.awayGoals + "," + 0 + "," + 0;
                    file.WriteLine(line);
                }
                file.Close();
            }
        }
    };

    public class CreateInputFileCorners : CreateInputFile
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override void Create(String workingDirectory, ArrayList games)
        {
            String file_name = Path.Combine(workingDirectory, "input.txt");

            log.Info("CreateInputFileCorners --> start");

            using (var file = new System.IO.StreamWriter(file_name, false))
            {
                // write header
                file.WriteLine("HomeTeam,AwayTeam,HomeGoals,AwayGoals,HomeCorners,AwayCorners");
                foreach (GameResult game in games)
                {
                    String line = game.homeTeam + "," + game.awayTeam + "," + game.homeCorners + "," + game.awayCorners + "," + 0 + "," + 0;
                    file.WriteLine(line);
                }

                file.Close();
            }

            log.Info("CreateInputFileCorners --> finish");
        }
    };

}