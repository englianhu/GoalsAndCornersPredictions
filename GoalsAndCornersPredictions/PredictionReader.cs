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
    public class ProbabilityHolder
    {
        public int team1Id;
        public int team2Id;
        public string probability;
    }

    public class TeamNameToId
    {
        Dictionary<string, int> name2id = new Dictionary<string, int>();
        Database dbStuff = null;
        private readonly object syncLock = new object();

        public TeamNameToId(Database db)
        {
            dbStuff = db;
        }

        public int getId(string name)
        {
            lock (syncLock)
            {
                int id = -1;

                if (!name2id.TryGetValue(name, out id))
                {

                    dbStuff.RunSQL("select id from teams where name = '" + name + "';",
                                        (dr) =>
                                        {
                                            id = int.Parse(dr[0].ToString());
                                        }
                                    );

                    name2id.Add(name, id);
                }

                return id;
            }
        }
    }

    public class PredictionReader
    {
        private static readonly log4net.ILog log
          = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static List<ProbabilityHolder> Read(TeamNameToId team2id, String full_name)
        {

            var data = new List<ProbabilityHolder>();

            if (File.Exists(full_name) == false)
            {
                return null;
            }

            var reader = new StreamReader(File.OpenRead(full_name));

            //read header which is:
            //Teams, TeamName1, TeamName2
            var header = reader.ReadLine();
            var team_names = header.Split(';').ToList();
            team_names.RemoveAt(0);

            log.Debug(team_names);

            //store team with their team id not team name
            List<int> team_ids = new List<int>();
            team_ids.Add(0);

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            foreach (String team_name in team_names)
            {
                team_ids.Add(team2id.getId(team_name));
            }

            int j = 1;
            stopWatch.Stop();
            log.Info("reading results completed in: " + stopWatch.Elapsed.TotalSeconds + " seconds");

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                String[] values = line.Split(';');

                for (int i = 1; i < values.Length; i++)
                {
                    data.Add(new ProbabilityHolder() { team1Id = team_ids[j], team2Id = team_ids[i], probability = values[i] });
                }
                j++;
            }

            return data;
        }
    };
}
