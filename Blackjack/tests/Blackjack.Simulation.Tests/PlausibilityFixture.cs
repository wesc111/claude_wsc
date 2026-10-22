namespace Blackjack.Simulation.Tests;

/// <summary>Runs every strategy once for all plausibility tests: 1 million rounds, full basic strategy and hilo 10 million.</summary>
public sealed class PlausibilityFixture : IAsyncLifetime
{
    public const long Rounds = 1_000_000;
    public const long BasicRounds = 10_000_000;
    public const int Seed = 2026;

    internal Dictionary<string, SimulationResult> Results { get; } = [];

    public async ValueTask InitializeAsync()
    {
        foreach (var strategy in new[] { "basic", "basic-hs", "mimic", "neverbust", "hilo" })
        {
            var rounds = strategy is "basic" or "hilo" ? BasicRounds : Rounds;
            var options = new SimulationOptions { Strategy = strategy, Rounds = rounds, Workers = 8 };
            Results[strategy] = await Simulator.RunAsync(options, Seed, TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
