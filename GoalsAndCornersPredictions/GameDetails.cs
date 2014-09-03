namespace GoalsAndCornersPredictions
{
    public class GameDetails
    {
        public string gameId;
        public string team1Name;
        public string team2Name;
        public string leagueName;
        public string koDate;

        public PredRow prediction = new PredRow();

        public override string ToString()
        {
            return "game: " + gameId + " : " + team1Name + "|" + team2Name + "|" + leagueName + "|" + koDate;
        }
    };
}