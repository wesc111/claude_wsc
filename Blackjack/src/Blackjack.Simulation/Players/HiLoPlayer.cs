namespace Blackjack.Simulation.Players;

/// <summary>
/// Counts with Hi-Lo and plays basic strategy with the Illustrious 18 and a 1–8 bet spread (SPEC E13), strategy
/// <c>hilo</c>. One unit is the minimum bet. Register the player as an observer of its game, so it sees every card.
/// </summary>
/// <param name="shoe">The shoe of the game, for the true count.</param>
internal sealed class HiLoPlayer(ICardSource shoe) : IPlayerStrategy, IGameObserver
{
    private readonly HiLoCounter _counter = new(shoe);

    /// <summary>The count of the player.</summary>
    public HiLoCounter Counter => _counter;

    /// <inheritdoc />
    public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default) =>
        Task.FromResult<decimal?>(CountingStrategy.Bet(context, _counter.TrueCount));

    /// <inheritdoc />
    public Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default) =>
        Task.FromResult(CountingStrategy.Decide(context, _counter.TrueCount).Action);

    /// <inheritdoc />
    public Task<bool> DecideInsuranceAsync(InsuranceContext context, CancellationToken ct = default) =>
        Task.FromResult(CountingStrategy.TakeInsurance(_counter.TrueCount));

    /// <inheritdoc />
    public ValueTask OnShoeShuffledAsync(CancellationToken ct) => _counter.OnShoeShuffledAsync(ct);

    /// <inheritdoc />
    public ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct) => _counter.OnCardDealtAsync(notification, ct);

    /// <inheritdoc />
    public ValueTask OnHoleCardRevealedAsync(HoleCardRevealedNotification notification, CancellationToken ct) =>
        _counter.OnHoleCardRevealedAsync(notification, ct);
}
