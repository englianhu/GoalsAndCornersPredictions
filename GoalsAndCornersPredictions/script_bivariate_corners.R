##
## Football prediction model
## Poisson distns
## cf http://understandinguncertainty.org/node/228
##
## N Green
## 2014

data <- read.csv("input.txt") #external drive

library(plyr)
# library(bivpois)  #issue that its out-of-date. load functions directly
source("C:/Users/daddy/Documents/GitHub/GoalsAndCornersPredictions/GoalsAndCornersPredictions/pbivpois.R")
source("C:/Users/daddy/Documents/GitHub/GoalsAndCornersPredictions/GoalsAndCornersPredictions/simplebp.R")
source("C:/Users/daddy/Documents/GitHub/GoalsAndCornersPredictions/GoalsAndCornersPredictions/lmbp.R")
source("C:/Users/daddy/Documents/GitHub/GoalsAndCornersPredictions/GoalsAndCornersPredictions/newnamesbeta.R")
source("C:/Users/daddy/Documents/GitHub/GoalsAndCornersPredictions/GoalsAndCornersPredictions/splitbeta.R")

leagueStats <- function(data, stat=score,concede,games){
  ##TODO##
}

## total goals scored by Team
HomeGoals  <- aggregate(data$HomeGoals, by=list(data$HomeTeam), sum)
AwayGoals  <- aggregate(data$AwayGoals, by=list(data$AwayTeam), sum)
ScoreTotal <- join(HomeGoals, AwayGoals, by="Group.1")
names(ScoreTotal) <- c("Team","Home","Away")
ScoreTotal <- data.frame(Team=ScoreTotal$Team,
                         Goals=rowSums(ScoreTotal[,c("Home","Away")], na.rm=T))

## total number of games played by Team
numGamesH <- aggregate(data$HomeTeam, by=list(data$HomeTeam), length)
numGamesA <- aggregate(data$AwayTeam, by=list(data$AwayTeam), length)
numGames  <- join(numGamesH, numGamesA, by="Group.1")
names(numGames) <- c("Team","Home","Away")
numGames  <- data.frame(Team=numGames$Team,
                        numGames=rowSums(numGames[,c("Home","Away")], na.rm=T))

## goals conceded by Team
HomeGoals <- aggregate(data$AwayGoals, by=list(data$HomeTeam), sum)
AwayGoals <- aggregate(data$HomeGoals, by=list(data$AwayTeam), sum)
ConcedeTotal <- join(HomeGoals, AwayGoals, by="Group.1")
names(ConcedeTotal)<-c("Team","Home","Away")
ConcedeTotal <- data.frame(Team=ConcedeTotal$Team,
                           Goals=rowSums(ConcedeTotal[,c("Home","Away")], na.rm=T))


## average number of goals SCORED per game
## not equal to sum(ScoreTotal$Goals)/nrow(data)
## because dropped some teams in the join above
meanScore <- sum(data$HomeGoals+data$AwayGoals)/(nrow(data)*2)


## attack strength: num goals scores/average number scored
## adjusted for how many games each team have played
ScoreTotal <- cbind(ScoreTotal,
                    attackstrength=ScoreTotal$Goals/(numGames$numGames*meanScore))

## defence weakness
## average conceded is same as average scored!
ConcedeTotal <- cbind(ConcedeTotal,
                      defenceweakness=ConcedeTotal$Goals/(numGames$numGames*meanScore))


## average number of goals scored at HOME
avGoalsH <- mean(data$HomeGoals)

## average number of goals scored AWAY
avGoalsA <- mean(data$AwayGoals)

## all team names
team.names <- unique(ScoreTotal$Team)

## initialise
### expected number of goals SCORED by Home and Away teams
GoalsH <- GoalsA <- data.frame(Teams=team.names, row.names=team.names)
GoalsH <- GoalsA <- matrix(data = NA, nrow = length(team.names), ncol = length(team.names), dimnames = list(team.names, team.names))

