using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GoalsAndCornersPredictions
{
    public class Configuration
    {
        string dayJoin = "__";

        public RExecutor rExecutor = null;
        public CreateInputFile createInputFile = null;
        public PredictionReader predReader = null;

        public Configuration(string dayJoin, CreateInputFile createInputFile, PredictionReader reader, RExecutor r)
        {
            this.dayJoin = dayJoin;
            this.rExecutor = r;
            this.createInputFile = createInputFile;
            this.predReader = reader;
        }

        public string generateDay()
        {
            var uuid = System.Guid.NewGuid().ToString();
            return uuid.Substring(0, 8) + dayJoin + DateTime.Today.ToString("ddMMyyyy");     
        }
    }
}
