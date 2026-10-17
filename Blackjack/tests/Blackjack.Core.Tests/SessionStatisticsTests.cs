using Blackjack.Core.Tests.Helpers;
using static Blackjack.Core.PlayerAction;

namespace Blackjack.Core.Tests;

public sealed class SessionStatisticsTests
{
    [Fact]
    public void CountsOutcomesAndNet()
    {
        var statistics = new SessionStatistics();

        foreach (var (outcome, net) in new (HandOutcome, decimal)[]
                 {
                     (HandOutcome.BlackjackWin, 15m), (HandOutcome.Win, 10m), (HandOutcome.DealerBust, 10m),
                     (HandOutcome.Push, 0m),
                     (HandOutcome.Loss, -10m), (HandOutcome.PlayerBust, -10m), (HandOutcome.DealerBlackjack, -10m),
                 })
        {
            statistics.Record(Result(outcome, net));
        }

        Assert.Equal(7, statistics.Rounds);
        Assert.Equal(3, statistics.Wins);
        Assert.Equal(3, statistics.Losses);
        Assert.Equal(1, statistics.Pushes);
        Assert.Equal(5m, statistics.Net);
    }

    /// <summary>
    /// Five real rounds with bet 10 and bankroll 1000: Blackjack (+15), won Double (+20), split with one bust and
    /// one loss (−20), dealer bust (+10), player bust (−10).
    /// </summary>
    private static async Task<SessionStatistics> PlayFiveRoundsAsync()
    {
        var source = new FixedCardSource("A 9 K 7  6 10 5 7 10  8 10 8 6 6 K 10 5  10 6 2 10 K  10 6 6 10 6");
        var game = new BlackjackGame(Rules.Default, source, 1000m);
        var statistics = new SessionStatistics(1000m);
        ScriptedPlayer[] players =
        [
            new(10m),
            new(10m, PlayerAction.Double),
            new(10m, Split, Hit, Stand),
            new(10m, Stand),
            new(10m, Hit),
        ];
        foreach (var player in players)
        {
            statistics.Record((await game.PlayRoundAsync(player, TestContext.Current.CancellationToken))!);
        }

        return statistics;
    }

    [Fact]
    public async Task CountsBlackjacksBustsDoublesAndSplits()
    {
        var s = await PlayFiveRoundsAsync();

        Assert.Equal(5, s.Rounds);
        Assert.Equal(6, s.Hands);
        Assert.Equal(3, s.Wins);
        Assert.Equal(3, s.Losses);
        Assert.Equal(0, s.Pushes);
        Assert.Equal(1, s.Blackjacks);
        Assert.Equal(1, s.Doubles);
        Assert.Equal(1, s.DoublesWon);
        Assert.Equal(1, s.Splits);
        Assert.Equal(2, s.PlayerBusts);
        Assert.Equal(1, s.DealerBusts);
    }

    [Fact]
    public async Task MoneyAndBankrollFigures()
    {
        var s = await PlayFiveRoundsAsync();

        Assert.Equal(50m, s.InitialBets);
        Assert.Equal(70m, s.TotalWagered);
        Assert.Equal(15m, s.Net);
        Assert.Equal(30m, s.NetPercentOfInitialBets);
        Assert.Equal(1000m, s.StartingBankroll);
        Assert.Equal(1015m, s.FinalBankroll);
        Assert.Equal(1035m, s.HighestBankroll);
        Assert.Equal(1000m, s.LowestBankroll);
        Assert.Equal((20m, 2), (s.BiggestWin, s.BiggestWinRound));
        Assert.Equal((-20m, 3), (s.BiggestLoss, s.BiggestLossRound));
    }

    [Fact]
    public void LowestBankrollFallsBelowTheStart()
    {
        var s = new SessionStatistics(1000m);

        s.Record(Result(HandOutcome.Loss, -10m));

        Assert.Equal(990m, s.LowestBankroll);
        Assert.Equal(1000m, s.HighestBankroll);
        Assert.Null(s.BiggestWin);
    }

    [Fact]
    public void StartingBankrollIsDerivedFromTheFirstRound()
    {
        var s = new SessionStatistics();

        s.Record(Result(HandOutcome.Win, 10m));

        Assert.Equal(1000m, s.StartingBankroll);
        Assert.Equal(1010m, s.FinalBankroll);
    }

    [Fact]
    public void WithoutRounds()
    {
        var s = new SessionStatistics(500m);

        Assert.Equal(500m, s.FinalBankroll);
        Assert.Null(s.NetPercentOfInitialBets);
        Assert.Null(s.BiggestWin);
        Assert.Null(s.BiggestLoss);
    }

    [Fact]
    public void UnlimitedBankrollHasNoBankrollFigures()
    {
        var s = new SessionStatistics();

        s.Record(new RoundResult(10m, [new HandResult([], 0, false, 10m, HandOutcome.Win, 10m)], [], 0, 10m, null, 10m));

        Assert.Null(s.StartingBankroll);
        Assert.Null(s.FinalBankroll);
        Assert.Null(s.HighestBankroll);
        Assert.Equal(10m, s.Net);
    }

    private static RoundResult Result(HandOutcome outcome, decimal net) =>
        new(10m, [new HandResult([], 0, false, 10m, outcome, net)], [], 0, net, 1000m + net, 10m);
}
