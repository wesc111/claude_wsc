namespace Blackjack.ConsoleApp.Tests;

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

    private static RoundResult Result(HandOutcome outcome, decimal net) =>
        new(10m, [new HandResult([], 0, false, 10m, outcome, net)], [], 0, net, 1000m + net, 10m);
}
