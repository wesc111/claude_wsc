using Blackjack.Simulation.Statistics;

namespace Blackjack.Simulation;

/// <summary>The outcome of a simulation run.</summary>
/// <param name="Statistics">The merged statistics of all workers.</param>
/// <param name="Elapsed">Wall-clock time of the run.</param>
internal sealed record SimulationResult(SimulationStatistics Statistics, TimeSpan Elapsed)
{
    /// <summary>Rounds per second.</summary>
    public double RoundsPerSecond => Statistics.Total.Rounds / Math.Max(Elapsed.TotalSeconds, 1e-9);
}
