namespace Blackjack.Simulation.Tests;

/// <summary>
/// SPEC 6.4 – plausibility checks with 1 million rounds and a fixed seed (95 % CI ≈ ±0.2 %).
/// Reference values from 2 × 20 million rounds: basic 2.37 %, mimic 5.69 %, never-bust 6.03 %.
/// </summary>
public sealed class PlausibilityTests(PlausibilityFixture fixture) : IClassFixture<PlausibilityFixture>
{
    private double Edge(string strategy) => fixture.Results[strategy].Statistics.Total.HouseEdgePercent;

    [Fact]
    public void MimicDealerHouseEdgeIsFiveToSixPercent() =>
        Assert.InRange(Edge("mimic"), 5.0, 6.0);

    [Fact]
    public void BasicStrategyHouseEdgeMatchesReference() =>
        Assert.InRange(Edge("basic"), 2.37 - 0.4, 2.37 + 0.4);

    [Fact]
    public void NeverBustHouseEdgeMatchesReference() =>
        Assert.InRange(Edge("neverbust"), 6.03 - 0.4, 6.03 + 0.4);

    [Fact]
    public void Ranking()
    {
        Assert.True(Edge("basic") < Edge("mimic"), "basic strategy must beat mimic-the-dealer");
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
            Assert.Equal(PlausibilityFixture.Rounds, result.Statistics.Total.Rounds);
            Assert.True(result.Elapsed < TimeSpan.FromMinutes(1), $"took {result.Elapsed}");
        }
    }
}
