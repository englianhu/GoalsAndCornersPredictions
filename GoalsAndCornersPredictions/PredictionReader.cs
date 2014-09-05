using Db;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GoalsAndCornersPredictions
{
    public class Statistics
    {
        public Dictionary<string, int> statsId2teamName = new Dictionary<string,int>();
        public string[,] stats = null;
    }

    public class PredictionReader
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public virtual Statistics Read(string full_name)
        {
            if (File.Exists(full_name) == false) { return null; }

            Statistics holder;
            using (var reader = new StreamReader(File.OpenRead(full_name)))
            {
                holder = new Statistics();

            //read header which is:
            //Teams, TeamName1, TeamName2, TeamName3, ...
            var header = reader.ReadLine();

            var team_names = header.Split(';').ToList();
            team_names.RemoveAt(0);

            for (int i = 0; i < team_names.Count; i++ )
            {
                holder.statsId2teamName.Add(team_names[i], i);
            }

            int number_of_teams = holder.statsId2teamName.Count;

            holder.stats = new string[number_of_teams, number_of_teams];

            int j = 1;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                String[] values = line.Split(';');

                    for (int i = 1; i < values.Length; i++)
                    {
                        holder.stats[j - 1, i - 1] = values[i];
                    }
                    j++;
                }
            }

            return holder;
        }
    };

    public class PredictionReaderWithNoNames : PredictionReader
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override Statistics Read(String full_name)
        {
            if (File.Exists(full_name) == false) { return null; }

            Statistics holder;
            using (var reader = new StreamReader(File.OpenRead(full_name)))
            {
                holder = new Statistics();

            //read header which is:
            //Teams, TeamName1, TeamName2, TeamName3, ...
            var header = reader.ReadLine();

            var team_names = header.Split(';').ToList();

            for (int i = 0; i < team_names.Count; i++)
            {
                holder.statsId2teamName.Add(team_names[i], i);
            }

            int number_of_teams = holder.statsId2teamName.Count;

            holder.stats = new string[number_of_teams, number_of_teams];

            int j = 0;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                String[] values = line.Split(';');

                for (int i = 0; i < values.Length; i++)
                {
                    holder.stats[j, i] = values[i];
                }
                j++;
            }
        }

            return holder;
        }
    };
}
