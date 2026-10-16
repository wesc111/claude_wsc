namespace Blackjack.Simulation.Tests;

/// <summary>Runs 1 million rounds per strategy once for all plausibility tests.</summary>
public sealed class PlausibilityFixture : IAsyncLifetime
{
    public const long Rounds = 1_000_000;
    public const int Seed = 2026;

    internal Dictionary<string, SimulationResult> Results { get; } = [];

    public async ValueTask InitializeAsync()
    {
        foreach (var strategy in new[] { "basic", "mimic", "neverbust" })
        {
            var options = new SimulationOptions { Strategy = strategy, Rounds = Rounds, Workers = 8 };
            Results[strategy] = await Simulator.RunAsync(options, Seed, TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
