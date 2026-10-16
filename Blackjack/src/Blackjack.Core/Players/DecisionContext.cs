namespace Blackjack.Core;

/// <summary>Information available when deciding on an action.</summary>
/// <param name="PlayerCards">The cards of the current hand.</param>
/// <param name="PlayerTotal">The total of the current hand.</param>
/// <param name="IsSoft">Whether the current hand is soft.</param>
/// <param name="DealerUpcard">The dealer's face-up card.</param>
/// <param name="AllowedActions">The actions that are legal now.</param>
public sealed record DecisionContext(
    IReadOnlyList<Card> PlayerCards,
    int PlayerTotal,
    bool IsSoft,
    Card DealerUpcard,
    IReadOnlySet<PlayerAction> AllowedActions);
