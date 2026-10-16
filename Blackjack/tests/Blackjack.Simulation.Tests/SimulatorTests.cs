namespace Blackjack.Simulation.Tests;

/// <summary>SPEC 6.3 – parallel execution, seeds and reproducibility.</summary>
public sealed class SimulatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SimulationOptions Options(string strategy = "basic", long rounds = 20_000, int workers = 4) =>
        new() { Strategy = strategy, Rounds = rounds, Workers = workers };

    [Theory]
    [InlineData(10, 3, new long[] { 4, 3, 3 })]
    [InlineData(9, 3, new long[] { 3, 3, 3 })]
    [InlineData(2, 4, new long[] { 1, 1, 0, 0 })]
    [InlineData(1_000_000, 1, new long[] { 1_000_000 })]
    public void SplitRounds(long rounds, int workers, long[] expected) =>
        Assert.Equal(expected, Simulator.SplitRounds(rounds, workers));

    [Fact]
    public void DerivedSeedsAreDeterministicAndDistinct()
    {
        var seeds = Simulator.DeriveSeeds(42, 8);

        Assert.Equal(seeds, Simulator.DeriveSeeds(42, 8));
        Assert.Equal(8, seeds.Distinct().Count());
        Assert.NotEqual(seeds, Simulator.DeriveSeeds(43, 8));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task PlaysExactlyTheRequestedRounds(int workers)
    {
        var result = await Simulator.RunAsync(Options(rounds: 12_345, workers: workers), seed: 1, Ct);

        Assert.Equal(12_345, result.Statistics.Total.Rounds);
        Assert.Equal(123_450m, result.Statistics.Total.Wagered);
        Assert.Equal(12_345, result.Statistics.ByDealerUpcard.Sum(b => b.Rounds));
        Assert.Equal(12_345, result.Statistics.ByStartingTotal.Sum(b => b.Rounds));
    }

    [Fact]
    public async Task SameParametersGiveIdenticalResults()
    {
        var a = await Simulator.RunAsync(Options(), seed: 7, Ct);
        var b = await Simulator.RunAsync(Options(), seed: 7, Ct);

        StatisticsTests.AssertSame(a.Statistics.Total, b.Statistics.Total);
        for (var i = 0; i < a.Statistics.ByStartingTotal.Count; i++)
        {
            StatisticsTests.AssertSame(a.Statistics.ByStartingTotal[i], b.Statistics.ByStartingTotal[i]);
        }
    }

    [Fact]
    public async Task DifferentSeedsGiveDifferentResults()
    {
        var a = await Simulator.RunAsync(Options(), seed: 7, Ct);
        var b = await Simulator.RunAsync(Options(), seed: 8, Ct);

        Assert.NotEqual(a.Statistics.Total.Net, b.Statistics.Total.Net);
    }

    [Fact]
    public async Task RuleOptionsAreUsed()
    {
        // 6:5 instead of 3:2 must cost the player money; the same seed keeps the card sequences comparable.
        var standard = await Simulator.RunAsync(Options(), seed: 3, Ct);
        var sixToFive = await Simulator.RunAsync(Options() with { Payout = 1.2m }, seed: 3, Ct);

        Assert.True(sixToFive.Statistics.Total.Net < standard.Statistics.Total.Net);
    }

    [Fact]
    public async Task CancellationStopsTheSimulation()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Simulator.RunAsync(Options(rounds: 1_000_000), seed: 1, cts.Token));
    }

    [Fact]
    public void CreateRulesUsesTheBetAsLimits()
    {
        var rules = new SimulationOptions { Bet = 25m, Decks = 2, DealerHitsSoft17 = true, Payout = 1.2m, Penetration = 0.5m }.CreateRules();

        Assert.Equal(new Rules { Decks = 2, DealerHitsSoft17 = true, BlackjackPayout = 1.2m, Penetration = 0.5m, MinBet = 25m, MaxBet = 25m }, rules);
    }
}
