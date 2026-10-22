namespace Blackjack.Core;

/// <summary>Information available when the dealer shows an Ace and offers insurance (SPEC E12).</summary>
/// <param name="PlayerCards">The player's two cards.</param>
/// <param name="PlayerTotal">Their total.</param>
/// <param name="IsBlackjack">Whether the player has a Blackjack; insurance then works as even money (at 3:2).</param>
/// <param name="DealerUpcard">The dealer's face-up Ace.</param>
/// <param name="Amount">The insurance bet: half the bet.</param>
public sealed record InsuranceContext(
    IReadOnlyList<Card> PlayerCards,
    int PlayerTotal,
    bool IsBlackjack,
    Card DealerUpcard,
    decimal Amount);
