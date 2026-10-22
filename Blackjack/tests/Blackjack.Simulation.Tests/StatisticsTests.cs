using Blackjack.Simulation.Statistics;

namespace Blackjack.Simulation.Tests;

/// <summary>SPEC 6.4 – statistics math, rates, breakdown keys and merging.</summary>
public sealed class StatisticsTests
{
    [Fact]
    public void TotalsAndHouseEdge()
    {
        var s = new SimulationStatistics();
        s.Record(Rounds.Create("10 9", "10 8", HandOutcome.Win, 10m));
        s.Record(Rounds.Create("10 7", "10 8", HandOutcome.Loss, -10m));
        s.Record(Rounds.Create("10 6 8", "10 8", HandOutcome.PlayerBust, -10m));
        s.Record(Rounds.Create("10 8", "10 8", HandOutcome.Push, 0m));

        var t = s.Total;
        Assert.Equal(4, t.Rounds);
        Assert.Equal(40m, t.Wagered);
        Assert.Equal(-10m, t.Net);
        Assert.Equal(25.0, t.HouseEdgePercent, 10);
        Assert.Equal(25.0, t.WinPercent, 10);
        Assert.Equal(50.0, t.LossPercent, 10);
        Assert.Equal(25.0, t.PushPercent, 10);
        Assert.Equal(25.0, t.PlayerBustPercent, 10);
    }

    [Fact]
    public void WithABetSpreadTheStandardErrorIsThatOfTheRatio()
    {
        // Spec E13: bet 10 wins 10, bet 80 loses 80. Ratio R = −70 / 90; residuals n − R·b: 10 + 70/9, −80 + 560/9.
        var s = new SimulationStatistics();
        s.Record(Rounds.Create("10 9", "10 8", HandOutcome.Win, 10m, bet: 10m));
        s.Record(Rounds.Create("10 7", "10 8", HandOutcome.Loss, -80m, bet: 80m));

        var r = -70.0 / 90;
        var residuals = Math.Pow(10 - (r * 10), 2) + Math.Pow(-80 - (r * 80), 2);
        Assert.Equal(-r * 100, s.Total.HouseEdgePercent, 10);
        Assert.Equal(Math.Sqrt(residuals * 2 / 1) / 90 * 100, s.Total.StandardErrorPercent, 10);
    }

    [Fact]
    public void StandardErrorAndConfidenceInterval()
    {
        // Per-round results +1, −1, −1, +1: mean 0, sample variance 4/3, SE = sqrt(4/3 / 4).
        var s = new SimulationStatistics();
        s.Record(Rounds.Create("10 9", "10 8", HandOutcome.Win, 10m));
        s.Record(Rounds.Create("10 7", "10 8", HandOutcome.Loss, -10m));
        s.Record(Rounds.Create("10 7", "10 8", HandOutcome.Loss, -10m));
        s.Record(Rounds.Create("10 9", "10 8", HandOutcome.Win, 10m));

        var se = Math.Sqrt(4.0 / 3 / 4) * 100;
        Assert.Equal(0.0, s.Total.HouseEdgePercent, 10);
        Assert.Equal(se, s.Total.StandardErrorPercent, 10);
        Assert.Equal(-1.96 * se, s.Total.ConfidenceLowPercent, 10);
        Assert.Equal(1.96 * se, s.Total.ConfidenceHighPercent, 10);
    }

    [Fact]
    public void BlackjackPaysIntoTheResultAndRate()
    {
        var s = new SimulationStatistics();
        s.Record(Rounds.Create("A K", "10 7", HandOutcome.BlackjackWin, 15m));
        s.Record(Rounds.Create("10 7", "10 8", HandOutcome.Loss, -10m));

        Assert.Equal(50.0, s.Total.BlackjackPercent, 10);
        Assert.Equal(-25.0, s.Total.HouseEdgePercent, 10);
    }

    [Fact]
    public void NoRoundsGivesNotANumber()
    {
        var t = new StatisticsBucket();

        Assert.True(double.IsNaN(t.HouseEdgePercent));
        Assert.True(double.IsNaN(t.StandardErrorPercent));
        Assert.True(double.IsNaN(t.DealerBustPercent));
    }

