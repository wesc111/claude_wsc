namespace Blackjack.Simulation.Tests;

/// <summary>
/// SPEC 6.4 and 7.7 – plausibility checks with a fixed seed: 1 million rounds (95 % CI ≈ ±0.2 %),
/// full basic strategy 10 million rounds (≈ ±0.07 %).
/// Reference values from 2 × 20 million rounds: basic-hs 2.37 %, mimic 5.69 %, never-bust 6.03 %.
/// </summary>
public sealed class PlausibilityTests(PlausibilityFixture fixture) : IClassFixture<PlausibilityFixture>
{
    private double Edge(string strategy) => fixture.Results[strategy].Statistics.Total.HouseEdgePercent;

    [Fact]
    public void MimicDealerHouseEdgeIsFiveToSixPercent() =>
        Assert.InRange(Edge("mimic"), 5.0, 6.0);

    [Fact]
    public void HitStandBasicStrategyHouseEdgeMatchesReference() =>
        Assert.InRange(Edge("basic-hs"), 2.37 - 0.4, 2.37 + 0.4);

    [Fact]
    public void FullBasicStrategyHouseEdgeIsBelowOnePercent() =>
        Assert.InRange(Edge("basic"), 0.3, 0.6);

    [Fact]
    public void FullBasicStrategyDoublesAndSplits()
    {
        var total = fixture.Results["basic"].Statistics.Total;

        Assert.InRange(total.DoublePercent, 5.0, 15.0);
        Assert.InRange(total.SplitPercent, 1.0, 5.0);
        Assert.True(total.Wagered > total.InitialBets);
    }

    [Fact]
    public void HiLoCountingGivesThePlayerTheEdge() =>
        // SPEC E13: reference −0.49 % / −0.45 % (2 × 20 million rounds); 10 million rounds: 95 % CI ≈ ±0.11 %.
        Assert.InRange(Edge("hilo"), -1.0, -0.1);

    [Fact]
    public void HiLoSpreadsItsBets()
    {
        var total = fixture.Results["hilo"].Statistics.Total;

        Assert.True(total.InitialBets > 10m * PlausibilityFixture.BasicRounds, "more than one unit on average");
        Assert.True(total.InitialBets < 20m * PlausibilityFixture.BasicRounds, "mostly one unit");
    }

    [Fact]
    public void NeverBustHouseEdgeMatchesReference() =>
        Assert.InRange(Edge("neverbust"), 6.03 - 0.4, 6.03 + 0.4);

    [Fact]
    public void Ranking()
    {
        Assert.True(Edge("basic") < Edge("basic-hs"), "Double and Split must lower the house edge");
        Assert.True(Edge("basic-hs") < Edge("mimic"), "basic strategy must beat mimic-the-dealer");
        Assert.True(Edge("mimic") < Edge("neverbust"), "mimic-the-dealer must beat never-bust");
    }

    [Fact]
    public void BlackjackRateMatchesTheory()
    {
        // Six decks: P(Blackjack) = 2 × (24/312) × (96/311) ≈ 4.75 %.
        foreach (var result in fixture.Results.Values)
        {
            Assert.InRange(result.Statistics.Total.BlackjackPercent, 4.55, 4.95);
        }
    }

    [Fact]
    public void NeverBustNeverBusts() =>
        Assert.Equal(0, fixture.Results["neverbust"].Statistics.Total.PlayerBusts);

    [Fact]
    public void OneMillionRoundsTakeLessThanAMinute()
    {
        foreach (var result in fixture.Results.Values)
        {
            var perMillion = result.Elapsed / (result.Statistics.Total.Rounds / 1_000_000.0);
            Assert.True(perMillion < TimeSpan.FromMinutes(1), $"1 million rounds took {perMillion}");
        }
    }
}
