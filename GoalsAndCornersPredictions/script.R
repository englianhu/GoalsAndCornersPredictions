cat("test test")

data <- read.csv("input.txt")

cat("test test")

library(plyr)

## goals scored by Team
HomeGoals <- aggregate(data$HomeGoals, by=list(data$HomeTeam), sum)
AwayGoals <- aggregate(data$AwayGoals, by=list(data$AwayTeam), sum)
ScoreTotal <- join(HomeGoals, AwayGoals, by="Group.1")
names(ScoreTotal)<-c("Team","Home","Away")
ScoreTotal <- data.frame(Team=ScoreTotal$Team, Goals=rowSums(cbind(ScoreTotal$Home,ScoreTotal$Away), na.rm=T))

## number of games played by Team
numGamesH <- aggregate(data$HomeTeam, by=list(data$HomeTeam), length)
numGamesA <- aggregate(data$AwayTeam, by=list(data$AwayTeam), length)
numGames <- join(numGamesH, numGamesA, by="Group.1")
names(numGames)<-c("Team","Home","Away")
numGames <- data.frame(Team=numGames$Team, numGames=rowSums(cbind(numGames$Home,numGames$Away), na.rm=T))

## average number of goals scored per game
## not equal to sum(ScoreTotal$Goals)/nrow(data) because dropped some teams in the join
meanScore <- (sum(data$HomeGoals)+sum(data$AwayGoals))/(nrow(data)*2)

## adjust for how many games each team have played
ScoreTotal <- cbind(ScoreTotal, attackstrength=ScoreTotal$Goals/(numGames$numGames*meanScore))

## goals conceded by Team
HomeGoals <- aggregate(data$AwayGoals, by=list(data$HomeTeam), sum)
AwayGoals <- aggregate(data$HomeGoals, by=list(data$AwayTeam), sum)
ConcedeTotal <- join(HomeGoals, AwayGoals, by="Group.1")
names(ConcedeTotal)<-c("Team","Home","Away")
ConcedeTotal <- data.frame(Team=ConcedeTotal$Team, Goals=rowSums(cbind(ConcedeTotal$Home,ConcedeTotal$Away), na.rm=T))

## avergae conceded is same as average scored!
ConcedeTotal <- cbind(ConcedeTotal, defenceweakness=ConcedeTotal$Goals/(numGames$numGames*meanScore))

## average number of goals scored at home
avGoalsH <- mean(data$HomeGoals)

## average number of goals scored away
avGoalsA <- mean(data$AwayGoals)

team.names <- unique(ScoreTotal$Team)

GoalsH <- GoalsA <- data.frame(Teams=team.names, row.names=team.names)
likelyScore <- likelyProb <- data.frame(Teams=team.names, row.names=team.names)
winH <- winA <- data.frame(Teams=team.names, row.names=team.names)

goals <- 0:5

for (HomeTeam in team.names){
  for (AwayTeam in team.names){
    ## expected number of home goals
    GoalsH[HomeTeam, AwayTeam] <- avGoalsH*ScoreTotal$attackstrength[ScoreTotal$Team==HomeTeam]*ConcedeTotal$defenceweakness[ConcedeTotal$Team==AwayTeam]
    ## expected number of away goals
    GoalsA[HomeTeam, AwayTeam] <- avGoalsA*ScoreTotal$attackstrength[ScoreTotal$Team==AwayTeam]*ConcedeTotal$defenceweakness[ConcedeTotal$Team==HomeTeam]
    
    probsH <- dpois(goals, GoalsH[HomeTeam, AwayTeam])
    probsA <- dpois(goals, GoalsA[HomeTeam, AwayTeam])
    likelyScore[HomeTeam, AwayTeam] <- paste(goals[probsH==max(probsH)],goals[probsA==max(probsA)])
    likelyProb[HomeTeam, AwayTeam] <- max(probsH)*max(probsA)
    
    winH[HomeTeam, AwayTeam] <- sum(sapply(goals, function(x) (1-ppois(x, lambda=GoalsH[HomeTeam, AwayTeam]))*ppois(x, lambda=GoalsA[HomeTeam, AwayTeam])))/length(goals)
    winA[HomeTeam, AwayTeam] <- sum(sapply(goals, function(x) (1-ppois(x, lambda=GoalsA[HomeTeam, AwayTeam]))*ppois(x, lambda=GoalsH[HomeTeam, AwayTeam])))/length(goals)
    #draw[HomeTeam, AwayTeam] <- sum(sapply(goals, function(x) dpois(x, lambda=GoalsA[HomeTeam, AwayTeam])*dpois(x, lambda=GoalsH[HomeTeam, AwayTeam])))/length(goals)
  }
}

write.csv2(winH, "winH.csv", row.names=FALSE, sep=";",quote=FALSE)
write.csv2(winA, "winA.csv", row.names=FALSE, sep=";",quote=FALSE)

write.csv2(likelyProb, "likelyProb.csv", row.names=FALSE, sep=";",quote=FALSE)
write.csv2(likelyScore, "likelyScore.csv", row.names=FALSE, sep=";",quote=FALSE)