    [Theory]
    [InlineData("10 9", "10 6 K", HandOutcome.DealerBust, true, true)]
    [InlineData("10 9", "10 8", HandOutcome.Win, true, false)]
    [InlineData("10 6 8", "10 6", HandOutcome.PlayerBust, false, false)]
    [InlineData("A K", "10 7", HandOutcome.BlackjackWin, false, false)]
    [InlineData("10 7", "A K", HandOutcome.DealerBlackjack, false, false)]
    [InlineData("A K", "A K", HandOutcome.Push, false, false)]
    [InlineData("10 5 6", "10 7", HandOutcome.Win, true, false)]
    public void DealerPlayedAndBust(string player, string dealer, HandOutcome outcome, bool played, bool bust)
    {
        var facts = RoundFacts.From(Rounds.Create(player, dealer, outcome, 0m));

        Assert.Equal(played, facts.DealerPlayed);
        Assert.Equal(bust, facts.DealerBust);
    }

    [Fact]
    public void DealerBustRateCountsOnlyRoundsTheDealerPlayed()
    {
        var s = new SimulationStatistics();
        s.Record(Rounds.Create("10 9", "10 6 K", HandOutcome.DealerBust, 10m));
        s.Record(Rounds.Create("10 9", "10 8", HandOutcome.Win, 10m));
        s.Record(Rounds.Create("10 6 8", "10 6", HandOutcome.PlayerBust, -10m));
        s.Record(Rounds.Create("A K", "10 7", HandOutcome.BlackjackWin, 15m));

        Assert.Equal(2, s.Total.DealerPlayed);
        Assert.Equal(50.0, s.Total.DealerBustPercent, 10);
    }

    [Theory]
    [InlineData("2", "2")]
    [InlineData("9", "9")]
    [InlineData("10", "10")]
    [InlineData("J", "10")]
    [InlineData("K", "10")]
    [InlineData("A", "A")]
    public void UpcardKey(string upcard, string key) =>
        Assert.Equal(key, Breakdown.UpcardKeys[Breakdown.UpcardIndex(Cards.ParseOne(upcard))]);

    [Theory]
    [InlineData("2 2", "hard 4")]
    [InlineData("10 Q", "hard 20")]
    [InlineData("9 3", "hard 12")]
    [InlineData("A A", "soft 12")]
    [InlineData("A 6", "soft 17")]
    [InlineData("9 A", "soft 20")]
    [InlineData("A K", "Blackjack")]
    [InlineData("10 A", "Blackjack")]
    public void StartingTotalKey(string cards, string key)
    {
        var parsed = Cards.Parse(cards);

        Assert.Equal(key, Breakdown.StartingTotalKeys[Breakdown.StartingTotalIndex(parsed[0], parsed[1])]);
    }

    [Fact]
    public void BreakdownUsesUpcardAndFirstTwoCards()
    {
        var s = new SimulationStatistics();
        s.Record(Rounds.Create("10 6 5", "K 7", HandOutcome.Win, 10m));
        s.Record(Rounds.Create("A 6", "Q 8", HandOutcome.Loss, -10m));

        Assert.Equal(2, s.DealerUpcard("10").Rounds);
        Assert.Equal(1, s.StartingTotal("hard 16").Rounds);
        Assert.Equal(1, s.StartingTotal("soft 17").Rounds);
        Assert.Equal(0, s.StartingTotal("hard 20").Rounds);
        Assert.Equal(-100.0, s.StartingTotal("hard 16").HouseEdgePercent, 10);
        Assert.Equal(100.0, s.StartingTotal("soft 17").HouseEdgePercent, 10);
    }

    [Fact]
    public void KeyListsCoverAllStartingHands()
    {
        Assert.Equal(10, Breakdown.UpcardKeys.Count);
        Assert.Equal(27, Breakdown.StartingTotalKeys.Count);
        Assert.Equal(27, Breakdown.StartingTotalKeys.Distinct().Count());
    }

    [Fact]
    public void MergeEqualsRecordingEverythingInOne()
    {
        RoundResult[] rounds =
        [
            Rounds.Create("10 9", "10 6 K", HandOutcome.DealerBust, 10m),
            Rounds.Create("A K", "10 7", HandOutcome.BlackjackWin, 15m),
            Rounds.Create("10 6 8", "9 7", HandOutcome.PlayerBust, -10m),
            Rounds.Create("10 8", "10 8", HandOutcome.Push, 0m),
        ];
        var all = new SimulationStatistics();
        var a = new SimulationStatistics();
        var b = new SimulationStatistics();
        for (var i = 0; i < rounds.Length; i++)
        {
            all.Record(rounds[i]);
            (i < 2 ? a : b).Record(rounds[i]);
        }

        a.Merge(b);

        AssertSame(all.Total, a.Total);
        for (var i = 0; i < Breakdown.UpcardKeys.Count; i++)
        {
            AssertSame(all.ByDealerUpcard[i], a.ByDealerUpcard[i]);
        }

        for (var i = 0; i < Breakdown.StartingTotalKeys.Count; i++)
        {
            AssertSame(all.ByStartingTotal[i], a.ByStartingTotal[i]);
        }
    }

