using System.Diagnostics;
using Blackjack.Simulation.Players;
using Blackjack.Simulation.Statistics;

namespace Blackjack.Simulation;

/// <summary>
/// Runs a simulation on parallel workers. Each worker has its own shoe, engine and player, a seed derived from
/// the main seed and a fixed share of the rounds, so identical parameters give identical results.
/// </summary>
internal static class Simulator
{
    /// <summary>Runs the simulation described by <paramref name="options"/> with the main seed <paramref name="seed"/>.</summary>
    public static async Task<SimulationResult> RunAsync(SimulationOptions options, int seed, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var rules = options.CreateRules();
        var seeds = DeriveSeeds(seed, options.Workers);
        var shares = SplitRounds(options.Rounds, options.Workers);

        var stopwatch = Stopwatch.StartNew();
        var workers = new Task<SimulationStatistics>[options.Workers];
        for (var i = 0; i < workers.Length; i++)
        {
            var (workerSeed, rounds) = (seeds[i], shares[i]);
            workers[i] = Task.Run(() => RunWorkerAsync(options, rules, workerSeed, rounds, ct), ct);
        }

        var parts = await Task.WhenAll(workers);
        stopwatch.Stop();

        // Merge in worker order so that the double sums are reproducible as well.
        var statistics = new SimulationStatistics();
        foreach (var part in parts)
        {
            statistics.Merge(part);
        }

        return new SimulationResult(statistics, stopwatch.Elapsed);
    }

    /// <summary>Derives one seed per worker from the main seed.</summary>
    public static int[] DeriveSeeds(int seed, int workers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workers);
        var random = new Random(seed);
        return [.. Enumerable.Range(0, workers).Select(_ => random.Next())];
    }

    /// <summary>Splits <paramref name="rounds"/> as evenly as possible; the first workers get one more round.</summary>
    public static long[] SplitRounds(long rounds, int workers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rounds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workers);
        return [.. Enumerable.Range(0, workers).Select(i => (rounds / workers) + (i < rounds % workers ? 1 : 0))];
    }

    private static async Task<SimulationStatistics> RunWorkerAsync(
        SimulationOptions options, Rules rules, int seed, long rounds, CancellationToken ct)
    {
        var shoe = new Shoe(rules, seed);
        var player = PlayerFactory.Create(options.Strategy, options.Bet, rules, shoe);

        // A counting player sees every card as an observer.
        var game = new BlackjackGame(rules, shoe, startingBankroll: null, player is IGameObserver observer ? [observer] : null);
        var statistics = new SimulationStatistics();
        for (long round = 0; round < rounds; round++)
        {
            var result = await game.PlayRoundAsync(player, ct)
                ?? throw new InvalidOperationException("An automated player left the table.");
            statistics.Record(result);
        }

        return statistics;
    }
}
