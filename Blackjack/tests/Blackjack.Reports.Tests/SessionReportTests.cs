namespace Blackjack.Reports.Tests;

/// <summary>SPEC 8a, E2 – the readable report and both CSV files.</summary>
public sealed class SessionReportTests
{
    private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";

    private static StringWriter Writer() => new() { NewLine = "\n" };

    [Fact]
    public async Task TextReport()
    {
        var output = Writer();

        SessionReport.WriteText(output, await Sessions.TwoRoundsAsync(), Sessions.End);

        Assert.Equal(
            Lines(
                "Blackjack – session statistics",
                "Played:   2026-10-17 15:30 – 15:35 (5 min)",
                "Rules:    6 decks, S17, 3:2, penetration 75 %, bets 10–500",
                "",
                "Bankroll                 start 1000, end 1010, highest 1010, lowest 1000",
                "Rounds                   2   (3 hands)",
                "Wins / losses / pushes   1 / 1 / 1   (33.3 % / 33.3 % / 33.3 % of hands)",
                "Blackjacks               0   (0.0 % of rounds)",
                "Doubles                  1   (won 1)",
                "Splits                   1",
                "Insurance                0   (won 0, net 0)",
                "Player busts             0   (0.0 % of hands)",
                "Dealer busts             0   (0.0 % of rounds)",
                "Initial bets             20",
                "Total wagered            40   (including Double and Split)",
                "Net result               +10   (+50.0 % of initial bets)",
                "Biggest win              +10 (round 2)",
                "Biggest loss             –",
                "",
                "Rounds",
                "#  Time      Bet  Player                          Dealer       Result    Net  Bankroll",
                "1  15:31:05   10  10♠ 7♠ (17)                     9♠ 8♠ (17)   Push        0      1000",
                "2  15:33:10   20  Hand 1: 8♠ 3♠ 10♠ (21) doubled  10♠ 7♠ (17)  You win   +20",
                "              10  Hand 2: 8♠ 2♠ (10)                           You lose  -10      1010"),
            output.ToString());
    }

    [Fact]
    public async Task StatisticsCsv()
    {
        var output = Writer();

        SessionReport.WriteStatisticsCsv(output, await Sessions.TwoRoundsAsync(), Sessions.End);

        Assert.Equal(
            Lines(
                "Metric,Value",
                "Started,2026-10-17 15:30:00",
                "Ended,2026-10-17 15:35:00",
                "Rules,\"6 decks, S17, 3:2, penetration 75 %, bets 10–500\"",
                "StartingBankroll,1000",
                "FinalBankroll,1010",
                "HighestBankroll,1010",
                "LowestBankroll,1000",
                "Rounds,2",
                "Hands,3",
                "Wins,1",
                "Losses,1",
                "Pushes,1",
                "Blackjacks,0",
                "Doubles,1",
                "DoublesWon,1",
                "Splits,1",
                "Insurances,0",
                "InsurancesWon,0",
                "InsuranceNet,0",
                "PlayerBusts,0",
                "DealerBusts,0",
                "InitialBets,20",
                "TotalWagered,40",
                "Net,10",
                "NetPercentOfInitialBets,50",
                "BiggestWin,10",
                "BiggestWinRound,2",
                "BiggestLoss,",
                "BiggestLossRound,"),
            output.ToString());
    }

    [Fact]
    public async Task RoundsCsvHasOneRowPerHand()
    {
        var output = Writer();

        SessionReport.WriteRoundsCsv(output, await Sessions.TwoRoundsAsync());

        Assert.Equal(
            Lines(
                SessionReport.RoundsCsvHeader,
                "1,2026-10-17 15:31:05,1,10,10♠ 7♠,17,no,no,9♠ 8♠,17,Push,0,1000,,",
                "2,2026-10-17 15:33:10,1,20,8♠ 3♠ 10♠,21,yes,yes,10♠ 7♠,17,Win,20,1010,,",
                "2,2026-10-17 15:33:10,2,10,8♠ 2♠,10,no,yes,10♠ 7♠,17,Loss,-10,1010,,"),
            output.ToString());
    }

    [Fact]
    public void TheInsuranceIsReportedPerRoundAndInTheStatistics()
    {
        // Spec E12: a won insurance against a dealer Blackjack (net 0) and a lost one (net −15).
        var log = new SessionLog(Rules.Default, 1000m, () => Sessions.Start);
        log.Record(new RoundResult(10m, [new HandResult(Cards.Parse("10 7"), 17, false, 10m, HandOutcome.DealerBlackjack, -10m)],
            Cards.Parse("A K"), 21, 0m, 1000m, 10m, 5m, 10m));
        log.Record(new RoundResult(10m, [new HandResult(Cards.Parse("10 7"), 17, false, 10m, HandOutcome.Loss, -10m)],
            Cards.Parse("A 9"), 20, -15m, 985m, 10m, 5m, -5m));
        var text = Writer();
        var statistics = Writer();
        var rounds = Writer();

        SessionReport.WriteText(text, log, Sessions.End);
        SessionReport.WriteStatisticsCsv(statistics, log, Sessions.End);
        SessionReport.WriteRoundsCsv(rounds, log);

        Assert.Contains("Insurance                2   (won 1, net +5)", text.ToString(), StringComparison.Ordinal);
        Assert.Contains(
            Lines(
                "1  15:30:00   10  10♠ 7♠ (17)  A♠ K♠ (Blackjack)  Dealer has Blackjack – you lose  -10",
                "               5  Insurance                       Insurance wins                   +10      1000",
                "2  15:30:00   10  10♠ 7♠ (17)  A♠ 9♠ (soft 20)    You lose                         -10",
                "               5  Insurance                       Insurance lost                    -5       985"),
            text.ToString(), StringComparison.Ordinal);
        Assert.Contains("Insurances,2\nInsurancesWon,1\nInsuranceNet,5\n", statistics.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("DealerBlackjack,-10,1000,5,10\n" + "2,2026-10-17 15:30:00,1,10,10♠ 7♠,17,no,no,A♠ 9♠,20,Loss,-10,985,5,-5\n",
                        rounds.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ShortSessionAndUnlimitedBankroll()
    {
        var log = new SessionLog(Rules.Default, null, () => Sessions.Start);
        log.Record(new RoundResult(10m, [new HandResult(Cards.Parse("10 9"), 19, false, 10m, HandOutcome.Win, 10m)],
            Cards.Parse("10 8"), 18, 10m, null, 10m));
        var output = Writer();

        SessionReport.WriteText(output, log, Sessions.Start.AddSeconds(20));
        var text = output.ToString();

        Assert.Contains("(< 1 min)", text, StringComparison.Ordinal);
        Assert.Contains("start unlimited, end unlimited", text, StringComparison.Ordinal);
        Assert.Contains("You win  +10  unlimited", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RulesDescription() =>
        Assert.Equal("1 deck, H17, 6:5, penetration 50 %, bets 5–100",
            ReportText.Rules(new Rules { Decks = 1, DealerHitsSoft17 = true, BlackjackPayout = 1.2m, Penetration = 0.5m, MinBet = 5m, MaxBet = 100m }));

    [Theory]
    [InlineData("A K", false, "Blackjack")]
    [InlineData("A K", true, "soft 21")]
    [InlineData("A 6", false, "soft 17")]
    [InlineData("10 6 6", false, "22 bust")]
    public void Totals(string cards, bool fromSplit, string expected) =>
        Assert.Equal(expected, ReportText.Total(Cards.Parse(cards), fromSplit));
}
