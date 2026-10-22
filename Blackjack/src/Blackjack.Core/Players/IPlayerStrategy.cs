namespace Blackjack.Core;

/// <summary>Makes the player's decisions: human (console, GUI) or automated.</summary>
public interface IPlayerStrategy
{
    /// <summary>Asks for the bet of the next round.</summary>
    /// <param name="context">Bankroll and rules.</param>
    /// <param name="ct">Cancels the round.</param>
    /// <returns>The bet, or <c>null</c> to leave the table.</returns>
    Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default);

    /// <summary>Asks for the next action on the current hand.</summary>
    /// <param name="context">The hand, the dealer upcard and the allowed actions.</param>
    /// <param name="ct">Cancels the round.</param>
    /// <returns>One of <see cref="DecisionContext.AllowedActions"/>.</returns>
    Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default);

    /// <summary>
    /// Asks whether to take insurance (half the bet) when the dealer shows an Ace, before the dealer peeks (SPEC E12).
    /// The default declines, as basic strategy does.
    /// </summary>
    /// <param name="context">The player's hand, the dealer's Ace and the insurance amount.</param>
    /// <param name="ct">Cancels the round.</param>
    /// <returns><c>true</c> to take insurance.</returns>
    Task<bool> DecideInsuranceAsync(InsuranceContext context, CancellationToken ct = default) => Task.FromResult(false);
}
