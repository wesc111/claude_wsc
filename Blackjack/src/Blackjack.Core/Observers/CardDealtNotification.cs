namespace Blackjack.Core;

/// <summary>Data of <see cref="IGameObserver.OnCardDealtAsync"/>.</summary>
/// <param name="Recipient">The dealer or a player hand.</param>
/// <param name="Card">The card, or <c>null</c> if it was dealt face down.</param>
/// <param name="IsFaceUp">Whether the card was dealt face up.</param>
public sealed record CardDealtNotification(CardRecipient Recipient, Card? Card, bool IsFaceUp);