    [Fact]
    public void UnknownBreakdownKeyThrows() =>
        Assert.Throws<ArgumentException>(() => new SimulationStatistics().StartingTotal("hard 21"));

    [Fact]
    public void HouseEdgeIsPerInitialBet()
    {
        // Doubled win: +20 on an initial bet of 10; plus a plain loss of 10. Net +10 on 20 initial bets.
        var s = new SimulationStatistics();
        s.Record(Rounds.CreateHands("10 7", 10m, ("6 5 10", HandOutcome.Win, 20m, 20m, true)));
        s.Record(Rounds.Create("10 7", "10 8", HandOutcome.Loss, -10m));

        Assert.Equal(20m, s.Total.InitialBets);
        Assert.Equal(30m, s.Total.Wagered);
        Assert.Equal(-50.0, s.Total.HouseEdgePercent, 10);
        Assert.Equal(50.0, s.Total.DoublePercent, 10);
        Assert.Equal(0.0, s.Total.SplitPercent, 10);
    }

    [Fact]
    public void SplitRound()
    {
        // A A split: A K (21, not a Blackjack) wins, A 5 loses; dealer 10 7.
        var s = new SimulationStatistics();
        s.Record(Rounds.CreateHands("10 7", 10m,
            ("A K", HandOutcome.Win, 10m, 10m, false),
            ("A 5", HandOutcome.Loss, 10m, -10m, false)));

        var t = s.Total;
        Assert.Equal(1, t.Rounds);
        Assert.Equal(2, t.Hands);
        Assert.Equal(0, t.Blackjacks);
        Assert.Equal(100.0, t.SplitPercent, 10);
        Assert.Equal(1, t.Wins);
        Assert.Equal(1, t.Losses);
        Assert.Equal(1, t.DealerPlayed);
        Assert.Equal(1, s.StartingTotal("soft 12").Rounds);
    }

    [Fact]
    public void SplitTensCountAsHardTwentyStart()
    {
        var s = new SimulationStatistics();
        s.Record(Rounds.CreateHands("9 8", 10m,
            ("K 9", HandOutcome.Win, 10m, 10m, false),
            ("10 A", HandOutcome.Win, 10m, 10m, false)));

        Assert.Equal(1, s.StartingTotal("hard 20").Rounds);
        Assert.Equal(0, s.StartingTotal("Blackjack").Rounds);
        Assert.Equal(0, s.Total.Blackjacks);
    }

    [Fact]
    public void AllSplitHandsBustMeansDealerDidNotPlay()
    {
        var facts = RoundFacts.From(Rounds.CreateHands("10 6", 10m,
            ("8 6 K", HandOutcome.PlayerBust, 10m, -10m, false),
            ("8 6 Q", HandOutcome.PlayerBust, 10m, -10m, false)));

        Assert.False(facts.DealerPlayed);
    }

    internal static void AssertSame(StatisticsBucket expected, StatisticsBucket actual)
    {
        Assert.Equal(expected.Rounds, actual.Rounds);
        Assert.Equal(expected.Hands, actual.Hands);
        Assert.Equal(expected.InitialBets, actual.InitialBets);
        Assert.Equal(expected.Wagered, actual.Wagered);
        Assert.Equal(expected.Net, actual.Net);
        Assert.Equal(expected.Wins, actual.Wins);
        Assert.Equal(expected.Losses, actual.Losses);
        Assert.Equal(expected.Pushes, actual.Pushes);
        Assert.Equal(expected.Blackjacks, actual.Blackjacks);
        Assert.Equal(expected.PlayerBusts, actual.PlayerBusts);
        Assert.Equal(expected.DealerPlayed, actual.DealerPlayed);
        Assert.Equal(expected.DealerBusts, actual.DealerBusts);
        Assert.Equal(expected.DoubleRounds, actual.DoubleRounds);
        Assert.Equal(expected.SplitRounds, actual.SplitRounds);
        Assert.Equal(expected.StandardErrorPercent, actual.StandardErrorPercent);
    }
}