### most probable outcomes
likelyScore <- likelyProb <- GoalsH
winH <- winA <- GoalsH


goalMax <- 12
goals.seq <- 0:goalMax
goals.length <- length(goals.seq)

## "pitch" effect
## bivariate Poisson

## constant, simple
### most naive
# lambda3 <- cov(data$HomeGoals, data$AwayGoals)
### EM algorithm
# out <- simple.bp(data$HomeGoals, data$AwayGoals)
# lambda1 <- out$lambda[1]
# lambda2 <- out$lambda[2]
# lambda3 <- out$lambda[3]


## from The Statistician (2003), 52, Part 3, pp. 381-393, Analysis of sports data by using bivariate Poisson models, Dimitris Karlis et al
## E(X)=lambda_1 + lambda_3
## E(Y)=lambda_2 + lambda_3
## cov(X,Y)=lambda_3
## => lambda_3=0 is independent distns



##TODO##
## need to double check the combinations and labels are all right

covattack  <- expand.grid(attackstrengthH=ScoreTotal$attackstrength,
                          attackstrengthA=ScoreTotal$attackstrength)
covdefence <- expand.grid(defenceweaknessH=ConcedeTotal$defenceweakness,
                          defenceweaknessA=ConcedeTotal$defenceweakness)
pred.data  <- data.frame(HomeTeam=ScoreTotal$Team,
                         AwayTeam=rep(ScoreTotal$Team, each=length(ScoreTotal$Team)),
                         avGoalsH, avGoalsA, covattack, covdefence)


## estimating coefficients using bivpois package
# form1 <- ~c(HomeTeam,AwayTeam)+c(AwayTeam,HomeTeam)
# ex4.m1 <- lm.bp(HomeGoals~1, AwayGoals~1, l1l2=form1, zeroL3=TRUE, data=data)

fit.data <- data
fit.data <- na.omit(join(fit.data, pred.data))


lm.fit <- lm.bp(HomeGoals~attackstrengthH+defenceweaknessA,
                AwayGoals~attackstrengthA+defenceweaknessH, zeroL3=FALSE, data=fit.data)

## home lambda
lambda1 <- exp(cbind(1, pred.data$attackstrengthH, pred.data$defenceweaknessA) %*% lm.fit$beta1)
## away lambda
lambda2 <- exp(cbind(1, pred.data$attackstrengthA, pred.data$defenceweaknessH) %*% lm.fit$beta2)
## covariance (constant)
lambda3 <- exp(lm.fit$beta3)

perms   <- as.matrix(expand.grid(goals.seq, goals.seq))

for (i in 1:nrow(pred.data)){    
    #######################
    ## bivariate Poisson ##
    #######################

    probsHA <- pbivpois(x=perms, lambda=c(lambda1[i], lambda2[i], lambda3))

    likelyScore[pred.data$HomeTeam[i], pred.data$AwayTeam[i]] <- paste(perms[probsHA==max(probsHA),], collapse = ' ')
    likelyProb[pred.data$HomeTeam[i], pred.data$AwayTeam[i]]  <- max(probsHA)
    
    winH[pred.data$HomeTeam[i], pred.data$AwayTeam[i]] <- sum(probsHA[perms[,1]>perms[,2]])/sum(probsHA)
    winA[pred.data$HomeTeam[i], pred.data$AwayTeam[i]] <- sum(probsHA[perms[,2]>perms[,1]])/sum(probsHA)    
}

write.csv2(winH, "winH.csv", row.names=FALSE, sep=";",quote=FALSE)
write.csv2(winA, "winA.csv", row.names=FALSE, sep=";",quote=FALSE)

write.csv2(likelyProb, "likelyProb.csv", row.names=FALSE, sep=";",quote=FALSE)
write.csv2(likelyScore, "likelyScore.csv", row.names=FALSE, sep=";",quote=FALSE)

