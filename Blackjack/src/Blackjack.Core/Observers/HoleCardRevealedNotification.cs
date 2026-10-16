namespace Blackjack.Core;

/// <summary>Data of <see cref="IGameObserver.OnHoleCardRevealedAsync"/>.</summary>
/// <param name="Card">The dealer's hole card.</param>
public sealed record HoleCardRevealedNotification(Card Card);
