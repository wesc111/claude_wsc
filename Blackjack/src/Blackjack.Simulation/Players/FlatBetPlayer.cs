namespace Blackjack.Simulation.Players;

/// <summary>Base class of the automated players: always bets the same amount and decides synchronously.</summary>
internal abstract class FlatBetPlayer : IPlayerStrategy
{
    // Cached tasks: a simulation makes millions of decisions and should not allocate a task for each.
    private static readonly Task<PlayerAction> HitTask = Task.FromResult(PlayerAction.Hit);
    private static readonly Task<PlayerAction> StandTask = Task.FromResult(PlayerAction.Stand);
    private static readonly Task<PlayerAction> DoubleTask = Task.FromResult(PlayerAction.Double);
    private static readonly Task<PlayerAction> SplitTask = Task.FromResult(PlayerAction.Split);

    private readonly Task<decimal?> _betTask;

    /// <summary>Creates a player that bets <paramref name="bet"/> in every round.</summary>
    protected FlatBetPlayer(decimal bet)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bet);
        Bet = bet;
        _betTask = Task.FromResult<decimal?>(bet);
    }

    /// <summary>The fixed bet.</summary>
    public decimal Bet { get; }

    /// <inheritdoc />
    public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default) => _betTask;

    /// <inheritdoc />
    public Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default) =>
        Decide(context) switch
        {
            PlayerAction.Hit => HitTask,
            PlayerAction.Stand => StandTask,
            PlayerAction.Double => DoubleTask,
            PlayerAction.Split => SplitTask,
            var other => Task.FromResult(other),
        };

    /// <summary>Decides the next action.</summary>
    public abstract PlayerAction Decide(DecisionContext context);
}
