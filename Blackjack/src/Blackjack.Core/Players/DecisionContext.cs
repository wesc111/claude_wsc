namespace Blackjack.Core;

/// <summary>Information available when deciding on an action.</summary>
/// <param name="PlayerCards">The cards of the current hand.</param>
/// <param name="PlayerTotal">The total of the current hand.</param>
/// <param name="IsSoft">Whether the current hand is soft.</param>
/// <param name="DealerUpcard">The dealer's face-up card.</param>
/// <param name="AllowedActions">The actions that are legal now.</param>
/// <param name="HandIndex">Zero-based index of the current hand.</param>
/// <param name="HandCount">Number of player hands in the round (more than 1 after a split).</param>
public sealed record DecisionContext(
    IReadOnlyList<Card> PlayerCards,
    int PlayerTotal,
    bool IsSoft,
    Card DealerUpcard,
    IReadOnlySet<PlayerAction> AllowedActions,
    int HandIndex = 0,
    int HandCount = 1);
